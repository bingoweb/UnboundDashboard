@echo off
chcp 65001 >nul 2>&1
title UNBOUND DNS Monitor - Kurulum

echo.
echo  ╔══════════════════════════════════════════════════╗
echo  ║     UNBOUND DNS İZLEME MERKEZİ - KURULUM        ║
echo  ╚══════════════════════════════════════════════════╝
echo.

:: Self-contained EXE has .NET bundled, but WPF needs VC++ Runtime
:: Check if VC++ Runtime 2015-2022 x64 is installed
reg query "HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64" /v Installed >nul 2>&1
if %errorlevel% equ 0 (
    echo  [OK] Visual C++ Runtime bulundu.
    goto :launch
)

:: Also check the newer registry path
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\X64" /v Installed >nul 2>&1
if %errorlevel% equ 0 (
    echo  [OK] Visual C++ Runtime bulundu.
    goto :launch
)

:: VC++ Runtime not found — install it
echo  [!] Visual C++ Runtime bulunamadı. Kuruluyor...
echo.

:: Check if installer already exists
if exist "%~dp0vc_redist.x64.exe" (
    echo  [*] Mevcut yükleyici kullanılıyor...
    goto :install_vcpp
)

:: Download VC++ Redistributable
echo  [*] Visual C++ Runtime indiriliyor...
powershell -Command "& { try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -Uri 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile '%~dp0vc_redist.x64.exe' -UseBasicParsing } catch { Write-Host '  [HATA] İndirme başarısız: ' $_.Exception.Message; exit 1 } }"

if not exist "%~dp0vc_redist.x64.exe" (
    echo.
    echo  [HATA] İndirme başarısız oldu!
    echo  Lütfen aşağıdaki bağlantıdan manuel olarak indirin:
    echo  https://aka.ms/vs/17/release/vc_redist.x64.exe
    echo.
    pause
    exit /b 1
)

:install_vcpp
echo  [*] Visual C++ Runtime kuruluyor (yönetici izni gerekli)...
echo.

:: Run installer silently with elevation
powershell -Command "Start-Process '%~dp0vc_redist.x64.exe' -ArgumentList '/install /quiet /norestart' -Verb RunAs -Wait"

if %errorlevel% neq 0 (
    echo  [HATA] Kurulum başarısız oldu.
    echo  Yönetici olarak çalıştırmayı deneyin.
    pause
    exit /b 1
)

echo  [OK] Visual C++ Runtime başarıyla kuruldu!
echo.

:: Cleanup installer
del "%~dp0vc_redist.x64.exe" >nul 2>&1

:launch
echo.
echo  [*] UNBOUND DNS Monitor başlatılıyor...
echo.

:: Check that the EXE exists
if exist "%~dp0UnboundDashboard.exe" (
    start "" "%~dp0UnboundDashboard.exe"
) else (
    echo  [HATA] UnboundDashboard.exe bulunamadı!
    echo  Bu dosyayı UnboundDashboard.exe ile aynı klasöre koyun.
    pause
    exit /b 1
)

exit /b 0
