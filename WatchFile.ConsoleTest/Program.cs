using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WatchFile.Core;
using WatchFile.Core.Events;
using WatchFile.Core.Configuration.Models;

namespace WatchFile.ConsoleTest
{
    class Program
    {
        private static WatchFileManager? _manager;

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== WatchFile 控制台测试程序 ===");
            Console.WriteLine("版本: 1.0.0");
            Console.WriteLine("支持: .NET Framework 4.6.1+ 和 .NET 6+");
            Console.WriteLine("功能: CSV/Excel 文件监控与解析");
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

                // 注册事件处理器
                _manager.FileChanged += OnFileChanged;
                _manager.StatusChanged += OnStatusChanged;

                // 注册自定义处理器
                _manager.AddHandler(new TestFileHandler());

                Console.WriteLine("正在启动文件监控...");
                await _manager.StartAsync();

                Console.WriteLine($"✅ 监控已启动，活动监控器数量: {_manager.ActiveWatchersCount}");
                Console.WriteLine();

                // 显示监控项状态
                DisplayWatcherStatuses();

                Console.WriteLine("=== 测试说明 ===");
                Console.WriteLine("1. 修改 TestData 目录下的 CSV 文件来测试监控功能");
                Console.WriteLine("2. 添加新的 CSV 或 Excel 文件到 TestData 目录");
                Console.WriteLine("3. 程序会自动检测文件变化并显示解析结果");
                Console.WriteLine();

                // 测试手动解析功能
                await TestManualParsing();

                Console.WriteLine("📝 监控运行中...");
                Console.WriteLine("按 'q' 退出程序，按 't' 运行测试，按任意其他键显示状态...");
                
                ConsoleKeyInfo keyInfo;
                do
                {
                    keyInfo = Console.ReadKey(true);
                    
                    switch (keyInfo.KeyChar)
                    {
                        case 't':
                        case 'T':
                            await RunTests();
                            break;
                        case 's':
                        case 'S':
                            DisplayWatcherStatuses();
                            break;
                        case 'q':
                        case 'Q':
                            break;
                        default:
                            Console.WriteLine($"\n📊 活动监控器: {_manager.ActiveWatchersCount}");
                            Console.WriteLine("按 'q' 退出，'t' 测试，'s' 显示状态");
                            break;
                    }
                } while (keyInfo.KeyChar != 'q' && keyInfo.KeyChar != 'Q');

                Console.WriteLine("\n🔄 正在停止监控...");
                await _manager.StopAsync();
                Console.WriteLine("✅ 监控已停止。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 错误: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ 内部错误: {ex.InnerException.Message}");
                }
            }
            finally
            {
                _manager?.Dispose();
                Console.WriteLine("\n按任意键退出...");
                Console.ReadKey();
            }
        }

        private static async Task TestManualParsing()
        {
            Console.WriteLine("=== 测试手动解析功能 ===");
            
            try
            {
                var testFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "employees.csv");
                if (File.Exists(testFile))
                {
                    var result = await _manager!.ParseFileManuallyAsync(testFile, "test-csv-monitor");
                    
                    if (result.IsSuccess)
                    {
                        Console.WriteLine($"✅ 手动解析成功，共解析 {result.RowCount} 行数据");
                        Console.WriteLine("前3行数据预览:");
                        
                        var displayCount = Math.Min(3, result.Data.Count);
                        for (int i = 0; i < displayCount; i++)
                        {
                            Console.WriteLine($"  📄 行 {i + 1}:");
                            foreach (var column in result.Data[i])
                            {
                                Console.WriteLine($"      {column.Key}: {column.Value}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"❌ 手动解析失败: {result.ErrorMessage}");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️  测试文件不存在: {testFile}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 手动解析测试失败: {ex.Message}");
            }
            
            Console.WriteLine();
        }

        private static async Task RunTests()
        {
            Console.WriteLine("\n=== 运行功能测试 ===");
            
            try
            {
                // 创建测试文件
                var testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData");
                if (!Directory.Exists(testDir))
                {
                    Directory.CreateDirectory(testDir);
                }

                var testFile = Path.Combine(testDir, $"test_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                
                Console.WriteLine($"🔧 创建测试文件: {Path.GetFileName(testFile)}");
                await File.WriteAllTextAsync(testFile, "Name,Age,Email\n张三,25,test@example.com\n李四,30,test2@example.com");
                
                Console.WriteLine("⏱️  等待文件监控触发...");
                await Task.Delay(2000);
                
                Console.WriteLine("🔧 修改测试文件...");
                await File.AppendAllTextAsync(testFile, "\n王五,35,test3@example.com");
                
                Console.WriteLine("⏱️  等待文件监控触发...");
                await Task.Delay(2000);
                
                Console.WriteLine("✅ 测试完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 测试失败: {ex.Message}");
            }
            
            Console.WriteLine();
        }

        private static void OnFileChanged(object? sender, FileChangedEventArgs e)
        {
            Console.WriteLine($"\n🔔 === 文件变化事件 ===");
            Console.WriteLine($"⏰ 时间: {e.Timestamp:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"📂 监控项: {e.WatchItemName} ({e.WatchItemId})");
            Console.WriteLine($"📄 文件: {Path.GetFileName(e.FilePath)}");
            Console.WriteLine($"📍 完整路径: {e.FilePath}");
            Console.WriteLine($"🔄 变化类型: {e.ChangeType}");
            Console.WriteLine($"📊 文件大小: {e.FileSize:N0} 字节");
            Console.WriteLine($"✅ 处理状态: {(e.IsSuccess ? "成功" : "失败")}");

            if (e.Exception != null)
            {
                Console.WriteLine($"❌ 错误: {e.Exception.Message}");
            }

            if (e.ExtractedData != null && e.ExtractedData.Count > 0)
            {
                Console.WriteLine($"📋 提取数据行数: {e.DataRowCount}");
                Console.WriteLine("📝 数据内容预览:");
                
                var displayCount = Math.Min(3, e.ExtractedData.Count);
                for (int i = 0; i < displayCount; i++)
                {
                    Console.WriteLine($"  📄 行 {i + 1}:");
                    foreach (var column in e.ExtractedData[i])
                    {
                        Console.WriteLine($"      {column.Key}: {column.Value}");
                    }
                }
                
                if (e.ExtractedData.Count > 3)
                {
                    Console.WriteLine($"      ... 还有 {e.ExtractedData.Count - 3} 行数据");
                }
            }
            Console.WriteLine("=========================");
        }

        private static void OnStatusChanged(object? sender, MonitorStatusChangedEventArgs e)
        {
            var statusEmoji = e.Status switch
            {
                MonitorStatus.Running => "🟢",
                MonitorStatus.Stopped => "🔴",
                MonitorStatus.Starting => "🟡",
                MonitorStatus.Error => "❌",
                MonitorStatus.Paused => "⏸️",
                _ => "⚪"
            };
            
            Console.WriteLine($"📊 [状态变化] {statusEmoji} {e.WatchItemId}: {e.Status} - {e.Reason}");
            if (e.Exception != null)
            {
                Console.WriteLine($"❌ [错误] {e.Exception.Message}");
            }
        }

        private static void DisplayWatcherStatuses()
        {
            if (_manager == null) return;

            Console.WriteLine("📊 === 监控项状态 ===");
            var watchItems = _manager.GetAllWatchItems();
            
            if (watchItems.Count == 0)
            {
                Console.WriteLine("  ⚠️  没有配置的监控项");
                return;
            }
            
            foreach (var item in watchItems)
            {
                var status = _manager.WatcherStatuses.ContainsKey(item.Id) 
                    ? _manager.WatcherStatuses[item.Id] 
                    : MonitorStatus.Stopped;
                    
                var enabledText = item.Enabled ? "✅ 启用" : "❌ 禁用";
                var typeText = item.Type == WatchType.Directory ? "📁 目录" : "📄 文件";
                var statusEmoji = status switch
                {
                    MonitorStatus.Running => "🟢",
                    MonitorStatus.Stopped => "🔴",
                    MonitorStatus.Starting => "🟡",
                    MonitorStatus.Error => "❌",
                    MonitorStatus.Paused => "⏸️",
                    _ => "⚪"
                };
                
                Console.WriteLine($"  📋 {item.Name} ({item.Id})");
                Console.WriteLine($"      📍 路径: {item.Path}");
                Console.WriteLine($"      🏷️  类型: {typeText}");
                Console.WriteLine($"      📊 状态: {enabledText} / {statusEmoji} {status}");
                Console.WriteLine($"      🔔 监控事件: {string.Join(", ", item.WatchEvents)}");
                Console.WriteLine($"      📝 文件类型: {item.FileSettings.FileType}");
                Console.WriteLine($"      🔤 分隔符: '{item.FileSettings.Delimiter}'");
                Console.WriteLine($"      🌐 编码: {item.FileSettings.Encoding}");
                Console.WriteLine($"      🗂️  列映射: {item.FileSettings.ColumnMappings.Count} 个");
                Console.WriteLine();
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
    }

    /// <summary>
    /// 测试文件处理器
    /// </summary>
    public class TestFileHandler : FileChangedHandlerBase
    {
        private static int _processedCount = 0;

        public override async Task HandleFileChanged(FileChangedEventArgs args)
        {
            if (!ShouldHandle(args)) return;

            try
            {
                _processedCount++;
                LogInfo($"🔧 处理器 #{_processedCount}: 处理文件 {Path.GetFileName(args.FilePath)}");

                // 模拟异步处理
                await Task.Delay(100);

                switch (args.ChangeType)
                {
                    case System.IO.WatcherChangeTypes.Created:
                    case System.IO.WatcherChangeTypes.Changed:
                        await ProcessFileData(args.ExtractedData, args.WatchItemId);
                        break;
                    case System.IO.WatcherChangeTypes.Deleted:
                        await HandleFileDeleted(args.FilePath, args.WatchItemId);
                        break;
                }

                LogInfo($"✅ 处理器 #{_processedCount}: 完成处理 {args.DataRowCount} 行数据");
            }
            catch (Exception ex)
            {
                LogError($"❌ 处理器错误: {ex.Message}", ex);
            }
        }

        private async Task ProcessFileData(List<Dictionary<string, object>>? data, string watchItemId)
        {
            if (data == null || data.Count == 0)
                return;

            LogInfo($"📊 模拟数据处理: {data.Count} 行数据 (来源: {watchItemId})");
            
            // 模拟数据库保存延迟
            await Task.Delay(50);
            
            // 这里可以添加实际的业务逻辑，比如：
            // - 保存到数据库
            // - 发送通知
            // - 数据验证
            // - 格式转换
        }

        private async Task HandleFileDeleted(string filePath, string watchItemId)
        {
            LogInfo($"🗑️  模拟处理文件删除: {Path.GetFileName(filePath)} (来源: {watchItemId})");
            await Task.Delay(10);
        }
    }
}
