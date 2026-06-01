param(
    [string]$Configuration = "Release",
    [string]$Version = "0.1.2",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

if (-not $SkipBuild) {
    dotnet build .\PerkShop.csproj -c $Configuration
}

$dll = ".\bin\$Configuration\net6.0\PerkShop.dll"
if (-not (Test-Path $dll)) {
    throw "Missing $dll. Build the project first or run without -SkipBuild."
}

if (-not (Test-Path ".\icon.png")) { throw "Missing icon.png." }

# Basic icon validation through .NET. Thunderstore requires 256x256 PNG.
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile((Resolve-Path ".\icon.png"))
try {
    if ($img.Width -ne 256 -or $img.Height -ne 256) {
        throw "icon.png must be 256x256. Found $($img.Width)x$($img.Height)."
    }
}
finally {
    $img.Dispose()
}

$distRoot = ".\dist"
$packageDir = Join-Path $distRoot "package"
$zipPath = Join-Path $distRoot "PerkShop-$Version-Thunderstore.zip"

if (Test-Path $packageDir) { Remove-Item $packageDir -Recurse -Force }
New-Item -ItemType Directory -Path $packageDir | Out-Null

Copy-Item $dll "$packageDir\PerkShop.dll"
Copy-Item ".\manifest.json" "$packageDir\manifest.json"
Copy-Item ".\README.md" "$packageDir\README.md"
Copy-Item ".\CHANGELOG.md" "$packageDir\CHANGELOG.md"
Copy-Item ".\icon.png" "$packageDir\icon.png"

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$packageDir\*" -DestinationPath $zipPath -Force

Write-Host "Created $zipPath"
Write-Host "Package root contains:"
Get-ChildItem $packageDir | ForEach-Object { Write-Host " - $($_.Name)" }
