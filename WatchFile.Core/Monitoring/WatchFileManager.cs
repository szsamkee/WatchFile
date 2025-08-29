using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WatchFile.Core.Configuration.Models;
using WatchFile.Core.Events;
using WatchFile.Core.Parsing;

namespace WatchFile.Core.Monitoring
{
    /// <summary>
    /// 监控临时文件管理器
    /// </summary>
    public class WatchFileManager
    {
        private readonly WatchItem _config;
        private readonly string _watchDirectory;
        private readonly object _lockObject = new();

        public WatchFileManager(WatchItem config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            // 构建临时文件目录路径
            var basePath = _config.Type == WatchType.Directory ? _config.Path : Path.GetDirectoryName(_config.Path);
            _watchDirectory = Path.Combine(basePath!, _config.WatchFileSettings.WatchFileDirectory);
        }

        /// <summary>
        /// 初始化监控临时文件
        /// </summary>
        public async Task InitializeWatchFilesAsync()
        {
            try
            {
                // 确保临时文件目录存在
                if (!Directory.Exists(_watchDirectory))
                {
                    Directory.CreateDirectory(_watchDirectory);
                }

                if (_config.Type == WatchType.File)
                {
                    // 单文件监控
                    if (File.Exists(_config.Path))
                    {
                        await CreateWatchFileAsync(_config.Path);
                    }
                }
                else if (_config.Type == WatchType.Directory)
                {
                    // 目录监控
                    if (Directory.Exists(_config.Path))
                    {
                        var searchOption = _config.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                        var files = Directory.GetFiles(_config.Path, "*.*", searchOption);

                        var filteredFiles = files.Where(ShouldMonitorFile).ToList();
                        
                        // 限制并发创建临时文件的数量
                        var semaphore = new System.Threading.SemaphoreSlim(_config.WatchFileSettings.MaxConcurrentFiles, _config.WatchFileSettings.MaxConcurrentFiles);
                        var tasks = filteredFiles.Select(async filePath =>
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                await CreateWatchFileAsync(filePath);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        });

                        await Task.WhenAll(tasks);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"初始化监控临时文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 处理文件变化
        /// </summary>
        public async Task<DataChangeDetails?> ProcessFileChangeAsync(string filePath, WatcherChangeTypes changeType)
        {
            var watchFilePath = GetWatchFilePath(filePath);

            try
            {
                switch (changeType)
                {
                    case WatcherChangeTypes.Created:
                        return await HandleFileCreatedAsync(filePath, watchFilePath);

                    case WatcherChangeTypes.Changed:
                        return await HandleFileModifiedAsync(filePath, watchFilePath);

                    case WatcherChangeTypes.Deleted:
                        return await HandleFileDeletedAsync(filePath, watchFilePath);

                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                if (_config.WatchFileSettings.ThrowOnMissingWatchFile)
                {
                    throw new InvalidOperationException($"处理文件变化失败: {ex.Message}", ex);
                }
                
                // 临时文件问题时，按新增文件处理
                return await HandleFileCreatedAsync(filePath, watchFilePath);
            }
        }

        /// <summary>
        /// 处理新建文件
        /// </summary>
        private async Task<DataChangeDetails> HandleFileCreatedAsync(string filePath, string watchFilePath)
        {
            if (!File.Exists(filePath))
                return new DataChangeDetails();

            // 解析新文件
            var parseResult = FileParser.ParseFile(filePath, _config.FileSettings);
            
            var changeDetails = new DataChangeDetails();
            if (parseResult.IsSuccess)
            {
                // 整个文件都是新增内容
                changeDetails.AddedRows = new List<Dictionary<string, object>>(parseResult.Data);
            }

            // 记录差异日志
            if (_config.WatchFileSettings.EnableDifferenceLogging)
            {
                await LogDifferenceAsync("FILE_CREATED", filePath, changeDetails);
            }

            // 创建临时文件
            await CreateWatchFileAsync(filePath);

            return changeDetails;
        }

        /// <summary>
        /// 处理文件修改
        /// </summary>
        private async Task<DataChangeDetails> HandleFileModifiedAsync(string filePath, string watchFilePath)
        {
            if (!File.Exists(filePath))
                return new DataChangeDetails();

            // 检查临时文件是否存在
            if (!File.Exists(watchFilePath))
            {
                // 临时文件丢失，按新增文件处理
                return await HandleFileCreatedAsync(filePath, watchFilePath);
            }

            // 解析当前文件和临时文件
            var currentResult = FileParser.ParseFile(filePath, _config.FileSettings);
            var previousResult = FileParser.ParseFile(watchFilePath, _config.FileSettings);

            var changeDetails = new DataChangeDetails();
            
            if (currentResult.IsSuccess && previousResult.IsSuccess)
            {
                // 计算差异
                changeDetails = CalculateDataChanges(previousResult.Data, currentResult.Data);
            }
            else if (currentResult.IsSuccess)
            {
                // 之前的文件解析失败，当前成功，视为全部新增
                changeDetails.AddedRows = new List<Dictionary<string, object>>(currentResult.Data);
            }

            // 记录差异日志
            if (_config.WatchFileSettings.EnableDifferenceLogging && changeDetails.HasChanges)
            {
                await LogDifferenceAsync("FILE_MODIFIED", filePath, changeDetails);
            }

            // 更新临时文件
            if (currentResult.IsSuccess)
            {
                await UpdateWatchFileAsync(filePath, watchFilePath);
            }

            return changeDetails;
        }

        /// <summary>
        /// 处理文件删除
        /// </summary>
        private async Task<DataChangeDetails> HandleFileDeletedAsync(string filePath, string watchFilePath)
        {
            var changeDetails = new DataChangeDetails();

            // 如果临时文件存在，读取其内容作为删除的数据
            if (File.Exists(watchFilePath))
            {
                var previousResult = FileParser.ParseFile(watchFilePath, _config.FileSettings);
                if (previousResult.IsSuccess)
                {
                    changeDetails.DeletedRows = new List<Dictionary<string, object>>(previousResult.Data);
                }

                // 删除临时文件
                try
                {
                    File.Delete(watchFilePath);
                }
                catch
                {
                    // 忽略删除临时文件的错误
                }
            }

            // 记录差异日志
            if (_config.WatchFileSettings.EnableDifferenceLogging && changeDetails.HasChanges)
            {
                await LogDifferenceAsync("FILE_DELETED", filePath, changeDetails);
            }

            return changeDetails;
        }

        /// <summary>
        /// 创建临时文件
        /// </summary>
        private async Task CreateWatchFileAsync(string filePath)
        {
            var watchFilePath = GetWatchFilePath(filePath);
            
            try
            {
                // 确保临时文件目录存在
                var watchFileDir = Path.GetDirectoryName(watchFilePath);
                if (!Directory.Exists(watchFileDir))
                {
                    Directory.CreateDirectory(watchFileDir!);
                }

                // 复制原文件到临时文件
                await Task.Run(() => File.Copy(filePath, watchFilePath, true));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"创建监控临时文件失败: {watchFilePath}, 错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 更新临时文件
        /// </summary>
        private async Task UpdateWatchFileAsync(string filePath, string watchFilePath)
        {
            try
            {
                await Task.Run(() => File.Copy(filePath, watchFilePath, true));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"更新监控临时文件失败: {watchFilePath}, 错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取临时文件路径
        /// </summary>
        private string GetWatchFilePath(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            string relativePath;
            
            if (_config.Type == WatchType.Directory)
            {
                // 计算相对路径（兼容.NET Framework）
                var fullPath = Path.GetFullPath(filePath);
                var basePath = Path.GetFullPath(_config.Path);
                
                if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = fullPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                else
                {
                    relativePath = fileName;
                }
            }
            else
            {
                relativePath = fileName;
            }
            
            var watchFileName = fileName + _config.WatchFileSettings.WatchFileExtension;
            var watchFileDir = Path.GetDirectoryName(relativePath);
            
            if (string.IsNullOrEmpty(watchFileDir) || watchFileDir == ".")
            {
                return Path.Combine(_watchDirectory, watchFileName);
            }
            else
            {
                return Path.Combine(_watchDirectory, watchFileDir, watchFileName);
            }
        }

        /// <summary>
        /// 检查是否应该监控该文件
        /// </summary>
        private bool ShouldMonitorFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);

            // 检查排除模式
            if (_config.ExcludePatterns.Any())
            {
                foreach (var excludePattern in _config.ExcludePatterns)
                {
                    var pattern = excludePattern.Replace("*", ".*").Replace("?", ".");
                    if (Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase))
                    {
                        return false;
                    }
                }
            }

            // 检查包含模式
            if (_config.FileFilters.Any())
            {
                var shouldInclude = _config.FileFilters.Any(filter =>
                {
                    var pattern = filter.Replace("*", ".*").Replace("?", ".");
                    return Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase);
                });

                if (!shouldInclude)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 计算数据变化
        /// </summary>
        private DataChangeDetails CalculateDataChanges(List<Dictionary<string, object>> oldData, List<Dictionary<string, object>> newData)
        {
            var changeDetails = new DataChangeDetails();
            
            // 创建用于比较的字典（基于行的内容哈希）
            var oldDataMap = new Dictionary<string, Dictionary<string, object>>();
            var newDataMap = new Dictionary<string, Dictionary<string, object>>();
            
            // 为旧数据创建映射
            for (int i = 0; i < oldData.Count; i++)
            {
                var rowKey = GenerateRowKey(oldData[i]);
                oldDataMap[rowKey] = oldData[i];
            }
            
            // 为新数据创建映射
            for (int i = 0; i < newData.Count; i++)
            {
                var rowKey = GenerateRowKey(newData[i]);
                newDataMap[rowKey] = newData[i];
            }
            
            // 找出新增的行
            foreach (var kvp in newDataMap)
            {
                if (!oldDataMap.ContainsKey(kvp.Key))
                {
                    changeDetails.AddedRows.Add(kvp.Value);
                }
            }
            
            // 找出删除的行
            foreach (var kvp in oldDataMap)
            {
                if (!newDataMap.ContainsKey(kvp.Key))
                {
                    changeDetails.DeletedRows.Add(kvp.Value);
                }
            }
            
            // 检查修改的行（基于位置的比较）
            var minCount = Math.Min(oldData.Count, newData.Count);
            for (int i = 0; i < minCount; i++)
            {
                var oldRow = oldData[i];
                var newRow = newData[i];
                var fieldChanges = new List<FieldChange>();
                
                // 检查每个字段的变化
                var allKeys = oldRow.Keys.Union(newRow.Keys).ToList();
                foreach (var key in allKeys)
                {
                    var oldValue = oldRow.ContainsKey(key) ? oldRow[key] : null;
                    var newValue = newRow.ContainsKey(key) ? newRow[key] : null;
                    
                    if (!object.Equals(oldValue, newValue))
                    {
                        var changeType = FieldChangeType.Modified;
                        if (!oldRow.ContainsKey(key))
                            changeType = FieldChangeType.Added;
                        else if (!newRow.ContainsKey(key))
                            changeType = FieldChangeType.Removed;
                            
                        fieldChanges.Add(new FieldChange
                        {
                            FieldName = key,
                            OldValue = oldValue,
                            NewValue = newValue,
                            ChangeType = changeType
                        });
                    }
                }
                
                if (fieldChanges.Count > 0)
                {
                    changeDetails.ModifiedRows.Add(new RowChange
                    {
                        RowIndex = i,
                        OldValues = new Dictionary<string, object>(oldRow),
                        NewValues = new Dictionary<string, object>(newRow),
                        FieldChanges = fieldChanges
                    });
                }
            }
            
            return changeDetails;
        }
        
        private string GenerateRowKey(Dictionary<string, object> row)
        {
            // 基于行内容生成一个唯一键
            var values = row.Values.Select(v => v?.ToString() ?? "").OrderBy(v => v);
            return string.Join("|", values);
        }

        /// <summary>
        /// 记录差异日志
        /// </summary>
        private async Task LogDifferenceAsync(string operationType, string filePath, DataChangeDetails changeDetails)
        {
            try
            {
                var logPath = _config.WatchFileSettings.DifferenceLogPath;
                var logDir = Path.GetDirectoryName(logPath);
                
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                var logEntry = new StringBuilder();
                logEntry.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {operationType}: {filePath}");
                logEntry.AppendLine($"  变化摘要: {changeDetails.GetSummary()}");
                
                if (changeDetails.AddedRows.Count > 0)
                {
                    logEntry.AppendLine($"  新增行数: {changeDetails.AddedRows.Count}");
                }
                
                if (changeDetails.DeletedRows.Count > 0)
                {
                    logEntry.AppendLine($"  删除行数: {changeDetails.DeletedRows.Count}");
                }
                
                if (changeDetails.ModifiedRows.Count > 0)
                {
                    logEntry.AppendLine($"  修改行数: {changeDetails.ModifiedRows.Count}");
                }
                
                logEntry.AppendLine();

                // 兼容.NET Framework的异步文件写入
                await Task.Run(() => File.AppendAllText(logPath, logEntry.ToString(), Encoding.UTF8));
            }
            catch
            {
                // 忽略日志记录错误
            }
        }
    }
}
