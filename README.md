# UNBOUND DNS İZLEME MERKEZİ — Proje Dokümantasyonu

> **Bu dosya, projeyi gelecekte yapay zeka ile geliştirirken tüm teknik detayların
> hatırlanması ve bağlamın korunması için oluşturulmuştur.**

---

## 1. Proje Özeti

| Alan | Değer |
|---|---|
| **Proje Adı** | Unbound Cyber Threat Monitor |
| **Versiyon** | v6.0.0 |
| **Platform** | Windows (WPF — .NET 8) |
| **Dil** | C# + XAML |
| **Mimari** | MVVM (Model-View-ViewModel) |
| **Amaç** | Uzak Linux sunucudaki Unbound DNS Resolver servisini SSH üzerinden gerçek zamanlı izlemek ve yönetmek |
| **Hedef Kullanıcı** | Siber güvenlik uzmanları, ağ yöneticileri, self-hosted DNS kullananlar |
| **Dağıtım** | 100% Portatif Self-contained single-file EXE (~193 MB, tüm DLL'ler ve .NET gömülü) |

### Ne Yapar?
- Uzak sunucuya SSH ile bağlanır
- Docker içinde çalışan Unbound DNS Resolver'dan tam siber telemetri toplar
- CPU, RAM, Disk, Uptime, IP Adresleri, Unbound Derleme sürümü gibi sistem metriklerini getirir
- Matrix Stili "Siber İstihbarat Terminali" üzerinden gerçek zamanlı DNS logları ve teknik eğitim verileri (5'e 1 oranında) akıtır
- Sorgu türlerini, cache hit oranını, cevap süresini 12px oval profesyonel taktiksel arayüzde görselleştirir
- DNS önbellek ısıtma, temizleme, servis yeniden başlatma ve hız testi komutlarını çalıştırır

---

## 2. Teknoloji Yığını

| Teknoloji | Paket / Versiyon | Kullanım |
|---|---|---|
| **.NET** | 8.0-windows | Çalışma zamanı |
| **WPF** | .NET 8 dahili | UI framework |
| **SSH.NET** | 2024.2.0 | SSH bağlantısı (NuGet paket adı: `SSH.NET`, namespace: `Renci.SshNet`) |
| **LiveChartsCore** | 2.0.0-rc2 | QPS grafiği (`LiveChartsCore.SkiaSharpView.WPF`) |
| **SkiaSharp** | LiveCharts bağımlılığı | Grafik render |
| **MaterialDesignThemes** | 5.0.0 | Dark tema altyapısı |
| **MaterialDesignColors** | 3.0.0 | Renk paleti |
| **CommunityToolkit.Mvvm** | 8.2.2 | MVVM yardımcıları (şu an kısmen kullanılıyor) |

> [!IMPORTANT]
> **SSH.NET NuGet paketi**: NuGet'te paket adı `SSH.NET`'dir, `Renci.SshNet` değildir.
> Ancak C# kodunda `using Renci.SshNet;` namespace'i kullanılır. Bu karıştırılabilir.

> [!IMPORTANT]
> **MaterialDesignThemes v5.0**: Sadece `BundledTheme` kullanılır, ayrı
> `MaterialDesignTheme.Defaults.xaml` resource dictionary EKLENMEMELİDİR.
> v5.0'da BundledTheme zaten defaults'ı içerir. Eklenirse runtime crash verir.

---

## 3. Proje Yapısı

```
unbound2/
├── DNS_Paneli.bat              # Eski Python launcher (batch wrapper)
├── DNS_Paneli.ps1              # Eski Python launcher (PowerShell)
├── unbound_dashboard.py        # Eski Python versiyonu (artık kullanılmıyor)
│
└── UnboundDashboard/           # ★ ANA WPF PROJESİ
    ├── UnboundDashboard.csproj # Proje dosyası
    ├── appsettings.json        # SSH bağlantı ayarları
    ├── publish.bat             # Tek tıkla publish scripti
    ├── setup.bat               # İlk kurulum (VC++ Runtime kontrolü)
    │
    ├── App.xaml                # Uygulama kaynakları (tema, renkler, stiller)
    ├── App.xaml.cs             # Uygulama giriş noktası
    │
    ├── MainWindow.xaml         # ★ Ana dashboard arayüzü
    ├── MainWindow.xaml.cs      # Pencere arkaplani (title bar, login akışı)
    │
    ├── LoginDialog.xaml        # SSH şifre giriş dialog'u
    ├── LoginDialog.xaml.cs     # Bağlantı testi ve config kaydetme
    │
    ├── Models/
    │   └── DnsMetrics.cs       # Veri modeli (tüm metrikler)
    │
    ├── Services/
    │   └── SshService.cs       # SSH bağlantı ve veri toplama servisi
    │
    ├── ViewModels/
    │   └── DashboardViewModel.cs # MVVM ViewModel (binding'ler, komutlar)
    │
    └── publish/                # Derlenmiş çıktı
        ├── UnboundDashboard.exe # Self-contained EXE
        ├── appsettings.json
        └── setup.bat
```

---

## 4. Dosya Detayları

### 4.1 `App.xaml` — Tasarım Sistemi

**Sorumluluk**: Tüm uygulama genelinde kullanılan renkler, stiller ve kontrol şablonları.

**İçerik**:
- `BundledTheme` (MaterialDesign Dark + Cyan/DeepPurple)
- Renk paleti: `CyanAccent (#22d3ee)`, `EmeraldAccent (#34d399)`, `VioletAccent (#a78bfa)`, `AmberAccent (#fbbf24)`, `RoseAccent (#fb7185)`
- Legacy alias'lar: `MatrixGreen`, `OceanBlue`, `PurpleMetric`, `CyanMetric` — ViewModel'deki Brush binding'leri bu isimleri kullanır
- `GlowProgressBar` stili: Custom ControlTemplate, rounded corners, rainbow gradient dolgu (yeşil → cyan → mor), DropShadow glow
- `PillButton` stili: Custom ControlTemplate, rounded corners, hover'da scale efekti, glow shadow
- `PulseAnimation` storyboard: Opacity 1.0 ↔ 0.3, tekrar eden animasyon

> [!WARNING]
> ViewModel'deki `CpuColor`, `RamColor`, `CacheStatusColor` property'leri `System.Windows.Media.Brushes` döndürür.
> Bu brush isimleri (`LimeGreen`, `Yellow`, `Red`, `Cyan`) App.xaml'deki renk paleti ile uyumlu olmayabilir.
> Eğer renkler değiştirilecekse, ViewModel'deki bu property'ler de güncellenmelidir.

---

### 4.2 `MainWindow.xaml` — Ana Arayüz

**Sorumluluk**: Dashboard UI layout'u.

**Özellikler**:
- **Taktiksel Profesyonel Arayüz**: Tümüyle mat askeri renkler (`#08100c`, `#142e20`). Hiçbir gradient kullanılmaz.
- **Oval Köşeler**: Modern UI standardı olan `CornerRadius="12"` kullanılarak profesyonel, akışkan bir dizayn elde edildi.
- **Responsive ViewBox**: 16:9 1080p bazlı tasarım, pencere boyutundan bağımsız olarak hiçbir zaman kırpılmaz (`Viewbox Stretch="Uniform"`).
- **Metrik Kartları** (4 adet): Toplam Sorgu, Önbellek Yanıtı, Cevap Süresi, Tarama Hızı (QPS)
- **Siber İstihbarat Terminali**: Matrix tarzı daktilo animasyonlu, Türkçe DNS bilgi akışı sunan gerçek zamanlı log ekranı.
- **Sistem Paneli**: İşlemci/Bellek durumları bar ile gösterilir, SSH sunucu bilgileri çekilir.
- **Sorgu Türleri**: ItemsControl ile dinamik kaydırılabilir liste
- **Aksiyon Butonları**: Profesyonel SVG ikonlarıyla donatılmış 4 adet fonksiyon butonu.

**Layout**:
```
┌──────────────────────────────────────────┐
│              HEADER (CYBER THREAT)       │
├──────┬───────┬───────┬──────────────────┤
│ TOPLAM│ÖNBELLE│CEVAP   │ TARAMA         │
│ SORGU │K %    │SÜRESİ  │ HIZI (QPS)     │
├──────┴───────┴───────┴──────────────────┤
│  ÖNBELLEK DURUMU  │  SİSTEM AKTİVİTESİ  │
│  ├─ Progress bar  │  ├─ İşlemci bar     │
│  ├─ İstatistik    │  ├─ Bellek bar      │
│  └─ Durum mesajı  │  ├─ Disk            │
│                   │                     │
│  SİBER TERMİNAL   │  PROTOKOL DAĞILIMI  │
│  ├─ Eğitim Feed   │  ├─ A, AAAA, MX...  │
│  ├─ Canlı Loglar  │                     │
├───────────────────┴─────────────────────┤
│  ÖN ISITMA │ TEMİZLEME │ BAŞLAT │ TEST │
└─────────────────────────────────────────┘
```

**Data Binding'ler (tümü ViewModel'den gelir)**:
| XAML Binding | ViewModel Property | Tip |
|---|---|---|
| `TotalQueries` | `FormatNumber(_metrics.TotalQueries)` | string ("12.9K") |
| `CacheHitPercent` | `_metrics.CacheHitPercent` | double (0-100) |
| `ResponseTime` | `_metrics.ResponseTimeMs` | double (ms) |
| `QPS` | `_metrics.CurrentQPS` | double |
| `StatusText` | "● AKTİF" / "✕ BAĞLANTI YOK" | string |
| `StatusColor` | Brushes.LimeGreen/Orange/Red | Brush |
| `CpuUsage` | `_metrics.CpuUsage` | double (0-100) |
| `RamPercent` | `_metrics.RamPercent` | double (0-100) |
| `CacheStats` | Çok satırlı metin | string |
| `SecurityStatus` | DNSSEC durumu + detaylar | string |
| `QPSSeries` | LiveCharts ISeries koleksiyonu | ObservableCollection |
| `QueryTypes` | QueryTypeInfo listesi | ObservableCollection |

> [!IMPORTANT]
> **ProgressBar binding'lerinde `Mode=OneWay` zorunludur!**
> `CacheHitPercent`, `CpuUsage`, `RamPercent` read-only property'lerdir.
> ProgressBar.Value varsayılan olarak TwoWay bind eder — `Mode=OneWay` eklenmelidir,
> aksi halde runtime crash olur: `"TwoWay veya OneWayToSource bağlama çalışamaz"`.

> [!IMPORTANT]
> **XAML StringFormat escape kuralı**: `{0:F0}` gibi format ifadeleri XAML'de
> `{}` prefix ile escape edilmelidir: `StringFormat='{}{0:F0}ms'`
> Aksi halde XAML parser `{0:F0}` ifadesini markup extension olarak yorumlar ve crash verir.

---

### 4.3 `LoginDialog.xaml` + `.cs` — SSH Giriş Ekranı

**Sorumluluk**: SSH bilgilerinin girilmesi, test edilmesi ve kaydedilmesi.

**Akış**:
1. `MainWindow.xaml.cs` başlatılırken `appsettings.json` okunur
2. Eğer `password` ve `keyPath` ikisi de `null` ise → `LoginDialog` açılır
3. Kullanıcı bilgileri girer → "Bağlantıyı Test Et" ile doğrular
4. "Kaydet ve Bağlan" butonuyla → `appsettings.json`'a yazar → dialog kapanır
5. Sonraki açılışlarda şifre config'de varsa dialog gösterilmez

**Teknik Detaylar**:
- `PasswordBox` kullanılır (güvenlik — metin gizli)
- SSH testi `Renci.SshNet.SshClient` ile yapılır (8 saniye timeout)
- JSON kaydetme: Manuel string interpolation ile (System.Text.Json kullanılmaz)
- JSON parse: `ExtractValue()` methodu ile basit key-value arama

---

### 4.4 `DashboardViewModel.cs` — Ana ViewModel

**Sorumluluk**: Tüm UI verilerinin yönetimi, SSH servisi ile iletişim, komut çalıştırma.

**Constructor**: `DashboardViewModel(string hostname, int port, string username, string? password, string? keyPath)`
- Credential'ları `MainWindow.xaml.cs`'den alır (config veya LoginDialog'dan)
- `SshService` oluşturur
- `DispatcherTimer` başlatır (2 saniye interval)
- LiveChartsCore `LineSeries` yapılandırır
- İlk SSH bağlantısını `Task.Run` ile başlatır

**ICommand'lar**:
| Komut | SSH Komutu |
|---|---|
| `WarmupCommand` | `bash /opt/unbound/dns-warmup.sh` |
| `FlushCommand` | `docker exec unbound unbound-control flush_zone .` |
| `RestartCommand` | `docker restart unbound` |
| `SpeedTestCommand` | `dig @127.0.0.1 google.com` |

**Thread Safety**:
- `_isUpdating` flag ile eşzamanlı güncelleme engellenir
- `Application.Current.Dispatcher.Invoke()` ile UI thread'ine geçiş yapılır

**Hata Yönetimi**:
- `ErrorMessage` property → UI'da header altındaki error banner'da gösterilir
- `HasError` → `BooleanToVisibilityConverter` ile banner visibility'sini kontrol eder

**Sayı Formatlama**: `FormatNumber()` methodu:
- `< 1000` → direkt göster
- `< 1_000_000` → `"12.9K"` formatı
- `≥ 1_000_000` → `"1.5M"` formatı

**Bellek Limiti**: `MaxHistorySize = 60` — QPS history son 60 veriyi tutar

---

### 4.5 `SshService.cs` — SSH Bağlantı Servisi

**Sorumluluk**: SSH bağlantısı, komut çalıştırma, metrik toplama.

**Bağlantı Yöntemleri** (öncelik sırasıyla):
1. Private key dosyası (`privateKeyPath` parametresi)
2. Şifre (`password` parametresi)
3. Varsayılan SSH anahtarları (`~/.ssh/id_rsa` veya `~/.ssh/id_ed25519`)

**Otomatik Yeniden Bağlanma**: `EnsureConnectedAsync()` — max 3 deneme

**Tek SSH Çağrısı ile Tüm Veri Toplama**: `CollectMetricsAsync()` tek bir SSH komutu çalıştırır:
```bash
echo '==S=='; docker inspect -f '{{.State.Status}}' unbound
echo '==V=='; docker exec unbound unbound-control stats_noreset
echo '==U=='; uptime -p
echo '==M=='; free -m | awk 'NR==2{print $2,$3}'
echo '==C=='; grep 'cpu ' /proc/stat | awk '{...}'
echo '==D=='; df -h / | awk 'NR==2{...}'
echo '==X=='
```

**Section Parsing**: `==S==`, `==V==`, `==U==` gibi delimiter'lar ile çıktı bölümlere ayrılır.

**Unbound Stats Parsing**: `total.num.cachehits=12345` formatındaki `key=value` satırları parse edilir.

**Sorgu Türü Mapping** (DNS → Türkçe):
| DNS Türü | Türkçe Adı |
|---|---|
| A | Web Sitesi |
| AAAA | Web (IPv6) |
| HTTPS | Güvenli Bağlantı |
| MX | E-posta Sunucu |
| TXT | Doğrulama Kaydı |
| PTR | Ters Sorgu |
| SRV | Servis Kaydı |

---

### 4.6 `DnsMetrics.cs` — Veri Modeli

**Tüm Property'ler**:

| Kategori | Property | Tip | Açıklama |
|---|---|---|---|
| Durum | `Status` | string | "active" / "stopped" / "disconnected" |
| Durum | `Uptime` | string | "1 g 8 s 9 d" formatında |
| Sistem | `CpuUsage` | double | 0-100 |
| Sistem | `RamUsed` / `RamTotal` | int | MB cinsinden |
| Sistem | `RamPercent` | double | 0-100 |
| Sistem | `DiskUsage` | string | "1.3G/20G (7%)" |
| Sorgu | `TotalQueries` | long | Toplam DNS sorgusu |
| Sorgu | `CacheHits` / `CacheMisses` | long | Önbellek isabet/kaçırma |
| Sorgu | `CacheHitPercent` | double | 0-100 |
| Sorgu | `ResponseTimeMs` | double | Ortalama cevap süresi (ms) |
| Sorgu | `CacheSize` | int | Önbellekteki kayıt sayısı |
| Güvenlik | `DnssecActive` | bool | DNSSEC aktif mi |
| Güvenlik | `BogusBlocked` | int | Engellenen sahte yanıt |
| Güvenlik | `SuccessfulQueries` | long | NOERROR yanıtlar |
| Güvenlik | `NxDomain` | long | Alan adı bulunamadı |
| Güvenlik | `ServerFail` | long | Sunucu hatası |
| Sorgu Türleri | `QueryTypes` | Dictionary | DNS türü → sayı |
| Geçmiş | `QpsHistory` | List | Son 60 QPS değeri |

---

### 4.7 `appsettings.json` — Konfigürasyon

```json
{
    "ssh": {
        "hostname": "192.168.1.123",
        "port": 22,
        "username": "root",
        "password": null,
        "keyPath": null
    }
}
```

- `password: null` → LoginDialog açılır
- `password: "xxx"` → Direkt bağlanır
- `keyPath` → SSH private key dosya yolu (isteğe bağlı)
- LoginDialog "Şifreyi hatırla" seçimi bu dosyayı günceller

---

## 5. Derleme ve Dağıtım

### Geliştirme
```bash
dotnet restore          # Paketleri indir
dotnet build            # Derle
dotnet run              # Çalıştır
```

### Publish (Tam Portatif Standalone EXE)
```bash
dotnet publish -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o ./publish_portable
```

### Dağıtım Dosyaları
```
publish_portable/
├── UnboundDashboard.exe    # ~193 MB (Tek dosya, Native DLL'ler + .NET 8 gömülü)
└── appsettings.json        # SSH ayarları (login panelinden otomatik de oluşturulabilir)
```

- Tamamen `self-contained` olarak derlenir.
- `IncludeNativeLibrariesForSelfExtract=true` ile WPF, SkiaSharp ve C++ DLL'lerinin hepsi Exe içine hapsedilir.
- İlk çalıştırmada dahi hedef PC'de internet veya .NET kurulumuna ihtiyaç duymaz. Doğrudan USB'den bile taşınabilir portatiftir.

---

## 6. Uzak Sunucu Gereksinimleri

Dashboard'un bağlandığı Linux sunucuda şunlar olmalıdır:

| Gereksinim | Açıklama |
|---|---|
| **Docker** | Unbound DNS container'ının çalıştığı ortam |
| **Unbound DNS** | Docker container adı: `unbound` |
| **unbound-control** | Container içinde `unbound-control stats_noreset` çalışmalı |
| **SSH Erişimi** | Root veya sudo yetkili kullanıcı (komut çalıştırma için) |
| **Opsiyonel** | `/opt/unbound/dns-warmup.sh` (cache warmup scripti) |

---

## 7. Bilinen Kısıtlamalar ve Uyarılar

1. **JSON Parse**: `System.Text.Json` kullanılmaz — manuel string parsing ile `ExtractJsonValue()` methodu kullanılır. Basit ama nested JSON'da kırılabilir.

2. **CS0067 Uyarısı**: `RelayCommand.CanExecuteChanged` event'i hiç kullanılmaz. Zararsız uyarı.

3. **Nullable uyarıları**: `.csproj`'da `<Nullable>enable</Nullable>` vardır. Tüm nullable referanslar `?` ile işaretlenmelidir.

4. **Encoding**: PowerShell ile dosya yazarken `[Console]::OutputEncoding = [System.Text.Encoding]::UTF8` kullanılmalıdır. Aksi halde Türkçe karakterler (İ, Ş, Ö, Ü, Ç, Ğ) bozulur.

5. **WPF'de LetterSpacing yok**: CSS'deki `letter-spacing` WPF'de desteklenmez. Karakter aralığı için `T E X T` şeklinde boşluk eklenmelidir.

---

## 8. Tasarım Sistemi

### Renk Paleti
| İsim | Hex | Kullanım |
|---|---|---|
| Window BG | `#050a18` → `#0a1228` | Gradient arka plan |
| Card BG | `#0c1225` → `#0a1525` | Kart gradient |
| Cyan | `#22d3ee` | Ana accent, toplam sorgu, QPS |
| Emerald | `#34d399` | Başarı, önbellek, durum |
| Violet | `#a78bfa` | Cevap süresi, güvenlik |
| Amber | `#fbbf24` | Uyarı, QPS kartı |
| Rose | `#fb7185` | Hata |
| Text Primary | `#f1f5f9` | Ana metin |
| Text Muted | `#64748b` | İkincil metin |

### Stil Kuralları
- Card corner radius: `16px`
- Button corner radius: `10px`
- İç padding: `24px`
- Kartlar arası boşluk: `14-18px`
- Font: Segoe UI (sistem), Consolas (monospace değerler)
- Her metrik kartında: Gradient top border (2px), inner radial orb, gradient text, DropShadow glow

---

## 9. Gelecek Geliştirme Alanları

- [ ] `System.Text.Json` ile düzgün JSON serialization/deserialization
- [ ] Çoklu sunucu desteği (birden fazla SSH hedefi)
- [ ] Geçmiş veri kaydetme (SQLite veya dosya)
- [ ] Bildirim sistemi (cache hit düşükse, sunucu erişilemezse)
- [ ] Tray icon ile minimize
- [ ] Otomatik güncelleme mekanizması
- [ ] Tema seçimi (açık/koyu)
- [ ] Dil desteği (İngilizce/Türkçe)
