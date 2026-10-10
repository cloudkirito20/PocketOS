# PocketOS v2.6 embedded RNG workspace

The Windows Pokemon Studio is entirely in-window. Its Generation 4 **raw LCRNG** explorer now has nature, shiny (TID/SID), and minimum-IV filters, with CSV export and candidate-count summaries. This is **not** full PokeFinder and does not validate legal encounters. The save inspector remains read-only and is **not** PKHeX.Core. No standalone tool is launched.

Your uploaded `PokeFinder-win.zip` is a binary Qt distribution; the official C++ repository could not be downloaded from this environment. No upstream source has been copied or embedded. Follow its license requirements if source code is incorporated later.

The ESP32 USB-only firmware is unchanged. This project has not been compiled on Windows in this environment; run `Windows/Publish_EXE.cmd` and report any compiler errors.
