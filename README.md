# PocketOS v2.1 - Complete project (USB-only firmware)

This complete source package reconstructs the v2.0 project with the updated v2.1 No-Wi-Fi ESP32 sketch and Windows Program.cs overlaid.

## Windows
- Open `Windows/Publish_EXE.cmd` on Windows with the .NET 10 SDK installed.
- The build output should appear at `Windows/Build_Output/PocketOSManager.exe`.
- This package contains source, not a precompiled EXE.

## ESP32
- Open `ESP32/pokemon_partner_encounter/pokemon_partner_encounter.ino` in Arduino IDE.
- Select the same ESP32 board, partition scheme and OLED wiring as the previously working firmware.
- Compile and upload, using Arduino IDE for flashing when needed.

## Important
- Firmware does not initialize Wi-Fi; attempts to issue old WIFI_* serial commands respond with `WIFI_DISABLED USB_ONLY`.
- The Windows source retains some legacy HTTP and Wi-Fi-related internal code; the removed Wi-Fi UI does not provide a replacement for the old HTTP game-data bridge. Live game data over USB is not implemented by this patch.
- Windows compilation and ESP32 hardware operation have not been verified in this environment.
