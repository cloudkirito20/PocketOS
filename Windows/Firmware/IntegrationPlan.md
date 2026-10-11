PocketOS ESP32 Firmware Update Integration

Existing:
- BuildFirmware() already exists
- FlashAsync() already exists
- COM detection already exists

Add:

Phase 1
-------
Manifest:
FirmwareUrl
FirmwareSha256

Phase 2
-------
Firmware tab:
[Download Latest BIN]

Downloads to:
%LOCALAPPDATA%\LazyTech\PocketOS\Firmware\PocketOS_ESP32.bin

Phase 3
-------
Verify SHA256

Phase 4
-------
Use existing FlashAsync()

Flow:

Check Updates
      |
      v
Manifest
      |
      v
FirmwareUrl
      |
      v
Download BIN
      |
      v
Verify
      |
      v
Confirm
      |
      v
Flash ESP32

Safety:
Never flash automatically after download.
