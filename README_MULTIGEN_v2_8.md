# PocketOS v2.8 - Multi-generation Studio UI

## Included
- Generation selector covering generations 1 through 9 in the embedded Pokemon Studio.
- Cleaner read-only save details: file name, size, modified date; no raw hex dump by default.
- Existing experimental raw Gen 4 frame explorer explicitly labeled Advanced and Gen 4 only.
- All Windows USB/OLED, Wi-Fi, ESP32 firmware and startup assets carried forward unchanged.

## Not implemented
- No PKHeX.Core integration, save decoding or editing, legality engine, or generation-specific PokéFinder algorithms. Selecting a generation does not parse or validate the save.
- No claims of a full multi-generation editor are made.

## Build
Run `Windows/Publish_EXE.cmd` on Windows with .NET 10 SDK. Close earlier app instances before building and launch the newly published executable.

## Verification
ZIP/source inspection only. This environment has not compiled or tested the Windows executable or ESP32 firmware.
