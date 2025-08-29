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
        /// 提取的数据（仅在Created和Modified时有值）
        /// </summary>
        public List<Dictionary<string, object>>? ExtractedData { get; set; }

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
        public int DataRowCount => ExtractedData?.Count ?? 0;
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
}
