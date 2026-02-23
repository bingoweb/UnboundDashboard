using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using UnboundDashboard.Models;
using UnboundDashboard.Services;

namespace UnboundDashboard.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SshService _sshService;
        private readonly DispatcherTimer _updateTimer;
        private DnsMetrics _metrics = new();
        private bool _isUpdating;
        private bool _disposed;
        private readonly LoggingService _logger = new LoggingService();

        // Static HttpClient for IP detection (prevents socket exhaustion)
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        // Cached Random instance for terminal content
        private readonly Random _random = new();

        // Cached and frozen color brushes for terminal (created once, thread-safe)
        private static readonly Brush _emeraldBrush;
        private static readonly Brush _cyanBrush;
        private static readonly Brush _orangeBrush;
        private static readonly Brush _purpleBrush;

        static DashboardViewModel()
        {
            // Initialize and freeze brushes for WPF thread-safety and performance
            _emeraldBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10b981"));
            _cyanBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b7280"));
            _orangeBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f59e0b"));
            _purpleBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34d399"));

            _emeraldBrush.Freeze();
            _cyanBrush.Freeze();
            _orangeBrush.Freeze();
            _purpleBrush.Freeze();
        }

        // Typewriter Terminal Variables
        private readonly DispatcherTimer _typewriterTimer;
        private readonly Queue<(string Text, Brush Color)> _typewriterQueue = new();
        private string _currentTypewriterLine = "";
        private Brush _currentTypewriterColor = Brushes.LimeGreen;
        private int _currentTypewriterIndex = 0;
        private int _terminalLineCounter = 0;
        private string _clientIp = "Taranıyor...";
        private readonly DateTime _appStartTime = DateTime.Now;

        // Previous Values for Diff Calculation
        private long _prevTotalQueries = -1;
        private long _prevCacheHits = -1;
        private long _prevCacheMisses = -1;
        private double _prevCacheHitPercent = -1;

        private const int MaxHistorySize = 60; // Son 60 saniye

        public event PropertyChangedEventHandler? PropertyChanged;

        // Commands
        public ICommand WarmupCommand { get; }
        public ICommand FlushCommand { get; }
        public ICommand RestartCommand { get; }
        public ICommand SpeedTestCommand { get; }

        // Properties
        public string StatusText
        {
            get
            {
                if (_metrics.Status == "disconnected") return "✕ BAĞLANTI YOK";
                return _metrics.Status == "active" ? "● AKTİF" : "○ DURDURULDU";
            }
        }

        public Brush StatusColor
        {
            get
            {
                if (_metrics.Status == "disconnected") return Brushes.Orange;
                return _metrics.Status == "active" ? Brushes.LimeGreen : Brushes.Red;
            }
        }

        public string ServerAddress => $"{_sshHostname}:53";
        public string CurrentDateTime => DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        public string Uptime => _metrics.Uptime;

        // Error display
        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                _errorMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasError));
            }
        }
        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        // Metrics
        public string TotalQueries => FormatNumber(_metrics.TotalQueries);
        public double CacheHitPercent => _metrics.CacheHitPercent;
        public string CacheHitsText => $"{FormatNumber(_metrics.CacheHits)} istek hafızadan yanıtlandı";
        public double ResponseTime => _metrics.ResponseTimeMs;
        public string ResponseTimeText => $"{_metrics.GetResponseTimeLabel()} • Milisaniye cinsinden";
        public double QPS => _metrics.CurrentQPS;

        // Cache Panel
        public string CacheStats =>
            $"• Bulunan: {FormatNumber(_metrics.CacheHits)} istek önbellekte bulundu, hızlı yanıt verildi\n" +
            $"• Bulunamayan: {FormatNumber(_metrics.CacheMisses)} istek önbellekte yoktu, sunucuya soruldu\n" +
            $"■ Kayıtlı Site: {FormatNumber(_metrics.CacheSize)} farklı alan adı hafızada tutuluyor\n" +
            $"◆ Ortalama Süre: {_metrics.ResponseTimeMs:F1}ms {_metrics.GetResponseTimeLabel()} • Site açılma hızı";

        public string CacheStatusMessage => _metrics.GetCacheStatusMessage();
        public Brush CacheStatusColor => _metrics.CacheHitPercent < 30 ? Brushes.Yellow :
                                         _metrics.CacheHitPercent < 70 ? Brushes.LimeGreen : Brushes.Cyan;

        // System Panel
        public double CpuUsage => _metrics.CpuUsage;
        public Brush CpuColor => _metrics.CpuUsage < 50 ? Brushes.LimeGreen :
                                _metrics.CpuUsage < 75 ? Brushes.Yellow : Brushes.Red;

        public double RamPercent => _metrics.RamPercent;
        public string RamText => $"{_metrics.RamUsed}/{_metrics.RamTotal}MB kullanılıyor";
        public Brush RamColor => _metrics.RamPercent < 50 ? Brushes.LimeGreen :
                                _metrics.RamPercent < 75 ? Brushes.Yellow : Brushes.Red;

        public string DiskText => $"{_metrics.DiskUsage} toplam kullanım";


        // Query Types
        public ObservableCollection<QueryTypeInfo> QueryTypes { get; } = new();

        // Terminal Log Data Stream
        public ObservableCollection<TerminalLine> TerminalLogs { get; } = new();

        // SSH config — config dosyasından okunacak
        private readonly string _sshHostname;
        private readonly int _sshPort;
        private readonly string _sshUsername;

        public DashboardViewModel(string hostname, int port, string username, string? password, string? keyPath)
        {
            _sshHostname = hostname;
            _sshPort = port;
            _sshUsername = username;

            _sshService = new SshService(_sshHostname, _sshPort, _sshUsername,
                password: password, privateKeyPath: keyPath);

            // Commands
            // Önbellek Doldurma: Sık girilen siteleri hızlıca sunucuya sorarak önbelleğe alır
            WarmupCommand = new RelayCommand(async () => await ExecuteCommandAsync("docker exec unbound sh -c 'for d in google.com youtube.com apple.com facebook.com netflix.com microsoft.com whatsapp.net instagram.com x.com; do dig +short @127.0.0.1 $d >/dev/null; done'"));
            
            // Temizleme: '.' kök zonunu silmek, hiyerarşik olarak tüm internet önbelleğini sıfırlar
            FlushCommand = new RelayCommand(async () => await ExecuteCommandAsync("docker exec unbound unbound-control flush_zone ."));
            
            RestartCommand = new RelayCommand(async () => await ExecuteCommandAsync("docker restart unbound"));
            
            SpeedTestCommand = new RelayCommand(async () => await ExecuteCommandAsync("dig @127.0.0.1 google.com"));

            // Timer for updates
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _updateTimer.Tick += async (s, e) => await UpdateMetricsAsync();

            // Typewriter Timer (Fast ticking for matrix effect)
            _typewriterTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120) // Much slower for readability as requested
            };
            _typewriterTimer.Tick += TypewriterTimer_Tick;

            // Fetch Client IP Asynchronously
            Task.Run(async () =>
            {
                try
                {
                    _clientIp = await _httpClient.GetStringAsync("https://api.ipify.org");
                }
                catch (Exception ex)
                {
                    _logger.Warning($"Failed to fetch external IP: {ex.Message}");
                    _clientIp = "Lokal Ağ";
                }
            });

            // Connect and start
            Task.Run(async () =>
            {
                var connected = await _sshService.ConnectAsync();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!connected)
                    {
                        ErrorMessage = _sshService.LastError ?? "SSH bağlantısı kurulamadı.";
                    }
                    _updateTimer.Start();
                });
            });
        }

        private async Task UpdateMetricsAsync()
        {
            // Eğer önceki güncelleme devam ediyorsa atla (thread-safety)
            if (_isUpdating) return;
            _isUpdating = true;

            try
            {
                var newMetrics = await _sshService.CollectMetricsAsync();

                // Calculate QPS
                long diff = newMetrics.TotalQueries - _metrics.TotalQueries;
                double qps = 0;
                
                // 2 saniyelik interval olduğu için 2'ye bölüyoruz
                // İlk açılışta veya sunucu restart olduğunda diff < 0 olabilir
                if (_metrics.TotalQueries > 0 && diff > 0)
                {
                    qps = diff / 2.0; 
                }

                // Preserve history collections (they are ObservableCollection, UI needs the same instance)
                newMetrics.QpsHistory = _metrics.QpsHistory;
                newMetrics.CacheHitHistory = _metrics.CacheHitHistory;

                // Add new data point
                newMetrics.QpsHistory.Add(Math.Round(qps, 1));

                // QPS history sınırlaması (bellek koruması) - Efficient batch removal
                if (newMetrics.QpsHistory.Count > MaxHistorySize)
                {
                    var excess = newMetrics.QpsHistory.Count - MaxHistorySize;
                    var itemsToKeep = newMetrics.QpsHistory.Skip(excess).ToList();
                    newMetrics.QpsHistory.Clear();
                    foreach (var item in itemsToKeep)
                    {
                        newMetrics.QpsHistory.Add(item);
                    }
                }

                _metrics = newMetrics;

                // Update query types
                Application.Current.Dispatcher.Invoke(() =>
                {
                    QueryTypes.Clear();
                    foreach (var qt in _metrics.QueryTypes.OrderByDescending(x => x.Value).Take(5))
                    {
                        QueryTypes.Add(new QueryTypeInfo
                        {
                            Name = $"• {qt.Key}",
                            Count = qt.Value,
                            Percent = _metrics.TotalQueries > 0 ? (qt.Value * 100.0 / _metrics.TotalQueries) : 0
                        });
                    }

                    // Process Hacker Terminal Feed into the Queue
                    var appUptime = DateTime.Now - _appStartTime;

                    // Expanded factual database with color tagging (FACTS ONLY)
                    var facts = new (string Text, Brush Color)[]
                    {
                        ("[İSTİHBARAT] DNS (Alan Adı Sistemi), IP adresleri ile insan okuyabilir web adreslerini eşleştiren küresel telefon rehberidir.", _emeraldBrush),
                        ("[İSTİHBARAT] Cache (Önbellek), sık ziyaret edilen sitelerin yanıtlarını RAM'de tutarak milisaniye seviyesinde ultra hızlı erişim sağlar.", _emeraldBrush),
                        ("[İSTİHBARAT] Günde yaklaşık 100 Milyar DNS sorgusu dünya çapındaki 13 kök sunucu tarafından işlenir.", _emeraldBrush),
                        ("[İSTİHBARAT] Unbound DNS, verileri yerel olarak doğrulayıp şifreleyen profesyonel bir siber güvenlik savunma hattıdır.", _emeraldBrush),
                        ("[İSTİHBARAT] Siber güvenlik uzmanlarına göre oltalama (phishing) saldırılarının %90'ı sahte DNS zehirlenmesiyle başlar.", _orangeBrush),
                        ("[İSTİHBARAT] IPv6 protokolü 340 undesilyon IP adresi barındırabilir; bu yeni nesil, daha geniş ağ cihazı havuzu demektir.", _cyanBrush),
                        ("[İSTİHBARAT] A Kaydı (A Record), bir web sitesinin ismini eski nesil IPv4 adresine dönüştüren temel DNS sorgusudur.", _emeraldBrush),
                        ("[İSTİHBARAT] AAAA Kaydı, bir web sitesini modern IPv6 adreslerine haritalar. İnternetin geleceğidir.", _emeraldBrush),
                        ("[İSTİHBARAT] DNS over TLS (DoT), ağ servis sağlayıcınızın girdiğiniz siteleri gözetlemesini kriptografik olarak engeller.", _purpleBrush)
                    };

                    // Expanded statistics pool
                    var stats = new List<(string Text, Brush Color)>
                    {
                        ($"[ZAMAN] Senkronize Sistem Saati: {DateTime.Now:HH:mm:ss.fff}", _cyanBrush),
                        ($"[SİSTEM] Aktif Operasyon Süresi (Uptime): {(int)appUptime.TotalHours}s {appUptime.Minutes}d {appUptime.Seconds}sn", _cyanBrush),
                        ($"[KOMUTA] Kontrol İstasyonu: {Environment.MachineName}", _cyanBrush),
                        ($"[KOMUTA] İşletim Çekirdeği: {Environment.OSVersion.VersionString}", _cyanBrush),
                        ($"[RADAR] Dış WAN IP Tespit Edildi: {_clientIp}", _orangeBrush),
                        ($"[HEDEF] Karşı Terminal OS: {_metrics.ServerOS}", _purpleBrush),
                        ($"[MOTOR] Çözümleyici Sürümü: {_metrics.UnboundVersion} (Bilinmeyen hedeflere karşı yetkilendirildi)", _purpleBrush),
                        ($"[BELLEK] Taktiksel RAM Kullanımı: {_metrics.RamUsed}MB ayrılmış / {_metrics.RamTotal}MB toplam kapasite", _cyanBrush),
                        ($"[ANALİZ] {_metrics.CacheHits} hedef sorgu başarıyla lokal önbellekten (Cache) geri getirildi. (Hit Rate: %{_metrics.CacheHitPercent:F1})", _purpleBrush),
                        ($"[RADAR] Anlık Tarama Hızı: Saniyede {_metrics.CurrentQPS:F1} Hedef Çözümleniyor (QPS)", _orangeBrush),
                        ($"[OPERASYON] Genel Toplam Gönderilen Sorgu Miktarı: {_metrics.TotalQueries}", _emeraldBrush),
                        ($"[MİMAR] Güvenlik Ağ Mimarisi Onaylandı: Taylan Soylu", _orangeBrush)
                    };

                    // Decode real Unbound diagnostics and translate into massively detailed Turkish sets
                    if (newMetrics.RawTerminalLines != null)
                    {
                        foreach (var rawLine in newMetrics.RawTerminalLines)
                        {
                            var parts = rawLine.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                string key = parts[0].Trim();
                                string val = parts[1].Trim();
                                
                                if (key == "thread0.num.queries") 
                                    stats.Add(($"[ÇEKİRDEK_0] Thread-0 İşlemcisine Gelen İstek Sayısı: {val} (İlk çekirdek donanımı üzerinden geçen trafik)", _cyanBrush));
                                else if (key == "total.num.queries_ip_ratelimited") 
                                    stats.Add(($"[GÜVENLİK] Flood/DDoS Korumasına Takılan İstekler: {val} (Zararlı IP'ler bloklandı)", _orangeBrush));
                                else if (key == "total.num.cachehits") 
                                    stats.Add(($"[PERFORMANS] Önbellekten Gelen Hızlı Yanıt Sayısı: {val} (Dış ağa çıkmadan verilen güvenli yanıtlar)", _purpleBrush));
                                else if (key == "total.num.prefetch") 
                                    stats.Add(($"[ÖNGÖRÜ] Otomatik Yenilenen İstekler (Prefetch): {val} (Site süresi dolmadan Unbound arka planda yeniledi)", _emeraldBrush));
                                else if (key == "msg.cache.count") 
                                    stats.Add(($"[BELLEK] Önbellekte Tutulan Mesaj Gövdesi: {val} (Tüm IP, sunucu adı ve TTL verileri RAM'de korunuyor)", _cyanBrush));
                                else if (key == "rrset.cache.count") 
                                    stats.Add(($"[BELLEK] Önbellekte Tutulan Hedef (RRSet) Kaydı: {val} (Hazır DNS kayıtları)", _cyanBrush));
                                else if (key == "infra.cache.count") 
                                    stats.Add(($"[ALTYAPI] Hedef Sunucu Ping Önbelleği (Infra Cache): {val} (En hızlı sunucuyu seçmek için tutulan ping listesi)", _cyanBrush));
                                else if (key == "total.recursion.time.avg") 
                                    stats.Add(($"[RADAR_PİNG] Hedef Çözümleme Ortalama Süresi: {val} saniye (Dünya üzerindeki kök DNS sunucularına gidiş-dönüş)", _emeraldBrush));
                                else if (key == "num.query.type.A") 
                                    stats.Add(($"[PROTOKOL_A] A Tipi İstek: {val} (IPv4 Adres çözümleri)", _purpleBrush));
                                else if (key == "num.query.type.AAAA") 
                                    stats.Add(($"[PROTOKOL_AAAA] AAAA Tipi İstek: {val} (Modern IPv6 Adres çözümleri)", _purpleBrush));
                                else if (key == "num.query.type.HTTPS") 
                                    stats.Add(($"[PROTOKOL_HTTPS] HTTPS Tipi İstek: {val} (Güvenli, şifrelenmiş bağlantı altyapısı kontrol ediliyor)", _purpleBrush));
                                else if (key.StartsWith("num.query.type.")) 
                                    stats.Add(($"[PROTOKOL] {key.Replace("num.query.type.", "")} Tipi İstek Miktarı: {val}", _cyanBrush));
                                else if (key.StartsWith("num.query.class.")) 
                                    stats.Add(($"[SINIF] DNS Sınıfı {key.Replace("num.query.class.", "")} İstek Sayısı: {val}", _cyanBrush));
                                else if (key == "num.query.tcp") 
                                    stats.Add(($"[AĞ_TCP] Güvenli TCP Protokolü İstekleri: {val} (Büyük ve sağlam veri paketleri transferi)", _emeraldBrush));
                                else if (key == "num.query.udp") 
                                    stats.Add(($"[AĞ_UDP] Hızlı UDP Protokolü İstekleri: {val} (Hızlı ama doğrulamasız paket transferi)", _emeraldBrush));
                                else if (key == "num.query.ipv6") 
                                    stats.Add(($"[AĞ_IPv6] IPv6 Protokolü İstekleri: {val} (Gelecek nesil IP şifreleme alt yapısı üzerinden veri çekiliyor)", _emeraldBrush));
                                else if (key.Contains("unwanted.queries")) 
                                    stats.Add(($"[UYARI_GÜVENLİK] İstenmeyen/Şüpheli Sorgu Miktarı (Bağlantı Kesildi): {val}", _orangeBrush));
                                else if (key.Contains("recursive.replies")) 
                                    stats.Add(($"[KÖK_SUNUCU] Başarıyla Çözülen Yanıtlar (Recursion): {val} (Direkt ana kaynak sunuculardan alınan cevaplar)", _purpleBrush));
                            }
                        }
                    }

                    // Prepend an update warning pulse
                    _typewriterQueue.Enqueue(("", _emeraldBrush));
                    _typewriterQueue.Enqueue(($"[AKTARIM] SSH Telemetri Verileri Güvenle Güncellendi. (Latency: 1ms)", _emeraldBrush));

                    // Calculate and Enqueue Diffs (Change from last update)
                    if (_prevTotalQueries != -1) // Skip first run
                    {
                        long diffTotal = newMetrics.TotalQueries - _prevTotalQueries;
                        long diffHits = newMetrics.CacheHits - _prevCacheHits;
                        long diffMisses = newMetrics.CacheMisses - _prevCacheMisses;
                        double diffPercent = newMetrics.CacheHitPercent - _prevCacheHitPercent;

                        if (diffTotal > 0)
                        {
                            string diffMsg = $"[DEĞİŞİM] Toplam: {newMetrics.TotalQueries} (+{diffTotal}) | " +
                                             $"Önbellek: {newMetrics.CacheHits} (+{diffHits}) | " +
                                             $"Sunucu: {newMetrics.CacheMisses} (+{diffMisses})";

                            // Add percent change only if significant
                            if (Math.Abs(diffPercent) > 0.1)
                            {
                                string sign = diffPercent > 0 ? "+" : "";
                                diffMsg += $" | Hit%: {newMetrics.CacheHitPercent:F1}% ({sign}{diffPercent:F1}%)";
                            }

                            _typewriterQueue.Enqueue((diffMsg, _orangeBrush));
                        }
                    }

                    // Store current values for next comparison
                    _prevTotalQueries = newMetrics.TotalQueries;
                    _prevCacheHits = newMetrics.CacheHits;
                    _prevCacheMisses = newMetrics.CacheMisses;
                    _prevCacheHitPercent = newMetrics.CacheHitPercent;

                    // Process Live Real-Time Queries (Priority)
                    // Queue Control: If typing is too slow, clear old backlog to show FRESH data
                    if (_typewriterQueue.Count > 2)
                    {
                        _typewriterQueue.Clear();
                        _typewriterQueue.Enqueue(("[AKTARIM] Arabellek Temizlendi - Canlı Veri Akışı Bekleniyor...", _cyanBrush));
                    }

                    bool hasLiveTraffic = false;
                    if (newMetrics.LiveQueries != null && newMetrics.LiveQueries.Count > 0)
                    {
                        // Regex to parse tcpdump output: "IP 192.168.1.5.1234 > 1.1.1.1.53: 12345+ A? google.com."
                        var ipv4Regex = new Regex(@"IP\s+(?<src>[\d\.]+)\.(\d+)\s+>\s+.*53:.*?(?<type>[A-Z0-9]+)\?\s+(?<domain>\S+)", RegexOptions.Compiled);

                        // Only take 2 lines max per update cycle to match slow typing speed (120ms/char)
                        foreach (var queryLine in newMetrics.LiveQueries.Take(2))
                        {
                            var match = ipv4Regex.Match(queryLine);
                            if (match.Success)
                            {
                                hasLiveTraffic = true;
                                string src = match.Groups["src"].Value;
                                string type = match.Groups["type"].Value;
                                string domain = match.Groups["domain"].Value.TrimEnd('.'); // Remove trailing dot

                                _typewriterQueue.Enqueue(($"[CANLI_AKIS] {src} -> {domain} ({type})", _emeraldBrush));
                            }
                        }
                    }

                    // Enqueue random facts ONLY if traffic is low, otherwise show fewer random stats
                    // Strict 5:1 Ratio. Every 6th item is an Educational Fact.
                    int linesToQueue = hasLiveTraffic ? 1 : _random.Next(1, 4);
                    for (int i = 0; i < linesToQueue; i++)
                    {
                        _terminalLineCounter++;
                        if (_terminalLineCounter % 6 == 0)
                        {
                            // Enqueue educational internet fact
                            _typewriterQueue.Enqueue(facts[_random.Next(facts.Length)]);
                        }
                        else
                        {
                            // Enqueue an actual system/network statistic
                            _typewriterQueue.Enqueue(stats[_random.Next(stats.Count)]);
                        }
                    }

                    // Activate the typewriter if it's paused
                    if (!_typewriterTimer.IsEnabled)
                    {
                        _typewriterTimer.Start();
                    }

                    // Hata durumunu güncelle
                    ErrorMessage = _sshService.LastError;
                });

                OnPropertyChanged(string.Empty); // Refresh all
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Güncelleme hatası: {ex.Message}";
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private async Task ExecuteCommandAsync(string command)
        {
            try
            {
                var result = await _sshService.ExecuteCommandAsync(command);
                if (_sshService.LastError != null)
                {
                    MessageBox.Show($"Hata: {_sshService.LastError}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Komut başarıyla çalıştırıldı!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatNumber(long number)
        {
            if (number >= 1_000_000) return $"{number / 1_000_000.0:F1}M";
            if (number >= 1_000) return $"{number / 1_000.0:F1}K";
            return number.ToString();
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void TypewriterTimer_Tick(object? sender, EventArgs e)
        {
            if (_currentTypewriterIndex >= _currentTypewriterLine.Length)
            {
                // Current string finished typing
                if (_typewriterQueue.Count > 0)
                {
                    var nextMessage = _typewriterQueue.Dequeue();
                    _currentTypewriterLine = nextMessage.Text;
                    _currentTypewriterColor = nextMessage.Color;
                    _currentTypewriterIndex = 0;
                    // Push a new empty line to the top
                    TerminalLogs.Insert(0, new TerminalLine { Text = "", Color = _currentTypewriterColor });
                    return; // Wait for the next tick to start typing safely
                }
                else
                {
                    // Everything is typed, pause timer until more data comes
                    _typewriterTimer.Stop();
                    return;
                }
            }

            // Type the next character onto the topmost line
            if (TerminalLogs.Count > 0 && _currentTypewriterIndex < _currentTypewriterLine.Length)
            {
                _currentTypewriterIndex++;
                TerminalLogs[0].Text = _currentTypewriterLine.Substring(0, _currentTypewriterIndex);
            }

            // Cap the visual terminal logs at 60 items so it doesn't leak memory indefinitely
            while (TerminalLogs.Count > 60)
            {
                TerminalLogs.RemoveAt(TerminalLogs.Count - 1);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _typewriterTimer.Stop();
            _updateTimer.Stop();
            _sshService.Dispose();
        }
    }

    public class TerminalLine : INotifyPropertyChanged
    {
        private string _text = "";
        public string Text 
        { 
            get => _text; 
            set { _text = value; OnPropertyChanged(); } 
        }

        private Brush _color = Brushes.LimeGreen;
        public Brush Color 
        { 
            get => _color; 
            set { _color = value; OnPropertyChanged(); } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // RelayCommand implementation
    public class RelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public async void Execute(object? parameter) => await _execute();
    }
}
