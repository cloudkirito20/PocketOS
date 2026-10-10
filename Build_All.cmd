@echo off
setlocal EnableExtensions
cd /d "%~dp0"
title LazyTech PocketOS - Build Installer Manager Bridge
where dotnet >nul 2>&1 || (echo ERROR: .NET 10 SDK required. & pause & exit /b 1)
set "STAMP=%RANDOM%_%RANDOM%"
set "RELEASE=%~dp0Releases\PocketOS_%STAMP%"
set "APP=%RELEASE%\Manager"
mkdir "%APP%" >nul 2>&1
echo [1/3] Building Manager...
dotnet publish "%~dp0Windows\PocketOSManager.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -o "%APP%"
if errorlevel 1 goto failed
echo [2/3] Building Bridge...
dotnet publish "%~dp0Bridge\PokemonBridge.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -o "%APP%"
if errorlevel 1 goto failed
copy /y "%~dp0Install_Update.ps1" "%APP%\Install_Update.ps1" >nul
if not exist "%APP%\PokemonBridge.exe" goto failed
if not exist "%APP%\PocketOSManager.exe" goto failed
powershell -NoProfile -Command "Compress-Archive -Path '%APP%\*' -DestinationPath '%RELEASE%\PocketOS_Update.zip' -Force"
if errorlevel 1 goto failed
if exist "%~dp0ESP32\Release\PocketOS_ESP32.bin" copy /y "%~dp0ESP32\Release\PocketOS_ESP32.bin" "%RELEASE%\PocketOS_ESP32.bin" >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Create_Release_Manifest.ps1" -ReleaseDirectory "%RELEASE%"
if errorlevel 1 goto failed
echo [3/3] Building installer...
dotnet publish "%~dp0Installer\PocketOSInstaller.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -o "%RELEASE%"
if errorlevel 1 goto failed
echo BUILD SUCCESSFUL: %RELEASE%
echo Upload release assets to GitHub Releases.
start "" "%RELEASE%"
pause
exit /b 0
:failed
echo BUILD FAILED
pause
exit /b 1
