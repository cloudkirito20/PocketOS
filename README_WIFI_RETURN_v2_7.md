# PocketOS v2.7 — Optional Wi-Fi Restoration

Wi-Fi functionality restored from earlier v2.0 ESP32 firmware and added as a manual Windows Wi-Fi tab.

**Safety:** Previous logs proved a brownout during WIFI_INIT. Keeping Wi-Fi off at boot prevents a repeat startup loop, but manual Wi-Fi scan/connect can still trigger the brownout. If it does, leave Wi-Fi off and use the stable USB mode; this cannot be corrected solely by software. Brownout protection remains enabled.

In Windows: Settings & Tools > Wi-Fi (Manual). Choose USB COM port in Settings, close Arduino Serial Monitor, then scan/join. No passwords are stored by the Windows UI.

ESP32 sketch: ESP32/pokemon_partner_encounter/pokemon_partner_encounter.ino
Windows: Windows/Program.cs

The prior v2.0 firmware includes HTTP endpoints for compatibility with the legacy bridge. It is not guaranteed to have the exact v2.1 no-Wi-Fi app menu layout; hardware compilation and testing are required.

This source package has not been compiled or tested on an ESP32 or Windows here.
