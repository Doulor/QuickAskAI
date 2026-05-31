$ErrorActionPreference = "Stop"

function Test-SideloadingEnabled {
    $key = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"
    try {
        $value = Get-ItemProperty -Path $key -Name AllowAllTrustedApps -ErrorAction Stop
        return $value.AllowAllTrustedApps -eq 1
    }
    catch {
        return $false
    }
}

function Write-SideloadingHelp {
    Write-Host "Windows sideloading may be disabled on this PC." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "This is unusual on Windows 10 version 1903 or later." -ForegroundColor Yellow
    Write-Host "Please check your organization's device policy, then try again." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "You can also enable Developer Mode as a workaround:" -ForegroundColor Yellow
    Write-Host "start ms-settings:developers" -ForegroundColor Yellow
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$msixPath = Join-Path $scriptRoot "QuickAskAI.msix"
$cerPath = Join-Path $scriptRoot "QuickAskAI.cer"

if (-not (Test-Path -LiteralPath $msixPath)) {
    Write-Error "QuickAskAI.msix was not found next to install.ps1. Please extract the release zip before installing."
}

if (-not (Test-Path -LiteralPath $cerPath)) {
    Write-Error "QuickAskAI.cer was not found next to install.ps1. Please extract the release zip before installing."
}

Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force

$existing = Get-AppxPackage -Name AIExtension
if ($existing) {
    Remove-AppxPackage -Package $existing.PackageFullName
}

# Import the self-signed certificate into the user's Trusted People store
# This is required so Windows trusts the signed MSIX for sideloading
try {
    certutil -user -addstore "TrustedPeople" $cerPath *> $null
}
catch {
    Write-Error "Unable to import the signing certificate. Please run this script as the current user (not as administrator from a different account)."
}

try {
    Add-AppxPackage -Path $msixPath -ForceApplicationShutdown -ForceUpdateFromAnyVersion
}
catch {
    $message = $_.Exception.Message
    if ($message -match "0x80073D0D" -or $message -match "blocked by AppLocker") {
        Write-Host "This PC has blocked sideloading of unsigned or untrusted packages." -ForegroundColor Red
        throw
    }

    if ($message -match "0x80073CFF" -or $message -match "developer license" -or $message -match "sideload") {
        Write-SideloadingHelp
        throw
    }

    throw
}

Write-Host "快速询问AI installed." -ForegroundColor Green
Write-Host "Open PowerToys Command Palette, reload extensions if needed, then search for 快速询问AI." -ForegroundColor Green
