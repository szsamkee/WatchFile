using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WatchFile.Core.Configuration.Models;
using WatchFile.Core.Events;
using WatchFile.Core.Parsing;

namespace WatchFile.Core.Monitoring
{
    /// <summary>
    /// 目录监控器
    /// </summary>
    public class DirectoryWatcher : IDisposable
    {
        private readonly WatchItem _config;
        private readonly GlobalSettings _globalSettings;
        private readonly FileSystemWatcher? _watcher;
        private readonly Timer _bufferTimer;
        private readonly Dictionary<string, DateTime> _pendingChanges = new();
        private readonly WatchFileManager _watchFileManager;
        private readonly object _lockObject = new();
        private bool _disposed = false;

        public event EventHandler<FileChangedEventArgs>? FileChanged;
        public event EventHandler<MonitorStatusChangedEventArgs>? StatusChanged;

        public MonitorStatus Status { get; private set; } = MonitorStatus.Stopped;

        public DirectoryWatcher(WatchItem config, GlobalSettings globalSettings)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _globalSettings = globalSettings ?? throw new ArgumentNullException(nameof(globalSettings));
            _watchFileManager = new WatchFileManager(_config);

            try
            {
                if (_config.Type == WatchType.Directory)
                {
                    // 注意：不在构造函数中验证路径是否存在
                    // 路径验证在 StartAsync() 时进行
                    // 这样 GetAllWatchItems() 能返回所有配置项，包括路径有问题的项
                    try
                    {
                        _watcher = new FileSystemWatcher(_config.Path)
                        {
                            IncludeSubdirectories = _config.Recursive,
                            NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName
                        };

                        // 设置文件过滤器
                        if (_config.FileFilters.Any())
                        {
                            _watcher.Filter = "*.*"; // 监控所有文件，在事件中过滤
                        }

                        _watcher.Created += OnFileSystemEvent;
                        _watcher.Changed += OnFileSystemEvent;
                        _watcher.Deleted += OnFileSystemEvent;
                        _watcher.Renamed += OnFileSystemRenamed;
                    }
                    catch (ArgumentException)
                    {
                        // 如果路径无效，_watcher 保持为 null
                        // 在 StartAsync() 时会进行验证并抛出更友好的异常
                        _watcher = null;
                    }
                }

                // 创建缓冲计时器
                _bufferTimer = new Timer(ProcessPendingChanges, null, Timeout.Infinite, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                UpdateStatus(MonitorStatus.Error, $"初始化监控器失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 启动监控
        /// </summary>
        public async Task StartAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DirectoryWatcher));

            if (Status == MonitorStatus.Running)
                return;

            try
            {
                UpdateStatus(MonitorStatus.Starting, "正在启动监控");

                // 在启动时验证路径是否存在
                if (_config.Type == WatchType.Directory && !Directory.Exists(_config.Path))
                {
                    throw new DirectoryNotFoundException($"监控目录不存在: {_config.Path}");
                }

                if (_config.Type == WatchType.File)
                {
                    var directory = Path.GetDirectoryName(_config.Path);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        throw new DirectoryNotFoundException($"文件所在目录不存在: {directory}");
                    }
                }

                // 初始化临时文件
                await _watchFileManager.InitializeWatchFilesAsync();

                if (_config.Type == WatchType.Directory)
                {
                    _watcher?.Invoke(w => w.EnableRaisingEvents = true);
                }
                else if (_config.Type == WatchType.File)
                {
                    // 对于单文件监控，启动定期检查
                    StartFileMonitoring();
                }

                UpdateStatus(MonitorStatus.Running, "监控已启动");
            }
            catch (Exception ex)
            {
                UpdateStatus(MonitorStatus.Error, $"启动监控失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 启动监控（同步版本）
        /// </summary>
        public void Start()
        {
            StartAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        public void Stop()
        {
            if (Status == MonitorStatus.Stopped)
                return;

            try
            {
                _watcher?.Invoke(w => w.EnableRaisingEvents = false);
                _bufferTimer.Change(Timeout.Infinite, Timeout.Infinite);
                
                lock (_lockObject)
                {
                    _pendingChanges.Clear();
                }

                UpdateStatus(MonitorStatus.Stopped, "监控已停止");
            }
            catch (Exception ex)
            {
                UpdateStatus(MonitorStatus.Error, $"停止监控失败: {ex.Message}", ex);
            }
        }

        private void StartFileMonitoring()
        {
            // 对于单文件监控，可以使用定期检查文件修改时间的方式
            // 这里暂时使用目录监控的方式，监控文件所在目录
            if (File.Exists(_config.Path))
            {
                var directory = Path.GetDirectoryName(_config.Path);
                if (!string.IsNullOrEmpty(directory) && _watcher != null)
                {
                    _watcher.Path = directory;
                    _watcher.Filter = Path.GetFileName(_config.Path);
                    _watcher.EnableRaisingEvents = true;
                }
            }
        }

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (!ShouldProcessFile(e.FullPath, e.ChangeType))
                    return;

                lock (_lockObject)
                {
                    _pendingChanges[e.FullPath] = DateTime.Now;
                }

                // 启动或重置缓冲计时器
                _bufferTimer.Change(_globalSettings.BufferTimeMs, Timeout.Infinite);
                
                // 调试信息
                Console.WriteLine($"[DEBUG] 检测到文件变化: {Path.GetFileName(e.FullPath)}, 类型: {e.ChangeType}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] OnFileSystemEvent异常: {ex.Message}");
                // 不要抛出异常，避免影响FileSystemWatcher
            }
        }

        private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
        {
            // 重命名事件处理为删除旧文件和创建新文件
            if (_config.WatchEvents.Contains(WatchEvent.Deleted) && ShouldProcessFile(e.OldFullPath, WatcherChangeTypes.Deleted))
            {
                _ = Task.Run(() => ProcessFileChange(e.OldFullPath, WatcherChangeTypes.Deleted));
            }

            if (_config.WatchEvents.Contains(WatchEvent.Created) && ShouldProcessFile(e.FullPath, WatcherChangeTypes.Created))
            {
                lock (_lockObject)
                {
                    _pendingChanges[e.FullPath] = DateTime.Now;
                }
                _bufferTimer.Change(_globalSettings.BufferTimeMs, Timeout.Infinite);
            }
        }

        private bool ShouldProcessFile(string filePath, WatcherChangeTypes changeType)
        {
            // 检查是否启用了对应的事件类型
            var watchEvent = changeType switch
            {
                WatcherChangeTypes.Created => WatchEvent.Created,
                WatcherChangeTypes.Changed => WatchEvent.Modified,
                WatcherChangeTypes.Deleted => WatchEvent.Deleted,
                WatcherChangeTypes.Renamed => WatchEvent.Renamed,
                _ => (WatchEvent?)null
            };

            if (watchEvent == null || !_config.WatchEvents.Contains(watchEvent.Value))
                return false;

            var fileName = Path.GetFileName(filePath);

            // 检查排除模式
            if (_config.ExcludePatterns.Any())
            {
                foreach (var excludePattern in _config.ExcludePatterns)
                {
                    var pattern = excludePattern.Replace("*", ".*").Replace("?", ".");
                    if (System.Text.RegularExpressions.Regex.IsMatch(fileName, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        return false;
                    }
                }
            }

            // 检查文件过滤器
            if (_config.FileFilters.Any())
            {
                var shouldInclude = _config.FileFilters.Any(filter =>
                {
                    var pattern = filter.Replace("*", ".*").Replace("?", ".");
                    return System.Text.RegularExpressions.Regex.IsMatch(fileName, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                });

                if (!shouldInclude)
                    return false;
            }

            // 对于单文件监控，检查是否是目标文件
            if (_config.Type == WatchType.File)
            {
                return string.Equals(filePath, _config.Path, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        private void ProcessPendingChanges(object? state)
        {
            List<string> filesToProcess;
            
            lock (_lockObject)
            {
                // 获取所有待处理的文件
                filesToProcess = _pendingChanges.Keys.ToList();
                _pendingChanges.Clear();
            }

            foreach (var filePath in filesToProcess)
            {
                _ = Task.Run(() => ProcessFileChange(filePath, WatcherChangeTypes.Changed));
            }
        }

        private async Task ProcessFileChange(string filePath, WatcherChangeTypes changeType)
        {
            var args = new FileChangedEventArgs
            {
                WatchItemId = _config.Id,
                WatchItemName = _config.Name,
                FilePath = filePath,
                ChangeType = changeType,
                Timestamp = DateTime.Now
            };

            try
            {
                // 获取文件信息
                if (changeType != WatcherChangeTypes.Deleted && File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    args.FileSize = fileInfo.Length;
                }

                // 等待文件释放（文件可能正在被写入）
                if (changeType != WatcherChangeTypes.Deleted)
                {
                    await WaitForFileRelease(filePath);
                }

                // 使用临时文件管理器处理变化
                var changeDetails = await _watchFileManager.ProcessFileChangeAsync(filePath, changeType);
                args.ChangeDetails = changeDetails;

                // 如果需要提取当前数据
                if (changeType != WatcherChangeTypes.Deleted && File.Exists(filePath))
                {
                    var parseResult = FileParser.ParseFile(filePath, _config.FileSettings);
                    if (parseResult.IsSuccess)
                    {
                        args.ExtractedData = parseResult.Data;
                    }
                    else
                    {
                        args.Exception = parseResult.Exception;
                        // 即使解析失败也要触发事件，不要抛出异常
                    }
                }

                OnFileChanged(args);
            }
            catch (Exception ex)
            {
                // 确保异常不会阻止后续的文件监控
                args.Exception = ex;
                OnFileChanged(args);
                
                // 记录详细的错误信息到控制台（用于调试）
                Console.WriteLine($"[DEBUG] ProcessFileChange异常: {ex.Message}");
                Console.WriteLine($"[DEBUG] 文件: {filePath}, 变化类型: {changeType}");
                Console.WriteLine($"[DEBUG] 监控器状态: {Status}");
                
                // 不要重新抛出异常，避免影响监控器的运行状态
            }
        }

        private async Task WaitForFileRelease(string filePath)
        {
            var maxRetries = _globalSettings.MaxRetries;
            var retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return; // 文件可以打开，说明已释放
                }
                catch (IOException)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                        throw;
                    
                    await Task.Delay(100); // 等待100ms后重试
                }
            }
        }

        private void OnFileChanged(FileChangedEventArgs args)
        {
            FileChanged?.Invoke(this, args);
        }

        private void UpdateStatus(MonitorStatus status, string reason, Exception? exception = null)
        {
            Status = status;
            StatusChanged?.Invoke(this, new MonitorStatusChangedEventArgs
            {
                WatchItemId = _config.Id,
                Status = status,
                Reason = reason,
                Exception = exception
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();
            _watcher?.Dispose();
            _bufferTimer?.Dispose();
            _disposed = true;
        }
    }

    // 扩展方法用于安全调用
    internal static class Extensions
    {
        public static void Invoke<T>(this T obj, Action<T> action) where T : class
        {
            if (obj != null)
                action(obj);
        }
    }
}
