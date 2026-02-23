using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Renci.SshNet;
using UnboundDashboard.Models;

namespace UnboundDashboard.Services
{
    /// <summary>
    /// SSH bağlantısı ve veri toplama servisi - Python SSH sınıfının C# karşılığı
    /// </summary>
    public class SshService : IDisposable
    {
        private SshClient? _client;
        private readonly string _hostname;
        private readonly int _port;
        private readonly string _username;
        private readonly string? _password;
        private readonly string? _privateKeyPath;
        private int _reconnectAttempts;
        private const int MaxReconnectAttempts = 3;
        private readonly LoggingService _logger = new LoggingService();

        /// <summary>Son hata mesajı (UI'da gösterilmek üzere)</summary>
        public string? LastError { get; private set; }

        public bool IsConnected => _client?.IsConnected ?? false;

        public SshService(string hostname, int port, string username, string? password = null, string? privateKeyPath = null)
        {
            _hostname = hostname;
            _port = port;
            _username = username;
            _password = password;
            _privateKeyPath = privateKeyPath;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                // Dispose existing connection
                _client?.Dispose();
                _client = null;

                // Create authentication method
                AuthenticationMethod auth;
                if (!string.IsNullOrEmpty(_privateKeyPath))
                {
                    auth = new PrivateKeyAuthenticationMethod(_username, new PrivateKeyFile(_privateKeyPath));
                }
                else if (!string.IsNullOrEmpty(_password))
                {
                    auth = new PasswordAuthenticationMethod(_username, _password);
                }
                else
                {
                    // Try default SSH keys
                    var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var keyPaths = new[]
                    {
                        System.IO.Path.Combine(homeDir, ".ssh", "id_rsa"),
                        System.IO.Path.Combine(homeDir, ".ssh", "id_ed25519")
                    };

                    var keyPath = keyPaths.FirstOrDefault(System.IO.File.Exists);
                    if (keyPath == null)
                        throw new Exception("SSH anahtarı bulunamadı. ~/.ssh/id_rsa veya ~/.ssh/id_ed25519 dosyasını kontrol edin.");

                    auth = new PrivateKeyAuthenticationMethod(_username, new PrivateKeyFile(keyPath));
                }

                var connectionInfo = new ConnectionInfo(_hostname, _port, _username, auth)
                {
                    Timeout = TimeSpan.FromSeconds(10)
                };
                _client = new SshClient(connectionInfo);

                await Task.Run(() => _client.Connect());
                _reconnectAttempts = 0;
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = $"SSH bağlantı hatası: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Bağlantı kopmuşsa otomatik yeniden bağlanmayı dener
        /// </summary>
        private async Task<bool> EnsureConnectedAsync()
        {
            if (IsConnected) return true;

            if (_reconnectAttempts >= MaxReconnectAttempts)
            {
                LastError = $"SSH bağlantısı {MaxReconnectAttempts} deneme sonrası kurulamadı. Sunucu erişilebilir mi kontrol edin.";
                return false;
            }

            _reconnectAttempts++;
            LastError = $"Yeniden bağlanılıyor... (Deneme {_reconnectAttempts}/{MaxReconnectAttempts})";
            return await ConnectAsync();
        }

        public async Task<string?> ExecuteCommandAsync(string command)
        {
            // Double-check connection status and null safety
            if (!await EnsureConnectedAsync() || _client == null || !_client.IsConnected)
                return null;

            try
            {
                return await Task.Run(() =>
                {
                    // Thread-safety: check again inside Task.Run
                    if (_client == null || !_client.IsConnected)
                        return null;

                    using var cmd = _client.CreateCommand(command);
                    cmd.CommandTimeout = TimeSpan.FromSeconds(10);
                    var result = cmd.Execute();
                    return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
                });
            }
            catch (Exception ex)
            {
                LastError = $"Komut hatası: {ex.Message}";
                return null;
            }
        }

        public async Task<DnsMetrics> CollectMetricsAsync()
        {
            var metrics = new DnsMetrics();

            // Bağlantı kontrolü
            if (!await EnsureConnectedAsync())
            {
                metrics.Status = "disconnected";
                return metrics;
            }

            // Python V.al() metodunun C# karşılığı - tek SSH çağrısıyla tüm veriyi topla
            var command = @"
echo '==S==';docker inspect -f '{{.State.Status}}' unbound 2>/dev/null;
echo '==V==';docker exec unbound unbound-control stats_noreset 2>/dev/null;
echo '==U==';uptime -p 2>/dev/null;
echo '==M==';free -m|awk 'NR==2{print $2,$3}';
echo '==C==';grep 'cpu ' /proc/stat|awk '{u=$2+$4;t=$2+$4+$5;printf ""%.1f"",u/t*100}';
echo '';echo '==D==';df -h /|awk 'NR==2{print $3""/""$2"" (""$5"")""}';
echo '==O==';grep -oP '(?<=^PRETTY_NAME="").*(?="")' /etc/os-release 2>/dev/null || cat /etc/os-release | grep PRETTY_NAME | cut -d= -f2 | tr -d '""""';
echo '==R==';docker exec unbound unbound -V 2>/dev/null | head -n 1 | awk '{print $2}';
echo '==X=='
";

            var result = await ExecuteCommandAsync(command);
            if (string.IsNullOrEmpty(result)) return metrics;

            // Parse sections
            var sections = ParseSections(result);

            // Status
            if (sections.TryGetValue("S", out var status))
            {
                metrics.Status = status.FirstOrDefault() == "running" ? "active" : "stopped";
            }

            // Uptime
            if (sections.TryGetValue("U", out var uptime))
            {
                var uptimeRaw = uptime.FirstOrDefault() ?? "—";
                metrics.Uptime = FormatUptime(uptimeRaw);
            }

            // Memory
            if (sections.TryGetValue("M", out var memory))
            {
                var parts = memory.FirstOrDefault()?.Split(' ');
                if (parts?.Length >= 2)
                {
                    if (int.TryParse(parts[0], out var total) && int.TryParse(parts[1], out var used))
                    {
                        metrics.RamTotal = total;
                        metrics.RamUsed = used;
                        metrics.RamPercent = total > 0 ? (used * 100.0 / total) : 0;
                    }
                }
            }

            // CPU
            if (sections.TryGetValue("C", out var cpu))
            {
                if (double.TryParse(cpu.FirstOrDefault(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var cpuVal))
                {
                    metrics.CpuUsage = cpuVal;
                }
            }

            // Disk
            if (sections.TryGetValue("D", out var disk))
            {
                metrics.DiskUsage = disk.FirstOrDefault() ?? "—";
            }

            // Server OS
            if (sections.TryGetValue("O", out var os))
            {
                metrics.ServerOS = os.FirstOrDefault()?.Trim() ?? "Bilinmeyen OS";
            }

            // Unbound Version
            if (sections.TryGetValue("R", out var version))
            {
                metrics.UnboundVersion = version.FirstOrDefault()?.Trim() ?? "Bilinmeyen Versiyon";
            }

            // Parse Unbound stats
            if (sections.TryGetValue("V", out var unboundStats))
            {
                // Capture raw lines for the hacker terminal feed
                foreach (var line in unboundStats)
                {
                    if (line.Contains('='))
                        metrics.RawTerminalLines.Add(line.Trim());
                }

                var stats = ParseUnboundStats(unboundStats);

                metrics.CacheHits = (long)stats.GetValueOrDefault("total.num.cachehits", 0);
                metrics.CacheMisses = (long)stats.GetValueOrDefault("total.num.cachemiss", 0);
                metrics.TotalQueries = metrics.CacheHits + metrics.CacheMisses;
                metrics.CacheHitPercent = metrics.TotalQueries > 0
                    ? (metrics.CacheHits * 100.0 / metrics.TotalQueries) : 0;

                var avgTime = stats.GetValueOrDefault("total.recursion.time.avg", 0);
                metrics.ResponseTimeMs = avgTime * 1000;

                metrics.CacheSize = (int)stats.GetValueOrDefault("msg.cache.count", 0) +
                                   (int)stats.GetValueOrDefault("rrset.cache.count", 0);

                metrics.BogusBlocked = (int)stats.GetValueOrDefault("num.answer.bogus", 0);
                metrics.DnssecActive = metrics.BogusBlocked == 0;

                metrics.SuccessfulQueries = (long)stats.GetValueOrDefault("num.answer.rcode.NOERROR", 0);
                metrics.NxDomain = (long)stats.GetValueOrDefault("num.answer.rcode.NXDOMAIN", 0);
                metrics.ServerFail = (long)stats.GetValueOrDefault("num.answer.rcode.SERVFAIL", 0);

                // Query types - Türkçe mapping
                var queryTypeMapping = new Dictionary<string, string>
                {
                    ["A"] = "Web Sitesi",
                    ["AAAA"] = "Web (IPv6)",
                    ["HTTPS"] = "Güvenli Bağlantı",
                    ["MX"] = "E-posta Sunucu",
                    ["TXT"] = "Doğrulama Kaydı",
                    ["PTR"] = "Ters Sorgu",
                    ["SRV"] = "Servis Kaydı"
                };

                foreach (var (key, name) in queryTypeMapping)
                {
                    var count = (long)stats.GetValueOrDefault($"num.query.type.{key}", 0);
                    if (count > 0)
                    {
                        metrics.QueryTypes[name] = count;
                    }
                }
            }

            LastError = null;
            return metrics;
        }

        private Dictionary<string, List<string>> ParseSections(string output)
        {
            var sections = new Dictionary<string, List<string>>();
            string? currentKey = null;

            foreach (var line in output.AsSpan().EnumerateLines())
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("==") && trimmed.EndsWith("=="))
                {
                    currentKey = trimmed.Trim('=').ToString();
                    sections[currentKey] = new List<string>();
                }
                else if (currentKey != null && !trimmed.IsEmpty)
                {
                    sections[currentKey].Add(trimmed.ToString());
                }
            }

            return sections;
        }

        private Dictionary<string, double> ParseUnboundStats(List<string> lines)
        {
            var stats = new Dictionary<string, double>();

            foreach (var line in lines)
            {
                if (!line.Contains('=')) continue;

                var parts = line.Split('=', 2);
                if (parts.Length == 2 && double.TryParse(parts[1].Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value))
                {
                    stats[parts[0].Trim()] = value;
                }
            }

            return stats;
        }

        private string FormatUptime(string rawUptime)
        {
            // Python versiyonundaki format mantığı
            var formatted = rawUptime
                .Replace("up ", "")
                .Replace("days", "g")
                .Replace("day", "g")
                .Replace("hours", "s")
                .Replace("hour", "s")
                .Replace("minutes", "d")
                .Replace("minute", "d")
                .Replace(",", "")
                .Replace("  ", " ")
                .Trim();

            // Max 15 karakter
            if (formatted.Length > 15)
                return formatted.Substring(0, 15);

            return formatted;
        }

        public void Dispose()
        {
            try
            {
                _client?.Disconnect();
            }
            catch (Exception ex)
            {
                // Log disposal errors but don't throw
                _logger.Warning($"Error during SSH disconnect: {ex.Message}");
            }
            finally
            {
                _client?.Dispose();
                _client = null;
            }
        }
    }
}
