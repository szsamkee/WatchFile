using System;
using System.Collections.Generic;
using System.IO;
using WatchFile.Core.Configuration.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace WatchFile.Core.Configuration
{
    /// <summary>
    /// 配置文件管理器
    /// </summary>
    public class ConfigurationManager
    {
        private readonly string _defaultConfigPath;

        public ConfigurationManager(string? configPath = null)
        {
            _defaultConfigPath = configPath ?? "watchfile-config.json";
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        public WatchFileConfiguration LoadConfiguration(string? path = null)
        {
            var configPath = path ?? _defaultConfigPath;
            
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"配置文件不存在: {configPath}");
            }

            try
            {
                var jsonContent = File.ReadAllText(configPath);
                
                var settings = new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore
                };
                
                var config = JsonConvert.DeserializeObject<WatchFileConfiguration>(jsonContent, settings);
                
                if (config == null)
                {
                    throw new InvalidOperationException("无法解析配置文件");
                }

                ValidateConfiguration(config);
                return config;
            }
            catch (Exception ex) when (!(ex is FileNotFoundException))
            {
                throw new InvalidOperationException($"加载配置文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 保存配置文件
        /// </summary>
        public void SaveConfiguration(WatchFileConfiguration config, string? path = null)
        {
            var configPath = path ?? _defaultConfigPath;
            
            try
            {
                ValidateConfiguration(config);
                
                var directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore
                };
                
                var jsonContent = JsonConvert.SerializeObject(config, settings);
                
                File.WriteAllText(configPath, jsonContent);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"保存配置文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 验证配置文件有效性
        /// </summary>
        public bool ValidateConfiguration(WatchFileConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "配置不能为空");

            if (config.GlobalSettings == null)
                throw new InvalidOperationException("全局设置不能为空");

            if (config.WatchItems == null)
                throw new InvalidOperationException("监控项列表不能为空");

            // 验证全局设置
            ValidateGlobalSettings(config.GlobalSettings);

            // 验证监控项
            foreach (var item in config.WatchItems)
            {
                ValidateWatchItem(item);
            }

            return true;
        }

        private void ValidateGlobalSettings(GlobalSettings settings)
        {
            if (settings.BufferTimeMs < 0)
                throw new InvalidOperationException("缓冲时间不能为负数");

            if (settings.MaxRetries < 0)
                throw new InvalidOperationException("最大重试次数不能为负数");

            if (!Enum.TryParse<LogLevel>(settings.LogLevel, true, out _))
                throw new InvalidOperationException($"无效的日志级别: {settings.LogLevel}");
        }

        private void ValidateWatchItem(WatchItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                throw new InvalidOperationException("监控项ID不能为空");

            if (string.IsNullOrWhiteSpace(item.Path))
                throw new InvalidOperationException($"监控项 {item.Id} 的路径不能为空");

            if (item.Type == WatchType.Directory && !Directory.Exists(item.Path))
                throw new InvalidOperationException($"监控目录不存在: {item.Path}");

            if (item.Type == WatchType.File)
            {
                var directory = Path.GetDirectoryName(item.Path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    throw new InvalidOperationException($"文件所在目录不存在: {directory}");
            }

            if (item.WatchEvents == null || item.WatchEvents.Count == 0)
                throw new InvalidOperationException($"监控项 {item.Id} 必须指定至少一个监控事件");

            if (item.FileSettings != null)
            {
                ValidateFileSettings(item.FileSettings, item.Id);
            }
        }

        private void ValidateFileSettings(FileSettings settings, string itemId)
        {
            if (settings.FileType == FileType.Excel && string.IsNullOrWhiteSpace(settings.SheetName))
                throw new InvalidOperationException($"监控项 {itemId} 的Excel文件必须指定工作表名称");

            if (settings.StartRow < 1)
                throw new InvalidOperationException($"监控项 {itemId} 的开始行号必须大于0");

            if (settings.ColumnMappings != null)
            {
                foreach (var mapping in settings.ColumnMappings)
                {
                    if (string.IsNullOrWhiteSpace(mapping.TargetName))
                        throw new InvalidOperationException($"监控项 {itemId} 的列映射目标名称不能为空");

                    if (mapping.SourceColumn == null)
                        throw new InvalidOperationException($"监控项 {itemId} 的列映射源列不能为空");
                }
            }
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static WatchFileConfiguration CreateDefaultConfiguration()
        {
            return new WatchFileConfiguration
            {
                Version = "1.0",
                GlobalSettings = new GlobalSettings
                {
                    EnableLogging = true,
                    LogLevel = "Info",
                    BufferTimeMs = 500,
                    MaxRetries = 3,
                    LogFilePath = "logs/watchfile.log"
                },
                WatchItems = new List<WatchItem>
                {
                    new WatchItem
                    {
                        Id = "sample-monitor",
                        Name = "示例监控",
                        Enabled = false,
                        Path = @"C:\Data",
                        Type = WatchType.Directory,
                        Recursive = true,
                        FileFilters = new List<string> { "*.csv", "*.xlsx" },
                        WatchEvents = new List<WatchEvent> { WatchEvent.Created, WatchEvent.Modified },
                        FileSettings = new FileSettings
                        {
                            FileType = FileType.CSV,
                            HasHeader = true,
                            Delimiter = ",",
                            Encoding = "UTF-8",
                            ColumnMappings = new List<ColumnMapping>
                            {
                                new ColumnMapping
                                {
                                    SourceColumn = "Name",
                                    TargetName = "Name",
                                    DataType = DataType.String,
                                    Required = true
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
