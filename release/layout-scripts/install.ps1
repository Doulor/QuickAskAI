$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifest = Join-Path $scriptRoot "AppxManifest.xml"

if (-not (Test-Path -LiteralPath $manifest)) {
    Write-Error "AppxManifest.xml was not found next to install.ps1. Please extract the release zip before installing."
}

Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force

$existing = Get-AppxPackage -Name AIExtension
if ($existing) {
    Remove-AppxPackage -Package $existing.PackageFullName
}

Add-AppxPackage -Register $manifest -ForceApplicationShutdown -ForceUpdateFromAnyVersion

Write-Host "快速询问AI installed." -ForegroundColor Green
Write-Host "Open PowerToys Command Palette, reload extensions if needed, then search for 快速询问AI." -ForegroundColor Green
