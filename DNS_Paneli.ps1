# UNBOUND DNS İZLEME MERKEZİ v5.0 - PowerShell Başlatıcı
# UTF-8 encoding
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Console boyutunu ayarla
try {
    $Host.UI.RawUI.WindowTitle = "UNBOUND // DNS İZLEME MERKEZİ v5.0"

    # Buffer size ayarla (önce büyük buffer)
    $BufferSize = $Host.UI.RawUI.BufferSize
    $BufferSize.Width = 130
    $BufferSize.Height = 9001
    $Host.UI.RawUI.BufferSize = $BufferSize

    # Window size ayarla (buffer'dan sonra)
    $WindowSize = $Host.UI.RawUI.WindowSize
    $WindowSize.Width = 130
    $WindowSize.Height = 42
    $Host.UI.RawUI.WindowSize = $WindowSize

    Write-Host "Terminal boyutlandırıldı: 130x42" -ForegroundColor Green
}
catch {
    Write-Host "Uyarı: Terminal boyutlandırılamadı" -ForegroundColor Yellow
}

Clear-Host

# Python kontrolü
$pythonExists = Get-Command python -ErrorAction SilentlyContinue
if (-not $pythonExists) {
    Write-Host ""
    Write-Host "   ============================================" -ForegroundColor Red
    Write-Host "   [HATA] Python Bulunamadı" -ForegroundColor Red
    Write-Host "   ============================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "   DNS İzleme Merkezi çalıştırmak için Python gerekli."
    Write-Host ""
    Write-Host "   Lütfen aşağıdaki adresten Python yükleyin:"
    Write-Host "   > https://python.org/downloads" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   Yükleme sırasında 'Add Python to PATH'"
    Write-Host "   seçeneğini işaretlemeyi unutmayın!"
    Write-Host ""
    Write-Host "   ============================================" -ForegroundColor Red
    Write-Host ""
    Pause
    exit 1
}

# Gerekli kütüphaneleri kontrol et
$libCheck = python -c "import paramiko, rich" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "   ============================================" -ForegroundColor Yellow
    Write-Host "   [BİLGİ] Gerekli kütüphaneler yükleniyor..." -ForegroundColor Yellow
    Write-Host "   ============================================" -ForegroundColor Yellow
    Write-Host ""
    pip install paramiko rich
    Write-Host ""
    Write-Host "   Kütüphaneler yüklendi!" -ForegroundColor Green
    Write-Host ""
    Start-Sleep -Seconds 2
}

# Dashboard'u başlat
Clear-Host
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
python "$scriptPath\unbound_dashboard.py"

# Hata kontrolü
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "   ============================================" -ForegroundColor Red
    Write-Host "   [HATA] Program beklenmedik bir hatayla karşılaştı" -ForegroundColor Red
    Write-Host "   ============================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "   Hata kodu: $LASTEXITCODE" -ForegroundColor Red
    Write-Host ""
}

# Her zaman pause (hata görmek için)
Pause
