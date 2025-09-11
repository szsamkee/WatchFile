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

        /// <summary>
        /// 文件变化事件
        /// </summary>
        public event EventHandler<FileChangedEventArgs>? FileChanged;
        
        /// <summary>
        /// 监控状态变化事件
        /// </summary>
        public event EventHandler<MonitorStatusChangedEventArgs>? StatusChanged;

        /// <summary>
        /// 离线变化检测完成事件
        /// </summary>
        public event EventHandler<OfflineChangesDetectedEventArgs>? OfflineChangesDetected;

        /// <summary>
        /// 当前监控状态
        /// </summary>
        public MonitorStatus Status { get; private set; } = MonitorStatus.Stopped;

        /// <summary>
        /// 初始化目录监控器
        /// </summary>
        /// <param name="config">监控项配置</param>
        /// <param name="globalSettings">全局设置</param>
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

                // 先执行离线变化检测（在创建watchfile之前）
                await PerformOfflineChangeDetection();

                // 初始化临时文件（为现有文件创建watchfile）
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
            }
            catch
            {
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
                        args.CurrentData = parseResult.Data; // 设置变化后的完整数据
                    }
                    else
                    {
                        args.Exception = parseResult.Exception;
                        // 即使解析失败也要触发事件，不要抛出异常
                    }
                }

                // 触发文件变化事件（同步调用，确保用户可以设置ProcessResult）
                OnFileChanged(args);
                
                // 🚀 智能删除：根据用户设置的ProcessResult决定是否删除文件
                await HandleAutoDeleteIfEnabled(args, filePath);
            }
            catch (Exception ex)
            {
                // 确保异常不会阻止后续的文件监控
                args.Exception = ex;
                OnFileChanged(args);
                
                // 不要重新抛出异常，避免影响监控器的运行状态
            }
        }

        /// <summary>
        /// 处理自动删除功能（如果配置启用）
        /// </summary>
        private async Task HandleAutoDeleteIfEnabled(FileChangedEventArgs args, string filePath)
        {
            try
            {
                // 检查配置是否启用自动删除
                if (!_config.DeleteAfterProcessing)
                    return;

                // 根据删除策略决定是否删除文件
                if (!ShouldDeleteFile(args))
                {
                    if (!string.IsNullOrEmpty(args.ProcessResultReason))
                    {
                        Console.WriteLine($"[AUTO DELETE] 文件保留: {Path.GetFileName(filePath)} - {args.ProcessResultReason}");
                    }
                    return;
                }

                // 只对成功处理的创建和修改事件执行删除
                if (!args.IsSuccess || 
                    (args.ChangeType != WatcherChangeTypes.Created && args.ChangeType != WatcherChangeTypes.Changed))
                    return;

                // 确保文件仍然存在
                if (!File.Exists(filePath))
                    return;

                Console.WriteLine($"[AUTO DELETE] 根据处理结果({args.ProcessResult})删除文件: {Path.GetFileName(filePath)}");

                // 等待一段时间确保文件处理完成
                await Task.Delay(1000);

                // 调用 WatchFileManager 的内部删除方法
                var fileName = Path.GetFileName(filePath);
                await _watchFileManager.TriggerAutoDeleteAsync(fileName, _config.Id);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUTO DELETE] 自动删除处理异常: {ex.Message}");
                // 不要重新抛出异常，避免影响监控器运行
            }
        }

        /// <summary>
        /// 根据删除策略和处理结果判断是否应该删除文件
        /// </summary>
        private bool ShouldDeleteFile(FileChangedEventArgs args)
        {
            var deletePolicy = _config.DeletePolicy;
            
            // 根据删除策略类型判断
            switch (deletePolicy.Strategy)
            {
                case DeleteStrategy.Always:
                    return true;
                    
                case DeleteStrategy.Never:
                    return false;
                    
                case DeleteStrategy.RespectProcessResult:
                default:
                    var processResultString = args.ProcessResult.ToString();
                    
                    // 检查是否在删除列表中
                    if (deletePolicy.DeleteOn.Contains(processResultString))
                        return true;
                        
                    // 检查是否在保留列表中
                    if (deletePolicy.KeepOn.Contains(processResultString))
                        return false;
                        
                    // 默认行为：Success删除，其他保留
                    return args.ProcessResult == WatchFile.Core.Events.FileProcessResult.Success;
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

        /// <summary>
        /// 执行离线变化检测
        /// </summary>
        private async Task PerformOfflineChangeDetection()
        {
            if (_globalSettings?.OfflineChangeDetection == null)
            {
                return;
            }
            
            if (_globalSettings.OfflineChangeDetection.Enabled != true)
            {
                return;
            }

            var detectionStartTime = DateTime.Now;
            var eventArgs = new OfflineChangesDetectedEventArgs
            {
                WatchItemId = _config.Id,
                WatchItemName = _config.Name,
                DetectionStartTime = detectionStartTime
            };

            try
            {
                // 执行离线变化检测
                var offlineChanges = await _watchFileManager.DetectOfflineChangesAsync(_globalSettings.OfflineChangeDetection);
                eventArgs.Changes = offlineChanges;
                eventArgs.DetectionEndTime = DateTime.Now;

                // 根据配置决定是否自动触发FileChanged事件
                if (_globalSettings.OfflineChangeDetection.AutoTriggerFileChangedEvents)
                {
                    // 自动模式：为每个检测到的变化根据WatchItem配置触发相应的FileChanged事件
                    foreach (var change in offlineChanges)
                    {
                        // 检查当前变化类型是否在监控事件列表中
                        if (ShouldTriggerEventForOfflineChange(change))
                        {
                            await ProcessOfflineChange(change);
                        }
                    }
                }

                // 始终触发离线变化检测完成事件（用户可选择性处理）
                OfflineChangesDetected?.Invoke(this, eventArgs);

                if (offlineChanges.Count > 0)
                {
                    var summary = eventArgs.GetSummary();
                    // 可以选择保留这个信息输出，因为它提供有用的监控反馈
                    // Console.WriteLine($"[OFFLINE DETECTION] {_config.Name}: {summary}");
                }
            }
            catch (Exception ex)
            {
                eventArgs.Exception = ex;
                eventArgs.DetectionEndTime = DateTime.Now;
                OfflineChangesDetected?.Invoke(this, eventArgs);
                
                // 不要重新抛出异常，避免阻止监控启动
            }
        }

        /// <summary>
        /// 处理检测到的离线变化
        /// </summary>
        private async Task ProcessOfflineChange(OfflineChangeInfo changeInfo)
        {
            try
            {
                var changeType = changeInfo.ChangeType switch
                {
                    OfflineChangeType.Created => WatcherChangeTypes.Created,
                    OfflineChangeType.Modified => WatcherChangeTypes.Changed,
                    OfflineChangeType.Deleted => WatcherChangeTypes.Deleted,
                    OfflineChangeType.Recreated => WatcherChangeTypes.Created,
                    _ => WatcherChangeTypes.Changed
                };

                // 创建文件变化事件参数
                var args = new FileChangedEventArgs
                {
                    WatchItemId = _config.Id,
                    WatchItemName = _config.Name,
                    FilePath = changeInfo.FilePath,
                    ChangeType = changeType,
                    Timestamp = changeInfo.DetectedTime,
                    IsOfflineChange = true // 标记为离线变化
                };

                // 设置文件大小
                if (changeInfo.OriginalFileSize.HasValue)
                {
                    args.FileSize = changeInfo.OriginalFileSize.Value;
                }

                // 对于删除的文件，不需要解析内容
                if (changeType == WatcherChangeTypes.Deleted)
                {
                    // 可以从删除的watchfile中读取之前的数据作为PreviousData
                    OnFileChanged(args);
                    return;
                }

                // 对于存在的文件，解析当前内容
                if (File.Exists(changeInfo.FilePath))
                {
                    var parseResult = FileParser.ParseFile(changeInfo.FilePath, _config.FileSettings);
                    if (parseResult.IsSuccess)
                    {
                        args.CurrentData = parseResult.Data;

                        // 如果是修改事件，尝试获取变化详情
                        if (changeType == WatcherChangeTypes.Changed)
                        {
                            var changeDetails = await _watchFileManager.ProcessFileChangeAsync(changeInfo.FilePath, changeType);
                            args.ChangeDetails = changeDetails;
                        }
                    }
                    else
                    {
                        args.Exception = parseResult.Exception;
                    }
                }

                // 触发文件变化事件
                OnFileChanged(args);
                
                // 🚀 处理自动删除（如果启用）
                await HandleAutoDeleteIfEnabled(args, changeInfo.FilePath);
            }
            catch
            {
                // 静默处理异常，避免阻止监控启动
            }
        }

        /// <summary>
        /// 判断离线变化是否应该触发FileChanged事件
        /// </summary>
        private bool ShouldTriggerEventForOfflineChange(OfflineChangeInfo change)
        {
            var changeType = change.ChangeType switch
            {
                OfflineChangeType.Created => WatchEvent.Created,
                OfflineChangeType.Modified => WatchEvent.Modified,
                OfflineChangeType.Deleted => WatchEvent.Deleted,
                OfflineChangeType.Recreated => WatchEvent.Created,
                _ => WatchEvent.Modified
            };

            return _config.WatchEvents.Contains(changeType);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
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
