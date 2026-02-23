using System;
using System.Collections.Generic;

namespace UnboundDashboard.Models
{
    /// <summary>
    /// DNS metrikleri veri modeli - Python V sınıfının C# karşılığı
    /// </summary>
    public class DnsMetrics
    {
        // Status
        public string Status { get; set; } = "?";
        public string Uptime { get; set; } = "—";
        public int UptimeSeconds { get; set; }
        public string ServerOS { get; set; } = "—";
        public string UnboundVersion { get; set; } = "—";

        // System Resources
        public double CpuUsage { get; set; }
        public int RamUsed { get; set; }
        public int RamTotal { get; set; }
        public double RamPercent { get; set; }
        public string DiskUsage { get; set; } = "—";

        // Query Metrics
        public long TotalQueries { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public double CacheHitPercent { get; set; }
        public double ResponseTimeMs { get; set; }
        public int CacheSize { get; set; }

        // Security
        public bool DnssecActive { get; set; } = true;
        public int BogusBlocked { get; set; }
        public long SuccessfulQueries { get; set; }
        public long NxDomain { get; set; }
        public long ServerFail { get; set; }

        // Query Types
        public Dictionary<string, long> QueryTypes { get; set; } = new();

        // Raw Terminal Feed for Matrix UI
        public List<string> RawTerminalLines { get; set; } = new();

        // Historical Data for Charts
        public System.Collections.ObjectModel.ObservableCollection<double> QpsHistory { get; set; } = new();
        public System.Collections.ObjectModel.ObservableCollection<double> CacheHitHistory { get; set; } = new();

        // Calculated Properties
        public double CurrentQPS => QpsHistory.Count > 0 ? QpsHistory[^1] : 0;

        public string GetCacheStatusMessage()
        {
            if (CacheHitPercent < 30)
                return "ℹ Bilgi: Sistem yeni başladı, önbellek henüz dolmadı. Zaman içinde daha fazla site kaydedilecek ve hız artacak.";
            else if (CacheHitPercent < 70)
                return "✓ İyi: Önbellek dolmaya devam ediyor. Sık ziyaret ettiğiniz siteler artık hafızadan yanıtlanıyor.";
            else
                return "✓ Mükemmel: Önbellek tam kapasitede çalışıyor! Siteler artık çok daha hızlı açılıyor, bekleme süresi minimum seviyede.";
        }

        public string GetResponseTimeLabel()
        {
            if (ResponseTimeMs < 30) return "ÇOK HIZLI";
            else if (ResponseTimeMs < 100) return "HIZLI";
            else return "YAVAŞ";
        }
    }

    /// <summary>
    /// Sorgu tipi bilgisi
    /// </summary>
    public class QueryTypeInfo
    {
        public string Name { get; set; } = string.Empty;
        public long Count { get; set; }
        public double Percent { get; set; }
    }
}
