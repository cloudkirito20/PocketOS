# PocketOS v2.9 - USB-first Pokémon encounter bridge (development source)

This update routes existing live Pokémon detection through a local PC connection and sends the resulting encounter JSON over the same USB COM port used by PocketOS Manager. The ESP32 does not need Wi-Fi for encounter data.

## How to use

1. Compile and flash ESP32/pokemon_partner_encounter/pokemon_partner_encounter.ino. The earlier v2.8 firmware cannot accept POCKET_ENCOUNTER_JSON.
2. Rebuild Windows/PocketOSManager.csproj with Windows/Publish_EXE.cmd, and open the latest generated PocketOSManager.exe.
3. Close Arduino Serial Monitor. In PocketOS Manager, choose the ESP32 COM port under Settings > Pokémon Partner and verify live OLED mirroring.
4. Under Settings > Pokémon Partner, choose your EXISTING compiled Pokémon Partner bridge executable, then click Start Bridge. The Manager asks for the save folder if it hasn't been configured.
5. The original bridge performs the RetroArch/melonDS game-memory detection and sends /state to 127.0.0.1:18765. The Manager receives the data and sends POCKET_ENCOUNTER_JSON over USB. Navigate to Wild Encounter on the ESP32 to view the result. New encounters NEVER auto-open that screen.

Bridge/Program.cs is a copy of the original bridge source for reference. It requires the original bridge project's PKHeX.Core dependencies to compile. No replacement bridge EXE is bundled. You must already have the working compiled bridge executable or compile the original bridge separately. This update does not magically add emulator detection to PocketOSManager.exe.

## Notes

* Windows and ESP32 source were not compiled or tested with hardware in this environment. Treat this as a beta.
* Wi-Fi remains optional and manually enabled; the earlier brownout during WIFI_START is not fixed.
* The local HTTP endpoint is loopback-only, not accessible over the network. Only start a trusted bridge executable.
* The Manager alone opens COM; don't connect Serial Monitor simultaneously.
* The raw USB payload is sent as one JSON line, with an ESP32 input length limit of 4096 bytes.
