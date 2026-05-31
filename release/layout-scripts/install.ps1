$ErrorActionPreference = "Stop"

function Test-DeveloperModeEnabled {
    $key = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"
    try {
        $value = Get-ItemProperty -Path $key -Name AllowDevelopmentWithoutDevLicense -ErrorAction Stop
        return $value.AllowDevelopmentWithoutDevLicense -eq 1
    }
    catch {
        return $false
    }
}

function Write-DeveloperModeHelp {
    Write-Host "This release package is an unsigned appx layout package." -ForegroundColor Yellow
    Write-Host "Windows requires Developer Mode to register it locally." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please enable Developer Mode, then run install.ps1 again:" -ForegroundColor Yellow
    Write-Host "1. Open Windows Settings." -ForegroundColor Yellow
    Write-Host "2. Go to System > For developers." -ForegroundColor Yellow
    Write-Host "3. Turn on Developer Mode." -ForegroundColor Yellow
    Write-Host "4. Run this install.ps1 again." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "You can also open the settings page directly with:" -ForegroundColor Yellow
    Write-Host "start ms-settings:developers" -ForegroundColor Yellow
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifest = Join-Path $scriptRoot "AppxManifest.xml"

if (-not (Test-Path -LiteralPath $manifest)) {
    Write-Error "AppxManifest.xml was not found next to install.ps1. Please extract the release zip before installing."
}

if (-not (Test-DeveloperModeEnabled)) {
    Write-DeveloperModeHelp
    exit 1
}

Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force

$existing = Get-AppxPackage -Name AIExtension
if ($existing) {
    Remove-AppxPackage -Package $existing.PackageFullName
}

try {
    Add-AppxPackage -Register $manifest -ForceApplicationShutdown -ForceUpdateFromAnyVersion
}
catch {
    $message = $_.Exception.Message
    if ($message -match "0x80073CFF" -or $message -match "developer license" -or $message -match "sideload") {
        Write-DeveloperModeHelp
    }

    throw
}

Write-Host "快速询问AI installed." -ForegroundColor Green
Write-Host "Open PowerToys Command Palette, reload extensions if needed, then search for 快速询问AI." -ForegroundColor Green
