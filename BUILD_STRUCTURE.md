# PocketOS v3.4.0 Build Structure

Solution:

PocketOS.sln

Projects:

- Windows/PocketOSManager.csproj
- Bridge/PokemonBridge.csproj
- PocketOS.Updater/PocketOS.Updater.csproj
- Installer/LazyTech.PocketOS.Installer.csproj

Release output:

Release/
- PocketOSManager.exe
- PokemonBridge.exe
- PocketOS.Updater.exe
- LazyTech.PocketOS.Installer.exe

Before release:
- Build Release configuration
- Verify references
- Test installer
