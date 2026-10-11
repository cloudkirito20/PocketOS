Generated for existing PocketOSManager Program.cs.

Your current code already has:
- BuildFirmware()
- FlashAsync()
- SerialPort COM selection
- esptool flashing

This package only adds the missing pipeline:
manifest -> download -> verify -> flash.

Integrate into Program.cs instead of replacing the whole file.
