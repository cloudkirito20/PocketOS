# PocketOS Manager + Pokemon Bridge combined build

Double-click **Build_All.cmd** in the project root. This publishes both Windows Manager and Pokemon Bridge using the .NET 10 SDK, copies the bridge alongside the manager, and creates a `PocketOS_Update.zip` in the corresponding `Releases/PocketOS_*` folder.

For first installation, run the `PocketOSManager.exe` in `Releases/PocketOS_*/Manager`, and in Settings > Pokemon Partner choose `PokemonBridge.exe` from the same folder (once).

For subsequent releases, open Settings > Updates > Choose ZIP, select the *new release's* `PocketOS_Update.zip`, then Install Update and Restart. The external PowerShell helper waits for the app to exit before replacing executables. No internet update feed is configured and unknown ZIP files must not be trusted.

**Limitations:** Builds require .NET 10 SDK and internet for NuGet on first use; C# was not compiled in this environment. Existing bridge/emulator compatibility and ESP32 hardware have not been tested. Do not use this installer on a version currently missing `Install_Update.ps1`; start once from the new release's Manager folder.
