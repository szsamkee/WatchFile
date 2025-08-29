# WatchFile API 文档

## 命名空间

- `WatchFile.Core` - 主要类和接口
- `WatchFile.Core.Configuration` - 配置管理
- `WatchFile.Core.Configuration.Models` - 配置模型
- `WatchFile.Core.Events` - 事件参数和接口
- `WatchFile.Core.Monitoring` - 监控功能
- `WatchFile.Core.Parsing` - 文件解析

## 核心类

### WatchFileManager

文件监控管理器，库的主入口点。

```csharp
public class WatchFileManager : IDisposable
```

#### 构造函数

```csharp
public WatchFileManager(string? configPath = null)
```

- `configPath`: 配置文件路径，默认为 "watchfile-config.json"

#### 属性

```csharp
public bool IsRunning { get; }
```
获取监控是否正在运行。

```csharp
public int ActiveWatchersCount { get; }
```
获取活动监控器数量。

```csharp
public Dictionary<string, MonitorStatus> WatcherStatuses { get; }
```
获取所有监控项的状态。

#### 事件

```csharp
public event EventHandler<FileChangedEventArgs>? FileChanged;
```
文件变化事件。

```csharp
public event EventHandler<MonitorStatusChangedEventArgs>? StatusChanged;
```
监控状态变化事件。

#### 方法

```csharp
public async Task StartAsync()
```
启动文件监控。

```csharp
public async Task StopAsync()
```
停止文件监控。

```csharp
public async Task ReloadConfigurationAsync()
```
重新加载配置文件。

```csharp
public void AddHandler(IFileChangedHandler handler)
```
添加文件变化处理器。

```csharp
public void RemoveHandler(IFileChangedHandler handler)
```
移除文件变化处理器。

```csharp
public WatchItem? GetWatchItem(string id)
```
获取指定ID的监控项配置。

```csharp
public IReadOnlyList<WatchItem> GetAllWatchItems()
```
获取所有监控项配置。

```csharp
public async Task EnableWatchItemAsync(string id)
```
启用指定的监控项。

```csharp
public async Task DisableWatchItemAsync(string id)
```
禁用指定的监控项。

```csharp
public async Task<FileParseResult> ParseFileManuallyAsync(string filePath, string watchItemId)
```
手动解析文件内容。

```csharp
public void SaveConfiguration(string? path = null)
```
保存当前配置到文件。

```csharp
public void CreateDefaultConfiguration(string? path = null)
```
创建默认配置文件。

```csharp
public bool ValidateConfiguration(string? path = null)
```
验证配置文件有效性。

### ConfigurationManager

配置文件管理器。

```csharp
public class ConfigurationManager
```

#### 构造函数

```csharp
public ConfigurationManager(string? configPath = null)
```

#### 方法

```csharp
public WatchFileConfiguration LoadConfiguration(string? path = null)
```
加载配置文件。

```csharp
public void SaveConfiguration(WatchFileConfiguration config, string? path = null)
```
保存配置文件。

```csharp
public bool ValidateConfiguration(WatchFileConfiguration config)
```
验证配置有效性。

```csharp
public static WatchFileConfiguration CreateDefaultConfiguration()
```
创建默认配置。

### FileParser

静态文件解析器。

```csharp
public static class FileParser
```

#### 方法

```csharp
public static FileParseResult ParseFile(string filePath, FileSettings settings)
```
解析文件内容。

```csharp
public static FileParseResult ParseCsv(string filePath, FileSettings settings)
```
解析CSV文件。

```csharp
public static FileParseResult ParseExcel(string filePath, FileSettings settings)
```
解析Excel文件。

## 事件和数据模型

### FileChangedEventArgs

文件变化事件参数。

```csharp
public class FileChangedEventArgs : EventArgs
{
    public string WatchItemId { get; set; }        // 监控项ID
    public string WatchItemName { get; set; }      // 监控项名称
    public string FilePath { get; set; }           // 文件路径
    public WatcherChangeTypes ChangeType { get; set; } // 变化类型
    public DateTime Timestamp { get; set; }        // 事件时间
    public List<Dictionary<string, object>>? ExtractedData { get; set; } // 提取的数据
    public Exception? Exception { get; set; }      // 异常信息
    public long FileSize { get; set; }            // 文件大小
    public bool IsSuccess { get; }                // 是否成功
    public int DataRowCount { get; }              // 数据行数
}
```

### MonitorStatusChangedEventArgs

监控状态变化事件参数。

```csharp
public class MonitorStatusChangedEventArgs : EventArgs
{
    public string WatchItemId { get; set; }       // 监控项ID
    public MonitorStatus Status { get; set; }     // 监控状态
    public DateTime Timestamp { get; set; }       // 状态变化时间
    public string Reason { get; set; }            // 变化原因
    public Exception? Exception { get; set; }     // 相关异常
}
```

### FileParseResult

文件解析结果。

```csharp
public class FileParseResult
{
    public bool IsSuccess { get; set; }           // 是否成功
    public List<Dictionary<string, object>> Data { get; set; } // 解析数据
    public Exception? Exception { get; set; }     // 异常信息
    public int RowCount { get; }                  // 行数
    public string ErrorMessage { get; }          // 错误消息
}
```

## 接口

### IFileChangedHandler

文件变化处理器接口。

```csharp
public interface IFileChangedHandler
{
    Task HandleFileChanged(FileChangedEventArgs args);
}
```

### FileChangedHandlerBase

文件变化处理器基类。

```csharp
public abstract class FileChangedHandlerBase : IFileChangedHandler
{
    public abstract Task HandleFileChanged(FileChangedEventArgs args);
    protected virtual bool ShouldHandle(FileChangedEventArgs args);
    protected virtual void LogError(string message, Exception? exception = null);
    protected virtual void LogInfo(string message);
}
```

## 配置模型

### WatchFileConfiguration

根配置模型。

```csharp
public class WatchFileConfiguration
{
    public string Version { get; set; }           // 配置版本
    public GlobalSettings GlobalSettings { get; set; } // 全局设置
    public List<WatchItem> WatchItems { get; set; }   // 监控项列表
}
```

### GlobalSettings

全局设置。

```csharp
public class GlobalSettings
{
    public bool EnableLogging { get; set; }       // 是否启用日志
    public string LogLevel { get; set; }          // 日志级别
    public int BufferTimeMs { get; set; }         // 缓冲时间
    public int MaxRetries { get; set; }           // 最大重试次数
    public string LogFilePath { get; set; }       // 日志文件路径
}
```

### WatchItem

监控项配置。

```csharp
public class WatchItem
{
    public string Id { get; set; }                // 唯一标识
    public string Name { get; set; }              // 显示名称
    public bool Enabled { get; set; }             // 是否启用
    public string Path { get; set; }              // 监控路径
    public WatchType Type { get; set; }           // 监控类型
    public bool Recursive { get; set; }           // 是否递归
    public List<string> FileFilters { get; set; } // 文件过滤器
    public List<WatchEvent> WatchEvents { get; set; } // 监控事件
    public FileSettings FileSettings { get; set; } // 文件设置
}
```

### FileSettings

文件设置。

```csharp
public class FileSettings
{
    public FileType FileType { get; set; }        // 文件类型
    public bool HasHeader { get; set; }           // 是否有标题行
    public string Delimiter { get; set; }         // 分隔符
    public string Encoding { get; set; }          // 编码
    public string SheetName { get; set; }         // 工作表名称
    public int StartRow { get; set; }             // 开始行号
    public List<ColumnMapping> ColumnMappings { get; set; } // 列映射
}
```

### ColumnMapping

列映射配置。

```csharp
public class ColumnMapping
{
    public object SourceColumn { get; set; }      // 源列（名称或索引）
    public string TargetName { get; set; }        // 目标属性名
    public DataType DataType { get; set; }        // 数据类型
    public bool Required { get; set; }            // 是否必需
    public string Format { get; set; }            // 格式字符串
}
```

## 枚举

### WatchType

监控类型。

```csharp
public enum WatchType
{
    Directory,  // 目录
    File        // 文件
}
```

### WatchEvent

监控事件。

```csharp
public enum WatchEvent
{
    Created,    // 创建
    Modified,   // 修改
    Deleted,    // 删除
    Renamed     // 重命名
}
```

### FileType

文件类型。

```csharp
public enum FileType
{
    CSV,        // CSV文件
    Excel       // Excel文件
}
```

### DataType

数据类型。

```csharp
public enum DataType
{
    String,     // 字符串
    Integer,    // 整数
    Decimal,    // 小数
    DateTime,   // 日期时间
    Boolean     // 布尔值
}
```

### MonitorStatus

监控状态。

```csharp
public enum MonitorStatus
{
    Stopped,    // 停止
    Starting,   // 启动中
    Running,    // 运行中
    Error,      // 错误
    Paused      // 暂停
}
```

### LogLevel

日志级别。

```csharp
public enum LogLevel
{
    Debug,      // 调试
    Info,       // 信息
    Warning,    // 警告
    Error       // 错误
}
```

## 使用示例

### 基本监控

```csharp
var manager = new WatchFileManager("config.json");

manager.FileChanged += (sender, e) =>
{
    Console.WriteLine($"文件 {e.FilePath} 发生 {e.ChangeType} 变化");
    
    if (e.ExtractedData != null)
    {
        Console.WriteLine($"提取了 {e.DataRowCount} 行数据");
    }
};

await manager.StartAsync();
// ... 应用运行
await manager.StopAsync();
```

### 自定义处理器

```csharp
public class MyHandler : FileChangedHandlerBase
{
    public override async Task HandleFileChanged(FileChangedEventArgs args)
    {
        if (!ShouldHandle(args)) return;
        
        try
        {
            // 处理逻辑
            LogInfo($"处理文件: {args.FilePath}");
        }
        catch (Exception ex)
        {
            LogError("处理失败", ex);
        }
    }
}

manager.AddHandler(new MyHandler());
```

### 动态管理监控项

```csharp
// 启用监控项
await manager.EnableWatchItemAsync("item-id");

// 禁用监控项
await manager.DisableWatchItemAsync("item-id");

// 获取状态
var statuses = manager.WatcherStatuses;
foreach (var status in statuses)
{
    Console.WriteLine($"{status.Key}: {status.Value}");
}
```
