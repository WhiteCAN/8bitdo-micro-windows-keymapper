# MicroKey Studio

[한국어](README.md) | **English**

> [!CAUTION]
> **Warning: this is not fully verified official software.**
>
> This project was developed entirely through vibe coding with OpenAI Codex. The developer has not manually reviewed every line of source code, and no independent security audit or antivirus/malware scan of the distributed files has been completed. Passing automated tests and providing a SHA-256 checksum do not guarantee that the program is safe.
>
> This program directly reads and writes device settings. Unexpected behavior or configuration loss may occur. Start with test mappings, keep the official mobile app available for recovery, and use the program at your own risk.

MicroKey Studio is an unofficial Windows key mapping tool for the 8BitDo Micro.

The project is built with C#/.NET 8 and WPF. This preview supports reading device settings, editing local profiles, and generated writes to the currently connected device with backup and readback safeguards. Device save has been hardware-checked; final hardware verification of restore remains.

## Download

[Download MicroKey Studio 0.1.0-preview.31 for Windows x64 (portable ZIP)](https://github.com/WhiteCAN/8bitdo-micro-windows-keymapper/releases/download/v0.1.0-preview.31/MicroKeyStudio-v0.1.0-preview.31-2026-08-19-win-x64-portable.zip)

No installation is required. Extract the ZIP and run `MicroKeyStudio.App.exe`. Windows SmartScreen may display a warning.

- [Release notes](https://github.com/WhiteCAN/8bitdo-micro-windows-keymapper/releases/tag/v0.1.0-preview.31)
- SHA-256: `3766AA0ADB47E9C1B33375F69687A8BF6A906AC88E0ED2F235FD41BDB37762DF`

## Status

Current version: `0.1.0-preview.31` released on `2026-08-19`.

Early development. See [feature-status.md](docs/feature-status.md) for the working, conditional, local-only, and not-yet-implemented features, and [CHANGELOG.md](CHANGELOG.md) for version history.

## Safety

This is an experimental community project. Writing unsupported configuration data to a device can change its behavior. Use test mappings first and keep the official mobile app available for recovery or reset.

- A device save starts with a fresh configuration read, then performs read-modify-write so unchanged configuration bytes are preserved.
- The pre-save configuration is backed up to `Documents/MicroKeyStudio/backups` before confirmation. Restore uses the latest valid backup and is not a factory reset.
- Each save and restore requires a fresh complete configuration readback. A save rejects incomplete readback or a mismatch in a requested mapping; a restore requires the complete readback to match the backup byte-for-byte.
- Device writes apply to the active configuration of the connected device. Device-side profile names and profile selection are not decoded, so choosing among device profiles is not supported.

## UI Features

- Korean and English language setting choices.
- Find device, populate profile list, then load selected profile workflow.
- Device find, profile selector, profile count, and Load profile controls are grouped together.
- Local profile create, rename, delete, and load controls.
- Device reads update a temporary-named device-stored profile. Profile-name decoding has not been identified yet.
- Local profiles are saved to the user's Documents `MicroKeyStudio/profiles.json` file.
- Clickable mapping chips with an edit panel for selected button actions.
- Official-app-style key picker categories plus a freeform chord field for actions such as `Ctrl+C`.
- Above-image connector lines and button-center endpoints that precisely identify the physical button for each mapping row.
- Device-read Disable Sleep state with guarded save and fresh readback verification.
- Automatic backup before saving and fresh readback verification after saving.

## Local Files That Must Stay Private

The following files can help with troubleshooting but may contain device configuration or user-defined names. Do not upload them unchanged to a GitHub repository, Issue, or Discussion.

- `Documents/MicroKeyStudio/last-load-diagnostics.txt`: may contain a device name, profile name, and raw notification packets.
- `Documents/MicroKeyStudio/backups/micro-config-backup-*.json`: contains a complete 180-byte device configuration backup.
- `Documents/MicroKeyStudio/profiles.json`: contains user-created local profile names and key mappings.
- Android bug reports, HCI/BLE logs, APKs, decompiled output, and phone or Bluetooth identifiers.

When reporting an error, share only the status text after removing device names, personal paths, and raw HEX values.

## Assets

Product imagery used in the app is documented in [docs/asset-sources.md](docs/asset-sources.md).
