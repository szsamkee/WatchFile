using System;
using System.Collections.Generic;

namespace WatchFile.Core.Configuration.Models
{
    /// <summary>
    /// 文件监控配置根模型
    /// </summary>
    public class WatchFileConfiguration
    {
        public string Version { get; set; } = "1.0";
        public GlobalSettings GlobalSettings { get; set; } = new GlobalSettings();
        public List<WatchItem> WatchItems { get; set; } = new List<WatchItem>();
    }

    /// <summary>
    /// 全局设置
    /// </summary>
    public class GlobalSettings
    {
        public bool EnableLogging { get; set; } = true;
        public string LogLevel { get; set; } = "Info";
        public int BufferTimeMs { get; set; } = 500;
        public int MaxRetries { get; set; } = 3;
        public string LogFilePath { get; set; } = "logs/watchfile.log";
    }

    /// <summary>
    /// 监控项配置
    /// </summary>
    public class WatchItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        public string Path { get; set; } = string.Empty;
        public WatchType Type { get; set; } = WatchType.Directory;
        public bool Recursive { get; set; } = true;
        public List<string> FileFilters { get; set; } = new List<string>();
        public List<string> ExcludePatterns { get; set; } = new List<string>();
        public List<WatchEvent> WatchEvents { get; set; } = new List<WatchEvent>();
        public FileSettings FileSettings { get; set; } = new FileSettings();
        public WatchFileSettings WatchFileSettings { get; set; } = new WatchFileSettings();
    }

    /// <summary>
    /// 文件设置
    /// </summary>
    public class FileSettings
    {
        public FileType FileType { get; set; } = FileType.CSV;
        public bool HasHeader { get; set; } = true;
        public string Delimiter { get; set; } = ",";
        public string Encoding { get; set; } = "UTF-8";
        public string SheetName { get; set; } = "Sheet1";
        public int StartRow { get; set; } = 1;
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
        Directory,
        File
    }

    /// <summary>
    /// 监控事件类型
    /// </summary>
    public enum WatchEvent
    {
        Created,
        Modified,
        Deleted,
        Renamed
    }

    /// <summary>
    /// 文件类型
    /// </summary>
    public enum FileType
    {
        CSV,
        Excel
    }

    /// <summary>
    /// 数据类型
    /// </summary>
    public enum DataType
    {
        String,
        Integer,
        Decimal,
        DateTime,
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
        Debug,
        Info,
        Warning,
        Error
    }
}
