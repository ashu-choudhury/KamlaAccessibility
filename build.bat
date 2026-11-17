@echo off
echo Building Kamla Accessibility Mod...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    exit /b %ERRORLEVEL%
)
echo Build succeeded! Mod deployed to E:\games\Kamla\Kamla\Mods\
