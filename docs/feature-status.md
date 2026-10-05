# Feature Status

Updated: 2026-10-05 (`0.1.0-preview.32`)

## UI improvements in preview.32

Sidebar and key-picker scrolling, separated footer guidance, selection emphasis and accessible labels are included. Apply to PC profile is separate from Save to device; device saving is blocked while the mapping editor is open. Connection and save requirements are visible on screen.

Validation: 184 automated tests passed. Korean overview/editor screens and accessibility names were inspected. Physical device writes, restoration, English visual layout and DPI variations were not exercised in this UI update.

| Feature | Status | Notes |
| --- | --- | --- |
| Device discovery and mapping read | Working | Automatically connects to the discovered Micro and reads its current mapping. |
| Key mapping editor | Working | Supports picker keys, physical keyboard capture, and freeform chords. |
| Physical button connectors | Working | Above-image lines and button-center endpoints identify all 16 mapping rows. |
| Save to device | Working and hardware-checked | Creates a pre-save backup, writes, then verifies a fresh complete readback. |
| Reset changes | Working | Resets the screen and selected local PC profile to the last device-read values without writing to the device. |
| Restore latest backup | Conditional | Requires a connected device and a valid backup. Automated coverage is complete; final hardware restore verification remains. |
| Profile add, rename, and delete | PC-local only | Changes only the local profile file, not the device profile list. |
| Device profile names and list | Not implemented | Profile metadata has not been identified in the device protocol. |
| Disable Sleep | Implemented, PC hardware save check pending | Repeated official-app captures confirmed offset `0x03`, ON=`01`, OFF=`00`. Read, save, and readback verification are implemented. |
| Factory reset | Not planned | The official app provides it, and it is excluded from this project's scope due to its risk and limited need. |
| Mouse, media, and macros | Not implemented | These require device-format analysis beyond keyboard HID mappings. |
| Profile import and export | Not implemented | Local profile storage exists, but there is no user-facing file workflow. |

## Safety Rules

- Change only the confirmed Disable Sleep value through read-modify-write while preserving all other configuration bytes.
- If the Disable Sleep byte is not the confirmed `00/01`, keep the checkbox read-only and preserve that byte during mapping saves.
- Restore latest backup is not a factory reset; it restores the newest pre-save backup.
- Save and restore require a fresh complete readback instead of trusting write success alone.
