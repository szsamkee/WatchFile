# WatchFile - 智能文件监控与内容变化分析库

一个专为工控环境设计的高性能文件监控库，支持 .NET Framework 4.6.1+ 和 .NET 6+，能够监控大量小文件的变化，并提供详细的内容差异分析，特别适用于工控设备日志文件监控。

[![NuGet](https://img.shields.io/nuget/v/WatchFile.Core.svg)](https://www.nuget.org/packages/WatchFile.Core/)
[![Downloads](https://img.shields.io/nuget/dt/WatchFile.Core.svg)](https://www.nuget.org/packages/WatchFile.Core/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## 📚 文档导航

- **[完整API文档](https://github.com/szsamkee/WatchFile/blob/main/WatchFile.Core/API.md)** - 详细的类、方法、事件文档
- **[配置参考](https://github.com/szsamkee/WatchFile#配置文件结构)** - 完整的配置选项说明  
- **[示例代码](https://github.com/szsamkee/WatchFile#快速开始)** - 快速入门示例
- **[更新日志](https://github.com/szsamkee/WatchFile#更新日志)** - 版本更新历史

> 💡 **提示**: 如果通过NuGet安装，API.md文档也包含在包中，可在项目包文件夹中找到。

## 🆕 最新更新 v2.4.0

### 🔍 离线变化检测
- **智能恢复监控** - 监控器重启时自动检测停机期间的文件变化
- **无缝衔接** - 自动识别新增、修改、删除的文件并触发相应事件
- **配置驱动** - 完全可配置的检测策略和行为

### ⚡ 性能优化
- **内存优化** - 移除ExtractedData冗余属性，减少内存占用
- **类名优化** - WatchFileManager → WatchManager，API更清晰
- **执行优化** - 优化启动顺序，确保检测准确性

## 🚀 项目结构

```
WatchFile/
├── WatchFile.Core/                    # 核心类库项目
│   ├── Configuration/                 # 配置管理
│   ├── Events/                       # 事件定义与数据变化分析
│   ├── Monitoring/                   # 文件监控与临时文件管理
│   ├── Parsing/                      # 文件解析（CSV/Excel）
│   ├── watchfile-config.json        # 默认配置文件
│   └── API.md                        # API文档
├── WatchFile.ConsoleTest/            # 控制台测试项目
│   ├── TestData/                     # 测试数据文件
│   │   └── .watch/                   # 监控临时文件目录
│   ├── test-config.json             # 测试配置文件
│   └── Program.cs                    # 测试程序
├── WatchFile.sln                     # 解决方案文件
└── README.md                         # 项目说明
```

## ✨ 核心特性

### 📂 文件监控
- ✅ 支持多目标框架（.NET Framework 4.6.1+ 和 .NET 6+）
- ✅ 监控目录或单个文件的变化（新增、修改、删除、重命名）
- ✅ 智能排除模式（避免监控临时文件、备份文件等）
- ✅ 并发控制（工控环境优化，默认最大16个文件同时处理）
- ✅ 异步事件通知机制

### 📊 内容变化分析
- ✅ **临时文件缓存策略**：使用 `.watchfile` 文件存储历史快照
- ✅ **精确差异检测**：行级和字段级的详细变化分析
- ✅ **内存优化**：大量文件监控时有效控制内存使用
- ✅ **变化类型识别**：新增行、删除行、修改行的具体内容
- ✅ **实时变化通知**：`旧值 → 新值` 的详细变化报告

### 📄 文件格式支持
- ✅ CSV 文件解析（逗号/Tab/自定义分割符，多种编码）
- ✅ Excel 文件解析（.xls/.xlsx）
- ✅ 灵活的列映射和数据类型转换
- ✅ 可选的标题行处理

### ⚙️ 配置与扩展
- ✅ 基于 JSON 配置文件的灵活配置
- ✅ 可扩展的处理器架构
- ✅ 详细的差异日志记录
- ✅ 错误处理和重试机制
- ✅ 适用于 WinForms、WPF、控制台等应用

## 🏭 工控场景优化

本库特别针对工控环境进行了优化：

- **大量小文件监控**：支持监控几千甚至几万个不超过10MB的文件
- **设备日志解析**：专为工控设备生成的CSV/Excel日志文件设计
- **内存效率**：通过临时文件策略避免大量内存占用
- **异常容忍**：临时文件丢失时自动恢复机制
- **详细审计**：完整的文件变化历史和差异日志

## 🚀 快速开始

### 1. 构建项目

```bash
# 克隆或下载项目
git clone https://github.com/yourusername/WatchFile
cd WatchFile

# 构建解决方案
dotnet build

# 运行测试程序
cd WatchFile.ConsoleTest
dotnet run
```

### 2. 使用类库

#### 方法一：项目引用
在你的项目中添加对 `WatchFile.Core` 的引用：

```xml
<ProjectReference Include="path\to\WatchFile.Core\WatchFile.Core.csproj" />
```

#### 方法二：NuGet包（发布后）
```bash
Install-Package WatchFile.Core
```

### 2. 创建配置文件

```json
{
  "version": "1.0",
  "globalSettings": {
    "enableLogging": true,
    "logLevel": "Info",
    "bufferTimeMs": 500,
    "maxRetries": 3,
    "offlineChangeDetection": {
      "enabled": true,
      "triggerEventsForNewFiles": true,
      "triggerEventsForDeletedFiles": true,
      "comparisonMethod": "TimestampAndSize",
      "timestampToleranceSeconds": 2
    }
  },
  "watchItems": [
    {
      "id": "industrial-logs-monitor",
      "name": "工控设备日志监控",
      "enabled": true,
      "path": "D:\\IndustrialLogs",
      "type": "Directory",
      "recursive": true,
      "fileFilters": ["*.csv", "*.xlsx"],
      "excludePatterns": ["*.watchfile", "*.tmp", "*_backup_*", "system_*"],
      "watchEvents": ["Created", "Modified", "Deleted"],
      "fileSettings": {
        "fileType": "CSV",
        "hasHeader": true,
        "delimiter": ",",
        "encoding": "UTF-8",
        "columnMappings": [
          {
            "sourceColumn": "DeviceID",
            "targetName": "DeviceID",
            "dataType": "String",
            "required": true
          },
          {
            "sourceColumn": "Timestamp",
            "targetName": "Timestamp",
            "dataType": "DateTime",
            "required": true,
            "format": "yyyy-MM-dd HH:mm:ss"
          },
          {
            "sourceColumn": "Value",
            "targetName": "MeasureValue",
            "dataType": "Decimal",
            "required": true
          }
        ]
      },
      "watchFileSettings": {
        "watchFileDirectory": ".watch",
        "watchFileExtension": ".watchfile",
        "maxConcurrentFiles": 16,
        "throwOnMissingWatchFile": false,
        "enableDifferenceLogging": true,
        "differenceLogPath": "logs/differences.log"
      }
    }
  ]
}
```

### 4. 配置模式参考

以下是不同场景的配置示例，您可以根据需要参考使用：

#### 4.1 CSV文件监控（逗号分隔）

```json
{
  "id": "employees-csv-monitor",
  "name": "员工CSV文件监控",
  "enabled": true,
  "path": "Data/employees.csv",
  "type": "File",
  "watchEvents": ["Created", "Modified"],
  "fileSettings": {
    "fileType": "CSV",
    "hasHeader": true,
    "delimiter": ",",
    "encoding": "UTF-8",
    "columnMappings": [
      {
        "sourceColumn": "Name",
        "targetName": "EmployeeName",
        "dataType": "String",
        "required": true
      },
      {
        "sourceColumn": "Department",
        "targetName": "Department",
        "dataType": "String",
        "required": true
      },
      {
        "sourceColumn": "Salary",
        "targetName": "Salary",
        "dataType": "Decimal",
        "required": true
      }
    ]
  }
}
```

#### 4.2 CSV文件监控（Tab分隔）

```json
{
  "id": "sales-tab-monitor",
  "name": "销售数据Tab分割监控",
  "enabled": true,
  "path": "Data/sales.csv",
  "type": "File",
  "watchEvents": ["Modified"],
  "fileSettings": {
    "fileType": "CSV",
    "hasHeader": true,
    "delimiter": "\t",
    "encoding": "UTF-8",
    "columnMappings": [
      {
        "sourceColumn": "产品名称",
        "targetName": "ProductName",
        "dataType": "String",
        "required": true
      },
      {
        "sourceColumn": "销售额",
        "targetName": "SalesAmount",
        "dataType": "Decimal",
        "required": true
      },
      {
        "sourceColumn": "日期",
        "targetName": "SalesDate",
        "dataType": "DateTime",
        "required": true,
        "format": "yyyy-MM-dd"
      }
    ]
  }
}
```

#### 4.3 Excel文件监控（.xls）

```json
{
  "id": "excel-xls-monitor",
  "name": "员工Excel文件监控(XLS)",
  "enabled": true,
  "path": "Data/employees.xls",
  "type": "File",
  "watchEvents": ["Created", "Modified"],
  "fileSettings": {
    "fileType": "Excel",
    "sheetName": "Sheet1",
    "hasHeader": true,
    "encoding": "UTF-8",
    "columnMappings": [
      {
        "sourceColumn": "Name",
        "targetName": "EmployeeName",
        "dataType": "String",
        "required": true
      },
      {
        "sourceColumn": "Department",
        "targetName": "Department",
        "dataType": "String",
        "required": true
      },
      {
        "sourceColumn": "Salary",
        "targetName": "Salary",
        "dataType": "Decimal",
        "required": true
      }
    ]
  }
}
```

#### 4.4 Excel文件监控（.xlsx）

```json
{
  "id": "excel-xlsx-monitor",
  "name": "产品Excel文件监控(XLSX)",
  "enabled": true,
  "path": "Data/products.xlsx",
  "type": "File",
  "watchEvents": ["Created", "Modified"],
  "fileSettings": {
    "fileType": "Excel",
    "sheetName": "Sheet1",
    "hasHeader": true,
    "encoding": "UTF-8",
    "columnMappings": [
      {
        "sourceColumn": "ProductID",
        "targetName": "ProductID",
        "dataType": "String",
        "required": true
      },
      {
        "sourceColumn": "ProductName",
        "targetName": "ProductName",
        "dataType": "String",
        "required": true
      },
      {
        "sourceColumn": "Price",
        "targetName": "Price",
        "dataType": "Decimal",
        "required": true
      },
      {
        "sourceColumn": "Stock",
        "targetName": "Stock",
        "dataType": "Integer",
        "required": true
      }
    ]
  }
}
```

#### 4.5 目录监控（多文件类型）

```json
{
  "id": "directory-monitor",
  "name": "数据目录监控",
  "enabled": true,
  "path": "D:/DataFiles",
  "type": "Directory",
  "recursive": true,
  "fileFilters": ["*.csv", "*.xlsx", "*.xls"],
  "excludePatterns": ["*.watchfile", "*.tmp", "*_backup_*", "~$*"],
  "watchEvents": ["Created", "Modified", "Deleted"],
  "fileSettings": {
    "fileType": "CSV",
    "hasHeader": true,
    "delimiter": ",",
    "encoding": "UTF-8"
  },
  "watchFileSettings": {
    "watchFileDirectory": ".watch",
    "watchFileExtension": ".watchfile",
    "maxConcurrentFiles": 16,
    "throwOnMissingWatchFile": false,
    "enableDifferenceLogging": true,
    "differenceLogPath": "logs/differences.log"
  }
}
```

### 5. 基本使用

```csharp
using WatchFile.Core;
using WatchFile.Core.Events;

// 创建管理器
var manager = new WatchManager("watchfile-config.json");

// 注册事件处理
manager.FileChanged += (sender, e) =>
{
    Console.WriteLine($"\n[文件变化事件]");
    Console.WriteLine($"时间: {e.Timestamp:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine($"文件: {e.FilePath}");
    Console.WriteLine($"变化类型: {e.ChangeType}");
    
    // 显示详细的数据变化
    if (e.ChangeDetails?.HasChanges == true)
    {
        Console.WriteLine($"变化摘要: {e.ChangeDetails.GetSummary()}");
        
        // 显示新增的行
        if (e.ChangeDetails.AddedRows.Count > 0)
        {
            Console.WriteLine($"新增 {e.ChangeDetails.AddedRows.Count} 行:");
            foreach (var row in e.ChangeDetails.AddedRows.Take(3))
            {
                foreach (var column in row)
                {
                    Console.WriteLine($"  + {column.Key}: {column.Value}");
                }
            }
        }
        
        // 显示修改的行
        if (e.ChangeDetails.ModifiedRows.Count > 0)
        {
            Console.WriteLine($"修改 {e.ChangeDetails.ModifiedRows.Count} 行:");
            foreach (var change in e.ChangeDetails.ModifiedRows.Take(3))
            {
                foreach (var fieldChange in change.FieldChanges)
                {
                    Console.WriteLine($"  ~ {fieldChange.FieldName}: {fieldChange.OldValue} -> {fieldChange.NewValue}");
                }
            }
        }
        
        // 显示删除的行
        if (e.ChangeDetails.DeletedRows.Count > 0)
        {
            Console.WriteLine($"删除 {e.ChangeDetails.DeletedRows.Count} 行");
        }
    }
    
    // 显示当前数据
    if (e.ExtractedData != null)
    {
        Console.WriteLine($"当前文件数据行数: {e.ExtractedData.Count}");
    }
};

// 启动监控
await manager.StartAsync();

// 应用运行...

// 停止监控
await manager.StopAsync();
manager.Dispose();
```

### 4. 自定义处理器

```csharp
public class FileChangeHandler : IFileChangedHandler
{
    public async Task HandleFileChanged(FileChangedEventArgs args)
    {
        // 只处理CSV文件
        if (!args.FilePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            switch (args.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    await HandleNewFile(args);
                    break;
                    
                case WatcherChangeTypes.Changed:
                    await HandleFileModified(args);
                    break;
                    
                case WatcherChangeTypes.Deleted:
                    await HandleFileDeleted(args);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"处理文件变化时出错: {ex.Message}");
        }
    }

    private async Task HandleNewFile(FileChangedEventArgs args)
    {
        Console.WriteLine($"检测到新文件: {Path.GetFileName(args.FilePath)}");
        
        if (args.ExtractedData != null)
        {
            // 在这里添加您的业务逻辑
            // 例如：发送通知、记录日志、数据验证等
            await ProcessData(args.ExtractedData);
            Console.WriteLine($"已处理 {args.ExtractedData.Count} 条新记录");
        }
    }

    private async Task HandleFileModified(FileChangedEventArgs args)
    {
        if (args.ChangeDetails?.HasChanges == true)
        {
            Console.WriteLine($"文件更新: {args.ChangeDetails.GetSummary()}");
            
            // 处理新增的数据
            if (args.ChangeDetails.AddedRows.Count > 0)
            {
                await ProcessData(args.ChangeDetails.AddedRows);
                Console.WriteLine($"已处理 {args.ChangeDetails.AddedRows.Count} 条新增记录");
            }
            
            // 处理修改的数据
            if (args.ChangeDetails.ModifiedRows.Count > 0)
            {
                await ProcessModifiedData(args.ChangeDetails.ModifiedRows);
                Console.WriteLine($"已处理 {args.ChangeDetails.ModifiedRows.Count} 条修改记录");
            }
        }
    }

    private async Task HandleFileDeleted(FileChangedEventArgs args)
    {
        Console.WriteLine($"文件被删除: {Path.GetFileName(args.FilePath)}");
        
        if (args.ChangeDetails?.DeletedRows.Count > 0)
        {
            // 处理删除的数据
            await LogDeletedData(args.ChangeDetails.DeletedRows);
        }
    }

    private async Task ProcessData(List<Dictionary<string, object>> data)
    {
        // 实现您的数据处理逻辑
        await Task.Run(() => {
            foreach (var row in data)
            {
                // 示例：打印数据内容
                Console.WriteLine($"处理数据行:");
                foreach (var column in row)
                {
                    Console.WriteLine($"  {column.Key}: {column.Value}");
                }
            }
        });
    }

    private async Task ProcessModifiedData(List<RowChange> changes)
    {
        // 实现修改数据的处理逻辑
        foreach (var change in changes)
        {
            Console.WriteLine($"处理第 {change.RowIndex + 1} 行的修改:");
            foreach (var fieldChange in change.FieldChanges)
            {
                Console.WriteLine($"  {fieldChange.FieldName}: {fieldChange.OldValue} -> {fieldChange.NewValue}");
            }
        }
        await Task.CompletedTask;
    }

    private async Task LogDeletedData(List<Dictionary<string, object>> deletedData)
    {
        // 记录删除的数据
        Console.WriteLine($"记录 {deletedData.Count} 条删除的数据");
        await Task.CompletedTask;
    }
}

// 注册自定义处理器
manager.AddHandler(new FileChangeHandler());

```

## 配置说明

### 全局设置 (globalSettings)

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| enableLogging | bool | true | 是否启用日志 |
| logLevel | string | "Info" | 日志级别 (Debug/Info/Warning/Error) |
| bufferTimeMs | int | 500 | 文件变化缓冲时间（毫秒） |
| maxRetries | int | 3 | 文件读取失败重试次数 |

### 监控项 (watchItems)

| 属性 | 类型 | 说明 |
|------|------|------|
| id | string | 唯一标识符 |
| name | string | 显示名称 |
| enabled | bool | 是否启用 |
| path | string | 监控路径（目录或文件） |
| type | enum | 监控类型 (Directory/File) |
| recursive | bool | 是否递归监控子目录 |
| fileFilters | array | 文件过滤器（如 ["*.csv", "*.xlsx"]） |
| watchEvents | array | 监控事件 (Created/Modified/Deleted/Renamed) |

### 文件设置 (fileSettings)

| 属性 | 类型 | 说明 |
|------|------|------|
| fileType | enum | 文件类型 (CSV/Excel) |
| hasHeader | bool | 是否包含标题行 |
| delimiter | string | CSV分隔符 |
| encoding | string | 文件编码 |
| sheetName | string | Excel工作表名称 |
| startRow | int | 数据开始行号 |
| columnMappings | array | 列映射配置 |

### 列映射 (columnMappings)

| 属性 | 类型 | 说明 |
|------|------|------|
| sourceColumn | string/int | 源列（列名或索引） |
| targetName | string | 目标属性名 |
| dataType | enum | 数据类型 (String/Integer/Decimal/DateTime/Boolean) |
| required | bool | 是否必需 |
| format | string | 格式化字符串（如日期格式） |

## 支持的文件格式

### CSV 文件
- 逗号分割 (,)
- Tab 分割 (\t)
- 自定义分隔符
- 多种编码：UTF-8、GBK、GB2312、ASCII

### Excel 文件
- .xls (Excel 97-2003)
- .xlsx (Excel 2007+)
- 多工作表支持
- 指定数据开始行

## API 参考

### WatchFileManager

```csharp
### WatchManager

核心文件监控管理器。

```csharp
public class WatchManager : IDisposable
{
    // 事件
    public event EventHandler<FileChangedEventArgs> FileChanged;
    public event EventHandler<MonitorStatusChangedEventArgs> StatusChanged;
    
    // 属性
    public bool IsRunning { get; }
    public int ActiveWatchersCount { get; }
    public Dictionary<string, MonitorStatus> WatcherStatuses { get; }
    
    // 方法
    public async Task StartAsync();
    public async Task StopAsync();
    public async Task ReloadConfigurationAsync();
    public void AddHandler(IFileChangedHandler handler);
    public void RemoveHandler(IFileChangedHandler handler);
    public async Task EnableWatchItemAsync(string id);
    public async Task DisableWatchItemAsync(string id);
}
```

### FileChangedEventArgs

```csharp
public class FileChangedEventArgs : EventArgs
{
    public string WatchItemId { get; set; }
    public string WatchItemName { get; set; }
    public string FilePath { get; set; }
    public WatcherChangeTypes ChangeType { get; set; }
    public DateTime Timestamp { get; set; }
    public List<Dictionary<string, object>> ExtractedData { get; set; }
    public Exception Exception { get; set; }
    public long FileSize { get; set; }
    public bool IsSuccess { get; }
    public int DataRowCount { get; }
}
```

## 示例应用

项目包含完整的控制台测试程序：

### 运行测试程序
```bash
cd WatchFile.ConsoleTest
dotnet run
```

**测试程序功能：**
- 🔧 自动创建默认测试配置
- 📊 实时显示监控状态
- 🔔 文件变化事件通知
- 📝 数据解析结果展示
- 🧪 内置功能测试
- 📋 交互式操作界面

**测试操作：**
- 按 `t` - 运行自动测试
- 按 `s` - 显示监控状态
- 按 `q` - 退出程序
- 修改 `TestData` 目录下的文件来触发监控

## 构建和测试

```bash
# 克隆仓库
git clone https://github.com/yourusername/WatchFile
cd WatchFile

# 构建解决方案
dotnet build

# 运行控制台测试程序
cd WatchFile.ConsoleTest
dotnet run

# 打包类库项目
cd ..\WatchFile.Core
dotnet pack -c Release
```

## 依赖项

- **NPOI** (2.6.2) - Excel 文件处理
- **CsvHelper** (30.0.1) - CSV 文件处理
- **Newtonsoft.Json** (13.0.3) - .NET Framework JSON 支持
- **System.Text.Json** (7.0.3) - .NET 6+ JSON 支持

## 兼容性

- .NET Framework 4.6.1+
- .NET 6+
- .NET 7+（兼容）
- .NET 8+（兼容）

## 许可证

MIT License - 查看 [LICENSE](LICENSE) 文件

## 贡献

欢迎贡献代码！请查看 [CONTRIBUTING.md](CONTRIBUTING.md) 了解详情。

## 更新日志

### v2.4.0 (2025-09-07)

#### 🔍 离线变化检测
- **智能恢复监控** - 监控器重启时自动检测停机期间的文件变化
- **无缝衔接** - 自动识别新增、修改、删除的文件并触发相应事件
- **配置驱动** - 完全可配置的检测策略和行为

#### ⚡ 性能优化
- **内存优化** - 移除ExtractedData冗余属性，减少内存占用
- **类名优化** - WatchFileManager → WatchManager，API更清晰
- **执行优化** - 优化启动顺序，确保检测准确性

#### 🔧 Bug修复
- **修复自动删除** - 离线检测到的文件也支持配置驱动的自动删除
- **修复初始化顺序** - 先执行离线检测再初始化watchfile，确保检测准确

### v2.0.0

#### 🚀 重大功能增强
- **智能内容变化分析**：新增详细的文件内容差异检测
- **临时文件缓存策略**：使用 `.watchfile` 文件存储历史快照，优化内存使用
- **工控环境优化**：专为大量小文件监控场景设计
- **排除模式支持**：智能排除临时文件、备份文件等不需要监控的文件

#### 📊 数据变化分析
- 行级差异检测：精确识别新增、删除、修改的数据行
- 字段级变化跟踪：显示具体字段的 `旧值 → 新值` 变化
- 变化摘要报告：提供清晰的变化统计信息
- 实时差异日志：可选的详细变化记录

#### ⚙️ 配置增强
- 新增 `excludePatterns` 配置项：支持通配符排除模式
- 新增 `watchFileSettings` 配置组：临时文件管理设置
- 并发控制：可配置最大同时处理文件数（默认16个）
- 异常处理策略：临时文件丢失时的自动恢复机制

#### 🔧 技术改进
- 将 JSON 库统一为 Newtonsoft.Json（解决安全警告）
- 性能优化：减少内存占用，提高大量文件处理效率
- 兼容性增强：更好的 .NET Framework 4.6.1 支持
- 错误处理：更完善的异常信息和恢复机制

### v1.0.0

#### 🎯 初始功能
- 支持 CSV 和 Excel 文件监控
- 基于配置文件的监控规则
- 异步事件通知
- 列映射和数据转换
- 多目标框架支持（.NET Framework 4.6.1+ 和 .NET 6+）

## 支持

如果遇到问题或有建议，请：

1. 查看 [文档](docs/)
2. 提交 [Issue](https://github.com/yourusername/WatchFile/issues)
3. 参与 [讨论](https://github.com/yourusername/WatchFile/discussions)

---

**WatchFile** - 让文件监控变得简单高效！
