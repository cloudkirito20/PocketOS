# LazyTech PocketOS - GitHub updates

Repository: https://github.com/cloudkirito20/PocketOS

## First commit

`feat: initialize PocketOS with unified installer and GitHub release updates`

## Workflow

1. Commit project sources to GitHub (exclude `bin`, `obj`, `Releases`, and secrets).
2. On Windows with .NET 10 SDK, run `Build_All.cmd` to compile Manager, Bridge, and C# Installer.
3. Create a public GitHub Release tagged `v3.2.0` (or corresponding version).
4. Upload from the build's `Releases/PocketOS_...` folder:
   - `PocketOS_Update.zip` (Manager, Bridge, updater helper)
   - `pocketos-manifest.json` (version and SHA-256 metadata)
   - `PocketOSInstaller.exe` (new users)
   - `PocketOS_ESP32.bin` (optional, only after an independently compiled, board-compatible firmware binary has been placed in `ESP32/Release`)
5. PocketOS Manager checks `/releases/latest` once a day while running, or manually under Settings > Updates.
6. Manager + Bridge update together, and the Manager restarts. ESP32 updates are never automatically flashed: download first, then explicitly flash in Firmware Update.

Note: The first published release must contain a manifest before daily checks can work. Builds must not be installed over currently running executables. A public GitHub repo does not mean its first release exists yet.

Security note: SHA-256 ensures downloaded files match the public manifest. For stronger protection against a compromised GitHub release account, additionally sign release manifests or Windows binaries and validate signatures.

The installer installs per-user under `%LOCALAPPDATA%/Programs/LazyTech/PocketOS`, avoiding administrator rights.

Validation: Sources and ZIP structure checked in build environment; Windows compilation and firmware flashing were not performed here.
