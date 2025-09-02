using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WatchFile.Core.Configuration;
using WatchFile.Core.Configuration.Models;
using WatchFile.Core.Events;
using WatchFile.Core.Models;
using WatchFile.Core.Monitoring;

namespace WatchFile.Core
{
    /// <summary>
    /// 文件监控管理器 - 库的主入口点
    /// </summary>
    public class WatchFileManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, DirectoryWatcher> _watchers = new();
        private readonly List<IFileChangedHandler> _handlers = new();
        private readonly ConfigurationManager _configManager;
        private WatchFileConfiguration? _configuration;
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
        /// 是否正在运行
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// 活动监控器数量
        /// </summary>
        public int ActiveWatchersCount => _watchers.Count(w => w.Value.Status == MonitorStatus.Running);

        /// <summary>
        /// 监控项状态
        /// </summary>
        public Dictionary<string, MonitorStatus> WatcherStatuses => 
            _watchers.ToDictionary(w => w.Key, w => w.Value.Status);

        /// <summary>
        /// 初始化文件监控管理器
        /// </summary>
        /// <param name="configPath">配置文件路径</param>
        public WatchFileManager(string? configPath = null)
        {
            _configManager = new ConfigurationManager(configPath);
            
            // 立即加载配置，使 GetAllWatchItems 等方法可以正常工作
            try
            {
                _configuration = _configManager.LoadConfiguration();
            }
            catch
            {
                // 如果配置文件不存在或有问题，记录但不抛异常
                // 这样至少可以创建管理器实例，稍后可以创建默认配置
                _configuration = null;
            }
        }

        /// <summary>
        /// 启动监控
        /// </summary>
        public async Task StartAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WatchFileManager));

            if (IsRunning)
                return;

            try
            {
                // 如果配置尚未加载，则加载配置
                if (_configuration == null)
                {
                    _configuration = _configManager.LoadConfiguration();
                }
                
                // 创建并启动监控器
                await CreateWatchers();
                
                IsRunning = true;
            }
            catch (Exception ex)
            {
                await StopAsync();
                throw new InvalidOperationException($"启动文件监控失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 停止监控
        /// </summary>
        public async Task StopAsync()
        {
            if (!IsRunning)
                return;

            try
            {
                // 停止所有监控器
                var stopTasks = _watchers.Values.Select(watcher => Task.Run(() => watcher.Stop()));
                await Task.WhenAll(stopTasks);

                // 清理监控器
                foreach (var watcher in _watchers.Values)
                {
                    watcher.Dispose();
                }
                _watchers.Clear();

                IsRunning = false;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"停止文件监控失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public async Task ReloadConfigurationAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WatchFileManager));

            var wasRunning = IsRunning;
            
            if (wasRunning)
            {
                await StopAsync();
            }

            if (wasRunning)
            {
                await StartAsync();
            }
        }

        /// <summary>
        /// 添加文件变化处理器
        /// </summary>
        public void AddHandler(IFileChangedHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_handlers)
            {
                if (!_handlers.Contains(handler))
                {
                    _handlers.Add(handler);
                }
            }
        }

        /// <summary>
        /// 移除文件变化处理器
        /// </summary>
        public void RemoveHandler(IFileChangedHandler handler)
        {
            if (handler == null)
                return;

            lock (_handlers)
            {
                _handlers.Remove(handler);
            }
        }

        /// <summary>
        /// 获取监控项配置
        /// </summary>
        public WatchItem? GetWatchItem(string id)
        {
            return _configuration?.WatchItems.FirstOrDefault(w => w.Id == id);
        }

        /// <summary>
        /// 获取所有监控项配置
        /// </summary>
        public IReadOnlyList<WatchItem> GetAllWatchItems()
        {
            return _configuration?.WatchItems.AsReadOnly() ?? new List<WatchItem>().AsReadOnly();
        }

        /// <summary>
        /// 启用监控项
        /// </summary>
        public async Task EnableWatchItemAsync(string id)
        {
            var watchItem = GetWatchItem(id);
            if (watchItem == null)
                throw new ArgumentException($"找不到监控项: {id}", nameof(id));

            if (watchItem.Enabled)
                return;

            watchItem.Enabled = true;
            
            if (IsRunning)
            {
                await CreateWatcherForItem(watchItem);
            }
        }

        /// <summary>
        /// 禁用监控项
        /// </summary>
        public async Task DisableWatchItemAsync(string id)
        {
            var watchItem = GetWatchItem(id);
            if (watchItem == null)
                throw new ArgumentException($"找不到监控项: {id}", nameof(id));

            if (!watchItem.Enabled)
                return;

            watchItem.Enabled = false;

            if (_watchers.TryRemove(id, out var watcher))
            {
                await Task.Run(() =>
                {
                    watcher.Stop();
                    watcher.Dispose();
                });
            }
        }

        private async Task CreateWatchers()
        {
            if (_configuration == null)
                return;

            var createTasks = _configuration.WatchItems
                .Where(item => item.Enabled)
                .Select(CreateWatcherForItem);

            await Task.WhenAll(createTasks);
        }

        private async Task CreateWatcherForItem(WatchItem item)
        {
            try
            {
                var watcher = new DirectoryWatcher(item, _configuration!.GlobalSettings);
                
                // 订阅事件
                watcher.FileChanged += OnWatcherFileChanged;
                watcher.StatusChanged += OnWatcherStatusChanged;

                // 添加到字典
                _watchers.TryAdd(item.Id, watcher);

                // 启动监控
                await Task.Run(() => watcher.Start());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"创建监控器失败 (ID: {item.Id}): {ex.Message}", ex);
            }
        }

        private async void OnWatcherFileChanged(object? sender, FileChangedEventArgs e)
        {
            try
            {
                // 触发事件
                FileChanged?.Invoke(this, e);

                // 调用所有处理器
                var tasks = new List<Task>();
                
                lock (_handlers)
                {
                    foreach (var handler in _handlers)
                    {
                        tasks.Add(handler.HandleFileChanged(e));
                    }
                }

                if (tasks.Count > 0)
                {
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不抛出异常，避免影响其他处理器
                Console.WriteLine($"处理文件变化事件时出错: {ex.Message}");
            }
        }

        private void OnWatcherStatusChanged(object? sender, MonitorStatusChangedEventArgs e)
        {
            StatusChanged?.Invoke(this, e);
        }

        /// <summary>
        /// 手动触发文件解析（用于测试或手动处理）
        /// </summary>
        public async Task<FileParseResult> ParseFileManuallyAsync(string filePath, string watchItemId)
        {
            var watchItem = GetWatchItem(watchItemId);
            if (watchItem == null)
                throw new ArgumentException($"找不到监控项: {watchItemId}", nameof(watchItemId));

            return await Task.Run(() => Parsing.FileParser.ParseFile(filePath, watchItem.FileSettings));
        }

        /// <summary>
        /// 保存当前配置
        /// </summary>
        public void SaveConfiguration(string? path = null)
        {
            if (_configuration == null)
                throw new InvalidOperationException("没有加载的配置可以保存");

            _configManager.SaveConfiguration(_configuration, path);
        }

        /// <summary>
        /// 创建默认配置文件
        /// </summary>
        public void CreateDefaultConfiguration(string? path = null)
        {
            var defaultConfig = ConfigurationManager.CreateDefaultConfiguration();
            _configManager.SaveConfiguration(defaultConfig, path);
        }

        /// <summary>
        /// 验证配置文件
        /// </summary>
        public bool ValidateConfiguration(string? path = null)
        {
            try
            {
                var config = _configManager.LoadConfiguration(path);
                return _configManager.ValidateConfiguration(config);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全删除监控目录下的文件（包括对应的缓存文件），不触发监控事件
        /// </summary>
        /// <param name="filePath">要删除的文件路径（绝对路径）</param>
        /// <param name="watchItemId">对应的监控项ID，如果为null则自动查找</param>
        /// <param name="forceDelete">是否强制删除（清除只读属性）</param>
        /// <returns>删除结果信息</returns>
        public async Task<FileDeleteResult> SafeDeleteMonitoredFileAsync(string filePath, string? watchItemId = null, bool forceDelete = true)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            // .NET Framework 4.6.1 兼容性：手动检查绝对路径
            if (!IsAbsolutePath(filePath))
                throw new ArgumentException("必须提供绝对路径", nameof(filePath));

            var result = new FileDeleteResult
            {
                FilePath = filePath,
                RequestTime = DateTime.Now
            };

            try
            {
                // 1. 查找对应的监控项
                var watchItem = string.IsNullOrEmpty(watchItemId) 
                    ? FindWatchItemByPath(filePath) 
                    : _watchers.Keys.Select(id => _configuration?.WatchItems?.FirstOrDefault(w => w.Id == id))
                              .FirstOrDefault(w => w?.Id == watchItemId);

                if (watchItem == null)
                {
                    result.Success = false;
                    result.ErrorMessage = $"未找到文件 {filePath} 对应的监控项";
                    return result;
                }

                result.WatchItemId = watchItem.Id;
                result.WatchItemName = watchItem.Name;

                // 2. 临时禁用该监控项的删除事件监听（避免自触发）
                DirectoryWatcher? watcher = null;
                _watchers.TryGetValue(watchItem.Id, out watcher);
                var originalDeleteEnabled = watchItem.WatchEvents.Contains(WatchEvent.Deleted);
                
                if (watcher != null && originalDeleteEnabled)
                {
                    // 临时移除删除事件监听
                    await TemporarilyDisableDeleteEvent(watchItem, watcher);
                }

                try
                {
                    // 3. 删除缓存文件（如果存在）
                    var cacheFileDeleted = await DeleteCacheFile(filePath, watchItem, forceDelete);
                    result.CacheFileDeleted = cacheFileDeleted.Success;
                    result.CacheFilePath = cacheFileDeleted.CacheFilePath;
                    
                    if (!cacheFileDeleted.Success && !string.IsNullOrEmpty(cacheFileDeleted.ErrorMessage))
                    {
                        result.Warnings.Add($"缓存文件删除警告: {cacheFileDeleted.ErrorMessage}");
                    }

                    // 4. 删除原文件
                    if (File.Exists(filePath))
                    {
                        // 🔧 使用强制删除方法（清除只读属性）
                        ForceDeleteFile(filePath, forceDelete);
                        result.OriginalFileDeleted = true;
                        result.Message = "文件删除成功";
                    }
                    else
                    {
                        result.OriginalFileDeleted = false;
                        result.Warnings.Add("原文件不存在，可能已被删除");
                    }

                    result.Success = true;
                }
                finally
                {
                    // 5. 恢复删除事件监听
                    if (watcher != null && originalDeleteEnabled)
                    {
                        await Task.Delay(100); // 短暂延迟确保删除操作完成
                        await RestoreDeleteEvent(watchItem, watcher);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                result.Success = false;
                result.ErrorMessage = $"权限不足，无法删除文件: {ex.Message}";
            }
            catch (IOException ex)
            {
                result.Success = false;
                result.ErrorMessage = $"文件I/O错误: {ex.Message}";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"删除文件时发生未知错误: {ex.Message}";
            }

            result.CompletionTime = DateTime.Now;

            return result;
        }

        /// <summary>
        /// 批量安全删除监控目录下的文件
        /// </summary>
        /// <param name="filePaths">要删除的文件路径列表</param>
        /// <param name="watchItemId">对应的监控项ID，如果为null则自动查找</param>
        /// <returns>批量删除结果</returns>
        public async Task<BatchFileDeleteResult> SafeDeleteMonitoredFilesAsync(IEnumerable<string> filePaths, string? watchItemId = null)
        {
            var result = new BatchFileDeleteResult
            {
                RequestTime = DateTime.Now
            };

            var tasks = filePaths.Select(async filePath =>
            {
                try
                {
                    var deleteResult = await SafeDeleteMonitoredFileAsync(filePath, watchItemId);
                    if (deleteResult.Success)
                        result.SuccessCount++;
                    else
                        result.FailureCount++;
                    
                    return deleteResult;
                }
                catch (Exception ex)
                {
                    result.FailureCount++;
                    return new FileDeleteResult
                    {
                        FilePath = filePath,
                        Success = false,
                        ErrorMessage = ex.Message,
                        RequestTime = DateTime.Now,
                        CompletionTime = DateTime.Now
                    };
                }
            });

            result.Results = await Task.WhenAll(tasks);
            result.TotalCount = result.Results.Length;
            result.CompletionTime = DateTime.Now;

            return result;
        }

        #region 私有辅助方法

        /// <summary>
        /// 根据文件路径查找对应的监控项
        /// </summary>
        private WatchItem? FindWatchItemByPath(string filePath)
        {
            if (_configuration?.WatchItems == null) return null;

            foreach (var item in _configuration.WatchItems)
            {
                if (item.Type == WatchType.File)
                {
                    // 文件监控：直接比较路径
                    if (string.Equals(Path.GetFullPath(item.Path), Path.GetFullPath(filePath), StringComparison.OrdinalIgnoreCase))
                        return item;
                }
                else
                {
                    // 目录监控：检查文件是否在监控目录下
                    var itemPath = Path.GetFullPath(item.Path);
                    var fileDir = Path.GetDirectoryName(Path.GetFullPath(filePath));
                    
                    if (fileDir != null)
                    {
                        if (item.Recursive)
                        {
                            // 递归监控：检查是否在子目录中
                            if (fileDir.StartsWith(itemPath, StringComparison.OrdinalIgnoreCase))
                                return item;
                        }
                        else
                        {
                            // 非递归监控：只检查直接目录
                            if (string.Equals(fileDir, itemPath, StringComparison.OrdinalIgnoreCase))
                                return item;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 临时禁用监控项的删除事件
        /// </summary>
        private async Task TemporarilyDisableDeleteEvent(WatchItem watchItem, DirectoryWatcher watcher)
        {
            // 这里可以实现临时禁用逻辑
            // 由于DirectoryWatcher的限制，我们使用短暂延迟来避免自触发
            await Task.Delay(50);
        }

        /// <summary>
        /// 恢复监控项的删除事件
        /// </summary>
        private async Task RestoreDeleteEvent(WatchItem watchItem, DirectoryWatcher watcher)
        {
            // 恢复删除事件监听
            await Task.Delay(50);
        }

        /// <summary>
        /// 删除缓存文件
        /// </summary>
        private async Task<CacheFileDeleteResult> DeleteCacheFile(string originalFilePath, WatchItem watchItem, bool forceDelete = true)
        {
            try
            {
                // 构建缓存文件路径
                var fileName = Path.GetFileName(originalFilePath);
                var watchDir = Path.Combine(Path.GetDirectoryName(originalFilePath) ?? "", 
                                           watchItem.WatchFileSettings.WatchFileDirectory);
                var cacheFilePath = Path.Combine(watchDir, fileName + watchItem.WatchFileSettings.WatchFileExtension);

                var result = new CacheFileDeleteResult
                {
                    CacheFilePath = cacheFilePath
                };

                if (File.Exists(cacheFilePath))
                {
                    // 🔧 使用强制删除方法处理缓存文件
                    ForceDeleteFile(cacheFilePath, forceDelete);
                    result.Success = true;
                }
                else
                {
                    result.Success = true; // 文件不存在也算成功
                    result.ErrorMessage = "缓存文件不存在";
                }

                await Task.CompletedTask; // 保持异步接口一致性
                return result;
            }
            catch (Exception ex)
            {
                return new CacheFileDeleteResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 检查路径是否为绝对路径（.NET Framework 4.6.1 兼容性）
        /// </summary>
        private static bool IsAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            
            // Windows: C:\path 或 \\server\share
            if (path.Length >= 2 && path[1] == ':' && char.IsLetter(path[0]))
                return true;
                
            // UNC 路径: \\server\share
            if (path.StartsWith(@"\\"))
                return true;
                
            return false;
        }

        #endregion

        #region 文件安全删除功能

        /// <summary>
        /// 安全删除监控目录下的指定文件
        /// </summary>
        /// <param name="fileName">要删除的文件名（不包含路径）</param>
        /// <param name="watchItemId">监控项ID，如果为null则删除所有匹配的文件</param>
        /// <param name="forceDelete">是否强制删除（清除只读属性）</param>
        /// <returns>删除结果，包含成功/失败信息</returns>
        /// <exception cref="ArgumentException">文件名为空或包含路径分隔符时抛出</exception>
        /// <exception cref="InvalidOperationException">未找到指定的监控项时抛出</exception>
        public async Task<FileDeleteResult> SafeDeleteFileAsync(string fileName, string? watchItemId = null, bool forceDelete = true)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("文件名不能为空", nameof(fileName));

            if (fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
                throw new ArgumentException("文件名不能包含路径分隔符，请仅提供文件名", nameof(fileName));

            var result = new FileDeleteResult
            {
                FileName = fileName,
                RequestedWatchItemId = watchItemId,
                StartTime = DateTime.Now
            };

            try
            {
                // 如果指定了 watchItemId，则只在该监控项中查找
                IEnumerable<WatchItem> targetItems;
                if (!string.IsNullOrEmpty(watchItemId))
                {
                    var item = GetAllWatchItems().FirstOrDefault(w => w.Id == watchItemId);
                    if (item == null)
                    {
                        throw new InvalidOperationException($"未找到ID为 '{watchItemId}' 的监控项");
                    }
                    targetItems = new[] { item };
                }
                else
                {
                    // 查找所有启用的目录监控项
                    targetItems = GetAllWatchItems().Where(w => w.Enabled && w.Type == WatchType.Directory);
                }

                bool anyFileDeleted = false;
                var deletedFiles = new List<string>();
                var deletedCacheFiles = new List<string>();
                var errors = new List<string>();

                foreach (var watchItem in targetItems)
                {
                    try
                    {
                        var targetFilePath = Path.Combine(watchItem.Path, fileName);
                        
                        // 检查文件是否存在
                        if (!File.Exists(targetFilePath))
                        {
                            result.Messages.Add($"监控项 '{watchItem.Id}': 文件不存在");
                            continue;
                        }

                        // 临时禁用该文件的监控事件（防止删除操作触发监控）
                        await TemporarilyDisableFileMonitoring(targetFilePath, async () =>
                        {
                            // 🔧 强制垃圾回收，确保所有文件流都被释放
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                            
                            // 🔧 简单等待文件系统操作完成，不检查文件占用
                            await Task.Delay(1000);
                            
                            // 🔧 直接使用强制删除方法（清除只读属性）
                            ForceDeleteFile(targetFilePath, forceDelete);
                            deletedFiles.Add(targetFilePath);
                            anyFileDeleted = true;
                            result.Messages.Add($"✅ 已删除主文件: {targetFilePath}");

                            // 删除对应的缓存文件
                            await DeleteCacheFiles(targetFilePath, watchItem, deletedCacheFiles, errors, forceDelete);
                        });
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"删除监控项 '{watchItem.Id}' 中的文件时出错: {ex.Message}";
                        errors.Add(errorMsg);
                        result.Messages.Add($"❌ {errorMsg}");
                    }
                }

                result.DeletedFiles = deletedFiles;
                result.DeletedCacheFiles = deletedCacheFiles;
                result.Errors = errors;
                result.IsSuccess = anyFileDeleted && errors.Count == 0;
                result.EndTime = DateTime.Now;

                if (anyFileDeleted)
                {
                    result.Messages.Add($"🎉 删除操作完成，共删除 {deletedFiles.Count} 个主文件，{deletedCacheFiles.Count} 个缓存文件");
                }
                else
                {
                    result.Messages.Add("⚠️ 没有找到要删除的文件");
                }
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.EndTime = DateTime.Now;
                result.Messages.Add($"❌ 删除操作失败: {ex.Message}");
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        /// <summary>
        /// 临时禁用指定文件的监控，执行操作后恢复
        /// </summary>
        private async Task TemporarilyDisableFileMonitoring(string filePath, Func<Task> action)
        {
            var fileName = Path.GetFileName(filePath);
            var addedExclusions = new Dictionary<string, List<string>>();
            
            try
            {
                // 找到监控此文件的监控器并临时添加排除模式
                foreach (var kvp in _watchers)
                {
                    var watcher = kvp.Value;
                    var config = watcher.GetType().GetField("_config", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(watcher) as WatchItem;
                    
                    if (config != null && watcher.Status == MonitorStatus.Running)
                    {
                        var watcherPath = config.Path;
                        bool shouldExclude = false;

                        // 检查是否在监控范围内
                        if (config.Type == WatchType.Directory)
                        {
                            if (config.Recursive)
                            {
                                shouldExclude = filePath.StartsWith(watcherPath, StringComparison.OrdinalIgnoreCase);
                            }
                            else
                            {
                                shouldExclude = Path.GetDirectoryName(filePath)?.Equals(watcherPath, StringComparison.OrdinalIgnoreCase) == true;
                            }
                        }

                        if (shouldExclude && !config.ExcludePatterns.Contains(fileName))
                        {
                            config.ExcludePatterns.Add(fileName);
                            if (!addedExclusions.ContainsKey(kvp.Key))
                                addedExclusions[kvp.Key] = new List<string>();
                            addedExclusions[kvp.Key].Add(fileName);
                        }
                    }
                }

                // 执行实际操作
                await action();

                // 🔧 增加延迟时间确保文件系统事件处理完成和文件句柄释放
                await Task.Delay(1000); // 从 200ms 增加到 1000ms
            }
            finally
            {
                // 恢复监控（移除临时排除模式）
                foreach (var kvp in addedExclusions)
                {
                    if (_watchers.TryGetValue(kvp.Key, out var watcher))
                    {
                        var config = watcher.GetType().GetField("_config", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(watcher) as WatchItem;
                        
                        if (config != null)
                        {
                            foreach (var exclusion in kvp.Value)
                            {
                                config.ExcludePatterns.Remove(exclusion);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 删除指定文件对应的所有缓存文件
        /// </summary>
        private async Task DeleteCacheFiles(string originalFilePath, WatchItem watchItem, 
            List<string> deletedCacheFiles, List<string> errors, bool forceDelete = true)
        {
            try
            {
                var fileName = Path.GetFileName(originalFilePath);
                var baseDirectory = Path.GetDirectoryName(originalFilePath);
                var watchDirectory = Path.Combine(baseDirectory!, watchItem.WatchFileSettings.WatchFileDirectory);
                var watchFileExtension = watchItem.WatchFileSettings.WatchFileExtension;

                if (!Directory.Exists(watchDirectory))
                {
                    return; // 缓存目录不存在，无需删除
                }

                // 搜索所有相关的缓存文件（包括可能的递归缓存）
                var cacheFiles = Directory.GetFiles(watchDirectory, "*", SearchOption.AllDirectories)
                    .Where(f => 
                    {
                        var name = Path.GetFileName(f);
                        return name.StartsWith(fileName, StringComparison.OrdinalIgnoreCase) && 
                               name.Contains(watchFileExtension);
                    })
                    .ToList();

                foreach (var cacheFile in cacheFiles)
                {
                    try
                    {
                        if (File.Exists(cacheFile))
                        {
                            // 🔧 使用强制删除方法处理缓存文件
                            ForceDeleteFile(cacheFile, forceDelete);
                            deletedCacheFiles.Add(cacheFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"删除缓存文件 '{cacheFile}' 时出错: {ex.Message}";
                        errors.Add(errorMsg);
                    }
                }

                // 尝试删除空的缓存目录
                await TryDeleteEmptyDirectories(watchDirectory);
            }
            catch (Exception ex)
            {
                errors.Add($"处理缓存文件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 尝试删除空的目录结构
        /// </summary>
        private async Task TryDeleteEmptyDirectories(string directory)
        {
            try
            {
                if (!Directory.Exists(directory)) return;

                // 递归删除空的子目录
                var subDirectories = Directory.GetDirectories(directory);
                foreach (var subDir in subDirectories)
                {
                    await TryDeleteEmptyDirectories(subDir);
                }

                // 如果目录为空，则删除它
                if (!Directory.GetFiles(directory).Any() && !Directory.GetDirectories(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch
            {
                // 静默忽略删除空目录时的错误
            }
        }

        /// <summary>
        /// 强制删除文件（清除只读属性后删除）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="forceDelete">是否强制删除（清除只读属性）</param>
        private static void ForceDeleteFile(string filePath, bool forceDelete = true)
        {
            if (!File.Exists(filePath))
                return;

            try
            {
                if (forceDelete)
                {
                    // 🔧 一次性清除所有可能阻止删除的属性
                    var attributes = File.GetAttributes(filePath);
                    var originalAttributes = attributes;
                    
                    // 移除只读、隐藏、系统属性
                    attributes &= ~FileAttributes.ReadOnly;
                    attributes &= ~FileAttributes.Hidden;
                    attributes &= ~FileAttributes.System;
                    
                    // 只有当属性发生变化时才设置
                    if (attributes != originalAttributes)
                    {
                        File.SetAttributes(filePath, attributes);
                        
                        // 🔧 添加调试信息，确认属性已清除
                        Console.WriteLine($"[FORCE DELETE] 已清除文件属性: {filePath}");
                        Console.WriteLine($"[FORCE DELETE] 原属性: {originalAttributes} -> 新属性: {attributes}");
                    }
                }

                // 删除文件
                File.Delete(filePath);
                Console.WriteLine($"[FORCE DELETE] 文件删除成功: {filePath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                // 如果仍然无法删除，提供更详细的错误信息
                var currentAttributes = File.Exists(filePath) ? File.GetAttributes(filePath).ToString() : "文件不存在";
                throw new UnauthorizedAccessException($"无法删除文件 '{filePath}'，权限不足或文件仍被占用。当前文件属性: {currentAttributes}。原始错误: {ex.Message}");
            }
            catch (IOException ex)
            {
                // IO 异常，可能是文件被占用
                throw new IOException($"删除文件 '{filePath}' 时发生IO错误，文件可能被其他程序占用。错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 等待文件释放（确保文件可以被安全删除）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="delayMs">每次重试间隔毫秒数</param>
        private async Task WaitForFileRelease(string filePath, int maxRetries = 15, int delayMs = 300)
        {
            var retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    // 🔧 修改策略：检查文件是否可以被删除，而不是独占访问
                    // 尝试以删除兼容的方式打开文件
                    using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read);
                    
                    // 如果能成功打开，再尝试直接删除测试（创建临时副本方式）
                    var tempTestFile = Path.GetTempFileName();
                    try
                    {
                        File.Copy(filePath, tempTestFile, true);
                        File.Delete(tempTestFile);
                        return; // 文件可以被复制和删除，说明已释放
                    }
                    catch
                    {
                        if (File.Exists(tempTestFile))
                            File.Delete(tempTestFile);
                        throw; // 传播异常到外层处理
                    }
                }
                catch (FileNotFoundException)
                {
                    // 文件不存在，认为已释放
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    // 权限问题，增加重试
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        throw new InvalidOperationException($"文件 '{filePath}' 访问权限被拒绝，无法删除。可能原因：文件被其他程序占用、只读属性或权限不足");
                    }
                    
                    // 等待更长时间再重试
                    await Task.Delay(delayMs * 2);
                }
                catch (IOException)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        throw new InvalidOperationException($"文件 '{filePath}' 在 {maxRetries * delayMs}ms 内无法释放，可能被其他进程占用");
                    }
                    
                    // 等待后重试
                    await Task.Delay(delayMs);
                }
            }
        }

        #endregion

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                StopAsync().Wait(5000); // 等待最多5秒
            }
            catch (Exception ex)
            {
                Console.WriteLine($"停止监控时出错: {ex.Message}");
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// 简单的文件变化处理器基类
    /// </summary>
    public abstract class FileChangedHandlerBase : IFileChangedHandler
    {
        /// <summary>
        /// 处理文件变化事件
        /// </summary>
        /// <param name="args">文件变化事件参数</param>
        /// <returns>处理任务</returns>
        public abstract Task HandleFileChanged(FileChangedEventArgs args);

        /// <summary>
        /// 判断是否应该处理此文件变化事件
        /// </summary>
        /// <param name="args">文件变化事件参数</param>
        /// <returns>是否应该处理</returns>
        protected virtual bool ShouldHandle(FileChangedEventArgs args)
        {
            return args.IsSuccess;
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="exception">异常对象</param>
        protected virtual void LogError(string message, Exception? exception = null)
        {
            Console.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
            if (exception != null)
            {
                Console.WriteLine($"[ERROR] Exception: {exception}");
            }
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        /// <param name="message">日志消息</param>
        protected virtual void LogInfo(string message)
        {
            Console.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
        }
    }
}
