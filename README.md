# WatchFile - 文件监控与内容通知库

一个支持 .NET Framework 4.6.1+ 和 .NET 6+ 的文件监控库，可以监控目录或文件的变化，并自动解析 CSV 和 Excel 文件内容，通过回调函数通知应用程序。

## 🚀 项目结构

```
WatchFile/
├── WatchFile.Core/                    # 核心类库项目
│   ├── Configuration/                 # 配置管理
│   ├── Events/                       # 事件定义
│   ├── Monitoring/                   # 监控功能
│   ├── Parsing/                      # 文件解析
│   ├── watchfile-config.json        # 默认配置文件
│   └── API.md                        # API文档
├── WatchFile.ConsoleTest/            # 控制台测试项目
│   ├── TestData/                     # 测试数据文件
│   ├── test-config.json             # 测试配置文件
│   └── Program.cs                    # 测试程序
├── WatchFile.sln                     # 解决方案文件
└── README.md                         # 项目说明
```

## 特性

- ✅ 支持多目标框架（.NET Framework 4.6.1+ 和 .NET 6+）
- ✅ 监控目录或单个文件的变化（新增、修改、删除）
- ✅ 支持 CSV 文件解析（逗号/Tab 分割，多种编码）
- ✅ 支持 Excel 文件解析（.xls/.xlsx）
- ✅ 基于 JSON 配置文件的灵活配置
- ✅ 列映射和数据类型转换
- ✅ 异步事件通知
- ✅ 可扩展的处理器架构
- ✅ 错误处理和重试机制
- ✅ 适用于 WinForms、WPF、控制台等应用

## 快速开始

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
    "maxRetries": 3
  },
  "watchItems": [
    {
      "id": "sales-monitor",
      "name": "销售数据监控",
      "enabled": true,
      "path": "D:\\Data\\Sales",
      "type": "Directory",
      "recursive": true,
      "fileFilters": ["*.csv"],
      "watchEvents": ["Created", "Modified"],
      "fileSettings": {
        "fileType": "CSV",
        "hasHeader": true,
        "delimiter": ",",
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
          }
        ]
      }
    }
  ]
}
```

### 3. 基本使用

```csharp
using WatchFile.Core;
using WatchFile.Core.Events;

// 创建管理器
var manager = new WatchFileManager("watchfile-config.json");

// 注册事件处理
manager.FileChanged += (sender, e) =>
{
    Console.WriteLine($"文件变化: {e.FilePath}");
    
    if (e.ExtractedData != null)
    {
        foreach (var row in e.ExtractedData)
        {
            foreach (var column in row)
            {
                Console.WriteLine($"{column.Key}: {column.Value}");
            }
        }
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
public class DatabaseHandler : FileChangedHandlerBase
{
    public override async Task HandleFileChanged(FileChangedEventArgs args)
    {
        if (!ShouldHandle(args)) return;

        try
        {
            // 保存到数据库
            await SaveToDatabase(args.ExtractedData);
            LogInfo($"成功处理 {args.DataRowCount} 行数据");
        }
        catch (Exception ex)
        {
            LogError($"数据库保存失败: {ex.Message}", ex);
        }
    }

    private async Task SaveToDatabase(List<Dictionary<string, object>> data)
    {
        // 数据库操作实现
    }
}

// 注册处理器
manager.AddHandler(new DatabaseHandler());
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
public class WatchFileManager : IDisposable
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

### v1.0.0
- 初始版本
- 支持 CSV 和 Excel 文件监控
- 基于配置文件的监控规则
- 异步事件通知
- 列映射和数据转换

## 支持

如果遇到问题或有建议，请：

1. 查看 [文档](docs/)
2. 提交 [Issue](https://github.com/yourusername/WatchFile/issues)
3. 参与 [讨论](https://github.com/yourusername/WatchFile/discussions)

---

**WatchFile** - 让文件监控变得简单高效！
