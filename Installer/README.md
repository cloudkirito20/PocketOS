PocketOS Retro Installer UI v3.4.0

Included files:
- InstallerWindow.xaml
- InstallerWindow.xaml.cs
- InstallerEngine.cs

What changed:
- Monochrome retro WPF installer window
- Animated retro loading indicator during installation
- Integrated online installation flow
- Downloads Manager, Bridge, Updater, and firmware into:
  %LOCALAPPDATA%\LazyTech\PocketOS

Manifest behavior:
- Reads:
  https://raw.githubusercontent.com/cloudkirito20/PocketOS/main/Releases/v3.4.0-RC1/pocketos-manifest.json
- Supports either:
  1) direct URLs in the manifest, or
  2) package filenames that map to GitHub release assets

Recommended manifest format:
{
  "Version": "3.4.0-RC1",
  "ManagerPackage": "PocketOSManager.zip",
  "BridgePackage": "PocketOS.Bridge.zip",
  "UpdaterPackage": "PocketOS.Updater.zip",
  "Firmware": "PocketOS_ESP32.bin"
}
