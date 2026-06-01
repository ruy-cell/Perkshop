$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
dotnet build .\PerkShop.csproj -c Release
$src = Join-Path $PSScriptRoot 'bin\Release\net6.0\PerkShop.dll'
if (-not (Test-Path $src)) { throw "PerkShop.dll not found at $src" }
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item $src (Join-Path $dist 'PerkShop.dll') -Force
Write-Host "Done. DLL copied to $dist\PerkShop.dll"
