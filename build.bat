@echo off
echo Building Kamla Accessibility Mod...
setlocal
set "GAME_DIR=%~1"
if not defined GAME_DIR set "GAME_DIR=D:\game\Kamla"
dotnet build "%~dp0KamlaAccessibility.csproj" -c Release "-p:GameDir=%GAME_DIR%"
if errorlevel 1 exit /b %ERRORLEVEL%
echo Build succeeded! Mod deployed to "%GAME_DIR%\Mods".
