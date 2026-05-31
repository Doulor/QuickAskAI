@echo off
setlocal enabledelayedexpansion
title QuickAskAI Installer

:: Self-elevate if not running as admin
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
    echo Installation failed. You can try enabling Developer Mode:
    echo   start ms-settings:developers
    echo Then run this script again.
    echo.
    pause
    exit /b 1
)

echo.
echo ============================================
echo   QuickAskAI installed.
echo.
echo   Open PowerToys Command Palette,
echo   type Reload, then search 快速询问AI.
echo ============================================
echo.
pause
