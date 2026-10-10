PocketOS Pokemon Bridge - automatic emulator detection (beta)

Double-click Build_Bridge_EXE.cmd to build a Windows EXE using the .NET 10 SDK.
Start PocketOS Manager, connect ESP32 over USB, and select the newly built
PokemonBridge.exe in Settings -> Pokemon Partner -> Start Bridge.

On launch, bridge scans running emulators and waits without requiring a
game/emulator selection. Targets: RetroArch gpSP/mGBA/melonDS, standalone
melonDS, and Citra/Azahar/Lime3DS. It can use RetroArch Network Commands
(GET_STATUS) and process window titles to identify the active game.

LIMITS: RetroArch Network Commands should be enabled for reliable automatic
game detection. Detecting a process does not guarantee RAM/encounter-reading
support for that specific emulator or version. The existing bridge's memory
reader has not been expanded to cover new cores; standalone melonDS live
reading is not guaranteed. Citra/Azahar requires its supported RPC endpoint.
If only the emulator process is visible but no game title or supported
content status is available, bridge will wait instead of guessing.

The bridge sends encounter updates to PocketOS Manager on localhost:18765;
PocketOS Manager then forwards via USB. Do not run two Manager instances.
To force a game for diagnosis, use --game heartgold or --game emerald.
No compiler or hardware execution testing was possible in this environment.
