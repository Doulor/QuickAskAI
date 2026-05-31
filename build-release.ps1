param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Version = "beta"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = "dotnet"
$userDotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (Test-Path -LiteralPath $userDotnet) {
    $dotnet = $userDotnet
}

$tfm = "net9.0-windows10.0.26100.0"
$rid = if ($Platform -eq "ARM64") { "win-arm64" } else { "win-x64" }
$project = Join-Path $repoRoot "AIExtension\AIExtension.csproj"
$layout = Join-Path $repoRoot "AIExtension\bin\$Platform\$Configuration\$tfm\$rid"
$releaseRoot = Join-Path $repoRoot "release"
$artifactRoot = Join-Path $releaseRoot "artifacts"
$certDir = Join-Path $releaseRoot "cert"
$pfxPath = Join-Path $certDir "QuickAskAI.pfx"
$cerPath = Join-Path $certDir "QuickAskAI.cer"
$certPassword = "QuickAskAI"
$packageName = "QuickAskAI-$Version-$Platform"
$packageDir = Join-Path $artifactRoot $packageName
$zipPath = Join-Path $artifactRoot "$packageName.zip"
$msixName = "QuickAskAI.msix"
$scriptsDir = Join-Path $releaseRoot "layout-scripts"

Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force

# Generate self-signed certificate if not already present
if (-not (Test-Path -LiteralPath $pfxPath)) {
    New-Item -ItemType Directory -Path $certDir -Force | Out-Null
    $cert = New-SelfSignedCertificate `
        -Type Custom `
        -Subject "CN=QuickAskAI" `
        -KeyUsage DigitalSignature `
        -FriendlyName "QuickAskAI" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3") `
        -KeyExportPolicy Exportable
    $securePassword = ConvertTo-SecureString -String $certPassword -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null
    Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
    Write-Host "Generated self-signed certificate for MSIX signing." -ForegroundColor Green
}

& $dotnet build $project -c $Configuration -p:Platform=$Platform
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not (Test-Path -LiteralPath (Join-Path $layout "AppxManifest.xml"))) {
    Write-Error "AppxManifest.xml was not found at $layout"
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
if (Test-Path -LiteralPath $packageDir) {
    Remove-Item -LiteralPath $packageDir -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

# Build MSIX from the layout
$makeAppx = "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.26100.0\x64\MakeAppx.exe"
$msixPath = Join-Path $packageDir $msixName
& $makeAppx pack /d $layout /p $msixPath /l
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Sign the MSIX using cert from the user's certificate store
$signtool = "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.26100.0\x64\SignTool.exe"
& $signtool sign /fd SHA256 /n "QuickAskAI" /a $msixPath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Copy release files to the package directory
Copy-Item -LiteralPath $cerPath -Destination $packageDir -Force
Copy-Item -LiteralPath (Join-Path $scriptsDir "install.ps1") -Destination $packageDir -Force
Copy-Item -LiteralPath (Join-Path $scriptsDir "uninstall.ps1") -Destination $packageDir -Force
Copy-Item -LiteralPath (Join-Path $scriptsDir "README-release.md") -Destination (Join-Path $packageDir "README.md") -Force

Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -Force

Write-Host "Release artifact created:" -ForegroundColor Green
Write-Host $zipPath -ForegroundColor Green
