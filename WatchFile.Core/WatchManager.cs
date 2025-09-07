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
    public class WatchManager : IDisposable
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
        /// 离线变化检测完成事件
        /// </summary>
        public event EventHandler<OfflineChangesDetectedEventArgs>? OfflineChangesDetected;

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
        public WatchManager(string? configPath = null)
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
                throw new ObjectDisposedException(nameof(WatchManager));

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
                throw new ObjectDisposedException(nameof(WatchManager));

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

            var enabledItems = _configuration.WatchItems.Where(item => item.Enabled).ToList();

            var createTasks = enabledItems.Select(CreateWatcherForItem);

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
                watcher.OfflineChangesDetected += OnWatcherOfflineChangesDetected;

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

        private void OnWatcherOfflineChangesDetected(object? sender, OfflineChangesDetectedEventArgs e)
        {
            OfflineChangesDetected?.Invoke(this, e);
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
    }
}
