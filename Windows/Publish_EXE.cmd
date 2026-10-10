@echo off
setlocal
cd /d "%~dp0"
echo ==============================================
echo POCKET OS MANAGER - ONE CLICK EXE BUILD
echo ==============================================
where dotnet >nul 2>&1
if errorlevel 1 (
  echo .NET 10 SDK not found.
  pause
  exit /b 1
)
set "STAMP=%RANDOM%_%RANDOM%"
set "OUTPUT=Build_Output\Build_%STAMP%"
if not exist "%OUTPUT%" mkdir "%OUTPUT%"
dotnet publish "PocketOSManager.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o "%OUTPUT%"
if errorlevel 1 (
  echo BUILD FAILED - see messages above.
  pause
  exit /b 1
)
echo EXE READY: %~dp0%OUTPUT%\PocketOSManager.exe
start "" "%~dp0%OUTPUT%"
pause
