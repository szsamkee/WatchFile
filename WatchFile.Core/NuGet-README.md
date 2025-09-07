# WatchFile.Core - 智能文件监控库

[![NuGet](https://img.shields.io/nuget/v/WatchFile.Core.svg)](https://www.nuget.org/packages/WatchFile.Core/) [![Downloads](https://img.shields.io/nuget/dt/WatchFile.Core.svg)](https://www.nuget.org/packages/WatchFile.Core/)

专为工控环境设计的高性能文件监控库，支持 .NET Framework 4.6.1+ 和 .NET 6+。

## 🆕 v2.4.0 新功能

### 🔍 离线变化检测
- **智能恢复监控** - 监控器重启时自动检测停机期间的文件变化
- **无缝衔接** - 自动识别新增、修改、删除的文件并触发相应事件

### ⚡ 性能优化
- **内存优化** - 减少内存占用，提升大量文件监控性能
- **API优化** - WatchFileManager → WatchManager，更清晰的命名

## ✨ 核心特性

- ✅ **智能监控** - 目录/文件变化实时监控，支持递归监控
- ✅ **内容分析** - 精确的行级、字段级变化分析 
- ✅ **格式支持** - CSV、Excel文件解析，支持多种编码
- ✅ **离线检测** - 重启后自动检测停机期间的文件变化
- ✅ **工控优化** - 专为大量小文件监控场景设计
- ✅ **配置驱动** - JSON配置，支持排除模式和自动删除

## 🚀 快速开始

```csharp
// 1. 创建监控管理器
var manager = new WatchManager("watchfile-config.json");

// 2. 订阅事件
manager.FileChanged += (sender, e) => {
    Console.WriteLine($"文件变化: {e.FilePath}");
    Console.WriteLine($"变化类型: {e.ChangeType}");
    
    // 查看详细变化
    if (e.ChangeDetails != null && e.ChangeDetails.HasChanges) {
        Console.WriteLine($"新增行: {e.ChangeDetails.AddedRows.Count}");
        Console.WriteLine($"删除行: {e.ChangeDetails.DeletedRows.Count}");
        Console.WriteLine($"修改行: {e.ChangeDetails.ModifiedRows.Count}");
    }
};

// 3. 离线变化检测事件
manager.OfflineChangesDetected += (sender, e) => {
    var summary = e.GetSummary();
    Console.WriteLine($"离线检测: {summary}");
};

// 4. 启动监控
await manager.StartAsync();
```

## ⚙️ 配置示例

```json
{
  "version": "1.0",
  "globalSettings": {
    "enableLogging": true,
    "logLevel": "Info",
    "offlineChangeDetection": {
      "enabled": true,
      "triggerEventsForNewFiles": true,
      "triggerEventsForDeletedFiles": true,
      "comparisonMethod": "TimestampAndSize"
    }
  },
  "watchItems": [{
    "id": "csv-monitor", 
    "name": "CSV文件监控",
    "enabled": true,
    "path": "C:\\Data",
    "type": "Directory",
    "fileFilters": ["*.csv"],
    "watchEvents": ["Created", "Modified"],
    "deleteAfterProcessing": false,
    "fileSettings": {
      "fileType": "CSV",
      "hasHeader": true,
      "delimiter": ",",
      "encoding": "UTF-8"
    }
  }]
}
```

## 📚 完整文档

- **[API文档](https://github.com/szsamkee/WatchFile/blob/main/WatchFile.Core/API.md)** - 详细的类、方法、事件文档
- **[GitHub仓库](https://github.com/szsamkee/WatchFile)** - 源代码、示例、完整README
- **[配置参考](https://github.com/szsamkee/WatchFile#配置文件结构)** - 完整的配置选项
- **[更新日志](https://github.com/szsamkee/WatchFile#更新日志)** - 版本历史

## 🎯 适用场景

- 工控设备日志文件监控
- 数据文件变化实时分析  
- 批处理文件自动化处理
- 文件内容差异追踪
- 生产环境文件监控

## 📄 许可证

MIT License - 查看 [LICENSE](https://github.com/szsamkee/WatchFile/blob/main/LICENSE)
