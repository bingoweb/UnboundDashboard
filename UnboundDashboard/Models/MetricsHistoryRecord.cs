using System;

namespace UnboundDashboard.Models
{
    /// <summary>
    /// Geçmiş veri kaydı modeli
    /// </summary>
    public class MetricsHistoryRecord
    {
        public DateTime Timestamp { get; set; }
        public double QPS { get; set; }
        public double CacheHitPercent { get; set; }
        public double CpuUsage { get; set; }
        public double RamPercent { get; set; }
    }
}
