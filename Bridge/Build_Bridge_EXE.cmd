@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: .NET 10 SDK is missing or dotnet is not on PATH.
  echo Install .NET 10 SDK, then try again.
  pause
  exit /b 1
)
set "TARGET=Build_Output\Build_%RANDOM%_%RANDOM%"
echo.
echo Building Pokemon Partner Bridge for Windows x64...
echo Restoring PKHeX.Core from NuGet requires an Internet connection on first build.
echo.
dotnet publish "PokemonBridge.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "%TARGET%"
if errorlevel 1 (
  echo.
  echo BUILD FAILED. Please copy the error messages above.
  pause
  exit /b 1
)
echo.
echo BUILD SUCCESSFUL
if exist "%TARGET%\PokemonBridge.exe" (
  echo EXE: %CD%\%TARGET%\PokemonBridge.exe
  explorer "%TARGET%"
) else (
  echo ERROR: Expected PokemonBridge.exe was not generated.
)
pause
endlocal
