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