$ErrorActionPreference = "Stop"

Set-Location $PSScriptRoot

Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force
git switch main

$env:DOTNET_ROOT = Join-Path $env:USERPROFILE ".dotnet"
$dotnet = Join-Path $env:DOTNET_ROOT "dotnet.exe"
& $dotnet build "AIExtension.sln" --no-restore -c Debug -p:Platform=x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$pkg = Get-AppxPackage -Name AIExtension
if ($pkg) {
    Remove-AppxPackage -Package $pkg.PackageFullName
}

$manifest = Join-Path $PSScriptRoot "AIExtension\bin\x64\Debug\net9.0-windows10.0.26100.0\win-x64\AppxManifest.xml"
Add-AppxPackage -Register $manifest -ForceApplicationShutdown -ForceUpdateFromAnyVersion
Get-AppxPackage -Name AIExtension | Select-Object Name, PackageFullName, InstallLocation

Write-Host "Stable version restored. Reload PowerToys Command Palette." -ForegroundColor Green
