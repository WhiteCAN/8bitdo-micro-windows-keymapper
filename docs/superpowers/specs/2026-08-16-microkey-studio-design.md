# MicroKey Studio Design

Date: 2026-08-16

## Summary

MicroKey Studio is a C#/.NET 8 WPF Windows application for configuring the 8BitDo Micro controller's onboard key mappings. The project is intended to be open source and portfolio-friendly: the app should be usable by non-technical Windows users, while the repository documents the AI-assisted reverse-engineering workflow used to discover the device protocol.

The app targets device-level configuration, not PC-only remapping. A mapping written by MicroKey Studio should remain stored on the 8BitDo Micro after the app exits, matching the behavior of the official mobile app.

## Goals

- Provide a polished Windows UI for 8BitDo Micro key mapping.
- Connect to the 8BitDo Micro over BLE using Windows APIs.
- Read the current device configuration when protocol support is available.
- Write edited mappings back to the device.
- Support official-app-level scope over time: keyboard keys, shortcuts, mouse actions, media keys, profiles, macro editing, reset, and import/export.
- Keep protocol research, packet captures, and implementation notes well documented.
- Avoid committing private raw captures, APKs, local device identifiers, or phone-specific data.

## Non-Goals

- Support every 8BitDo controller in the first public release.
- Replace firmware update tooling.
- Include extracted official APKs, proprietary binaries, or raw personal logs in the public repository.
- Provide PC-only input remapping as the primary feature.

## Technical Baseline

Local analysis has identified the official app package as `com.abitdo.advance`. The Android app uses a native library named `libadvance-lib.so` to build Micro configuration packets, then sends those bytes through `SHBLEUtils.writeBLE(byte[])`.

Observed public protocol details:

- Device family/name observed: 8BitDo Micro BLE app mode, advertising name pattern such as `80EL`.
- Primary GATT service: `0000FF10-0000-1000-8000-00805F9B34FB`.
- Write/notify characteristic observed: `0000FF13-0000-1000-8000-00805F9B34FB`.
- Official app logs include `writeBleHidData:` entries that expose write payloads.
- `MicroUI` builds a 20-button mapping array where each logical mapping entry occupies 4 integer-like slots.
- Native functions such as `writeMicroCustomConfig`, `readMicroCustomConfig`, `setMicroReportEnable`, and `readVersion_Micro` coordinate configuration flows.

These details are acceptable to document publicly because they describe observed protocol behavior, not private account secrets. Raw logs and full APK extracts remain local-only.

## Sensitive/Private Data Handling

Do not commit:

- Raw APK files or extracted APK trees.
- Raw `adb logcat` captures.
- Raw Bluetooth HCI captures.
- Phone serial numbers.
- Bluetooth MAC addresses.
- Personal file paths from the user's machine.
- Any file under `docs/private/`.

Public docs may include:

- Sanitized BLE UUIDs.
- Sanitized packet examples with device addresses removed.
- High-level analysis notes.
- Reproducible research steps using public tools.

A private local note may store exact captured values needed during development, but it must stay ignored by git.

## Architecture

The solution will use C#/.NET 8 and WPF.

```text
MicroKeyStudio/
  src/
    MicroKeyStudio.App/
    MicroKeyStudio.Ble/
    MicroKeyStudio.Protocol/
    MicroKeyStudio.Storage/
  tests/
    MicroKeyStudio.Protocol.Tests/
    MicroKeyStudio.Storage.Tests/
  docs/
    protocol-notes.md
    capture-analysis.md
    superpowers/specs/
  tools/
    LogcatParser/
```

### MicroKeyStudio.App

WPF application using MVVM. It owns screens, dialogs, commands, validation messages, and user interaction state. It should not directly build protocol bytes or talk to Windows BLE APIs.

Main views:

- Device connection view: scan, connect, disconnect, signal/status, firmware/version when known.
- Mapping editor: visual Micro button list, selected action editor, conflict indicators.
- Profile manager: local profile save/load, duplicate, rename, import/export.
- Macro editor: record-like ordered steps, delays, key up/down events, validation.
- Protocol/debug panel: optional developer view for packet trace and experimental write/read flows.
- Settings/about: safety notice, project links, acknowledgements, diagnostic export.

### MicroKeyStudio.Ble

Windows BLE layer. It wraps `Windows.Devices.Bluetooth` and `Windows.Devices.Bluetooth.GenericAttributeProfile`.

Responsibilities:

- Discover candidate Micro devices.
- Connect by device id or BLE address.
- Resolve configured service and characteristic UUIDs.
- Subscribe to notifications.
- Write byte payloads with pacing and retry policy.
- Surface structured connection events to the app.

The BLE layer must be replaceable in tests with a fake transport.

### MicroKeyStudio.Protocol

Protocol and mapping model. This is the most important boundary.

Responsibilities:

- Represent Micro buttons and target actions.
- Convert UI mappings into protocol records.
- Parse known responses where possible.
- Generate read, write, save, report-enable, and reset packets.
- Keep captured packet fixtures for tests in sanitized form.

Initial implementation can replay known captured sequences behind an experimental flag. As the packet format is decoded, replay should be replaced with generated packets.

### MicroKeyStudio.Storage

Local profile persistence.

Responsibilities:

- Store profiles as readable JSON.
- Version profile schema.
- Validate imported profile files.
- Keep protocol-level bytes out of user profile files unless explicitly exported as diagnostics.

## Data Model

Primary concepts:

- `MicroButton`: physical source button on the device.
- `MappedAction`: target behavior. Variants include keyboard key, key chord, mouse action, media key, macro, disabled, and pass-through.
- `MacroStep`: key down, key up, mouse action, delay, or text input expansion if later supported.
- `MappingProfile`: named collection of button mappings plus metadata.
- `DeviceSession`: current BLE connection, discovered capabilities, firmware/version, and sync state.

Profiles should be user-readable JSON so GitHub users can share mapping presets.

## Protocol Flow

The app should treat device writes as a state machine:

1. Connect to the BLE device.
2. Set MTU where Windows supports it or adapt write chunking to the negotiated size.
3. Resolve service and characteristic UUIDs.
4. Subscribe to notifications.
5. Enable Micro reports.
6. Optionally read current configuration.
7. Write mapping data frames.
8. Send save/commit command.
9. Wait for acknowledgement or timeout.
10. Report success, partial success, or failure.

Observed official app behavior includes retries and occasional write failures that are later followed by successful responses. The Windows implementation should be tolerant: serialize writes, wait for acknowledgements where understood, and expose detailed diagnostics.

## Error Handling

User-facing errors should be specific and actionable:

- Device not found.
- Device found but service/characteristic missing.
- Notifications unavailable.
- Write timed out.
- Device rejected configuration.
- Protocol support incomplete for the selected action type.

Diagnostic logs should include sanitized packet direction, command id, length, and status, but not local private device identifiers by default.

## Testing

Protocol tests:

- Known packet fixtures generate expected bytes.
- Mapping entries encode consistently.
- Invalid mappings fail validation.
- Profile schema migration works.

BLE tests:

- Fake transport records write ordering.
- Retry and timeout behavior is deterministic.
- Notification parsing handles unknown packets safely.

App tests:

- View models validate incomplete mappings.
- Profile import/export round-trips.
- Save command is disabled when no device is connected.

Manual hardware verification:

- Connect to a real 8BitDo Micro.
- Read version/config where supported.
- Write one known-good mapping.
- Confirm the mapping persists after disconnect/reconnect.
- Confirm reset restores default behavior.

## Public Roadmap

Phase 1: project scaffold and BLE connection.

Phase 2: replay captured known-good write sequence from a developer-only screen.

Phase 3: decode keyboard/mouse/media mapping records and generate packets.

Phase 4: profile editor and local JSON profile storage.

Phase 5: macro editor.

Phase 6: reset/readback support and release packaging.

Phase 7: documentation polish, screenshots, signed release artifacts if practical.

## GitHub Presentation

README should emphasize:

- Windows key mapping tool for 8BitDo Micro.
- Unofficial, community research project.
- C#/.NET 8 WPF app.
- BLE protocol research from sanitized logs and APK analysis.
- AI-assisted development workflow.
- Clear safety notice: experimental writes can change device configuration.

Repository should include sanitized examples only. Exact local captures belong in ignored private notes.

## Open Questions

- Exact packet checksum/hash fields for generated mapping frames.
- Full action code table for keyboard, mouse, media, and macros.
- Whether Windows BLE write behavior needs explicit chunking for this device.
- Whether the same UUIDs apply across all 8BitDo Micro firmware versions.
- How reliable readback is compared with write-only configuration.

## Approval

This design assumes the app name is `MicroKey Studio`, the repository name is `microkey-studio`, and the implementation language is C# only.
