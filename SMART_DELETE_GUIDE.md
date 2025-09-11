# 智能文件删除控制指南

WatchFile.Core v2.6.0 新增了智能文件删除控制功能，让用户可以根据业务处理结果精确控制每个文件的生命周期。

## 🚀 核心特性

- **用户控制**：根据业务处理结果决定是否删除文件
- **灵活配置**：支持多种删除策略和重试机制
- **向后兼容**：默认行为保持不变（Success=删除，其他=保留）
- **离线友好**：保留的文件会在下次启动时被离线检测到

## 📋 处理结果类型

### 1. Success - 处理成功，可以删除文件（默认）
- 业务逻辑处理完全成功
- 数据已成功写入数据库
- 文件可以安全删除

```csharp
manager.FileChanged += (sender, e) => {
    // 处理业务逻辑
    await ProcessData(e.CurrentData);
    
    // 不设置ProcessResult，默认为Success，文件会被删除
    // e.ProcessResult = FileProcessResult.Success; // 可选，默认值
};
```

### 2. SuccessButKeep - 处理成功，但保留文件
- 业务处理成功，但由于特定原因需要保留文件
- 适用于批量处理、人工审核、定时处理等场景

```csharp
manager.FileChanged += (sender, e) => {
    // 处理数据
    var records = ProcessData(e.CurrentData);
    
    // 检查是否需要等待批量处理
    if (NeedWaitForBatch(records))
    {
        e.ProcessResult = FileProcessResult.SuccessButKeep;
        e.ProcessResultReason = "等待其他文件一起批量处理";
    }
};
```

### 3. Failed - 处理失败，保留文件重试
- 处理过程中出现可恢复的错误
- 文件会在配置的重试间隔后重新处理

```csharp
manager.FileChanged += (sender, e) => {
    try
    {
        await ProcessData(e.CurrentData);
        // 成功，默认删除
    }
    catch (NetworkException ex)
    {
        e.ProcessResult = FileProcessResult.Failed;
        e.ProcessResultReason = $"网络异常: {ex.Message}，将重试";
    }
};
```

### 4. Skipped - 跳过处理，保留文件
- 由于条件不满足，跳过当前处理
- 文件保留但不会自动重试

```csharp
manager.FileChanged += (sender, e) => {
    // 检查处理时间窗口
    if (!IsInWorkingHours())
    {
        e.ProcessResult = FileProcessResult.Skipped;
        e.ProcessResultReason = "非工作时间，跳过处理";
    }
};
```

## ⚙️ 配置说明

### 基础配置
```json
{
  "watchItems": [
    {
      "id": "my-processor",
      "deleteAfterProcessing": true,
      "deletePolicy": {
        "strategy": "RespectProcessResult",
        "deleteOn": ["Success"],
        "keepOn": ["Failed", "SuccessButKeep", "Skipped"],
        "retryPolicy": {
          "enabled": true,
          "maxRetries": 3,
          "retryInterval": "00:05:00",
          "retryOn": ["Failed"],
          "exponentialBackoff": true
        }
      }
    }
  ]
}
```

### 删除策略类型

#### 1. RespectProcessResult（推荐）
根据用户设置的ProcessResult决定是否删除文件。

#### 2. Always
总是删除文件，忽略ProcessResult。

#### 3. Never
从不删除文件，忽略ProcessResult。

### 重试机制

- **enabled**: 是否启用重试
- **maxRetries**: 最大重试次数
- **retryInterval**: 重试间隔（TimeSpan格式）
- **retryOn**: 需要重试的处理结果列表
- **exponentialBackoff**: 是否使用指数退避（5min → 10min → 20min）

## 🎯 实际应用场景

### 场景1：生产线数据批量处理
```csharp
private static void OnProductionData(object sender, FileChangedEventArgs e)
{
    var lineId = ExtractLineId(e.FilePath);
    var shift = ExtractShift(e.FilePath);
    
    // 存储单个生产线数据
    await _dataService.StoreAsync(lineId, shift, e.CurrentData);
    
    // 检查是否收齐了所有生产线的数据
    var completedLines = await _dataService.GetCompletedLinesAsync(shift);
    if (completedLines.Count < 4) // 需要等待4条生产线
    {
        e.ProcessResult = FileProcessResult.SuccessButKeep;
        e.ProcessResultReason = $"等待其他生产线数据，当前已收到：{completedLines.Count}/4";
        return;
    }
    
    // 生成班次报表
    await _reportService.GenerateShiftReportAsync(shift);
    e.ProcessResult = FileProcessResult.Success; // 可以删除
}
```

### 场景2：质量检测数据处理
```csharp
private static void OnQualityData(object sender, FileChangedEventArgs e)
{
    var qualityRecords = ParseQualityData(e.CurrentData);
    
    // 检查是否有质量异常
    var issues = qualityRecords.Where(r => r.IsOutOfSpec).ToList();
    if (issues.Any())
    {
        // 触发质量警报
        await _alertService.TriggerQualityAlertAsync(issues);
        
        e.ProcessResult = FileProcessResult.SuccessButKeep;
        e.ProcessResultReason = $"发现{issues.Count}个质量问题，需要工程师审核";
        return;
    }
    
    // 正常数据直接入库
    await _qualityService.StoreAsync(qualityRecords);
    e.ProcessResult = FileProcessResult.Success;
}
```

### 场景3：网络异常处理
```csharp
private static void OnFileChanged(object sender, FileChangedEventArgs e)
{
    try
    {
        // 上传到云端
        await _cloudService.UploadAsync(e.FilePath, e.CurrentData);
        e.ProcessResult = FileProcessResult.Success;
    }
    catch (NetworkException ex) when (ex.IsTransient)
    {
        e.ProcessResult = FileProcessResult.Failed;
        e.ProcessResultReason = $"网络异常，将在5分钟后重试: {ex.Message}";
    }
    catch (Exception ex)
    {
        e.ProcessResult = FileProcessResult.Skipped;
        e.ProcessResultReason = $"不可恢复的错误: {ex.Message}";
    }
}
```

## 🔄 与离线检测的配合

保留的文件（Failed、SuccessButKeep、Skipped）会在下次监控器启动时被离线检测到，重新触发处理逻辑。

```csharp
manager.FileChanged += (sender, e) => {
    if (e.IsOfflineChange)
    {
        Console.WriteLine($"处理离线文件: {e.FilePath}");
        // 之前保留的文件重新处理
    }
    
    // 处理业务逻辑...
};
```

## 📈 监控和调试

通过ProcessResultReason可以记录详细的处理原因：

```csharp
e.ProcessResultReason = $"批量ID: {batchId}, 已收到文件: {currentCount}/{expectedCount}";
```

这些信息会在日志中显示，便于问题排查和业务监控。

## 🎯 最佳实践

1. **默认行为**：不设置ProcessResult，让文件正常删除
2. **明确原因**：设置ProcessResultReason，便于调试和监控
3. **异常处理**：区分可重试错误（Failed）和不可重试错误（Skipped）
4. **批量处理**：使用SuccessButKeep等待其他文件
5. **人工介入**：对于需要审核的情况使用SuccessButKeep

这个功能让WatchFile.Core在工控环境中更加智能和可靠！🚀
