# AI Agent Handoff

Updated: 2026-08-19

## Objective

MicroKey Studio is a C#/.NET 8 WPF application for reading, editing, saving, and restoring the active keyboard mapping and Disable Sleep value stored on an 8BitDo Micro. Device save with full readback has been verified on hardware. The next objective is to independently confirm Disable Sleep through the official mobile app and verify restore on hardware.

This is an independent community project. Do not present it as official 8BitDo software.

## Repository State

- Active branch: `codex/full-editor-device-save`
- Working tree should be clean at handoff.
- Detailed implementation plan: `docs/superpowers/plans/2026-08-16-full-screen-editor-device-save.md`
- Design specifications:
  - `docs/superpowers/specs/2026-08-16-key-mapping-editor-design.md`
  - `docs/superpowers/specs/2026-08-16-key-mapping-editor-design.ko.md`
- Protocol evidence:
  - `docs/protocol-notes.md`
  - `docs/capture-analysis.md`

Do not work from an older branch or from generated release artifacts. Confirm the current branch and status first:

```powershell
git branch --show-current
git status --short
dotnet test MicroKeyStudio.sln -c Debug
```

## Completed Work

Tasks 1 through 8 in the implementation plan are complete. Device save has also been verified with the official mobile app.

1. CRC and HID codec
   - CRC-16/ANSI: polynomial `0xA001`, initial value `0xFFFF`.
   - Page CRC is stored low byte then high byte.
   - Normalized mappings include letters, numbers, symbols, navigation, keypad, F1-F24, modifiers, and chords.
   - Canonical chord order is `Ctrl`, `Alt`, `Shift`, `Win`, then the main key.

2. Read-modify-write packet builder
   - Starts from a complete 180-byte device snapshot.
   - Changes only known four-byte mapping slots.
   - Recalculates each page CRC and builds four `04 01` page writes plus the commit packet.
   - Supports full-payload packet generation for restoration and slot readback comparison.

3. Private configuration backup store
   - Stores exactly one 180-byte payload in a strict versioned JSON document.
   - JSON fields are exactly `schema`, `timestamp`, and `payload`.
   - Device names, addresses, and identifiers are rejected from the backup schema.

4. Full-screen editor state and keyboard capture
   - Mapping changes remain drafts until Apply succeeds.
   - Cancel preserves the original row/profile mapping.
   - Save failures roll back committed in-memory state and keep the draft editor open.
   - Physical input captures chords such as `Ctrl+C` and `Ctrl+Shift+S`.
   - `Esc` and `Ctrl+Esc` are mappings; Escape does not close the editor.

5. Full-screen WPF layout
   - The old narrow picker was replaced with a full-width editor state.
   - Freeform input and Cancel/Apply stay visible at the bottom.
   - Key buttons have stable 64x48 dimensions.
   - Picker keys use fixed physical-keyboard rows based on the official mobile app screens, including F1-F24, keypad, symbols, navigation, and `Win`.
   - Preview.26 adds all 21 shifted US symbols as direct picker actions such as `!` -> `Shift+1` and `?` -> `Shift+/`.
   - Korean and English editor strings are present.
   - Manual UI verification confirmed real `Ctrl+C` and `Esc` capture.

6. Verified device save and restore implementation
   - Save creates a strict private backup, confirms changes, writes generated pages, and requires a fresh readback match.
   - Restore discovers the newest valid backup across restarts and requires a full 180-byte readback match.
   - Hardware save was confirmed through the official mobile app; hardware restore still needs a connected-device test.

7. Disable Sleep read and save
   - Repeated official-app OFF → ON → OFF captures confirmed that only global offset `0x03` changes.
   - ON is `0x01`, OFF is `0x00`, and the repeated OFF 180-byte configurations were identical.
   - The checkbox becomes available after a complete read and saves through the same backup, read-modify-write, and complete-readback verification flow as mappings.

Relevant commits, oldest to newest:

```text
a528691 feat: encode Micro keyboard HID mappings
15ffaa7 fix: validate config pages and HID chords
7a9202e feat: build verified Micro save packets
b435347 feat: back up device config before writes
5f7d944 fix: strictly validate config backup documents
b438e3d test: validate missing backup fields as valid JSON
6d7ccb2 feat: add full screen mapping editor state
a58dd4f fix: preserve mapping edits on save failure
4a17578 feat: redesign the key editor workspace
ab284d4 fix: label the unimplemented device sync accurately
```

## Next Work

1. Save Disable Sleep as ON from the PC and confirm the complete-readback success message.
2. Disconnect the PC, then independently confirm Disable Sleep is ON in the official mobile app.
3. Connect the Micro in keyboard mode, run Restore Latest Backup, and confirm the restored state in the official mobile app.
4. Keep profile-name and profile-list decoding separate; current evidence shows button mappings are device-local but profile names are not yet decoded.

## Current Verification

The latest Release run reported:

```text
Protocol tests: 78 passed
Storage tests: 12 passed
App tests: 93 passed
Total: 183 passed
Release build: 0 warnings, 0 errors
```

Before trusting this snapshot, close any running `MicroKeyStudio.App.exe`; it can lock Debug output DLLs. Run tests again in the current environment.

## Known Limitations And Risks

- Only the active device mapping is currently understood. Device-side profile names and profile create/delete/select are not implemented.
- Hardware restore validation is still pending even though the automated restore workflow is covered.
- `MicroConfigSnapshot.TryCreate` has a deferred minor edge case: duplicate page offsets can still throw instead of returning false.
- The branch history contains an intermediate commit that accidentally tracked an internal task report before the next commit removed it. Before publishing, squash or otherwise clean the feature history after confirming that no user work is lost.
- Do not infer protocol bytes from UI labels. Use the codec, the original snapshot, and readback.

## Privacy And Publishing Rules

Never commit or publish:

- phone serial numbers or Android bug reports;
- Bluetooth addresses or device identifiers;
- raw HCI/BLE logs or packet captures;
- APKs, decompiled output, or proprietary app content;
- generated device backup JSON payloads;
- absolute personal filesystem paths.

The repository already ignores `docs/private/`, reverse-engineering artifacts, backups/build outputs, `.superpowers/`, and `.worktrees/`. Check staged files before every commit:

```powershell
git status --short
git diff --cached --name-only
git diff --check
```

## Completion Standard

Do not claim device save or restore works until:

1. automated success and failure-path tests pass;
2. a backup is created before the first write;
3. the application performs fresh readback and verifies it;
4. the official mobile app independently confirms the controlled mapping;
5. restore is read back and independently confirmed;
6. Release tests/build and portable package inspection pass.
