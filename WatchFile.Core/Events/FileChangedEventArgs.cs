using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace WatchFile.Core.Events
{
    /// <summary>
    /// 文件变化事件参数
    /// </summary>
    public class FileChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 监控项ID
        /// </summary>
        public string WatchItemId { get; set; } = string.Empty;

        /// <summary>
        /// 监控项名称
        /// </summary>
        public string WatchItemName { get; set; } = string.Empty;

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 变化类型
        /// </summary>
        public WatcherChangeTypes ChangeType { get; set; }

        /// <summary>
        /// 事件发生时间
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 变化前的数据（仅在Modified时有值）
        /// </summary>
        public List<Dictionary<string, object>>? PreviousData { get; set; }

        /// <summary>
        /// 变化后的完整文件内容（包含文件的所有数据行）
        /// </summary>
        public List<Dictionary<string, object>>? CurrentData { get; set; }

        /// <summary>
        /// 数据变化详情
        /// </summary>
        public DataChangeDetails? ChangeDetails { get; set; }

        /// <summary>
        /// 处理过程中的异常（如果有）
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 是否处理成功
        /// </summary>
        public bool IsSuccess => Exception == null;

        /// <summary>
        /// 提取的数据行数
        /// </summary>
        public int DataRowCount => CurrentData?.Count ?? 0;

        /// <summary>
        /// 是否为离线变化检测（监控器重启时检测到的变化）
        /// </summary>
        public bool IsOfflineChange { get; set; } = false;

        /// <summary>
        /// 用户处理结果（用户在事件处理中设置，默认为Success）
        /// </summary>
        public FileProcessResult ProcessResult { get; set; } = FileProcessResult.Success;

        /// <summary>
        /// 处理结果备注（可选，用于日志记录和调试）
        /// </summary>
        public string ProcessResultReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// 文件变化处理接口
    /// </summary>
    public interface IFileChangedHandler
    {
        /// <summary>
        /// 处理文件变化事件
        /// </summary>
        /// <param name="args">文件变化事件参数</param>
        /// <returns>异步任务</returns>
        Task HandleFileChanged(FileChangedEventArgs args);
    }

    /// <summary>
    /// 文件解析结果
    /// </summary>
    public class FileParseResult
    {
        /// <summary>
        /// 是否解析成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 解析后的数据
        /// </summary>
        public List<Dictionary<string, object>> Data { get; set; } = new List<Dictionary<string, object>>();

        /// <summary>
        /// 解析过程中的异常
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// 解析的行数
        /// </summary>
        public int RowCount => Data.Count;

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage => Exception?.Message ?? string.Empty;
    }

    /// <summary>
    /// 监控状态变化事件参数
    /// </summary>
    public class MonitorStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 监控项ID
        /// </summary>
        public string WatchItemId { get; set; } = string.Empty;

        /// <summary>
        /// 监控状态
        /// </summary>
        public MonitorStatus Status { get; set; }

        /// <summary>
        /// 状态变化时间
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 状态变化原因
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 相关的异常（如果有）
        /// </summary>
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// 监控状态
    /// </summary>
    public enum MonitorStatus
    {
        /// <summary>
        /// 停止
        /// </summary>
        Stopped,

        /// <summary>
        /// 启动中
        /// </summary>
        Starting,

        /// <summary>
        /// 运行中
        /// </summary>
        Running,

        /// <summary>
        /// 错误
        /// </summary>
        Error,

        /// <summary>
        /// 暂停
        /// </summary>
        Paused
    }

    /// <summary>
    /// 数据变化详情
    /// </summary>
    public class DataChangeDetails
    {
        /// <summary>
        /// 新增的行数据
        /// </summary>
        public List<Dictionary<string, object>> AddedRows { get; set; } = new();

        /// <summary>
        /// 删除的行数据
        /// </summary>
        public List<Dictionary<string, object>> DeletedRows { get; set; } = new();

        /// <summary>
        /// 修改的行数据（包含修改前后的值）
        /// </summary>
        public List<RowChange> ModifiedRows { get; set; } = new();

        /// <summary>
        /// 总行数变化
        /// </summary>
        public int RowCountChange => AddedRows.Count - DeletedRows.Count;

        /// <summary>
        /// 是否有数据变化
        /// </summary>
        public bool HasChanges => AddedRows.Count > 0 || DeletedRows.Count > 0 || ModifiedRows.Count > 0;

        /// <summary>
        /// 变化摘要
        /// </summary>
        public string GetSummary()
        {
            var parts = new List<string>();
            
            if (AddedRows.Count > 0)
                parts.Add($"新增{AddedRows.Count}行");
                
            if (DeletedRows.Count > 0)
                parts.Add($"删除{DeletedRows.Count}行");
                
            if (ModifiedRows.Count > 0)
                parts.Add($"修改{ModifiedRows.Count}行");
                
            return parts.Count > 0 ? string.Join(", ", parts) : "无变化";
        }
    }

    /// <summary>
    /// 行变化详情
    /// </summary>
    public class RowChange
    {
        /// <summary>
        /// 行索引（从0开始）
        /// </summary>
        public int RowIndex { get; set; }

        /// <summary>
        /// 修改前的数据
        /// </summary>
        public Dictionary<string, object> OldValues { get; set; } = new();

        /// <summary>
        /// 修改后的数据
        /// </summary>
        public Dictionary<string, object> NewValues { get; set; } = new();

        /// <summary>
        /// 变化的字段
        /// </summary>
        public List<FieldChange> FieldChanges { get; set; } = new();
    }

    /// <summary>
    /// 字段变化详情
    /// </summary>
    public class FieldChange
    {
        /// <summary>
        /// 字段名
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// 旧值
        /// </summary>
        public object? OldValue { get; set; }

        /// <summary>
        /// 新值
        /// </summary>
        public object? NewValue { get; set; }

        /// <summary>
        /// 变化类型
        /// </summary>
        public FieldChangeType ChangeType { get; set; }
    }

    /// <summary>
    /// 字段变化类型
    /// </summary>
    public enum FieldChangeType
    {
        /// <summary>
        /// 值修改
        /// </summary>
        Modified,

        /// <summary>
        /// 新增字段
        /// </summary>
        Added,

        /// <summary>
        /// 删除字段
        /// </summary>
        Removed
    }

    /// <summary>
    /// 文件处理结果
    /// </summary>
    public enum FileProcessResult
    {
        /// <summary>
        /// 处理成功，可以删除文件（默认值）
        /// </summary>
        Success,
        
        /// <summary>
        /// 处理成功，但保留文件（用于下次离线检测）
        /// </summary>
        SuccessButKeep,
        
        /// <summary>
        /// 处理失败，保留文件重试
        /// </summary>
        Failed,
        
        /// <summary>
        /// 跳过处理，保留文件
        /// </summary>
        Skipped
    }
}
