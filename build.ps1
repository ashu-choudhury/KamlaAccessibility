param([string]$GameDir = 'D:\game\Kamla')

$ErrorActionPreference = "Stop"
Write-Host "Building Kamla Accessibility Mod..." -ForegroundColor Cyan

dotnet build (Join-Path $PSScriptRoot 'KamlaAccessibility.csproj') -c Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Build succeeded! Mod deployed to $(Join-Path $GameDir 'Mods')." -ForegroundColor Green
