@echo off
REM ============================================
REM  UNBOUND DNS DASHBOARD - Standalone EXE Build
REM  .NET SDK 8.0+ gerekli
REM ============================================

echo.
echo  ╍╍╍ UNBOUND DASHBOARD - BUILD SCRIPT ╍╍╍
echo.

REM Temizlik
echo [1/3] Onceki build temizleniyor...
dotnet clean -c Release >nul 2>&1

REM Restore
echo [2/3] Paketler yukleniyor...
dotnet restore

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [HATA] Paket yukleme basarisiz!
    pause
    exit /b 1
)

REM Publish - Self-contained single file
echo [3/3] Standalone EXE olusturuluyor...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [HATA] Build basarisiz!
    pause
    exit /b 1
)

echo.
echo  ============================================
echo  [BASARILI] Standalone EXE olusturuldu!
echo  ============================================
echo.
echo  Konum: %~dp0publish\UnboundDashboard.exe
echo.
echo  Bu dosyayi .NET SDK kurulmamis herhangi bir
echo  Windows 10/11 bilgisayara kopyalayip
echo  calistirabilirsiniz.
echo.

REM appsettings.json'ı da kopyala
copy /Y appsettings.json .\publish\appsettings.json >nul 2>&1

pause
