$ErrorActionPreference = "Stop"
Write-Host "Building Kamla Accessibility Mod..." -ForegroundColor Cyan

dotnet build -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Build succeeded! Mod deployed to E:\games\Kamla\Kamla\Mods\" -ForegroundColor Green
