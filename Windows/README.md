# PocketOS Manager v1.6 — Full-screen studio intro

- A **full-screen black studio intro** launches before MainWindow is constructed.
- A Poké Ball capture sequence opens to reveal the **actual user-supplied LazyTech logo**, converted to monochrome.
- After ~5 seconds the intro closes and the simplified Game Boy-style dashboard opens.
- Escape skips the intro. The splash is no longer inside the dashboard.
- Existing D-pad and Enter/Backspace controls, OLED mirror, Wi-Fi, BIN firmware updater and settings are retained.
- `Publish_EXE.cmd` creates the single-file Windows EXE in `Build_Output`.

## Test
1. Extract to a NEW folder.
2. Double-click `Windows/Publish_EXE.cmd`.
3. Run `Windows/Build_Output/PocketOSManager.exe`.
4. Confirm **full-window splash first**, then **dashboard**; if the old tabbed GUI opens, check the launched EXE location.

*Source-only beta. Windows/.NET build and ESP32 hardware not verified here.*
