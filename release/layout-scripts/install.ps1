$ErrorActionPreference = "Stop"

# Pause before exit so the user can read any error message
$script:exitCleanly = $false
try {
    $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
    $msixPath = Join-Path $scriptRoot "QuickAskAI.msix"
    $cerPath = Join-Path $scriptRoot "QuickAskAI.cer"

    if (-not (Test-Path -LiteralPath $msixPath)) {
        Write-Host "QuickAskAI.msix was not found next to install.ps1." -ForegroundColor Red
        Write-Host "Please extract the entire release zip before running this script." -ForegroundColor Red
        exit 1
    }

    if (-not (Test-Path -LiteralPath $cerPath)) {
        Write-Host "QuickAskAI.cer was not found next to install.ps1." -ForegroundColor Red
        Write-Host "Please extract the entire release zip before running this script." -ForegroundColor Red
        exit 1
    }

    Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force

    $existing = Get-AppxPackage -Name AIExtension
    if ($existing) {
        Remove-AppxPackage -Package $existing.PackageFullName
    }

    # Install the signing certificate to the machine's Trusted Root store.
    # This requires administrator privileges the first time.
    # After the certificate is trusted, future installs do not need admin.
    Write-Host "Installing signing certificate..." -ForegroundColor Cyan
    $certResult = certutil -addstore Root $cerPath 2>&1
    $certOk = $LASTEXITCODE -eq 0
    if (-not $certOk) {
        # Check if cert is already trusted
        $certResult = certutil -verifystore Root QuickAskAI 2>&1
        $certOk = $LASTEXITCODE -eq 0
    }

    if (-not $certOk) {
        Write-Host ""
        Write-Host "Unable to install the signing certificate." -ForegroundColor Yellow
        Write-Host "The certificate must be in the machine Trusted Root store for MSIX sideloading." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Please right-click PowerShell and choose Run as Administrator," -ForegroundColor Yellow
        Write-Host "then run this script again." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Or enable Developer Mode as a fallback:" -ForegroundColor Yellow
        Write-Host "  start ms-settings:developers" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "Installing QuickAskAI..." -ForegroundColor Cyan
    try {
        Add-AppxPackage -Path $msixPath -ForceApplicationShutdown -ForceUpdateFromAnyVersion
    }
    catch {
        $message = $_.Exception.Message
        Write-Host ""
        if ($message -match "0x800B0109" -or $message -match "untrusted" -or $message -match "not trusted") {
            Write-Host "Windows still does not trust the signing certificate." -ForegroundColor Red
            Write-Host "Please run this PowerShell as Administrator to install the certificate:" -ForegroundColor Yellow
            Write-Host "  certutil -addstore Root `"$cerPath`"" -ForegroundColor Yellow
            Write-Host "Then run install.ps1 again." -ForegroundColor Yellow
        }
        elseif ($message -match "0x80073D0D" -or $message -match "blocked by AppLocker") {
            Write-Host "This PC has blocked sideloading." -ForegroundColor Red
            Write-Host "Please check your organization's AppLocker policy." -ForegroundColor Red
        }
        elseif ($message -match "0x80073CFF" -or $message -match "developer license") {
            Write-Host "This PC does not allow sideloading without Developer Mode." -ForegroundColor Red
            Write-Host "Enable Developer Mode and try again:" -ForegroundColor Yellow
            Write-Host "  start ms-settings:developers" -ForegroundColor Yellow
        }
        else {
            Write-Host "Installation failed:" -ForegroundColor Red
            Write-Host $message -ForegroundColor Red
        }
        exit 1
    }

    Write-Host ""
    Write-Host "QuickAskAI installed." -ForegroundColor Green
    Write-Host "Open PowerToys Command Palette, run Reload, then search for QuickAskAI." -ForegroundColor Green
    $script:exitCleanly = $true
}
finally {
    if (-not $script:exitCleanly) {
        Write-Host ""
        Write-Host "Press Enter to close this window..." -ForegroundColor Gray
        Read-Host
    }
}
