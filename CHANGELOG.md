# Changelog

All notable user-facing changes to MicroKey Studio are recorded here.

## [0.1.0-preview.31] - 2026-08-19

### Added

- Implemented Disable Sleep read and save using configuration offset `0x03`, confirmed through repeated official-mobile-app ON/OFF captures.
- Saves the checkbox draft through the same read-modify-write path as mappings, including a pre-save backup and fresh readback verification.
- Reset Changes now restores the Disable Sleep draft to the last device-read value.
- Added tests for flag decoding, CRC regeneration, ViewModel save and mismatch handling, and the XAML two-way binding.
- Unknown Disable Sleep values remain read-only and preserve their original byte; device rescanning is disabled while connected to prevent duplicate BLE subscriptions.
- Test tooling dependencies were updated to remove known vulnerable packages, and private local-file guidance was added.

## [0.1.0-preview.30] - 2026-08-19

### Changed

- Routed D-pad Down vertically from below so it is visually distinct from D-pad Right's horizontal approach.
- Routed B vertically from below so it is visually distinct from Y's horizontal approach.
- Added regression coverage for the final approach direction and intersection state of the Right/Down and Y/B connector pairs.

## [0.1.0-preview.29] - 2026-08-19

### Changed

- Routed `L/L2` and `R/R2` through separate vertical approaches above each shoulder instead of overlapping horizontal segments.
- Moved the `+` connector into a dedicated lane below the shoulder routes so it remains visually separate.
- Added a regression test that checks every segment of the four shoulder connectors and the plus connector for overlaps or crossings.

## [0.1.0-preview.28] - 2026-08-19

### Changed

- Moved all 16 mapping connectors to an overlay above the device image so the image no longer hides them.
- Aligned connector endpoints with the physical shoulder, D-pad, and face-button centers and added small endpoint markers.
- Aligned connector starts to the 50px mapping-row rhythm on both sides so each row clearly leads to its physical button.
- Added XAML regression tests for image/overlay order, per-button connector and endpoint coverage, and row-start alignment.

## [0.1.0-preview.27] - 2026-08-19

### Changed

- Replaced the unbound Disable Sleep toggle with a disabled checkbox until its device value is identified.
- Made Reset Changes restore the last device-read mappings to the screen and selected local profile without writing to the device.
- Renamed Restore Original Settings to Restore Latest Backup because it restores the newest pre-save backup, not factory defaults.
- Clarified that profile add, rename, and delete are PC-local operations.
- Removed obsolete captured-sequence replay and unimplemented sync commands from the app ViewModel.

## [0.1.0-preview.26] - 2026-08-19

### Added

- Added a fixed-row Shift Symbols picker for all 21 US keyboard symbols, including `~`, `!`, `@`, `{`, `|`, `"`, `<`, `>`, and `?`.
- Added complete picker audits for Tab, editing keys, navigation keys, modifiers, keypad keys, and F1-F24, plus physical Shift-key capture coverage.

## [0.1.0-preview.25] - 2026-08-19

### Changed

- Replaced width-dependent key wrapping with fixed physical-keyboard rows based on the official mobile app key-selection screens.
- Added the supported `Win` modifier to the picker and verified that every picker action is unique and encodable for device storage.

## [0.1.0-preview.24] - 2026-08-16

### Added

- Added generated device save and restore for the active configuration of the connected device.
- Saves create an original backup before confirmation and use a fresh complete configuration readback; restores require a complete byte-for-byte readback match.

### Known Limitations

- Device-side profile names and profile selection are not decoded; writes target the active connected-device configuration.
- Automated verification covers the save and restore flow. Independent hardware save and restore validation remains pending Task 9.

## [0.1.0-preview.23] - 2026-08-16

### Added

- Added an official-app-style key picker with Letters, Numeric Keypad, Function Keys, Number/Symbol, and Others categories.
- Kept the freeform action field for key chords such as `Ctrl+C` and `Ctrl+Shift+S`.

## [0.1.0-preview.22] - 2026-08-16

### Changed

- Updated the device-read model to treat loaded mappings as a device-stored profile with a temporary name until profile-name decoding is identified.

## [0.1.0-preview.21] - 2026-08-16

### Fixed

- Decoded HID keypad number values from official mobile saves, so keypad 1 now appears as `Keypad 1` instead of raw `0x59`.

## [0.1.0-preview.20] - 2026-08-16

### Fixed

- The Sync to device button no longer replays the captured save sequence. It now reports that generated device writes are not supported yet.

## [0.1.0-preview.19] - 2026-08-16

### Fixed

- Corrected the raw slot to physical button mapping using the official mobile app comparison capture.

## [0.1.0-preview.18] - 2026-08-16

### Added

- Mapping chips now show their raw source slot number to help calibrate the physical-button order.

## [0.1.0-preview.17] - 2026-08-16

### Fixed

- Prevented the Add profile action from duplicating the currently selected profile when the name field already shows that profile name.

## [0.1.0-preview.16] - 2026-08-16

### Fixed

- Fixed a startup deadlock that could leave the app process running without showing the main window.

## [0.1.0-preview.15] - 2026-08-16

### Fixed

- The selected profile name is now shown in the profile name field instead of staying blank.
- Saved selected profiles are applied to the visible button mapping rows when the profile library loads.
- Creating or renaming a profile keeps the current profile name visible after the save.

## [0.1.0-preview.14] - 2026-08-16

### Changed

- Switched Micro profile handling to the official-app-like local library model.
- Device reads now update a local `Current Device Settings` profile instead of pretending the device exposes a full profile list.
- Added local profile create, rename, and delete controls.
- Local profile libraries are saved to the user's Documents `MicroKeyStudio/profiles.json` file.

## [0.1.0-preview.13] - 2026-08-16

### Changed

- Removed the temporary fixed four-slot Micro profile assumption.
- The app now shows only profiles decoded from the current device read until the dynamic profile-list packet is identified.
- Documented the latest observed official-app behavior: Micro profiles can be added, renamed, and deleted.

## [0.1.0-preview.12] - 2026-08-16

### Changed

- The profile selector now shows four Micro profile slots after a successful device read.
- Slots that have not been decoded yet are marked as pending instead of silently missing from the list.
- Diagnostics now include both visible profile slot count and loaded profile count.

## [0.1.0-preview.11] - 2026-08-16

### Changed

- Successful profile loads now also write diagnostics with received config pages and profile count.

## [0.1.0-preview.10] - 2026-08-16

### Fixed

- Added fallback service discovery for Windows BLE cases where `FF10` appears in the full service list but UUID-specific lookup returns no services.

## [0.1.0-preview.9] - 2026-08-16

### Changed

- Failed device connections now write diagnostics even when service discovery throws.
- BLE service and characteristic discovery errors now include the UUIDs Windows can actually see.

## [0.1.0-preview.8] - 2026-08-16

### Changed

- Grouped Find device, profile selector, profile count, and Load profile into one device workflow section.

## [0.1.0-preview.7] - 2026-08-16

### Changed

- Added a visible profile count below the profile selector so failed profile loads are obvious.
- Added profile-load diagnostics to the app log, including profile count, config page count, and notification count.

## [0.1.0-preview.6] - 2026-08-16

### Changed

- Simplified the device flow to one visible Find device action and one Load profile action.
- Device scan now auto-selects the first discovered Micro device and fills the profile list.
- Profile selection no longer changes mappings until Load profile is pressed.

## [0.1.0-preview.5] - 2026-08-16

### Changed

- Removed hardcoded profile dropdown items.
- Device load now creates a selectable loaded profile and applies decoded button actions to the mapping rows.
- Selecting a loaded profile reapplies that profile's button actions.

## [0.1.0-preview.4] - 2026-08-16

### Changed

- Device scan now auto-selects and loads saved settings when exactly one Micro device is found.
- Micro load requests now match the current official Android app pattern: zero-filled `04 02` page requests with offsets `00`, `2d`, `5a`, and `87`.

## [0.1.0-preview.3] - 2026-08-16

### Changed

- Removed the top-right device action buttons and kept device actions in the left device panel.
- Reworked controller guide lines and removed overlay marker pills from the product image.
- Korean and English language selection now updates UI labels and physical button names.
- Failed device loads now write a sanitized diagnostic file to the user's Documents folder.

## [0.1.0-preview.2] - 2026-08-16

### Added

- Captured Micro load sequence that requests saved configuration pages from the device.
- BLE notification collection while loading saved settings.
- Micro configuration page parser that combines four 45-byte pages into a raw configuration snapshot.
- HID-style keyboard value decoder for visible raw slots such as `Ctrl+C`, `Alt+F4`, arrow keys, Enter, Backspace, and Esc.
- Device Slots panel in the mapping editor so loaded values are visible during protocol verification.

### Changed

- Load from device now attempts to read saved settings instead of only connecting.

### Known Limitations

- The app can decode raw keyboard slot values, but the exact official-app slot-to-physical-button order still needs more capture comparison before automatic button row replacement is enabled.
- Generated key-mapping writes remain experimental.

## [0.1.0-preview.1] - 2026-08-16

### Added

- First public preview foundation for MicroKey Studio.
- C#/.NET 8 WPF app shell.
- Official-app-inspired dark button mapping screen.
- Windows-style profile and button mapping screen that combines profile selection and button mapping in one view.
- Official 8BitDo Micro product image asset with source documentation.
- Language setting choices for Korean and English.
- Device workflow wording focused on finding a device and loading saved settings from it.
- Clickable mapping chips with an edit panel for updating the selected button action.
- Clearer front-facing product image crop with button location markers.
- Windows BLE scan, connect, disconnect, and experimental packet write boundary.
- Sanitized 8BitDo Micro BLE UUIDs and captured save sequence fixture.
- Local JSON profile storage foundation.
- Portable Windows release packaging script.

### Safety

- Experimental hardware writes remain behind explicit user actions.
- Raw captures, APKs, Bluetooth addresses, phone identifiers, and private notes are excluded from the public repository.

### Known Limitations

- Button mappings are display data in this preview UI.
- Real generated key-mapping writes are not complete yet.
- Users should keep the official mobile app available for recovery or reset while testing.
