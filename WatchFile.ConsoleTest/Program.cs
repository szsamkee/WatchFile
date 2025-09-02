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
        
        // 🧪 安全删除测试配置
        private static bool _enableSafeDeleteTest = true;  // 是否启用安全删除测试
        private static int _deleteDelaySeconds = 2;       // 删除前等待时间（秒）

        static async Task Main(string[] args)
        {
            // 🔧 修复后：不再需要手动注册编码提供程序，WatchFile.Core 会自动处理
            // Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);  // 已移除！
            
            // 设置控制台编码以支持中文显示
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            
            Console.WriteLine("=== WatchFile 智能监控程序 ===");
            Console.WriteLine("版本: 2.2.0");
            Console.WriteLine("支持: .NET Framework 4.6.1+ 和 .NET 6+");
            Console.WriteLine("功能: CSV/Excel 文件智能变化分析");
            Console.WriteLine("优化: 工控环境大量小文件监控");
            Console.WriteLine("特色: 临时文件缓存 + 详细差异分析");
            Console.WriteLine($"🧪 测试: 安全删除功能 ({(_enableSafeDeleteTest ? "已启用" : "已禁用")})");
            Console.WriteLine();

            try
            {
                // 设置配置文件路径
                //var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-config.json");
                var configPath = "D:\\aa\\abc.wat";
                
                // 创建管理器
                _manager = new WatchFileManager(configPath);

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
                Console.WriteLine($"1. 手工修改监控目录下的 CSV 文件来测试监控功能");
                Console.WriteLine("2. 手工添加新的 CSV 或 Excel 文件到监控目录");
                Console.WriteLine("3. 手工删除监控目录下的文件");
                Console.WriteLine("4. 程序会自动检测文件变化并显示详细的内容分析");
                Console.WriteLine("5. 支持显示具体的新增、修改、删除内容差异");
                Console.WriteLine($"6. 🧪 安全删除测试: {(_enableSafeDeleteTest ? "已启用" : "已禁用")} - 文件处理完成后自动删除");
                Console.WriteLine($"7. ⏱️  删除延迟: {_deleteDelaySeconds} 秒（模拟文件处理时间）");
                Console.WriteLine($"8. 🛡️  安全删除: 自动清理主文件和缓存文件，不触发监控事件");
                Console.WriteLine("9. 🔓 强制删除: 自动清除只读、隐藏、系统属性，解决权限问题");
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
                            Console.WriteLine("WatchFile 监控程序 v2.2.0");
                            Console.WriteLine("智能文件内容变化分析");
                            Console.WriteLine($"[成功] 监控状态: 运行中 ({_manager.ActiveWatchersCount} 个监控器)");
                            ShowOperationMenu();
                            break;
                        case 'd':
                        case 'D':
                            ToggleSafeDeleteTest();
                            ShowOperationMenu();
                            break;
                        case 't':
                        case 'T':
                            AdjustDeleteDelay();
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

        private static async void OnFileChanged(object? sender, FileChangedEventArgs e)
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

            // 🚀 新增：安全删除测试功能
            // 模拟工控环境：文件处理完成后自动删除，避免目录文件堆积
            await TestSafeFileDelete(e);
        }

        /// <summary>
        /// 测试安全删除功能
        /// </summary>
        private static async Task TestSafeFileDelete(FileChangedEventArgs e)
        {
            // 检查是否启用安全删除测试
            if (!_enableSafeDeleteTest)
            {
                return;
            }

            // 只对成功处理的新建和修改文件进行删除测试
            if (!e.IsSuccess || e.ChangeType == System.IO.WatcherChangeTypes.Deleted)
            {
                return;
            }

            // 跳过已经被删除的文件
            if (!File.Exists(e.FilePath))
            {
                return;
            }

            try
            {
                Console.WriteLine("\n🧪 === 安全删除测试 ===");
                Console.WriteLine($"📝 模拟场景: 文件 '{Path.GetFileName(e.FilePath)}' 已处理完成，现在进行安全删除");
                Console.WriteLine($"⏱️  等待 {_deleteDelaySeconds} 秒模拟文件处理时间...");
                
                // 模拟文件处理时间
                await Task.Delay(_deleteDelaySeconds * 1000);

                Console.WriteLine("🗑️  开始执行安全删除...");
                
                // 调用新的安全删除 API - 按文件名删除，启用强制删除（清除只读属性）
                var fileName = Path.GetFileName(e.FilePath);
                var deleteResult = await _manager!.SafeDeleteFileAsync(fileName, e.WatchItemId, forceDelete: true);

                // 显示删除结果
                Console.WriteLine("\n📊 === 删除结果报告 ===");
                Console.WriteLine($"✅ 删除状态: {(deleteResult.IsSuccess ? "成功" : "失败")}");
                Console.WriteLine($"🎯 目标文件: {deleteResult.FileName}");
                Console.WriteLine($"🔍 监控项ID: {deleteResult.RequestedWatchItemId}");
                Console.WriteLine($"⏱️  处理耗时: {deleteResult.Duration.TotalMilliseconds:F0} 毫秒");

                if (deleteResult.IsSuccess)
                {
                    Console.WriteLine($"📁 已删除主文件: {deleteResult.DeletedFiles.Count} 个");
                    Console.WriteLine($"🗂️  已删除缓存文件: {deleteResult.DeletedCacheFiles.Count} 个");
                    
                    if (deleteResult.Messages.Any())
                    {
                        Console.WriteLine("\n📋 详细信息:");
                        foreach (var message in deleteResult.Messages)
                        {
                            Console.WriteLine($"   {message}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("❌ 删除失败:");
                    foreach (var error in deleteResult.Errors)
                    {
                        Console.WriteLine($"   ⚠️  {error}");
                    }
                }

                Console.WriteLine("🎉 === 安全删除测试完成 ===\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ [安全删除测试错误] {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   [内部错误] {ex.InnerException.Message}");
                }
                Console.WriteLine();
            }
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
            Console.WriteLine("[D] - 切换安全删除测试 (当前: " + (_enableSafeDeleteTest ? "启用" : "禁用") + ")");
            Console.WriteLine("[T] - 调整删除延迟时间 (当前: " + _deleteDelaySeconds + " 秒)");
            Console.WriteLine("[H] - 显示此帮助菜单");
            Console.WriteLine("[Q] - 退出程序");
            Console.WriteLine("监控正在后台运行，请手工操作监控目录中的文件来测试...");
            Console.WriteLine("支持的操作：新增文件、修改文件内容、删除文件");
            if (_enableSafeDeleteTest)
            {
                Console.WriteLine($"🧪 安全删除测试已启用：文件处理完成后 {_deleteDelaySeconds} 秒自动删除");
                Console.WriteLine("🛡️  强制删除：自动清除只读属性，处理权限问题");
            }
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

        /// <summary>
        /// 切换安全删除测试状态
        /// </summary>
        private static void ToggleSafeDeleteTest()
        {
            _enableSafeDeleteTest = !_enableSafeDeleteTest;
            var status = _enableSafeDeleteTest ? "已启用" : "已禁用";
            Console.WriteLine($"\n🧪 安全删除测试: {status}");
            
            if (_enableSafeDeleteTest)
            {
                Console.WriteLine("   ✅ 文件处理完成后将自动执行安全删除");
                Console.WriteLine($"   ⏱️  删除延迟: {_deleteDelaySeconds} 秒");
            }
            else
            {
                Console.WriteLine("   ❌ 不会自动删除文件");
            }
        }

        /// <summary>
        /// 调整删除延迟时间
        /// </summary>
        private static void AdjustDeleteDelay()
        {
            Console.WriteLine($"\n⏱️  当前删除延迟: {_deleteDelaySeconds} 秒");
            Console.Write("请输入新的延迟时间（秒，1-60）: ");
            
            try
            {
                var input = Console.ReadLine();
                if (int.TryParse(input, out var newDelay) && newDelay >= 1 && newDelay <= 60)
                {
                    _deleteDelaySeconds = newDelay;
                    Console.WriteLine($"✅ 删除延迟已设置为: {_deleteDelaySeconds} 秒");
                }
                else
                {
                    Console.WriteLine("❌ 无效输入，请输入 1-60 之间的整数");
                }
            }
            catch
            {
                Console.WriteLine("❌ 输入错误，保持原设置");
            }
        }
    }
}
