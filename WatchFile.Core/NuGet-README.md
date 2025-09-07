# WatchFile.Core - 智能文件监控库

[![NuGet](https://img.shields.io/nuget/v/WatchFile.Core.svg)](https://www.nuget.org/packages/WatchFile.Core/) [![Downloads](https://img.shields.io/nuget/dt/WatchFile.Core.svg)](https://www.nuget.org/packages/WatchFile.Core/)

专为工控环境设计的高性能文件监控库，支持 .NET Framework 4.6.1+ 和 .NET 6+。

## 🆕 v2.5.0 新功能

### � 简化离线变化处理
- **自动联动模式** - 新增 `autoTriggerFileChangedEvents` 配置，离线变化可自动转换为 FileChanged 事件
- **统一事件处理** - 用户只需处理一个 FileChanged 事件，离线和实时变化统一处理
- **配置灵活性** - 支持自动模式和手动模式，满足不同使用场景

### ⚡ 用户体验优化
- **默认配置优化** - `TriggerEventsForNewFiles` 默认为 true，提升测试体验
- **文档完善** - 添加详细的使用模式说明和最佳实践

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
      "autoTriggerFileChangedEvents": true,
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
