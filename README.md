# MicroKey Studio

MicroKey Studio is an unofficial Windows key mapping tool for the 8BitDo Micro.

The project is built with C#/.NET 8 and WPF. This preview supports reading device settings, editing local profiles, and generated writes to the currently connected device with backup and readback safeguards. Device save has been hardware-checked; final hardware verification of restore remains.

## Status

Current version: `0.1.0-preview.31` released on `2026-08-19`.

Early development. See [feature-status.md](docs/feature-status.md) for the working, conditional, local-only, and not-yet-implemented features, and [CHANGELOG.md](CHANGELOG.md) for version history.

## Safety

This is an experimental community project. Writing unsupported configuration data to a device can change its behavior. Use test mappings first and keep the official mobile app available for recovery or reset.

- A device save starts with a fresh configuration read, then performs read-modify-write so unchanged configuration bytes are preserved.
- The pre-save configuration is backed up to `Documents/MicroKeyStudio/backups` before confirmation. Restore uses the latest valid backup and is not a factory reset.
- Each save and restore requires a fresh complete configuration readback. A save rejects incomplete readback or a mismatch in a requested mapping; a restore requires the complete readback to match the backup byte-for-byte.
- Device writes apply to the active configuration of the connected device. Device-side profile names and profile selection are not decoded, so choosing among device profiles is not supported.

## Architecture

- `MicroKeyStudio.App`: WPF UI.
- `MicroKeyStudio.Ble`: Windows BLE transport.
- `MicroKeyStudio.Protocol`: protocol constants, packet fixtures, mapping model.
- `MicroKeyStudio.Storage`: local profile storage.

## UI Features

- Korean and English language setting choices.
- Find device, populate profile list, then load selected profile workflow.
- Device find, profile selector, profile count, and Load profile controls are grouped together.
- Visible profile count below the profile selector for load diagnostics.
- BLE service and characteristic discovery diagnostics when Windows cannot find the Micro service.
- Fallback service discovery when Windows lists the Micro service but UUID-specific lookup returns empty.
- Successful profile loads also write diagnostics so received config pages can be inspected.
- Local profile create, rename, delete, and load controls.
- Device reads update a temporary-named device-stored profile. Profile-name decoding has not been identified yet.
- Local profiles are saved to the user's Documents `MicroKeyStudio/profiles.json` file.
- Clickable mapping chips with an edit panel for selected button actions.
- Official-app-style key picker categories plus a freeform chord field for actions such as `Ctrl+C`.
- Raw source slot labels under mapping chips so physical-button order can be calibrated against official mobile app settings.
- Above-image connector lines and button-center endpoints that precisely identify the physical button for each mapping row.
- Device-read Disable Sleep state with guarded save and fresh readback verification.

## Portable Release

Build a no-installer Windows package:

```powershell
.\scripts\package-portable.ps1
```

The script creates:

- `artifacts/publish/MicroKeyStudio-v0.1.0-preview.31-YYYY-MM-DD-win-x64-portable/`: portable app folder.
- `artifacts/releases/MicroKeyStudio-v0.1.0-preview.31-YYYY-MM-DD-win-x64-portable.zip`: upload this zip to GitHub Releases.

Users can extract the zip and run `MicroKeyStudio.App.exe`. No separate .NET install is required because the package is self-contained. The zip also includes `VERSION`, `CHANGELOG.md`, and `CHANGELOG.ko.md` so users can check release date and changes offline.

## Protocol Notes

Observed public Micro app-mode UUIDs:

- Service: `0000FF10-0000-1000-8000-00805F9B34FB`
- Write/notify characteristic: `0000FF13-0000-1000-8000-00805F9B34FB`

Raw captures, APKs, device addresses, and phone identifiers are intentionally excluded from this repository.

### Local Files That Must Stay Private

The following files can help with troubleshooting but may contain device configuration or user-defined names. Do not upload them unchanged to a GitHub repository, Issue, or Discussion.

- `Documents/MicroKeyStudio/last-load-diagnostics.txt`: may contain a device name, profile name, and raw notification packets.
- `Documents/MicroKeyStudio/backups/micro-config-backup-*.json`: contains a complete 180-byte device configuration backup.
- `Documents/MicroKeyStudio/profiles.json`: contains user-created local profile names and key mappings.
- Android bug reports, HCI/BLE logs, APKs, decompiled output, and phone or Bluetooth identifiers.

When reporting an error, share only the status text after removing device names, personal paths, and raw HEX values.

For each 62-byte write page, the CRC is CRC-16 with initial value `0xFFFF` and reflected polynomial `0xA001`. It covers payload bytes 17-61 (45 bytes) and is stored low byte then high byte in bytes 7-8; the page offset in bytes 13-16 is also little-endian. See [docs/capture-analysis.md](docs/capture-analysis.md) for the sanitized implementation facts and verification boundary.

## Assets

Product imagery used in the app is documented in [docs/asset-sources.md](docs/asset-sources.md).
