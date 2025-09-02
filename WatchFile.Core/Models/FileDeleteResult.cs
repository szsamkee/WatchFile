using System;
using System.Collections.Generic;
using System.Linq;

namespace WatchFile.Core.Models
{
    /// <summary>
    /// 文件删除结果
    /// </summary>
    public class FileDeleteResult
    {
        /// <summary>
        /// 请求删除的文件名
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 请求删除的文件路径（兼容性属性）
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 指定的监控项ID
        /// </summary>
        public string? RequestedWatchItemId { get; set; }

        /// <summary>
        /// 是否删除成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 是否删除成功（新版本属性）
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 详细消息列表
        /// </summary>
        public List<string> Messages { get; set; } = new List<string>();

        /// <summary>
        /// 已删除的主文件列表
        /// </summary>
        public List<string> DeletedFiles { get; set; } = new List<string>();

        /// <summary>
        /// 已删除的缓存文件列表
        /// </summary>
        public List<string> DeletedCacheFiles { get; set; } = new List<string>();

        /// <summary>
        /// 错误列表
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// 原文件是否删除成功
        /// </summary>
        public bool OriginalFileDeleted { get; set; }

        /// <summary>
        /// 缓存文件是否删除成功
        /// </summary>
        public bool CacheFileDeleted { get; set; }

        /// <summary>
        /// 缓存文件路径
        /// </summary>
        public string? CacheFilePath { get; set; }

        /// <summary>
        /// 对应的监控项ID
        /// </summary>
        public string? WatchItemId { get; set; }

        /// <summary>
        /// 对应的监控项名称
        /// </summary>
        public string? WatchItemName { get; set; }

        /// <summary>
        /// 成功消息
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 警告消息列表
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// 请求时间（兼容性属性）
        /// </summary>
        public DateTime RequestTime { get; set; }

        /// <summary>
        /// 完成时间（兼容性属性）
        /// </summary>
        public DateTime CompletionTime { get; set; }

        /// <summary>
        /// 处理耗时
        /// </summary>
        public TimeSpan Duration => EndTime - StartTime;

        /// <summary>
        /// 获取删除结果摘要
        /// </summary>
        public string GetSummary()
        {
            if (Success)
            {
                var parts = new List<string>();
                if (OriginalFileDeleted) parts.Add("原文件");
                if (CacheFileDeleted) parts.Add("缓存文件");
                
                return parts.Count > 0 
                    ? $"成功删除 {string.Join(" 和 ", parts)}"
                    : "删除操作完成（无文件需要删除）";
            }
            else
            {
                return $"删除失败: {ErrorMessage}";
            }
        }
    }

    /// <summary>
    /// 批量文件删除结果
    /// </summary>
    public class BatchFileDeleteResult
    {
        /// <summary>
        /// 各个文件的删除结果
        /// </summary>
        public FileDeleteResult[] Results { get; set; } = Array.Empty<FileDeleteResult>();

        /// <summary>
        /// 总文件数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 成功删除的文件数
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// 删除失败的文件数
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// 请求时间
        /// </summary>
        public DateTime RequestTime { get; set; }

        /// <summary>
        /// 完成时间
        /// </summary>
        public DateTime CompletionTime { get; set; }

        /// <summary>
        /// 总耗时
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 获取批量删除结果摘要
        /// </summary>
        public string GetSummary()
        {
            if (TotalCount == 0)
                return "没有文件需要删除";

            if (FailureCount == 0)
                return $"成功删除所有 {SuccessCount} 个文件";
            
            if (SuccessCount == 0)
                return $"所有 {FailureCount} 个文件删除失败";
            
            return $"删除完成: 成功 {SuccessCount} 个，失败 {FailureCount} 个，总计 {TotalCount} 个";
        }

        /// <summary>
        /// 获取失败的文件列表
        /// </summary>
        public IEnumerable<FileDeleteResult> GetFailedResults()
        {
            return Results.Where(r => !r.Success);
        }

        /// <summary>
        /// 获取成功的文件列表
        /// </summary>
        public IEnumerable<FileDeleteResult> GetSuccessfulResults()
        {
            return Results.Where(r => r.Success);
        }
    }

    /// <summary>
    /// 缓存文件删除结果
    /// </summary>
    internal class CacheFileDeleteResult
    {
        public bool Success { get; set; }
        public string? CacheFilePath { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
