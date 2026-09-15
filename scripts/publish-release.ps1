# Publish MacroForge portable zip + styled Setup.exe + SHA256SUMS.txt
# Usage: pwsh ./scripts/publish-release.ps1

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$payloadDir = Join-Path $root "artifacts\payload"
$setupPayload = Join-Path $root "tools\MacroForge.Setup\Payload"
$releaseDir = Join-Path $root "artifacts\release"
$zipPath = Join-Path $setupPayload "app.zip"
$portableZip = Join-Path $releaseDir "MacroForge-win-x64.zip"

Write-Host "==> Publishing MacroForge app"
if (Test-Path $payloadDir) { Remove-Item $payloadDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null
dotnet publish "src\MacroForge.App\MacroForge.App.csproj" `
  -c Release -r win-x64 --self-contained false `
  -o $payloadDir --nologo
if ($LASTEXITCODE -ne 0) { throw "App publish failed" }

Write-Host "==> Packing payload zip"
New-Item -ItemType Directory -Force -Path $setupPayload | Out-Null
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $payloadDir "*") -DestinationPath $zipPath -Force

Write-Host "==> Building installer (self-contained single file)"
$setupOut = Join-Path $root "artifacts\setup-build"
if (Test-Path $setupOut) { Remove-Item $setupOut -Recurse -Force }
dotnet publish "tools\MacroForge.Setup\MacroForge.Setup.csproj" `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $setupOut --nologo
if ($LASTEXITCODE -ne 0) { throw "Setup publish failed" }

Write-Host "==> Preparing release folder"
if (Test-Path $releaseDir) { Remove-Item $releaseDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
Copy-Item (Join-Path $root "artifacts\setup-build\MacroForge-Setup.exe") (Join-Path $releaseDir "MacroForge-Setup.exe") -Force
if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
Compress-Archive -Path (Join-Path $payloadDir "*") -DestinationPath $portableZip -Force

Write-Host "==> Writing SHA256SUMS.txt"
$sums = Join-Path $releaseDir "SHA256SUMS.txt"
$lines = @()
foreach ($name in @("MacroForge-Setup.exe", "MacroForge-win-x64.zip")) {
  $file = Join-Path $releaseDir $name
  $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
  $lines += "$hash  $name"
  Write-Host ("  {0}  {1}" -f $hash, $name)
}
Set-Content -Path $sums -Value ($lines -join "`n") -Encoding utf8

Write-Host ""
Write-Host "Release files ready in artifacts\release\"
Write-Host "Upload to GitHub Release:"
Write-Host "  - MacroForge-Setup.exe"
Write-Host "  - MacroForge-win-x64.zip"
Write-Host "  - SHA256SUMS.txt"
