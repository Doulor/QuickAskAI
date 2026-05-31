$ErrorActionPreference = "Stop"
$layoutScripts = Join-Path $PSScriptRoot "..\release\layout-scripts"

$installBat = @'
@echo off
setlocal enabledelayedexpansion
title QuickAskAI Installer

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs -WorkingDirectory '%~dp0'"
    exit /b
)

cd /d "%~dp0"

echo.
echo ============================================
echo   QuickAskAI Installer
echo ============================================
echo.

if not exist "QuickAskAI.msix" (
    echo [ERROR] QuickAskAI.msix not found.
    echo Please extract the entire release zip before running this script.
    echo.
    pause
    exit /b 1
)

if not exist "QuickAskAI.cer" (
    echo [ERROR] QuickAskAI.cer not found.
    echo Please extract the entire release zip before running this script.
    echo.
    pause
    exit /b 1
)

echo Stopping existing extension...
taskkill /f /im AIExtension.exe >nul 2>&1

echo Removing previous installation...
powershell -Command "$p=Get-AppxPackage -Name AIExtension; if($p){Remove-AppxPackage -Package $p.PackageFullName}" >nul 2>&1

echo Installing signing certificate...
certutil -addstore Root QuickAskAI.cer >nul 2>&1
if %errorlevel% neq 0 (
    certutil -verifystore Root QuickAskAI >nul 2>&1
    if %errorlevel% neq 0 (
        echo [ERROR] Failed to install the signing certificate.
        echo.
        pause
        exit /b 1
    )
)
echo Certificate trusted.

echo Installing QuickAskAI...
powershell -Command "$ErrorActionPreference='Stop';try{Add-AppxPackage -Path 'QuickAskAI.msix' -ForceApplicationShutdown -ForceUpdateFromAnyVersion;Write-Host 'OK'}catch{Write-Host $_.Exception.Message;exit 1}"
if %errorlevel% neq 0 (
    echo.
    echo Installation failed.
    echo You can try enabling Developer Mode and run this script again.
    echo.
    pause
    exit /b 1
)

echo.
echo ============================================
echo   QuickAskAI installed.
echo.
echo   Open PowerToys Command Palette,
echo   type Reload, then search QuickAskAI.
echo ============================================
echo.
pause
'@

$uninstallBat = @'
@echo off
setlocal enabledelayedexpansion
title QuickAskAI Uninstaller

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs -WorkingDirectory '%~dp0'"
    exit /b
)

cd /d "%~dp0"

echo.
echo ============================================
echo   QuickAskAI Uninstaller
echo ============================================
echo.

echo Stopping QuickAskAI...
taskkill /f /im AIExtension.exe >nul 2>&1

echo Removing QuickAskAI...
powershell -Command "$p=Get-AppxPackage -Name AIExtension; if($p){Remove-AppxPackage -Package $p.PackageFullName;Write-Host 'QuickAskAI removed.'}else{Write-Host 'QuickAskAI is not installed.'}"

echo Removing signing certificate...
certutil -delstore Root QuickAskAI >nul 2>&1

echo.
echo Uninstall complete.
echo.
pause
'@

[IO.File]::WriteAllText((Join-Path $layoutScripts "install.bat"), $installBat, [Text.Encoding]::ASCII)
[IO.File]::WriteAllText((Join-Path $layoutScripts "uninstall.bat"), $uninstallBat, [Text.Encoding]::ASCII)
Write-Host ".bat files generated with ASCII encoding"
