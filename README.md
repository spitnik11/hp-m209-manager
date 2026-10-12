# HP M209 Manager

A small Windows desktop utility for the HP LaserJet M209dw. It provides:

- live online/offline tracking;
- encrypted Bluetooth Wi-Fi provisioning using HP's installed setup components;
- automatic Windows printer/port repair after an address change;
- one-click Windows test pages;
- one-click access to the printer's web settings.

## Privacy

The Wi-Fi key is read from the active Windows Wi-Fi profile, held only in memory, and passed directly to the local connector. The app never displays, logs, or saves it. Only the printer's local IP address is saved under `%LOCALAPPDATA%\HP M209 Manager`.

## Requirements

- Windows 10 or 11, x64
- HP Smart installed (the connector uses HP's installed BLE protocol assemblies)
- HP LaserJet M207–M212 driver installed
- Bluetooth and Wi-Fi enabled

## Build

Run `Build.ps1` in PowerShell. It locates the current HP Smart installation, copies the required assemblies into the ignored local build folder, builds both projects, and creates `dist`.

Run `Install.ps1` to copy the built app to `%LOCALAPPDATA%\Programs\HP M209 Manager` and create a desktop shortcut.

## Printer confirmation

Put the printer in purple setup mode before reconnecting. When the app asks, press **Information + Continue** together once, then release them.

This project is independent and is not affiliated with or endorsed by HP. HP binaries are not included in this repository.
