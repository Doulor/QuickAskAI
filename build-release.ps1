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
$packageName = "QuickAskAI-$Version-$Platform"
$packageDir = Join-Path $artifactRoot $packageName
$zipPath = Join-Path $artifactRoot "$packageName.zip"
$scriptsDir = Join-Path $releaseRoot "layout-scripts"

Get-Process AIExtension -ErrorAction SilentlyContinue | Stop-Process -Force

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
Get-ChildItem -LiteralPath $layout -Force | Copy-Item -Destination $packageDir -Recurse -Force
Copy-Item -LiteralPath (Join-Path $scriptsDir "install.ps1") -Destination $packageDir -Force
Copy-Item -LiteralPath (Join-Path $scriptsDir "uninstall.ps1") -Destination $packageDir -Force
Copy-Item -LiteralPath (Join-Path $scriptsDir "README-release.md") -Destination (Join-Path $packageDir "README.md") -Force

Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -Force

Write-Host "Release artifact created:" -ForegroundColor Green
Write-Host $zipPath -ForegroundColor Green
