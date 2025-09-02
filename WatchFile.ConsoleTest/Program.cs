using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WatchFile.Core;
using WatchFile.Core.Events;
using WatchFile.Core.Configuration;
using WatchFile.Core.Configuration.Models;

namespace WatchFile.ConsoleTest
{
    class Program
    {
        private static WatchFileManager? _manager;

        static async Task Main(string[] args)
        {
            // 注册编码提供程序以支持GB2312等编码
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
            // 设置控制台编码以支持中文显示
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            
            Console.WriteLine("=== WatchFile 智能监控程序 ===");
            Console.WriteLine("版本: 2.0.0");
            Console.WriteLine("支持: .NET Framework 4.6.1+ 和 .NET 6+");
            Console.WriteLine("功能: CSV/Excel 文件智能变化分析");
            Console.WriteLine("优化: 工控环境大量小文件监控");
            Console.WriteLine("特色: 临时文件缓存 + 详细差异分析");
            Console.WriteLine();

            try
            {
                // 设置配置文件路径
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-config.json");
                
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"配置文件不存在: {configPath}");
                    Console.WriteLine("正在创建默认测试配置...");
                    CreateDefaultTestConfig(configPath);
                    Console.WriteLine("默认配置已创建，请根据需要修改配置文件。");
                    Console.WriteLine();
                }

                // 创建管理器
                _manager = new WatchFileManager(configPath);

                // 🧪 测试新的静态方法 LoadAndValidateConfiguration
                Console.WriteLine("=== 测试 LoadAndValidateConfiguration 静态方法 ===");
                Console.WriteLine("测试 v2.1.0 新增的一次性配置加载和校验方法...");
                
                try
                {
                    var (isValid, config, errorMessage) = ConfigurationManager.LoadAndValidateConfiguration(configPath);
                    
                    if (isValid && config != null)
                    {
                        Console.WriteLine($"✅ 静态方法成功加载配置: {config.WatchItems.Count} 个监控项");
                        Console.WriteLine($"   版本: {config.Version}");
                        Console.WriteLine($"   全局设置 - 日志级别: {config.GlobalSettings.LogLevel}");
                        Console.WriteLine($"   全局设置 - 缓冲时间: {config.GlobalSettings.BufferTimeMs}ms");
                        
                        foreach (var item in config.WatchItems)
                        {
                            Console.WriteLine($"   • {item.Name} ({item.Id}) - {(item.Enabled ? "启用" : "禁用")}");
                            Console.WriteLine($"     路径: {item.Path} | 类型: {item.FileSettings.FileType}");
                        }
                        
                        Console.WriteLine("🎉 LoadAndValidateConfiguration 方法工作正常！");
                    }
                    else
                    {
                        Console.WriteLine($"❌ 静态方法校验失败: {errorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 静态方法调用异常: {ex.Message}");
                }
                
                Console.WriteLine("按任意键继续测试 GetAllWatchItems...");
                Console.ReadKey();
                Console.WriteLine();

                // 🧪 测试：在 StartAsync() 之前获取监控项信息
                Console.WriteLine("=== 测试 GetAllWatchItems (StartAsync 之前) ===");
                Console.WriteLine("正在测试修复后的 GetAllWatchItems 方法...");
                
                try
                {
                    var watchItems = _manager.GetAllWatchItems();
                    Console.WriteLine($"✅ 成功获取到 {watchItems.Count} 个监控项配置：");
                    
                    foreach (var item in watchItems)
                    {
                        var status = item.Enabled ? "启用" : "禁用";
                        var type = item.Type == WatchType.Directory ? "目录" : "文件";
                        Console.WriteLine($"   • [{status}] {item.Name} ({item.Id})");
                        Console.WriteLine($"     类型: {type} | 路径: {item.Path}");
                        Console.WriteLine($"     文件类型: {item.FileSettings.FileType} | 过滤器: {string.Join(", ", item.FileFilters)}");
                        Console.WriteLine();
                    }
                    
                    Console.WriteLine("🎉 修复验证成功！GetAllWatchItems 在 StartAsync 之前可以正常工作！");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 测试失败: {ex.Message}");
                }
                
                Console.WriteLine("按任意键继续启动监控...");
                Console.ReadKey();
                Console.WriteLine();

                // 注册事件处理器
                _manager.FileChanged += OnFileChanged;
                _manager.StatusChanged += OnStatusChanged;

                Console.WriteLine("正在启动文件监控...");
                await _manager.StartAsync();

                Console.WriteLine("[成功] 监控已启动成功!");
                Console.WriteLine($"活动监控器数量: {_manager.ActiveWatchersCount}");
                Console.WriteLine();

                // 显示监控项状态
                DisplayWatcherStatuses();

                Console.WriteLine("\n=== 测试说明 ===");
                Console.WriteLine("1. 手工修改 TestData 目录下的 CSV 文件来测试监控功能");
                Console.WriteLine("2. 手工添加新的 CSV 或 Excel 文件到 TestData 目录");
                Console.WriteLine("3. 手工删除 TestData 目录下的文件");
                Console.WriteLine("4. 程序会自动检测文件变化并显示详细的内容分析");
                Console.WriteLine("5. 支持显示具体的新增、修改、删除内容差异");
                Console.WriteLine();

                ShowOperationMenu();
                
                ConsoleKeyInfo keyInfo;
                do
                {
                    keyInfo = Console.ReadKey(true);
                    
                    switch (keyInfo.KeyChar)
                    {
                        case 's':
                        case 'S':
                            DisplayWatcherStatuses();
                            ShowOperationMenu();
                            break;
                        case 'h':
                        case 'H':
                            ShowOperationMenu();
                            break;
                        case 'c':
                        case 'C':
                            Console.Clear();
                            Console.WriteLine("WatchFile 监控程序 v2.0.0");
                            Console.WriteLine("智能文件内容变化分析");
                            Console.WriteLine($"[成功] 监控状态: 运行中 ({_manager.ActiveWatchersCount} 个监控器)");
                            ShowOperationMenu();
                            break;
                        case 'q':
                        case 'Q':
                            break;
                        default:
                            Console.WriteLine($"\n活动监控器: {_manager.ActiveWatchersCount}");
                            Console.WriteLine("按 'h' 显示帮助菜单");
                            break;
                    }
                } while (keyInfo.KeyChar != 'q' && keyInfo.KeyChar != 'Q');

                Console.WriteLine("\n[停止] 正在停止监控...");
                await _manager.StopAsync();
                Console.WriteLine("[成功] 监控已停止。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[错误] {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[内部错误] {ex.InnerException.Message}");
                }
            }
            finally
            {
                _manager?.Dispose();
                Console.WriteLine("\n按任意键退出...");
                Console.ReadKey();
            }
        }

        private static void OnFileChanged(object? sender, FileChangedEventArgs e)
        {
            Console.WriteLine($"\n{'='*60}");
            Console.WriteLine($"[文件变化事件] {DateTime.Now:HH:mm:ss.fff}");
            Console.WriteLine($"{'='*60}");
            Console.WriteLine($"监控项: {e.WatchItemName} ({e.WatchItemId})");
            Console.WriteLine($"文件名: {Path.GetFileName(e.FilePath)}");
            Console.WriteLine($"路径: {e.FilePath}");
            
            // 显示详细的变化类型
            string changeTypeDesc = e.ChangeType switch
            {
                System.IO.WatcherChangeTypes.Created => "[新建文件]",
                System.IO.WatcherChangeTypes.Changed => "[文件修改]",
                System.IO.WatcherChangeTypes.Deleted => "[文件删除]",
                System.IO.WatcherChangeTypes.Renamed => "[文件重命名]",
                _ => $"[{e.ChangeType}]"
            };
            Console.WriteLine($"变化类型: {changeTypeDesc}");
            Console.WriteLine($"文件大小: {e.FileSize:N0} 字节");
            Console.WriteLine($"处理状态: {(e.IsSuccess ? "[成功]" : "[失败]")}");
            Console.WriteLine($"时间戳: {e.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");

            if (e.Exception != null)
            {
                Console.WriteLine($"[错误] 错误信息: {e.Exception.Message}");
                if (e.Exception.InnerException != null)
                {
                    Console.WriteLine($"   内部错误: {e.Exception.InnerException.Message}");
                }
            }

            // 显示数据变化详情（优先显示）
            if (e.ChangeDetails != null && e.ChangeDetails.HasChanges)
            {
                Console.WriteLine();
                Console.WriteLine("=== 数据变化分析 ===");
                Console.WriteLine($"变化摘要: {e.ChangeDetails.GetSummary()}");
                
                // 显示新增的行
                if (e.ChangeDetails.AddedRows.Count > 0)
                {
                    Console.WriteLine($"\n[新增行数据] 共 {e.ChangeDetails.AddedRows.Count} 行:");
                    var addedDisplayCount = Math.Min(3, e.ChangeDetails.AddedRows.Count);
                    for (int i = 0; i < addedDisplayCount; i++)
                    {
                        Console.WriteLine($"   + 第 {i + 1} 行:");
                        foreach (var column in e.ChangeDetails.AddedRows[i])
                        {
                            Console.WriteLine($"       {column.Key}: {column.Value}");
                        }
                    }
                    if (e.ChangeDetails.AddedRows.Count > 3)
                        Console.WriteLine($"       ... 还有 {e.ChangeDetails.AddedRows.Count - 3} 行新增数据");
                }
                
                // 显示删除的行
                if (e.ChangeDetails.DeletedRows.Count > 0)
                {
                    Console.WriteLine($"\n[删除行数据] 共 {e.ChangeDetails.DeletedRows.Count} 行:");
                    var deletedDisplayCount = Math.Min(3, e.ChangeDetails.DeletedRows.Count);
                    for (int i = 0; i < deletedDisplayCount; i++)
                    {
                        Console.WriteLine($"   - 第 {i + 1} 行:");
                        foreach (var column in e.ChangeDetails.DeletedRows[i])
                        {
                            Console.WriteLine($"       {column.Key}: {column.Value}");
                        }
                    }
                    if (e.ChangeDetails.DeletedRows.Count > 3)
                        Console.WriteLine($"       ... 还有 {e.ChangeDetails.DeletedRows.Count - 3} 行删除数据");
                }
                
                // 显示修改的行
                if (e.ChangeDetails.ModifiedRows.Count > 0)
                {
                    Console.WriteLine($"\n[修改行数据] 共 {e.ChangeDetails.ModifiedRows.Count} 行:");
                    var modifiedDisplayCount = Math.Min(3, e.ChangeDetails.ModifiedRows.Count);
                    for (int i = 0; i < modifiedDisplayCount; i++)
                    {
                        var change = e.ChangeDetails.ModifiedRows[i];
                        Console.WriteLine($"   ~ 第 {change.RowIndex + 1} 行 (共 {change.FieldChanges.Count} 个字段变化):");
                        
                        foreach (var fieldChange in change.FieldChanges)
                        {
                            string changeIcon = fieldChange.ChangeType switch
                            {
                                FieldChangeType.Modified => "~",
                                FieldChangeType.Added => "+",
                                FieldChangeType.Removed => "-",
                                _ => "?"
                            };
                            
                            Console.WriteLine($"       {changeIcon} {fieldChange.FieldName}: [{fieldChange.OldValue}] → [{fieldChange.NewValue}]");
                        }
                    }
                    if (e.ChangeDetails.ModifiedRows.Count > 3)
                        Console.WriteLine($"       ... 还有 {e.ChangeDetails.ModifiedRows.Count - 3} 行修改数据");
                }
            }

            // 显示当前文件完整数据（如果没有详细变化信息）
            if (e.ExtractedData != null && e.ExtractedData.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"=== 当前文件数据 === (共 {e.DataRowCount} 行)");
                
                // 如果没有变化详情，显示前几行数据
                if (e.ChangeDetails == null || !e.ChangeDetails.HasChanges)
                {
                    var displayCount = Math.Min(5, e.ExtractedData.Count);
                    for (int i = 0; i < displayCount; i++)
                    {
                        Console.WriteLine($"   第 {i + 1} 行:");
                        foreach (var column in e.ExtractedData[i])
                        {
                            Console.WriteLine($"       {column.Key}: {column.Value}");
                        }
                    }
                    
                    if (e.ExtractedData.Count > 5)
                    {
                        Console.WriteLine($"       ... 还有 {e.ExtractedData.Count - 5} 行数据");
                    }
                }
                
                // 显示文件统计信息
                if (e.ChangeType == System.IO.WatcherChangeTypes.Changed && e.PreviousData != null)
                {
                    Console.WriteLine();
                    Console.WriteLine("=== 变化统计 ===");
                    Console.WriteLine($"   之前行数: {e.PreviousData.Count}");
                    Console.WriteLine($"   当前行数: {e.DataRowCount}");
                    Console.WriteLine($"   行数变化: {(e.DataRowCount - e.PreviousData.Count):+#;-#;0}");
                    Console.WriteLine($"   文件修改时间: {File.GetLastWriteTime(e.FilePath):yyyy-MM-dd HH:mm:ss}");
                }
            }
            else if (e.ChangeType == System.IO.WatcherChangeTypes.Deleted)
            {
                Console.WriteLine("\n文件已被删除");
                if (e.ChangeDetails?.DeletedRows.Count > 0)
                {
                    Console.WriteLine($"   删除前包含 {e.ChangeDetails.DeletedRows.Count} 行数据");
                }
            }
            else if (!e.IsSuccess)
            {
                Console.WriteLine("\n[错误] 数据提取失败");
                Console.WriteLine("   可能原因: 文件格式不支持、文件被占用或数据格式错误");
            }
            
            Console.WriteLine($"{'='*60}");
        }

        private static void OnStatusChanged(object? sender, MonitorStatusChangedEventArgs e)
        {
            var statusDesc = e.Status switch
            {
                MonitorStatus.Running => "[运行中]",
                MonitorStatus.Stopped => "[已停止]",
                MonitorStatus.Starting => "[启动中]",
                MonitorStatus.Error => "[错误]",
                MonitorStatus.Paused => "[暂停]",
                _ => "[未知状态]"
            };
            
            Console.WriteLine($"[状态变化] {statusDesc} {e.WatchItemId}: {e.Status} - {e.Reason}");
            if (e.Exception != null)
            {
                Console.WriteLine($"[错误] {e.Exception.Message}");
            }
        }

        private static void DisplayWatcherStatuses()
        {
            if (_manager == null) return;

            Console.WriteLine("=== 监控项状态 ===");
            var watchItems = _manager.GetAllWatchItems();
            
            if (watchItems.Count == 0)
            {
                Console.WriteLine("  [警告] 没有配置的监控项");
                return;
            }
            
            foreach (var item in watchItems)
            {
                var status = _manager.WatcherStatuses.ContainsKey(item.Id) 
                    ? _manager.WatcherStatuses[item.Id] 
                    : MonitorStatus.Stopped;
                    
                var typeIcon = item.Type == WatchType.Directory ? "[目录]" : "[文件]";
                var statusIcon = status switch
                {
                    MonitorStatus.Running => "[运行]",
                    MonitorStatus.Stopped => "[停止]",
                    MonitorStatus.Starting => "[启动]",
                    MonitorStatus.Error => "[错误]",
                    MonitorStatus.Paused => "[暂停]",
                    _ => "[未知]"
                };
                
                var enabledIcon = item.Enabled ? "[启用]" : "[禁用]";
                var statusText = status switch
                {
                    MonitorStatus.Running => "[运行中]",
                    MonitorStatus.Stopped => "[已停止]",
                    MonitorStatus.Starting => "[启动中]",
                    MonitorStatus.Error => "[错误]",
                    MonitorStatus.Paused => "[暂停]",
                    _ => "[未知]"
                };
                
                Console.WriteLine($"{statusIcon} {item.Name} ({item.Id}) {enabledIcon}");
                Console.WriteLine($"   {typeIcon} 路径: {item.Path}");
                Console.WriteLine($"   状态: {statusText}");
                Console.WriteLine($"   监控事件: {string.Join(", ", item.WatchEvents)}");
                Console.WriteLine($"   文件类型: {item.FileSettings.FileType}");
                
                if (item.FileFilters.Any())
                {
                    Console.WriteLine($"   文件过滤: {string.Join(", ", item.FileFilters)}");
                }
                
                if (item.ExcludePatterns.Any())
                {
                    Console.WriteLine($"   排除模式: {string.Join(", ", item.ExcludePatterns)}");
                }
                
                Console.WriteLine($"   编码: {item.FileSettings.Encoding} | 分隔符: '{item.FileSettings.Delimiter}' | 列映射: {item.FileSettings.ColumnMappings.Count} 个");
                
                // 显示临时文件设置
                var watchFileSettings = item.WatchFileSettings;
                Console.WriteLine($"   临时文件: {watchFileSettings.WatchFileDirectory} | 扩展名: {watchFileSettings.WatchFileExtension}");
                Console.WriteLine($"   并发数: {watchFileSettings.MaxConcurrentFiles} | 差异日志: {(watchFileSettings.EnableDifferenceLogging ? "启用" : "禁用")}");
                Console.WriteLine();
            }
        }

        private static void ShowOperationMenu()
        {
            Console.WriteLine("\n=== 操作菜单 ===");
            Console.WriteLine("[S] - 显示监控状态");
            Console.WriteLine("[C] - 清理屏幕");
            Console.WriteLine("[H] - 显示此帮助菜单");
            Console.WriteLine("[Q] - 退出程序");
            Console.WriteLine("监控正在后台运行，请手工操作TestData目录中的文件来测试...");
            Console.WriteLine("支持的操作：新增文件、修改文件内容、删除文件");
        }

        private static void CreateDefaultTestConfig(string configPath)
        {
            var defaultConfig = new WatchFileConfiguration
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
                        Id = "test-csv-monitor",
                        Name = "测试CSV文件监控",
                        Enabled = true,
                        Path = "TestData",
                        Type = WatchType.Directory,
                        Recursive = false,
                        FileFilters = new List<string> { "*.csv" },
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
                                    TargetName = "EmployeeName",
                                    DataType = DataType.String,
                                    Required = true
                                },
                                new ColumnMapping
                                {
                                    SourceColumn = "Age",
                                    TargetName = "Age",
                                    DataType = DataType.Integer,
                                    Required = false
                                },
                                new ColumnMapping
                                {
                                    SourceColumn = "Email",
                                    TargetName = "Email",
                                    DataType = DataType.String,
                                    Required = false
                                }
                            }
                        }
                    }
                }
            };

            var configManager = new WatchFile.Core.Configuration.ConfigurationManager();
            configManager.SaveConfiguration(defaultConfig, configPath);
        }
    }
}
