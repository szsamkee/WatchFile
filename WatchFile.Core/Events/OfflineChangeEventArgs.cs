using System;
using System.Collections.Generic;

namespace WatchFile.Core.Events
{
    /// <summary>
    /// 离线变化检测信息
    /// </summary>
    public class OfflineChangeInfo
    {
        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// 离线变化类型
        /// </summary>
        public OfflineChangeType ChangeType { get; set; }

        /// <summary>
        /// 检测时间
        /// </summary>
        public DateTime DetectedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 原文件最后修改时间
        /// </summary>
        public DateTime? OriginalFileLastWriteTime { get; set; }

        /// <summary>
        /// WatchFile最后修改时间
        /// </summary>
        public DateTime? WatchFileLastWriteTime { get; set; }

        /// <summary>
        /// 原文件大小
        /// </summary>
        public long? OriginalFileSize { get; set; }

        /// <summary>
        /// WatchFile大小
        /// </summary>
        public long? WatchFileSize { get; set; }

        /// <summary>
        /// 变化描述
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// 离线变化类型
    /// </summary>
    public enum OfflineChangeType
    {
        /// <summary>
        /// 新创建的文件（之前不存在对应的watchfile）
        /// </summary>
        Created,

        /// <summary>
        /// 修改的文件（原文件与watchfile不一致）
        /// </summary>
        Modified,

        /// <summary>
        /// 删除的文件（watchfile存在但原文件不存在）
        /// </summary>
        Deleted,

        /// <summary>
        /// 重新出现的文件（之前被DeleteAfterProcessing删除，现在又出现）
        /// </summary>
        Recreated
    }

    /// <summary>
    /// 离线变化检测结果事件参数
    /// </summary>
    public class OfflineChangesDetectedEventArgs : EventArgs
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
        /// 检测到的变化列表
        /// </summary>
        public List<OfflineChangeInfo> Changes { get; set; } = new List<OfflineChangeInfo>();

        /// <summary>
        /// 检测开始时间
        /// </summary>
        public DateTime DetectionStartTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 检测完成时间
        /// </summary>
        public DateTime DetectionEndTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 检测过程中的异常（如果有）
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// 是否检测成功
        /// </summary>
        public bool IsSuccess => Exception == null;

        /// <summary>
        /// 变化总数
        /// </summary>
        public int TotalChanges => Changes.Count;

        /// <summary>
        /// 按类型分组的变化统计
        /// </summary>
        public Dictionary<OfflineChangeType, int> ChangeStatistics
        {
            get
            {
                var stats = new Dictionary<OfflineChangeType, int>();
                foreach (var change in Changes)
                {
                    if (stats.ContainsKey(change.ChangeType))
                        stats[change.ChangeType]++;
                    else
                        stats[change.ChangeType] = 1;
                }
                return stats;
            }
        }

        /// <summary>
        /// 获取统计摘要
        /// </summary>
        public string GetSummary()
        {
            if (TotalChanges == 0)
                return "未检测到变化";

            var parts = new List<string>();
            var stats = ChangeStatistics;

            if (stats.ContainsKey(OfflineChangeType.Created))
                parts.Add($"新增{stats[OfflineChangeType.Created]}个");
            if (stats.ContainsKey(OfflineChangeType.Modified))
                parts.Add($"修改{stats[OfflineChangeType.Modified]}个");
            if (stats.ContainsKey(OfflineChangeType.Deleted))
                parts.Add($"删除{stats[OfflineChangeType.Deleted]}个");
            if (stats.ContainsKey(OfflineChangeType.Recreated))
                parts.Add($"重建{stats[OfflineChangeType.Recreated]}个");

            return string.Join("，", parts);
        }
    }
}
