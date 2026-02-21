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
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using SkiaSharp;
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

        public string SecurityStatus
        {
            get
            {
                var status = _metrics.DnssecActive
                    ? "✓ DNSSEC Koruma: AKTİF - Sahte ve zararlı sitelerden korunuyorsunuz\n"
                    : $"⚠ DNSSEC Uyarı: {_metrics.BogusBlocked} TEHDİT ENGELLENDI! Zararlı site girişimi tespit edildi\n";

                status += $"• Başarılı Sorgu: {FormatNumber(_metrics.SuccessfulQueries)} doğru yanıt verildi";

                if (_metrics.NxDomain > 0)
                    status += $"\n• Bulunamayan Site: {FormatNumber(_metrics.NxDomain)} alan adı mevcut değil (NXDOMAIN)";

                if (_metrics.ServerFail > 0)
                    status += $"\n⚠ Sunucu Hatası: {FormatNumber(_metrics.ServerFail)} DNS sunucusu yanıt veremedi (SERVFAIL)";

                return status;
            }
        }

        // Query Types
        public ObservableCollection<QueryTypeInfo> QueryTypes { get; } = new();

        // Charts
        public ObservableCollection<ISeries> QPSSeries { get; set; }
        public Axis[] XAxes { get; set; }
        public Axis[] YAxes { get; set; }

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
            WarmupCommand = new RelayCommand(async () => await ExecuteCommandAsync("bash /opt/unbound/dns-warmup.sh"));
            FlushCommand = new RelayCommand(async () => await ExecuteCommandAsync("docker exec unbound unbound-control flush_zone ."));
            RestartCommand = new RelayCommand(async () => await ExecuteCommandAsync("docker restart unbound"));
            SpeedTestCommand = new RelayCommand(async () => await ExecuteCommandAsync("dig @127.0.0.1 google.com"));

            // Initialize advanced charts
            var darkCyan = new SKColor(34, 211, 238); // #22d3ee
            var transparentCyan = new SKColor(34, 211, 238, 40); // 40 alpha

            QPSSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<double>
                {
                    Values = _metrics.QpsHistory,
                    Name = "Sorgu / Saniye",
                    LineSmoothness = 0.65, // Yumuşak kıvrımlar
                    Fill = new LinearGradientPaint(
                        new[] { transparentCyan, new SKColor(34, 211, 238, 0) },
                        new SKPoint(0.5f, 0), new SKPoint(0.5f, 1) // Top to bottom
                    ),
                    Stroke = new SolidColorPaint(darkCyan) { StrokeThickness = 3 },
                    GeometryFill = new SolidColorPaint(darkCyan), // Nokta içi
                    GeometryStroke = new SolidColorPaint(SKColors.White) { StrokeThickness = 2 }, // Nokta dışı
                    GeometrySize = 8,
                    YToolTipLabelFormatter = (chartPoint) => $"{chartPoint.Coordinate.PrimaryValue} QPS"
                }
            };

            XAxes = new Axis[]
            {
                new Axis
                {
                    IsVisible = false // Alt eksen rakamlarını gizle temiz doku için
                }
            };

            YAxes = new Axis[]
            {
                new Axis
                {
                    MinStep = 1,
                    MinLimit = 0,
                    LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)), // #64748b - TextMuted
                    SeparatorsPaint = new SolidColorPaint(new SKColor(30, 41, 59, 100)) // Hafif çizgiler
                    {
                        StrokeThickness = 1,
                        PathEffect = new DashEffect(new float[] { 3, 3 }) // Kesik çizgili
                    }
                }
            };

            // Timer for updates
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _updateTimer.Tick += async (s, e) => await UpdateMetricsAsync();

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

        private (string hostname, int port, string username, string? password, string? keyPath) LoadConfig()
        {
            try
            {
                var configPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

                if (System.IO.File.Exists(configPath))
                {
                    var json = System.IO.File.ReadAllText(configPath);
                    // Basit JSON parse (System.Text.Json gerekli değil, manuel parse)
                    var hostname = ExtractJsonValue(json, "hostname") ?? "192.168.1.123";
                    var portStr = ExtractJsonValue(json, "port");
                    var port = int.TryParse(portStr, out var p) ? p : 22;
                    var username = ExtractJsonValue(json, "username") ?? "root";
                    var password = ExtractJsonValue(json, "password");
                    var keyPath = ExtractJsonValue(json, "keyPath");

                    return (hostname, port, username, password, keyPath);
                }
            }
            catch { /* Config okunamazsa varsayılana dön */ }

            return ("192.168.1.123", 22, "root", null, null);
        }

        private string? ExtractJsonValue(string json, string key)
        {
            var pattern = $"\"{key}\"";
            var idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var colonIdx = json.IndexOf(':', idx + pattern.Length);
            if (colonIdx < 0) return null;

            var rest = json.Substring(colonIdx + 1).TrimStart();

            // Numara ise
            if (char.IsDigit(rest[0]))
            {
                var end = 0;
                while (end < rest.Length && (char.IsDigit(rest[end]) || rest[end] == '.'))
                    end++;
                return rest.Substring(0, end);
            }

            // String ise
            if (rest[0] == '"')
            {
                var closeQuote = rest.IndexOf('"', 1);
                if (closeQuote > 1)
                    return rest.Substring(1, closeQuote - 1);
            }

            // null ise
            if (rest.StartsWith("null")) return null;

            return null;
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

                // QPS history sınırlaması (bellek koruması)
                while (newMetrics.QpsHistory.Count > MaxHistorySize)
                {
                    newMetrics.QpsHistory.RemoveAt(0);
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _updateTimer.Stop();
            _sshService.Dispose();
        }
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
