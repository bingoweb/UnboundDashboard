# UNBOUND DNS İZLEME MERKEZİ - WPF Desktop Uygulaması

Modern WPF tabanlı DNS monitoring dashboard. UNBOUND DNS sunucunuzu SSH üzerinden izleyin.

## 📋 Gereksinimler

- **.NET 8.0 SDK** veya üzeri
  - İndirmek için: https://dotnet.microsoft.com/download/dotnet/8.0
- **Windows 10/11** (WPF uygulaması)
- **SSH erişimi** olan UNBOUND DNS sunucusu

## 🚀 Kurulum

### 1. Projeyi İndirin veya Klonlayın

```bash
cd c:\Users\tayla\Desktop\unbound2\UnboundDashboard
```

### 2. Bağımlılıkları Yükleyin

```bash
dotnet restore
```

Bu komut aşağıdaki NuGet paketlerini otomatik yükler:
- `Renci.SshNet` - SSH bağlantısı için
- `LiveChartsCore.SkiaSharpView.WPF` - Canlı grafikler için
- `MaterialDesignThemes` - Modern UI teması için

### 3. Projeyi Derleyin

```bash
dotnet build
```

## ▶️ Çalıştırma

### Geliştirme Modunda Çalıştırma

```bash
dotnet run
```

### Derlenmiş Uygulamayı Çalıştırma

```bash
cd bin\Debug\net8.0-windows
.\UnboundDashboard.exe
```

## ⚙️ Yapılandırma

### SSH Bağlantı Ayarları

**DashboardViewModel.cs** dosyasında (satır 98) SSH ayarlarını düzenleyin:

```csharp
_sshService = new SshService("192.168.1.123", 22, "root"); // IP, Port, Kullanıcı
```

**Değiştirmeniz gerekenler:**
- `192.168.1.123` → UNBOUND sunucunuzun IP adresi
- `22` → SSH portu (varsayılan 22)
- `root` → SSH kullanıcı adı

### SSH Kimlik Doğrulama

Uygulama iki yöntemle bağlanabilir:

#### 1. SSH Anahtarı (Önerilir)
```bash
# Windows'da varsayılan konum:
C:\Users\[KULLANICI]\.ssh\id_rsa
```

#### 2. Şifre
Eğer SSH anahtarı yoksa, **SshService.cs** dosyasında şifre desteği ekleyin.

## 🎨 Özellikler

### 📊 Gerçek Zamanlı Metrikler
- **Toplam İstek** - Gönderilen toplam DNS sorgusu
- **Önbellek Başarısı** - Cache hit yüzdesi (%)
- **Cevap Süresi** - Ortalama yanıt süresi (ms)
- **Saniyedeki Hız** - Sorgu/saniye (QPS)

### 📈 Canlı Grafikler
- **QPS Grafiği** - Son 60 saniye sorgu hızı
- **Cache Hit Grafiği** - Önbellek başarı trendi

### 💻 Sistem Bilgileri
- CPU Kullanımı (%)
- RAM Kullanımı (MB)
- Disk Kullanımı
- DNSSEC Güvenlik Durumu

### 🎯 Sorgu Türleri
Top 5 DNS sorgu tipi:
- Web Sitesi (A)
- Web IPv6 (AAAA)
- Güvenli Bağlantı (HTTPS)
- E-posta Sunucu (MX)
- Doğrulama Kaydı (TXT)

### ⚡ Hızlı Komutlar
- **ÖN ISITMA** - Cache warmup script çalıştır
- **TEMİZLEME** - Önbelleği temizle
- **YENİDEN BAŞLAT** - UNBOUND servisini yeniden başlat
- **HIZ TESTİ** - DNS sorgu hızını test et

## 🔄 Otomatik Güncelleme

Dashboard her **2 saniyede** bir otomatik olarak güncellenir. Timer ayarı için **DashboardViewModel.cs** (satır 122):

```csharp
_updateTimer = new DispatcherTimer
{
    Interval = TimeSpan.FromSeconds(2) // Saniye cinsinden
};
```

## 🎨 Tema Renkleri

**App.xaml** dosyasında tanımlı renkler:

| Renk | Hex Kod | Kullanım |
|------|---------|----------|
| Matrix Green | `#00FF41` | Ana tema, başarı |
| Ocean Blue | `#00A0FF` | Bilgi, veri |
| Purple Metric | `#AA00FF` | Metrikler |
| Cyan Metric | `#00FFFF` | Hız göstergeleri |
| Dark Background | `#0E0E0E` | Arka plan |
| Card Background | `#1A1A1A` | Panel arka planı |

## 📁 Proje Yapısı

```
UnboundDashboard/
├── Models/
│   └── DnsMetrics.cs          # Veri modeli
├── Services/
│   └── SshService.cs          # SSH bağlantı servisi
├── ViewModels/
│   └── DashboardViewModel.cs  # MVVM ViewModel
├── App.xaml                   # Uygulama kaynakları ve tema
├── App.xaml.cs                # Uygulama başlangıcı
├── MainWindow.xaml            # Ana pencere UI tasarımı
├── MainWindow.xaml.cs         # Ana pencere code-behind
└── UnboundDashboard.csproj    # Proje dosyası
```

## 🐛 Sorun Giderme

### SSH Bağlantı Hatası
```
HATA: Connection failed
```

**Çözüm:**
1. Sunucu IP adresini kontrol edin
2. SSH servisinin çalıştığından emin olun: `systemctl status ssh`
3. Firewall ayarlarını kontrol edin (Port 22 açık mı?)
4. SSH anahtarının doğru konumda olduğundan emin olun

### Türkçe Karakter Sorunu
```
HATA: ��������
```

**Çözüm:**
**App.xaml.cs** dosyasında UTF-8 encoding etkin (satır 12):
```csharp
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
```

### Grafik Görünmüyor

**Çözüm:**
LiveCharts paketinin doğru yüklendiğinden emin olun:
```bash
dotnet add package LiveChartsCore.SkiaSharpView.WPF --version 2.0.0-rc2
```

### Yüksek CPU Kullanımı

Güncelleme aralığını artırın (**DashboardViewModel.cs** satır 122):
```csharp
Interval = TimeSpan.FromSeconds(5) // 2 → 5 saniye
```

## 📝 Notlar

- Uygulama **MVVM (Model-View-ViewModel)** mimarisi kullanır
- **Material Design** temaları ile modern görünüm
- **Async/Await** pattern ile performanslı SSH operasyonları
- **INotifyPropertyChanged** ile otomatik UI güncellemesi
- Python terminal versiyonu ile aynı metrikleri gösterir

## 🔗 Bağlantılar

- [.NET 8 Documentation](https://docs.microsoft.com/dotnet)
- [Material Design in XAML](http://materialdesigninxaml.net/)
- [LiveCharts Documentation](https://livecharts.dev/)
- [Renci.SSH.NET](https://github.com/sshnet/SSH.NET)

## 📄 Lisans

Bu proje UNBOUND DNS sunucusu izleme amaçlı geliştirilmiştir.

---

**Geliştirici:** Claude Code
**Versiyon:** 5.0
**Platform:** Windows WPF .NET 8.0
