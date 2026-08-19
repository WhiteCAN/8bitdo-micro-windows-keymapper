# Key Mapping Editor Redesign

Date: 2026-08-16

## Goal

Make button editing easy to find and comfortable to use, then safely persist verified mappings to the connected controller. Selecting a mapping button opens a full-width editor inside the existing application window. The editor supports a physical keyboard capture area, large categorized key choices, and a clearly visible freeform field for values such as `Ctrl+C`.

## Current Problem

The preview.23 key picker was inserted into a fixed 240-pixel side panel. Its category grid pushes the freeform field below the visible area and makes every key too small. The application also has no physical keyboard capture handler; the existing text box only accepts ordinary text editing. This makes the advertised chord workflow difficult to discover and incomplete.

## Chosen Interaction

The application has two main states in the same `MainWindow`:

1. Mapping overview: profile controls, controller image, connector lines, and all mappings remain visible.
2. Mapping editor: selecting any action button replaces the overview content with a full-width editor for that Micro button.

The editor contains, in order:

- A back command and the selected Micro button name.
- A prominent physical-keyboard capture control.
- Category tabs and a large wrapping grid of key choices.
- A persistent freeform action field.
- Cancel and Apply commands.

Apply updates the selected mapping, saves the local profile, and returns to the overview. Cancel returns without changing the mapping. A separate Save to Device command performs an explicit, confirmed hardware write after validating the complete active mapping.

## Keyboard Capture Rules

The capture control receives keyboard focus when clicked. It builds a normalized action from the currently held modifiers and the main key.

- `C` becomes `C`.
- Ctrl plus C becomes `Ctrl+C`.
- Ctrl plus Shift plus S becomes `Ctrl+Shift+S`.
- Modifier order is always `Ctrl`, `Alt`, `Shift`, `Win`, followed by the main key.
- Escape is a valid key and becomes `Esc`; it never cancels capture.
- Combinations such as `Ctrl+Esc` and `Alt+Esc` are valid.
- Modifier-only mappings such as `Ctrl` remain available through the categorized key list and freeform field.
- Capture completes when a non-modifier key is pressed. The captured text is placed in the freeform field but is not saved until Apply is selected.
- Cancel is available only through visible UI commands: Cancel or Back.

## Components

`MainWindow.xaml` owns the two visual states and routes WPF preview key events from the capture control. It does not format chords itself.

`MainViewModel` owns editor state: whether the editor is open, the selected mapping, the draft action, category selection, cancel behavior, and apply behavior.

A small keyboard chord formatter converts WPF key plus modifier input into the application's existing action strings. Keeping formatting separate makes physical input deterministic and unit-testable.

The protocol project owns HID usage encoding, CRC calculation, and save-packet generation. It accepts a device-read snapshot plus the active mappings and returns either a validated packet sequence or a specific unsupported-action error. The BLE transport only sends the generated sequence and collects responses.

## Data Flow

1. The user selects a mapping action in the overview.
2. The view model copies the current action into a draft and opens the editor.
3. A categorized key, physical capture, or freeform edit changes only the draft.
4. Apply writes the normalized draft to the selected row and local profile store, then closes the editor.
5. Cancel or Back discards the draft and closes the editor.

## Safe Device Save

Device save uses a read-modify-write-readback flow for the currently active device-stored profile:

1. Require one connected Micro and a previously decoded mapping snapshot.
2. Read all four 45-byte configuration pages again immediately before writing.
3. Save the unmodified 180-byte payload as a timestamped local backup outside the repository.
4. Encode every supported mapping into its known four-byte button slot. Preserve all unknown bytes, profile metadata, and undecoded fields exactly as read.
5. Calculate the page check value with CRC-16/ANSI using polynomial `0xA001`, initial value `0xFFFF`, over each 45-byte page. Store the low byte and then the high byte in the `04 01` packet.
6. Show a confirmation summary of the changed Micro buttons. Send the four generated page writes followed by the observed official commit sequence.
7. Read all four pages again and compare every written slot with the requested HID values.
8. Report success only after readback matches. A mismatch or timeout keeps the backup and offers a separate Restore Original Settings command; restoration is never triggered silently.

This version writes only the currently active device-stored profile. Profile-name writing and selecting or creating additional device profiles remain unavailable until their protocol fields are decoded.

## Error Handling

- Apply is disabled when no mapping is selected or the draft is blank.
- Unsupported or unidentified physical keys do not replace the current draft.
- Repeated key-down events are ignored so holding a key does not repeatedly change the draft.
- Local profile save errors use the existing status reporting path and keep the editor open so the draft is not lost.
- Save to Device is disabled while disconnected, while no complete device snapshot is available, or while any mapping cannot be encoded.
- Unsupported freeform values and chords requiring more than four HID usages are listed before any packet is sent.
- A BLE failure or readback mismatch is never reported as success and never deletes the pre-write backup.
- Restore requires a second explicit confirmation and verifies the restored values by reading the device again.

## Localization And Layout

All new labels are included in the existing Korean and English `UiText` resources. Key action values remain language-neutral protocol strings such as `Ctrl+C`, `Esc`, and `Page Up`.

The editor uses responsive wrapping with stable minimum key-button dimensions. It avoids nested cards and internal vertical scrolling at the normal supported window size. At narrower widths, category controls and the key grid wrap instead of shrinking text.

## Test Strategy

- View-model tests cover opening, canceling, applying, and draft preservation.
- Formatter tests cover single keys, modifier ordering, `Ctrl+C`, `Ctrl+Shift+S`, `Esc`, and `Ctrl+Esc`.
- A view-level test or XAML inspection test verifies the full editor exposes the capture and freeform controls and that the old 240-pixel editor panel is gone.
- HID encoder tests cover every selectable key category and reject unsupported values without producing packets.
- CRC fixture tests reproduce the observed official save-page values, including `D33A`, `8E09`, and `CF30`.
- Packet generator tests prove that unknown snapshot bytes are unchanged and only known mapping slots plus page CRC bytes differ.
- View-model and fake-transport tests cover confirmation, disconnected state, partial write failure, readback success, readback mismatch, backup retention, and explicit restore.
- A controlled hardware test writes one recoverable mapping, reads it back in MicroKey Studio, and verifies it independently in the official mobile app.
- Existing profile, protocol, localization, and device-read tests must remain green.
- The final WPF build and portable publish must succeed before packaging preview.24.

## Out Of Scope

- Decoding device profile names.
- Creating, deleting, renaming, or selecting additional profiles stored on the device.
- Mac-specific keyboard labels or behavior.
- Macro sequences, timed key presses, or mouse actions.
