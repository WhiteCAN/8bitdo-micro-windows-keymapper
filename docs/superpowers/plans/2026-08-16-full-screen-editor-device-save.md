# Full-Screen Key Editor And Safe Device Save Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the cramped mapping sidebar with a full-screen keyboard editor and safely save the active mapping to an 8BitDo Micro through a backed-up read-modify-write-readback flow.

**Architecture:** Pure protocol classes encode normalized action strings into four HID usage bytes, calculate the observed CRC-16/ANSI page check, and build save packets from a device-read snapshot while preserving unknown bytes. The WPF view model owns draft editor state and orchestrates backup, explicit confirmation, BLE write, readback, and explicit restore; the view only handles layout and WPF keyboard events.

**Tech Stack:** C# 12, .NET 8, WPF, Windows BLE GATT, xUnit, existing JSON storage and portable packaging script.

## Global Constraints

- Use only the currently active device-stored profile; do not create, select, rename, or delete profiles on the controller.
- Preserve all unknown payload bytes and profile metadata exactly as read.
- Compute each page CRC with polynomial `0xA001`, initial value `0xFFFF`, over exactly 45 payload bytes; store low byte then high byte.
- Never write while disconnected, without a complete fresh four-page snapshot, or with an unsupported mapping.
- Keep `Esc` as a valid key; cancellation occurs only through visible UI commands.
- Do not report device-save success until a four-page readback matches every requested mapping slot.
- Keep the pre-write backup after all failures; restore requires a separate explicit confirmation and readback verification.
- Keep raw captures, APKs, device addresses, and backup payloads outside Git.
- Keep Korean and English UI text in sync.
- Package the completed build as `0.1.0-preview.24` dated `2026-08-16`.

---

## File Structure

- Create `src/MicroKeyStudio.Protocol/Configuration/MicroCrc16.cs`: CRC-16/ANSI calculation only.
- Create `src/MicroKeyStudio.Protocol/Configuration/MicroHidCodec.cs`: normalized action string to four HID usages and reverse display formatting.
- Create `src/MicroKeyStudio.Protocol/Packets/MicroSavePacketBuilder.cs`: immutable read-modify-write packet generation and slot verification.
- Modify `src/MicroKeyStudio.Protocol/Configuration/MicroConfigSnapshot.cs`: construct from payload, expose copied payload, and delegate decoding to `MicroHidCodec`.
- Create `src/MicroKeyStudio.Storage/MicroConfigBackupStore.cs`: timestamped payload backup save/load with no device identifiers.
- Create `src/MicroKeyStudio.App/Input/KeyboardChordFormatter.cs`: WPF key/modifier normalization.
- Create `src/MicroKeyStudio.App/Services/IUserConfirmationService.cs`: testable confirmation boundary.
- Create `src/MicroKeyStudio.App/Services/MessageBoxConfirmationService.cs`: Windows confirmation implementation.
- Modify `src/MicroKeyStudio.App/ViewModels/MainViewModel.cs`: editor state plus save, readback, and restore orchestration.
- Modify `src/MicroKeyStudio.App/MainWindow.xaml`: overview/editor state switch and large key layout.
- Modify `src/MicroKeyStudio.App/MainWindow.xaml.cs`: physical keyboard capture event routing.
- Modify `tests/MicroKeyStudio.Protocol.Tests/*`, `tests/MicroKeyStudio.Storage.Tests/*`, and `tests/MicroKeyStudio.App.Tests/*`: behavior-first regression coverage.
- Modify version, changelog, README, and capture-analysis documents for preview.24.

---

### Task 1: CRC And HID Codec

**Files:**
- Create: `src/MicroKeyStudio.Protocol/Configuration/MicroCrc16.cs`
- Create: `src/MicroKeyStudio.Protocol/Configuration/MicroHidCodec.cs`
- Modify: `src/MicroKeyStudio.Protocol/Configuration/MicroConfigSnapshot.cs`
- Create: `tests/MicroKeyStudio.Protocol.Tests/MicroCrc16Tests.cs`
- Create: `tests/MicroKeyStudio.Protocol.Tests/MicroHidCodecTests.cs`
- Modify: `tests/MicroKeyStudio.Protocol.Tests/MicroConfigSnapshotTests.cs`

**Interfaces:**
- Produces: `public static ushort MicroCrc16.Compute(ReadOnlySpan<byte> data)`.
- Produces: `public static bool MicroHidCodec.TryEncode(string action, out byte[] usages, out string? error)` where success always returns exactly four bytes.
- Produces: `public static string MicroHidCodec.Decode(ReadOnlySpan<byte> usages)`.
- Produces: `public static MicroConfigSnapshot MicroConfigSnapshot.FromPayload(byte[] payload)` and `public byte[] CopyPayload()`.

- [ ] **Step 1: Write failing CRC fixture tests**

Use literal 45-byte pages from the official mobile save and assert:

```csharp
[Theory]
[InlineData("ff 6d 00 00 11 09 20 20 11 09 20 20 0a 00 00 00 0d 00 00 00 0b 00 00 00 0c 00 00 00 59 00 00 00 10 00 00 00 0f 00 00 00 15 00 00 00 00", 0xD33A)]
[InlineData("00 00 00 00 00 00 00 11 00 00 00 12 00 00 00 00 00 00 00 16 00 00 00 09 00 00 00 51 00 00 00 07 00 00 00 4f 00 00 00 00 00 00 00 00 00", 0x8E09)]
[InlineData("00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00", 0xCF30)]
public void Official_pages_produce_observed_crc(string hex, ushort expected)
{
    Assert.Equal(expected, MicroCrc16.Compute(Hex(hex)));
}
```

- [ ] **Step 2: Run the CRC tests and verify RED**

Run: `dotnet test tests/MicroKeyStudio.Protocol.Tests/MicroKeyStudio.Protocol.Tests.csproj --filter FullyQualifiedName~MicroCrc16Tests`

Expected: FAIL because `MicroCrc16` does not exist.

- [ ] **Step 3: Implement the minimal ANSI CRC loop**

Initialize `ushort crc = 0xffff`; xor each byte; shift eight times; xor `0xa001` when bit zero is set. Return the final `ushort` without byte swapping.

- [ ] **Step 4: Run the CRC tests and verify GREEN**

Run the Task 1 Step 2 command. Expected: all three fixtures PASS.

- [ ] **Step 5: Write failing HID round-trip and rejection tests**

Use literal expected usage arrays:

```csharp
[Theory]
[InlineData("A", "04 00 00 00")]
[InlineData("Esc", "29 00 00 00")]
[InlineData("Ctrl+C", "E0 06 00 00")]
[InlineData("Ctrl+Shift+S", "E0 E1 16 00")]
[InlineData("Ctrl+Esc", "E0 29 00 00")]
[InlineData("Keypad 1", "59 00 00 00")]
public void Supported_action_encodes_to_literal_hid_usages(string action, string expectedHex)
{
    Assert.True(MicroHidCodec.TryEncode(action, out byte[] usages, out string? error), error);
    Assert.Equal(Hex(expectedHex), usages);
    Assert.Equal(action, MicroHidCodec.Decode(usages));
}

[Theory]
[InlineData("")]
[InlineData("Ctrl+Alt+Shift+Win+C")]
[InlineData("Mouse Left")]
public void Unsupported_action_is_rejected_without_usages(string action)
{
    Assert.False(MicroHidCodec.TryEncode(action, out byte[] usages, out string? error));
    Assert.Empty(usages);
    Assert.False(string.IsNullOrWhiteSpace(error));
}
```

Add table cases for A-Z, 0-9, symbols, F1-F24, arrows, navigation, keypad keys, modifiers, Print Screen, Scroll Lock, Pause, Insert, Delete, Caps Lock, Num Lock, Backspace, Tab, Enter, Space, and None.

- [ ] **Step 6: Run HID tests and verify RED**

Run: `dotnet test tests/MicroKeyStudio.Protocol.Tests/MicroKeyStudio.Protocol.Tests.csproj --filter FullyQualifiedName~MicroHidCodecTests`

Expected: FAIL because the codec does not exist.

- [ ] **Step 7: Implement the HID dictionaries and normalized modifier order**

Map left modifiers to `E0` Ctrl, `E2` Alt, `E1` Shift, and `E3` Win. Parse case-insensitively but emit canonical labels. Reject more than four usages and unknown labels. Move the existing decoder switch from `MicroConfigSnapshot` into `MicroHidCodec.Decode` and extend it to every selectable key.

- [ ] **Step 8: Add snapshot immutability tests and implementation**

Test that `FromPayload` rejects lengths other than 180, copies its input, and `CopyPayload` returns a new array. Refactor `TryCreate` to call `FromPayload` only after four valid 45-byte pages are assembled.

- [ ] **Step 9: Run all protocol tests and commit**

Run: `dotnet test tests/MicroKeyStudio.Protocol.Tests/MicroKeyStudio.Protocol.Tests.csproj`

Expected: all tests PASS.

Commit:

```powershell
git add src/MicroKeyStudio.Protocol/Configuration tests/MicroKeyStudio.Protocol.Tests
git commit -m "feat: encode Micro keyboard HID mappings"
```

---

### Task 2: Read-Modify-Write Packet Builder

**Files:**
- Create: `src/MicroKeyStudio.Protocol/Packets/MicroSavePacketBuilder.cs`
- Create: `tests/MicroKeyStudio.Protocol.Tests/MicroSavePacketBuilderTests.cs`
- Modify: `src/MicroKeyStudio.Protocol/Packets/KnownMicroPackets.cs`

**Interfaces:**
- Consumes: `MicroCrc16.Compute`, `MicroHidCodec.TryEncode`, `MicroConfigSnapshot.CopyPayload`.
- Produces: `public sealed record MicroSaveBuildResult(PacketSequence Sequence, byte[] ExpectedPayload, IReadOnlyList<MicroMappingChange> Changes)`.
- Produces: `public static MicroSaveBuildResult MicroSavePacketBuilder.Build(MicroConfigSnapshot baseline, IReadOnlyDictionary<int, string> actionsBySlot)`.
- Produces: `public static PacketSequence MicroSavePacketBuilder.BuildFromPayload(MicroConfigSnapshot snapshot)` for exact backup restoration.
- Produces: `public static bool MicroSavePacketBuilder.MatchesSlots(MicroConfigSnapshot snapshot, IReadOnlyDictionary<int, string> actionsBySlot, out IReadOnlyList<int> mismatchedSlots)`.

- [ ] **Step 1: Write a failing literal packet-generation test**

Create a 180-byte baseline from the four observed pages, change slot 7 from `0E 00 00 00` to `59 00 00 00`, and assert the first generated `04 01` page equals the literal official packet beginning:

```text
04 01 00 00 00 2d 00 3a d3 b4 00 00 00 00 00 00 00 ff 6d ... 59 00 00 00 ...
```

Assert pages have offsets `00`, `2D`, `5A`, `87`, are 62 bytes long, and contain CRC low byte then high byte.

- [ ] **Step 2: Run packet-builder tests and verify RED**

Run: `dotnet test tests/MicroKeyStudio.Protocol.Tests/MicroKeyStudio.Protocol.Tests.csproj --filter FullyQualifiedName~MicroSavePacketBuilderTests`

Expected: FAIL because the builder does not exist.

- [ ] **Step 3: Implement immutable payload overlay and page construction**

Copy the 180-byte baseline. For every slot, validate `0 <= slot < 45`, encode exactly four usages, and overwrite only `slot * 4 .. slot * 4 + 3`. Build each page as:

```text
04 01 00 00 00 2D 00 [CRC low] [CRC high] B4 00 00 00 [offset uint32 LE] [45 payload bytes]
```

Append the observed commit packet `04 06 00 5b 00 00 00 ff ff 00 00 00 00 00 00 00 00`. Put page construction in `BuildFromPayload`; `Build` overlays validated slots and delegates to it. Do not include any captured profile payload.

- [ ] **Step 4: Test unknown-byte preservation and atomic rejection**

Assert every byte outside requested slots is unchanged. Assert one unsupported action throws `MicroMappingEncodingException` containing every invalid slot and produces no `PacketSequence`.

- [ ] **Step 5: Test readback slot comparison**

Assert `MatchesSlots` returns true for the expected payload, and reports only slot 7 after changing that slot back to `0E 00 00 00`.

- [ ] **Step 6: Run protocol tests and commit**

Run: `dotnet test tests/MicroKeyStudio.Protocol.Tests/MicroKeyStudio.Protocol.Tests.csproj`

Expected: all tests PASS.

Commit:

```powershell
git add src/MicroKeyStudio.Protocol/Packets tests/MicroKeyStudio.Protocol.Tests
git commit -m "feat: build verified Micro save packets"
```

---

### Task 3: Private Device Snapshot Backups

**Files:**
- Create: `src/MicroKeyStudio.Storage/MicroConfigBackupStore.cs`
- Create: `tests/MicroKeyStudio.Storage.Tests/MicroConfigBackupStoreTests.cs`

**Interfaces:**
- Produces: `public sealed record MicroConfigBackup(DateTimeOffset CreatedAt, byte[] Payload)`.
- Produces: `public static Task<string> MicroConfigBackupStore.SaveAsync(byte[] payload, string directory, DateTimeOffset createdAt, CancellationToken cancellationToken)`.
- Produces: `public static Task<MicroConfigBackup> MicroConfigBackupStore.LoadAsync(string path, CancellationToken cancellationToken)`.

- [ ] **Step 1: Write failing real-filesystem backup tests**

Use an xUnit temporary directory. Save a literal 180-byte payload and assert:

```csharp
Assert.StartsWith("micro-config-backup-20260816-", Path.GetFileName(path));
Assert.Equal(payload, loaded.Payload);
Assert.Equal(createdAt, loaded.CreatedAt);
Assert.DoesNotContain("device", await File.ReadAllTextAsync(path), StringComparison.OrdinalIgnoreCase);
```

Also assert save rejects payloads not exactly 180 bytes and load rejects a wrong schema version or payload length.

- [ ] **Step 2: Run storage tests and verify RED**

Run: `dotnet test tests/MicroKeyStudio.Storage.Tests/MicroKeyStudio.Storage.Tests.csproj --filter FullyQualifiedName~MicroConfigBackupStoreTests`

Expected: FAIL because the store does not exist.

- [ ] **Step 3: Implement versioned JSON backup storage**

Write schema version `1`, ISO timestamp, and Base64 payload only. Use `Directory.CreateDirectory`, `FileMode.CreateNew`, and asynchronous JSON serialization. Do not store Bluetooth address, device name, profile name, or diagnostics.

- [ ] **Step 4: Run storage tests and commit**

Run: `dotnet test tests/MicroKeyStudio.Storage.Tests/MicroKeyStudio.Storage.Tests.csproj`

Expected: all tests PASS.

Commit:

```powershell
git add src/MicroKeyStudio.Storage/MicroConfigBackupStore.cs tests/MicroKeyStudio.Storage.Tests
git commit -m "feat: back up device config before writes"
```

---

### Task 4: Full-Screen Editor State And Keyboard Capture

**Files:**
- Create: `src/MicroKeyStudio.App/Input/KeyboardChordFormatter.cs`
- Modify: `src/MicroKeyStudio.App/ViewModels/MainViewModel.cs`
- Modify: `tests/MicroKeyStudio.App.Tests/MainViewModelTests.cs`
- Create: `tests/MicroKeyStudio.App.Tests/KeyboardChordFormatterTests.cs`

**Interfaces:**
- Produces: `public static bool KeyboardChordFormatter.TryFormat(Key key, ModifierKeys modifiers, out string action)`.
- Produces view-model properties: `bool IsMappingEditorOpen`, `string EditorTitleText`.
- Produces commands: `CancelMappingEditCommand`, existing `ApplySelectedMappingCommand` now closes only after a successful local save.

- [ ] **Step 1: Write failing editor-state tests**

Assert selecting L opens the editor and copies the existing action. Changing the draft then canceling closes the editor and leaves both row and profile unchanged. Applying `Ctrl+C` saves it and closes the editor.

- [ ] **Step 2: Run editor tests and verify RED**

Run: `dotnet test tests/MicroKeyStudio.App.Tests/MicroKeyStudio.App.Tests.csproj --filter "FullyQualifiedName~Mapping_editor"`

Expected: FAIL because editor state and cancel command do not exist.

- [ ] **Step 3: Implement draft-only selection and explicit apply/cancel**

Change `SelectKeyOption` to update only `PendingAction`; it must no longer mutate or save the selected row immediately. `CancelMappingEdit` discards the draft. `ApplySelectedMappingAsync` updates the row and profile, awaits `SaveProfileLibraryAsync`, then closes the editor. If saving throws, keep the editor open.

- [ ] **Step 4: Write failing physical keyboard formatting tests**

Use literal WPF inputs:

```csharp
[Theory]
[InlineData(Key.C, ModifierKeys.None, "C")]
[InlineData(Key.C, ModifierKeys.Control, "Ctrl+C")]
[InlineData(Key.S, ModifierKeys.Control | ModifierKeys.Shift, "Ctrl+Shift+S")]
[InlineData(Key.Escape, ModifierKeys.None, "Esc")]
[InlineData(Key.Escape, ModifierKeys.Control, "Ctrl+Esc")]
public void Physical_key_is_normalized(Key key, ModifierKeys modifiers, string expected)
```

Assert `Key.LeftCtrl` alone returns false because modifier-only mappings use the key list/freeform field. Assert `Key.System` is normalized by the caller to the actual system key.

- [ ] **Step 5: Run formatter tests RED, implement, then run GREEN**

Run: `dotnet test tests/MicroKeyStudio.App.Tests/MicroKeyStudio.App.Tests.csproj --filter FullyQualifiedName~KeyboardChordFormatterTests`

Implement canonical modifier order Ctrl, Alt, Shift, Win and use the same labels accepted by `MicroHidCodec`.

- [ ] **Step 6: Run app tests and commit**

Run: `dotnet test tests/MicroKeyStudio.App.Tests/MicroKeyStudio.App.Tests.csproj`

Expected: all tests PASS.

Commit:

```powershell
git add src/MicroKeyStudio.App/Input src/MicroKeyStudio.App/ViewModels/MainViewModel.cs tests/MicroKeyStudio.App.Tests
git commit -m "feat: add full screen mapping editor state"
```

---

### Task 5: Full-Screen WPF Editor Layout

**Files:**
- Modify: `src/MicroKeyStudio.App/MainWindow.xaml`
- Modify: `src/MicroKeyStudio.App/MainWindow.xaml.cs`
- Modify: `src/MicroKeyStudio.App/ViewModels/MainViewModel.cs` (`UiText` only)

**Interfaces:**
- Consumes: `IsMappingEditorOpen`, `CancelMappingEditCommand`, `ApplySelectedMappingCommand`, `KeyCategories`, `AvailableKeyOptions`, `PendingAction`.
- Produces view handler: `OnKeyboardCapturePreviewKeyDown(object sender, KeyEventArgs e)`.

- [ ] **Step 1: Remove the fixed 240-pixel edit column and create two visual states**

Keep the sidebar and footer stable. Use WPF style data triggers on `IsMappingEditorOpen` to show either the mapping overview or the full-width editor; do not add nested cards.

- [ ] **Step 2: Build the editor hierarchy**

Place back command and selected button first, then a focusable keyboard-capture button with `PreviewKeyDown`, then category tabs/list, then a wrapping key grid with `MinWidth="64"` and `Height="48"`, then a permanently visible `PendingAction` text box and Cancel/Apply commands.

- [ ] **Step 3: Route physical keyboard events**

In `MainWindow.xaml.cs`, ignore `e.IsRepeat`, normalize `Key.System` to `e.SystemKey`, call `KeyboardChordFormatter.TryFormat`, set `_viewModel.PendingAction`, and set `e.Handled = true` only for a captured action. Escape follows this exact path and becomes `Esc`.

- [ ] **Step 4: Add Korean and English UI copy**

Add paired strings for back, physical keyboard capture, capture instructions, freeform action, cancel, apply, save to device, restore original settings, confirmation, unsupported mapping, readback mismatch, and verified success.

- [ ] **Step 5: Build and manually inspect both languages**

Run: `dotnet build MicroKeyStudio.sln -c Debug`

Launch the app. Verify at 1280x720 and 1600x900 that the freeform input and Apply button are visible without scrolling, key buttons remain readable, Escape enters `Esc`, Ctrl+C enters `Ctrl+C`, Cancel preserves the old mapping, and language switching updates all editor labels.

- [ ] **Step 6: Commit**

```powershell
git add src/MicroKeyStudio.App/MainWindow.xaml src/MicroKeyStudio.App/MainWindow.xaml.cs src/MicroKeyStudio.App/ViewModels/MainViewModel.cs
git commit -m "feat: redesign the key editor workspace"
```

---

### Task 6: Confirmed Save And Readback Orchestration

**Files:**
- Create: `src/MicroKeyStudio.App/Services/IUserConfirmationService.cs`
- Create: `src/MicroKeyStudio.App/Services/MessageBoxConfirmationService.cs`
- Modify: `src/MicroKeyStudio.App/ViewModels/MainViewModel.cs`
- Modify: `tests/MicroKeyStudio.App.Tests/FakeBleTransport.cs`
- Create: `tests/MicroKeyStudio.App.Tests/FakeConfirmationService.cs`
- Modify: `tests/MicroKeyStudio.App.Tests/MainViewModelTests.cs`

**Interfaces:**
- Produces: `Task<bool> IUserConfirmationService.ConfirmAsync(string title, string message)`.
- Produces: `ICommand SaveToDeviceCommand`, `string? LastBackupPath`, and private `Task<MicroConfigSnapshot> ReadCurrentSnapshotAsync()`.
- Consumes: `MicroSavePacketBuilder.Build`, `MicroConfigBackupStore.SaveAsync`, `IBleTransport.WriteSequenceAsync`.

- [ ] **Step 1: Extend the fake transport with ordered notification batches and write failures**

Add `QueueCollectedNotificationBatch(params byte[][] packets)` and dequeue one batch per `WriteSequenceAndCollectNotificationsAsync` call. Add `ThrowOnWrite(Exception exception)`. Preserve full request sequences so tests can inspect pre-read, write, and readback order.

- [ ] **Step 2: Write failing save precondition tests**

Assert `SaveToDeviceCommand.CanExecute(null)` is false while disconnected, after an incomplete read, and when an action is unsupported. When confirmation returns false, assert no write occurs and the fresh pre-write backup still exists.

- [ ] **Step 3: Write the failing successful save test**

Queue four baseline pages for the fresh pre-read and four expected pages for readback. Confirm true, change L to `Keypad 1`, execute Save to Device, then assert:

```csharp
Assert.NotNull(fakeTransport.LastWrittenSequence);
Assert.StartsWith("04-01", BitConverter.ToString(fakeTransport.LastWrittenSequence.Writes[0]));
Assert.True(File.Exists(viewModel.LastBackupPath));
Assert.Contains("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
```

Also load the backup and assert it equals the pre-write baseline, not the edited payload.

- [ ] **Step 4: Run save tests and verify RED**

Run: `dotnet test tests/MicroKeyStudio.App.Tests/MicroKeyStudio.App.Tests.csproj --filter FullyQualifiedName~Save_to_device`

Expected: FAIL because save orchestration does not exist.

- [ ] **Step 5: Implement fresh read, confirmation, backup, write, and readback**

Refactor current connect-load parsing into `ReadCurrentSnapshotAsync`. At save time:

1. Build `actionsBySlot` from all 16 displayed mappings and their existing `SourceSlot` values.
2. Fresh-read four pages.
3. Build and validate before writing.
4. Save the baseline to `Documents/MicroKeyStudio/backups`.
5. Confirm with a localized list of changed Micro button labels and old/new actions.
6. Send only the generated four `04 01` pages and commit packet at 120ms intervals.
7. Fresh-read four pages and call `MatchesSlots`.
8. Set verified success only on a full match and refresh `RawConfigValues` from readback.

- [ ] **Step 6: Add failure-path tests**

Assert unsupported input sends nothing, write exceptions retain the backup path, missing readback produces mismatch status, wrong slot produces mismatch status, and command re-entry is blocked by `IsBusy`.

- [ ] **Step 7: Run app tests and commit**

Run: `dotnet test tests/MicroKeyStudio.App.Tests/MicroKeyStudio.App.Tests.csproj`

Expected: all tests PASS.

Commit:

```powershell
git add src/MicroKeyStudio.App/Services src/MicroKeyStudio.App/ViewModels/MainViewModel.cs tests/MicroKeyStudio.App.Tests
git commit -m "feat: save mappings with verified readback"
```

---

### Task 7: Explicit Restore From The Last Backup

**Files:**
- Modify: `src/MicroKeyStudio.App/ViewModels/MainViewModel.cs`
- Modify: `src/MicroKeyStudio.App/MainWindow.xaml`
- Modify: `tests/MicroKeyStudio.App.Tests/MainViewModelTests.cs`

**Interfaces:**
- Produces: `ICommand RestoreDeviceBackupCommand` enabled only when connected and `LastBackupPath` exists.
- Consumes: `MicroConfigBackupStore.LoadAsync`, `MicroConfigSnapshot.FromPayload`, `MicroSavePacketBuilder.BuildFromPayload` with the backup payload unchanged.

- [ ] **Step 1: Write failing restore confirmation and success tests**

Assert declining confirmation sends nothing. On confirmation, assert the packets contain the backup pages, readback matches the backup slots, and status reports verified restoration.

- [ ] **Step 2: Run restore tests and verify RED**

Run: `dotnet test tests/MicroKeyStudio.App.Tests/MicroKeyStudio.App.Tests.csproj --filter FullyQualifiedName~Restore_device_backup`

Expected: FAIL because restore command does not exist.

- [ ] **Step 3: Implement explicit restore**

Load the backup, confirm again, generate page packets directly from its complete 180-byte payload, write, fresh-read, and compare the full 180-byte payload. Keep the backup even after successful restore.

- [ ] **Step 4: Add restore failure tests and UI command**

Assert malformed backup, write failure, timeout, and payload mismatch do not report success. Place Restore Original Settings next to Save to Device with lower visual emphasis and disable it when unavailable.

- [ ] **Step 5: Run app tests and commit**

Run: `dotnet test tests/MicroKeyStudio.App.Tests/MicroKeyStudio.App.Tests.csproj`

Expected: all tests PASS.

Commit:

```powershell
git add src/MicroKeyStudio.App tests/MicroKeyStudio.App.Tests
git commit -m "feat: restore backed up Micro settings"
```

---

### Task 8: Documentation, Version, And Full Automated Verification

**Files:**
- Modify: `VERSION`
- Modify: `src/MicroKeyStudio.App/MicroKeyStudio.App.csproj`
- Modify: `src/MicroKeyStudio.App/MainWindow.xaml` version label
- Modify: `README.md`
- Modify: `README.ko.md`
- Modify: `CHANGELOG.md`
- Modify: `CHANGELOG.ko.md`
- Modify: `docs/capture-analysis.md`
- Modify: `docs/capture-analysis.ko.md`

- [ ] **Step 1: Document the verified protocol without private data**

Record CRC polynomial/init/range/byte order, read-modify-write safety, backup location, active-profile-only limitation, and readback requirement. Do not copy phone serials, Bluetooth addresses, raw logs, or APK content.

- [ ] **Step 2: Bump preview version**

Set `VERSION`, project `Version`, `InformationalVersion`, and visible version label to `0.1.0-preview.24`. Add dated English and Korean changelog entries.

- [ ] **Step 3: Run fresh full verification**

Run:

```powershell
dotnet test MicroKeyStudio.sln -c Release
dotnet build MicroKeyStudio.sln -c Release --no-restore
```

Expected: zero failed tests and both commands exit 0.

- [ ] **Step 4: Package and inspect the portable release**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/package-portable.ps1
```

Expected: `artifacts/releases/MicroKeyStudio-v0.1.0-preview.24-2026-08-16-win-x64-portable.zip` exists. Expand to a temporary directory, launch `MicroKeyStudio.App.exe`, and confirm the version label and image load without installation.

- [ ] **Step 5: Commit release metadata**

```powershell
git add VERSION src/MicroKeyStudio.App/MicroKeyStudio.App.csproj src/MicroKeyStudio.App/MainWindow.xaml README.md README.ko.md CHANGELOG.md CHANGELOG.ko.md docs/capture-analysis.md docs/capture-analysis.ko.md
git commit -m "chore: prepare preview.24"
```

---

### Task 9: Controlled Hardware Verification

**Files:**
- No committed raw capture or device backup files.
- Update sanitized findings only if observed behavior differs: `docs/capture-analysis.md`, `docs/capture-analysis.ko.md`.

- [ ] **Step 1: Establish a recoverable baseline**

Connect the Micro, use Find Device to load the active mapping, and verify the app created a complete four-page snapshot. Keep the official mobile app available for independent recovery.

- [ ] **Step 2: Make one controlled change**

Change only L to `Keypad 0`, apply locally, choose Save to Device, review the one-change confirmation, and approve.

- [ ] **Step 3: Verify automatic readback**

Require the app to report verified success and show L as `Keypad 0` after the fresh read. If it reports mismatch or timeout, stop further writes and retain the backup.

- [ ] **Step 4: Verify independently in the official mobile app**

Disconnect Windows, connect the official app, load the controller mapping, and verify L is `Keypad 0`.

- [ ] **Step 5: Exercise restore**

Reconnect Windows, choose Restore Original Settings, confirm, require verified readback, then independently confirm the original L mapping in the mobile app.

- [ ] **Step 6: Record sanitized result and run final regression**

Update capture-analysis documents with pass/fail and no identifiers. Run `dotnet test MicroKeyStudio.sln -c Release` again. Commit documentation only if changed.
