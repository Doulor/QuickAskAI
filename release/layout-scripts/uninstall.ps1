$ErrorActionPreference = "Stop"

Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force

$pkg = Get-AppxPackage -Name AIExtension
if ($pkg) {
    Remove-AppxPackage -Package $pkg.PackageFullName
    Write-Host "快速询问AI uninstalled." -ForegroundColor Green
} else {
    Write-Host "快速询问AI is not installed." -ForegroundColor Yellow
}

# Remove the self-signed certificate from the user's Trusted People store
$cerPath = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "QuickAskAI.cer"
if (Test-Path -LiteralPath $cerPath) {
    try {
        certutil -user -delstore "TrustedPeople" "QuickAskAI" *> $null
    }
    catch {
        # The certificate may already have been removed.
    }
}
