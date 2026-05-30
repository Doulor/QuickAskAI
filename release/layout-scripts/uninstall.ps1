$ErrorActionPreference = "Stop"

Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force

$pkg = Get-AppxPackage -Name AIExtension
if ($pkg) {
    Remove-AppxPackage -Package $pkg.PackageFullName
    Write-Host "快速询问AI uninstalled." -ForegroundColor Green
} else {
    Write-Host "快速询问AI is not installed." -ForegroundColor Yellow
}
