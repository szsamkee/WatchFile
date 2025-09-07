using System;
using System.Collections.Generic;

namespace WatchFile.Core.Configuration.Models
{
    /// <summary>
    /// 文件监控配置根模型
    /// </summary>
    public class WatchFileConfiguration
    {
        /// <summary>
        /// 配置文件版本
        /// </summary>
        public string Version { get; set; } = "1.0";
        
        /// <summary>
        /// 全局设置
        /// </summary>
        public GlobalSettings GlobalSettings { get; set; } = new GlobalSettings();
        
        /// <summary>
        /// 监控项列表
        /// </summary>
        public List<WatchItem> WatchItems { get; set; } = new List<WatchItem>();
    }

    /// <summary>
    /// 全局设置
    /// </summary>
    public class GlobalSettings
    {
        /// <summary>
        /// 是否启用日志记录
        /// </summary>
        public bool EnableLogging { get; set; } = true;
        
        /// <summary>
        /// 日志级别 (Debug, Info, Warning, Error)
        /// </summary>
        public string LogLevel { get; set; } = "Info";
        
        /// <summary>
        /// 缓冲时间（毫秒），用于合并短时间内的多次文件变化
        /// </summary>
        public int BufferTimeMs { get; set; } = 500;
        
        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetries { get; set; } = 3;
        
        /// <summary>
        /// 日志文件路径
        /// </summary>
        public string LogFilePath { get; set; } = "logs/watchfile.log";
        
        /// <summary>
        /// 离线变化检测设置
        /// </summary>
        public OfflineChangeDetectionSettings OfflineChangeDetection { get; set; } = new OfflineChangeDetectionSettings();
    }

    /// <summary>
    /// 监控项配置
    /// </summary>
    public class WatchItem
    {
        /// <summary>
        /// 监控项唯一标识
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// 监控项显示名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// 是否启用此监控项
        /// </summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// 监控路径（文件或目录）
        /// </summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>
        /// 监控类型（Directory 或 File）
        /// </summary>
        public WatchType Type { get; set; } = WatchType.Directory;
        
        /// <summary>
        /// 是否递归监控子目录
        /// </summary>
        public bool Recursive { get; set; } = true;
        
        /// <summary>
        /// 文件过滤器列表（如 *.csv, *.xlsx）
        /// </summary>
        public List<string> FileFilters { get; set; } = new List<string>();
        
        /// <summary>
        /// 排除模式列表（如 *.watchfile, *.tmp）
        /// </summary>
        public List<string> ExcludePatterns { get; set; } = new List<string> { "*.watchfile", "*.tmp", "*_backup_*" };
        
        /// <summary>
        /// 监控的事件类型列表
        /// </summary>
        public List<WatchEvent> WatchEvents { get; set; } = new List<WatchEvent>();
        
        /// <summary>
        /// 文件解析设置
        /// </summary>
        public FileSettings FileSettings { get; set; } = new FileSettings();
        
        /// <summary>
        /// 监控文件管理设置
        /// </summary>
        public WatchFileSettings WatchFileSettings { get; set; } = new WatchFileSettings();
        
        /// <summary>
        /// 是否在文件处理完成后自动删除
        /// 警告：启用此选项将导致文件被永久删除，请谨慎使用
        /// </summary>
        public bool DeleteAfterProcessing { get; set; } = false;
    }

    /// <summary>
    /// 文件设置
    /// </summary>
    public class FileSettings
    {
        /// <summary>
        /// 文件类型（CSV 或 Excel）
        /// </summary>
        public FileType FileType { get; set; } = FileType.CSV;
        
        /// <summary>
        /// 是否包含标题行
        /// </summary>
        public bool HasHeader { get; set; } = true;
        
        /// <summary>
        /// CSV 分隔符（默认为逗号）
        /// </summary>
        public string Delimiter { get; set; } = ",";
        
        /// <summary>
        /// 文件编码（如 UTF-8, GB2312, GBK）
        /// </summary>
        public string Encoding { get; set; } = "UTF-8";
        
        /// <summary>
        /// Excel 工作表名称
        /// </summary>
        public string SheetName { get; set; } = "Sheet1";
        
        /// <summary>
        /// 数据开始行号（1 基索引）
        /// </summary>
        public int StartRow { get; set; } = 1;
        
        /// <summary>
        /// 列映射配置列表
        /// </summary>
        public List<ColumnMapping> ColumnMappings { get; set; } = new List<ColumnMapping>();
    }

    /// <summary>
    /// 列映射配置
    /// </summary>
    public class ColumnMapping
    {
        /// <summary>
        /// 源列名（字符串表示列名）
        /// </summary>
        public string SourceColumn { get; set; } = string.Empty;
        
        /// <summary>
        /// 源列序号（整数表示列索引，从0开始，优先于SourceColumn）
        /// </summary>
        public int? SourceColumnSeq { get; set; }
        
        /// <summary>
        /// 目标属性名
        /// </summary>
        public string TargetName { get; set; } = string.Empty;
        
        /// <summary>
        /// 数据类型
        /// </summary>
        public DataType DataType { get; set; } = DataType.String;
        
        /// <summary>
        /// 是否必需
        /// </summary>
        public bool Required { get; set; } = false;
        
        /// <summary>
        /// 格式化字符串（如日期格式）
        /// </summary>
        public string Format { get; set; } = string.Empty;
    }

    /// <summary>
    /// 监控类型
    /// </summary>
    public enum WatchType
    {
        /// <summary>
        /// 目录监控
        /// </summary>
        Directory,
        
        /// <summary>
        /// 单文件监控
        /// </summary>
        File
    }

    /// <summary>
    /// 监控事件类型
    /// </summary>
    public enum WatchEvent
    {
        /// <summary>
        /// 文件创建事件
        /// </summary>
        Created,
        
        /// <summary>
        /// 文件修改事件
        /// </summary>
        Modified,
        
        /// <summary>
        /// 文件删除事件
        /// </summary>
        Deleted,
        
        /// <summary>
        /// 文件重命名事件
        /// </summary>
        Renamed
    }

    /// <summary>
    /// 文件类型
    /// </summary>
    public enum FileType
    {
        /// <summary>
        /// CSV 文件
        /// </summary>
        CSV,
        
        /// <summary>
        /// Excel 文件（.xls/.xlsx）
        /// </summary>
        Excel
    }

    /// <summary>
    /// 数据类型
    /// </summary>
    public enum DataType
    {
        /// <summary>
        /// 字符串类型
        /// </summary>
        String,
        
        /// <summary>
        /// 整数类型
        /// </summary>
        Integer,
        
        /// <summary>
        /// 十进制数字类型
        /// </summary>
        Decimal,
        
        /// <summary>
        /// 日期时间类型
        /// </summary>
        DateTime,
        
        /// <summary>
        /// 布尔类型
        /// </summary>
        Boolean
    }

    /// <summary>
    /// 监控临时文件设置
    /// </summary>
    public class WatchFileSettings
    {
        /// <summary>
        /// 临时文件目录名
        /// </summary>
        public string WatchFileDirectory { get; set; } = ".watch";

        /// <summary>
        /// 临时文件扩展名
        /// </summary>
        public string WatchFileExtension { get; set; } = ".watchfile";

        /// <summary>
        /// 最大并发处理文件数
        /// </summary>
        public int MaxConcurrentFiles { get; set; } = 16;

        /// <summary>
        /// 是否在临时文件丢失时抛出异常
        /// </summary>
        public bool ThrowOnMissingWatchFile { get; set; } = false;

        /// <summary>
        /// 是否启用差异日志记录
        /// </summary>
        public bool EnableDifferenceLogging { get; set; } = true;

        /// <summary>
        /// 差异日志文件路径
        /// </summary>
        public string DifferenceLogPath { get; set; } = "logs/differences.log";
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 调试级别，显示详细的调试信息
        /// </summary>
        Debug,
        
        /// <summary>
        /// 信息级别，显示一般信息
        /// </summary>
        Info,
        
        /// <summary>
        /// 警告级别，显示警告信息
        /// </summary>
        Warning,
        
        /// <summary>
        /// 错误级别，仅显示错误信息
        /// </summary>
        Error
    }

    /// <summary>
    /// 离线变化检测设置
    /// </summary>
    public class OfflineChangeDetectionSettings
    {
        /// <summary>
        /// 是否启用离线变化检测
        /// </summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// 是否为重启后检测到的新文件触发创建事件
        /// </summary>
        public bool TriggerEventsForNewFiles { get; set; } = false;
        
        /// <summary>
        /// 是否为重启后消失的文件触发删除事件
        /// </summary>
        public bool TriggerEventsForDeletedFiles { get; set; } = true;
        
        /// <summary>
        /// 文件对比方法
        /// </summary>
        public FileComparisonMethod ComparisonMethod { get; set; } = FileComparisonMethod.TimestampAndSize;
        
        /// <summary>
        /// 时间戳对比的容差（秒），用于避免精度问题
        /// </summary>
        public int TimestampToleranceSeconds { get; set; } = 2;
    }

    /// <summary>
    /// 文件对比方法
    /// </summary>
    public enum FileComparisonMethod
    {
        /// <summary>
        /// 仅对比时间戳
        /// </summary>
        Timestamp,
        
        /// <summary>
        /// 对比时间戳和文件大小
        /// </summary>
        TimestampAndSize,
        
        /// <summary>
        /// 对比内容哈希（更准确但耗时）
        /// </summary>
        ContentHash
    }
}
