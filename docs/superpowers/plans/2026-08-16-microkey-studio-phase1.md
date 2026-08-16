# MicroKey Studio Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first working MicroKey Studio Windows app foundation: C#/.NET WPF shell, protocol model, BLE transport boundary, local profiles, and a developer-only captured-packet replay flow for 8BitDo Micro.

**Architecture:** The app is split into focused projects: WPF UI, BLE transport, protocol/model, storage, and tests. BLE and protocol are separated so packet generation can evolve from captured replay to decoded mappings without rewriting the UI.

**Tech Stack:** C#/.NET 8 SDK, WPF, MSTest or xUnit, Windows BLE APIs via `Microsoft.Windows.SDK.Contracts`, JSON storage via `System.Text.Json`.

## Global Constraints

- App name: `MicroKey Studio`.
- Repository name: `microkey-studio`.
- Implementation language: C# only.
- Framework target: .NET 8 unless local SDK availability requires installing it first.
- Public repository must not include APKs, raw logcat captures, raw HCI captures, phone serial numbers, Bluetooth MAC addresses, or files under `docs/private/`.
- Observed BLE service UUID: `0000FF10-0000-1000-8000-00805F9B34FB`.
- Observed BLE write/notify characteristic UUID: `0000FF13-0000-1000-8000-00805F9B34FB`.
- Initial hardware writes must be gated behind an explicit developer/experimental UI action.

---

## File Structure

Create:

- `global.json`: pins the SDK once .NET 8 SDK is installed.
- `MicroKeyStudio.sln`: solution file.
- `src/MicroKeyStudio.Protocol/MicroKeyStudio.Protocol.csproj`: protocol/model library.
- `src/MicroKeyStudio.Protocol/Buttons/MicroButton.cs`: physical button enum.
- `src/MicroKeyStudio.Protocol/Actions/MappedAction.cs`: target action model.
- `src/MicroKeyStudio.Protocol/Profiles/MappingProfile.cs`: profile model.
- `src/MicroKeyStudio.Protocol/Packets/KnownMicroPackets.cs`: sanitized known packet fixtures.
- `src/MicroKeyStudio.Protocol/Packets/PacketSequence.cs`: named write sequence model.
- `src/MicroKeyStudio.Protocol/Packets/MicroProtocolConstants.cs`: public UUID constants.
- `src/MicroKeyStudio.Storage/MicroKeyStudio.Storage.csproj`: profile persistence library.
- `src/MicroKeyStudio.Storage/ProfileStore.cs`: JSON profile read/write.
- `src/MicroKeyStudio.Ble/MicroKeyStudio.Ble.csproj`: BLE library.
- `src/MicroKeyStudio.Ble/IBleTransport.cs`: testable BLE transport interface.
- `src/MicroKeyStudio.Ble/MicroBleDevice.cs`: discovered device model.
- `src/MicroKeyStudio.Ble/WindowsBleTransport.cs`: Windows BLE implementation.
- `src/MicroKeyStudio.App/MicroKeyStudio.App.csproj`: WPF app.
- `src/MicroKeyStudio.App/App.xaml`: WPF app entry.
- `src/MicroKeyStudio.App/MainWindow.xaml`: main UI.
- `src/MicroKeyStudio.App/MainWindow.xaml.cs`: main window code-behind.
- `src/MicroKeyStudio.App/ViewModels/MainViewModel.cs`: app state and commands.
- `src/MicroKeyStudio.App/ViewModels/RelayCommand.cs`: command helper.
- `tests/MicroKeyStudio.Protocol.Tests/MicroKeyStudio.Protocol.Tests.csproj`: protocol tests.
- `tests/MicroKeyStudio.Protocol.Tests/KnownMicroPacketsTests.cs`: packet fixture tests.
- `tests/MicroKeyStudio.Storage.Tests/MicroKeyStudio.Storage.Tests.csproj`: storage tests.
- `tests/MicroKeyStudio.Storage.Tests/ProfileStoreTests.cs`: JSON profile tests.
- `README.md`: public project introduction.

Modify:

- `.gitignore`: keep build outputs and local hardware artifacts ignored.
- `docs/superpowers/specs/2026-08-16-microkey-studio-design.ko.md`: update status after Phase 1 if needed.

---

### Task 1: Install/Verify .NET SDK and Create Solution

**Files:**
- Create: `global.json`
- Create: `MicroKeyStudio.sln`
- Create: project files under `src/` and `tests/`

**Interfaces:**
- Produces: solution with projects named `MicroKeyStudio.App`, `MicroKeyStudio.Ble`, `MicroKeyStudio.Protocol`, `MicroKeyStudio.Storage`, `MicroKeyStudio.Protocol.Tests`, and `MicroKeyStudio.Storage.Tests`.

- [ ] **Step 1: Verify SDK availability**

Run:

```powershell
dotnet --list-sdks
```

Expected: a line beginning with `8.`. If no SDK is listed, install it:

```powershell
winget install --id Microsoft.DotNet.SDK.8 --exact --accept-package-agreements --accept-source-agreements
```

- [ ] **Step 2: Verify SDK after installation**

Run:

```powershell
dotnet --list-sdks
```

Expected: a .NET 8 SDK line such as `8.0.xxx`.

- [ ] **Step 3: Create SDK pin**

Create `global.json`:

```json
{
  "sdk": {
    "version": "8.0.413",
    "rollForward": "latestFeature"
  }
}
```

If the installed .NET 8 SDK is newer than `8.0.413`, set `version` to the installed `8.0.xxx` value.

- [ ] **Step 4: Create solution and projects**

Run:

```powershell
dotnet new sln -n MicroKeyStudio
dotnet new classlib -n MicroKeyStudio.Protocol -o src/MicroKeyStudio.Protocol -f net8.0
dotnet new classlib -n MicroKeyStudio.Storage -o src/MicroKeyStudio.Storage -f net8.0
dotnet new classlib -n MicroKeyStudio.Ble -o src/MicroKeyStudio.Ble -f net8.0-windows10.0.19041.0
dotnet new wpf -n MicroKeyStudio.App -o src/MicroKeyStudio.App -f net8.0-windows
dotnet new xunit -n MicroKeyStudio.Protocol.Tests -o tests/MicroKeyStudio.Protocol.Tests -f net8.0
dotnet new xunit -n MicroKeyStudio.Storage.Tests -o tests/MicroKeyStudio.Storage.Tests -f net8.0
dotnet sln MicroKeyStudio.sln add src/MicroKeyStudio.Protocol/MicroKeyStudio.Protocol.csproj
dotnet sln MicroKeyStudio.sln add src/MicroKeyStudio.Storage/MicroKeyStudio.Storage.csproj
dotnet sln MicroKeyStudio.sln add src/MicroKeyStudio.Ble/MicroKeyStudio.Ble.csproj
dotnet sln MicroKeyStudio.sln add src/MicroKeyStudio.App/MicroKeyStudio.App.csproj
dotnet sln MicroKeyStudio.sln add tests/MicroKeyStudio.Protocol.Tests/MicroKeyStudio.Protocol.Tests.csproj
dotnet sln MicroKeyStudio.sln add tests/MicroKeyStudio.Storage.Tests/MicroKeyStudio.Storage.Tests.csproj
```

- [ ] **Step 5: Add project references**

Run:

```powershell
dotnet add src/MicroKeyStudio.Storage/MicroKeyStudio.Storage.csproj reference src/MicroKeyStudio.Protocol/MicroKeyStudio.Protocol.csproj
dotnet add src/MicroKeyStudio.Ble/MicroKeyStudio.Ble.csproj reference src/MicroKeyStudio.Protocol/MicroKeyStudio.Protocol.csproj
dotnet add src/MicroKeyStudio.App/MicroKeyStudio.App.csproj reference src/MicroKeyStudio.Protocol/MicroKeyStudio.Protocol.csproj
dotnet add src/MicroKeyStudio.App/MicroKeyStudio.App.csproj reference src/MicroKeyStudio.Storage/MicroKeyStudio.Storage.csproj
dotnet add src/MicroKeyStudio.App/MicroKeyStudio.App.csproj reference src/MicroKeyStudio.Ble/MicroKeyStudio.Ble.csproj
dotnet add tests/MicroKeyStudio.Protocol.Tests/MicroKeyStudio.Protocol.Tests.csproj reference src/MicroKeyStudio.Protocol/MicroKeyStudio.Protocol.csproj
dotnet add tests/MicroKeyStudio.Storage.Tests/MicroKeyStudio.Storage.Tests.csproj reference src/MicroKeyStudio.Storage/MicroKeyStudio.Storage.csproj
```

- [ ] **Step 6: Add Windows SDK package to BLE project**

Run:

```powershell
dotnet add src/MicroKeyStudio.Ble/MicroKeyStudio.Ble.csproj package Microsoft.Windows.SDK.Contracts
```

- [ ] **Step 7: Build solution**

Run:

```powershell
dotnet build MicroKeyStudio.sln
```

Expected: build succeeds.

- [ ] **Step 8: Commit**

```powershell
git add global.json MicroKeyStudio.sln src tests
git commit -m "chore: scaffold MicroKey Studio solution"
```

---

### Task 2: Protocol Models and Known Packet Fixtures

**Files:**
- Create: `src/MicroKeyStudio.Protocol/Buttons/MicroButton.cs`
- Create: `src/MicroKeyStudio.Protocol/Actions/MappedAction.cs`
- Create: `src/MicroKeyStudio.Protocol/Profiles/MappingProfile.cs`
- Create: `src/MicroKeyStudio.Protocol/Packets/MicroProtocolConstants.cs`
- Create: `src/MicroKeyStudio.Protocol/Packets/PacketSequence.cs`
- Create: `src/MicroKeyStudio.Protocol/Packets/KnownMicroPackets.cs`
- Test: `tests/MicroKeyStudio.Protocol.Tests/KnownMicroPacketsTests.cs`

**Interfaces:**
- Produces: `MicroProtocolConstants.ServiceUuid`, `MicroProtocolConstants.WriteCharacteristicUuid`, `KnownMicroPackets.CreateCapturedSaveSequence()`, and `PacketSequence`.
- Consumes: no project-specific types from earlier tasks except the project scaffold.

- [ ] **Step 1: Write failing packet fixture tests**

Create `tests/MicroKeyStudio.Protocol.Tests/KnownMicroPacketsTests.cs`:

```csharp
using MicroKeyStudio.Protocol.Packets;

namespace MicroKeyStudio.Protocol.Tests;

public sealed class KnownMicroPacketsTests
{
    [Fact]
    public void Constants_match_observed_micro_ble_uuids()
    {
        Assert.Equal(Guid.Parse("0000FF10-0000-1000-8000-00805F9B34FB"), MicroProtocolConstants.ServiceUuid);
        Assert.Equal(Guid.Parse("0000FF13-0000-1000-8000-00805F9B34FB"), MicroProtocolConstants.WriteCharacteristicUuid);
    }

    [Fact]
    public void Captured_save_sequence_contains_expected_public_shape()
    {
        PacketSequence sequence = KnownMicroPackets.CreateCapturedSaveSequence();

        Assert.Equal("Captured Micro save sequence", sequence.Name);
        Assert.Equal(17, sequence.Writes.Count);
        Assert.All(sequence.Writes, packet => Assert.StartsWith((byte)0x04, packet));
        Assert.Contains(sequence.Writes, packet => packet.Length == 62);
        Assert.Contains(sequence.Writes, packet => packet.SequenceEqual(new byte[]
        {
            0x04, 0x06, 0x00, 0x5b, 0x00, 0x00, 0x00, 0xff, 0xff, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        }));
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test tests/MicroKeyStudio.Protocol.Tests/MicroKeyStudio.Protocol.Tests.csproj
```

Expected: FAIL because protocol files do not exist yet.

- [ ] **Step 3: Implement protocol constants and sequence type**

Create `src/MicroKeyStudio.Protocol/Packets/MicroProtocolConstants.cs`:

```csharp
namespace MicroKeyStudio.Protocol.Packets;

public static class MicroProtocolConstants
{
    public static readonly Guid ServiceUuid = Guid.Parse("0000FF10-0000-1000-8000-00805F9B34FB");
    public static readonly Guid WriteCharacteristicUuid = Guid.Parse("0000FF13-0000-1000-8000-00805F9B34FB");
}
```

Create `src/MicroKeyStudio.Protocol/Packets/PacketSequence.cs`:

```csharp
namespace MicroKeyStudio.Protocol.Packets;

public sealed record PacketSequence(string Name, IReadOnlyList<byte[]> Writes);
```

- [ ] **Step 4: Implement known captured sequence**

Create `src/MicroKeyStudio.Protocol/Packets/KnownMicroPackets.cs`:

```csharp
namespace MicroKeyStudio.Protocol.Packets;

public static class KnownMicroPackets
{
    public static PacketSequence CreateCapturedSaveSequence()
    {
        return new PacketSequence("Captured Micro save sequence", new[]
        {
            Hex("04 5a 00 00 00 01 00 3e 81 01 00 00 00 00 00 00 00 02"),
            Hex("04 0b 00 00 00 04 00 00 24 04 00 00 00 40 70 01 01 00 00 00 00"),
            Hex("04 11 00 01 00 00 00 ff ff 00 00 00 00 00 00 00 00"),
            Hex("04 11 00 00 00 00 00 ff ff 00 00 00 00 00 00 00 00"),
            Hex("04 02 00 00 00 2d 00 9d 95 b4 00 00 00 00 00 00 00 8c b8 00 00 11 09 20 20 11 09 20 20 28 00 00 00 2a 00 00 00 e0 1a 00 00 e0 e1 17 00 e0 00 00 00 e2 00 00 00 e0 06 00 00 e0 19 00 00 00"),
            Hex("04 02 00 00 00 2d 00 69 07 b4 00 00 00 2d 00 00 00 00 00 00 00 00 00 00 4e 00 00 00 4b 00 00 00 e2 3d 00 00 29 00 00 00 52 00 00 00 51 00 00 00 50 00 00 00 4f 00 00 00 00 00 00 00 00 00"),
            Hex("04 02 00 00 00 2d 00 30 cf b4 00 00 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"),
            Hex("04 02 00 00 00 2d 00 30 cf b4 00 00 00 87 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"),
            Hex("04 50 00 00 05 00 00 ff ff 00 00 00 00 00 00 00 00"),
            Hex("04 50 00 01 05 00 00 ff ff 00 00 00 00 00 00 00 00"),
            Hex("04 03 00 00 00 0c 00 c0 e1 0c 00 00 00 00 00 00 00 00 00 00 00 8f 3d 00 00 00 00 00 00"),
            Hex("04 01 00 00 00 2d 00 9d 95 b4 00 00 00 00 00 00 00 8c b8 00 00 11 09 20 20 11 09 20 20 28 00 00 00 2a 00 00 00 e0 1a 00 00 e0 e1 17 00 e0 00 00 00 e2 00 00 00 e0 06 00 00 e0 19 00 00 00"),
            Hex("04 01 00 00 00 2d 00 9f e7 b4 00 00 00 2d 00 00 00 00 00 00 00 00 00 00 4e 00 00 00 4b 00 00 00 e2 3d 00 00 29 00 00 00 00 00 00 00 51 00 00 00 50 00 00 00 4f 00 00 00 00 00 00 00 00 00"),
            Hex("04 01 00 00 00 2d 00 30 cf b4 00 00 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"),
            Hex("04 01 00 00 00 2d 00 30 cf b4 00 00 00 87 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"),
            Hex("04 06 00 5b 00 00 00 ff ff 00 00 00 00 00 00 00 00"),
            Hex("04 03 00 00 00 0c 00 92 d8 0c 00 00 00 00 00 00 00 00 00 00 00 9c 1e 02 00 00 00 00 00")
        });
    }

    private static byte[] Hex(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Convert.ToByte(part, 16))
            .ToArray();
    }
}
```

- [ ] **Step 5: Implement initial mapping models**

Create `src/MicroKeyStudio.Protocol/Buttons/MicroButton.cs`:

```csharp
namespace MicroKeyStudio.Protocol.Buttons;

public enum MicroButton
{
    A,
    B,
    X,
    Y,
    Up,
    Down,
    Left,
    Right,
    L,
    R,
    L2,
    R2,
    Minus,
    Plus,
    Home,
    Star,
    P1,
    P2,
    LStick,
    RStick
}
```

Create `src/MicroKeyStudio.Protocol/Actions/MappedAction.cs`:

```csharp
namespace MicroKeyStudio.Protocol.Actions;

public abstract record MappedAction
{
    public sealed record KeyboardKey(string Key) : MappedAction;
    public sealed record KeyChord(IReadOnlyList<string> Keys) : MappedAction;
    public sealed record MouseAction(string Action) : MappedAction;
    public sealed record MediaKey(string Key) : MappedAction;
    public sealed record Macro(string MacroId) : MappedAction;
    public sealed record Disabled : MappedAction;
    public sealed record PassThrough : MappedAction;
}
```

Create `src/MicroKeyStudio.Protocol/Profiles/MappingProfile.cs`:

```csharp
using MicroKeyStudio.Protocol.Actions;
using MicroKeyStudio.Protocol.Buttons;

namespace MicroKeyStudio.Protocol.Profiles;

public sealed record MappingProfile(
    string Id,
    string Name,
    int SchemaVersion,
    IReadOnlyDictionary<MicroButton, MappedAction> Mappings);
```

- [ ] **Step 6: Run protocol tests**

Run:

```powershell
dotnet test tests/MicroKeyStudio.Protocol.Tests/MicroKeyStudio.Protocol.Tests.csproj
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add src/MicroKeyStudio.Protocol tests/MicroKeyStudio.Protocol.Tests
git commit -m "feat: add Micro protocol models and captured sequence"
```

---

### Task 3: Profile JSON Storage

**Files:**
- Create: `src/MicroKeyStudio.Storage/ProfileStore.cs`
- Test: `tests/MicroKeyStudio.Storage.Tests/ProfileStoreTests.cs`

**Interfaces:**
- Consumes: `MappingProfile`, `MicroButton`, `MappedAction`.
- Produces: `ProfileStore.SaveAsync(MappingProfile profile, string path, CancellationToken cancellationToken)` and `ProfileStore.LoadAsync(string path, CancellationToken cancellationToken)`.

- [ ] **Step 1: Write failing storage tests**

Create `tests/MicroKeyStudio.Storage.Tests/ProfileStoreTests.cs`:

```csharp
using MicroKeyStudio.Protocol.Actions;
using MicroKeyStudio.Protocol.Buttons;
using MicroKeyStudio.Protocol.Profiles;

namespace MicroKeyStudio.Storage.Tests;

public sealed class ProfileStoreTests
{
    [Fact]
    public async Task Profile_round_trips_to_json()
    {
        string path = Path.Combine(Path.GetTempPath(), $"microkey-profile-{Guid.NewGuid():N}.json");
        var profile = new MappingProfile(
            "default",
            "Default",
            1,
            new Dictionary<MicroButton, MappedAction>
            {
                [MicroButton.A] = new MappedAction.KeyboardKey("Enter"),
                [MicroButton.B] = new MappedAction.KeyChord(new[] { "Ctrl", "C" })
            });

        await ProfileStore.SaveAsync(profile, path, CancellationToken.None);
        MappingProfile loaded = await ProfileStore.LoadAsync(path, CancellationToken.None);

        Assert.Equal(profile.Id, loaded.Id);
        Assert.Equal(profile.Name, loaded.Name);
        Assert.Equal(profile.SchemaVersion, loaded.SchemaVersion);
        Assert.Equal(new MappedAction.KeyboardKey("Enter"), loaded.Mappings[MicroButton.A]);
        Assert.Equal(new MappedAction.KeyChord(new[] { "Ctrl", "C" }), loaded.Mappings[MicroButton.B]);
    }

    [Fact]
    public async Task Load_rejects_missing_file()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        await Assert.ThrowsAsync<FileNotFoundException>(() => ProfileStore.LoadAsync(path, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:

```powershell
dotnet test tests/MicroKeyStudio.Storage.Tests/MicroKeyStudio.Storage.Tests.csproj
```

Expected: FAIL because `ProfileStore` does not exist.

- [ ] **Step 3: Implement JSON polymorphism and file IO**

Create `src/MicroKeyStudio.Storage/ProfileStore.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using MicroKeyStudio.Protocol.Actions;
using MicroKeyStudio.Protocol.Profiles;

namespace MicroKeyStudio.Storage;

public static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static async Task SaveAsync(MappingProfile profile, string path, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, profile, Options, cancellationToken);
    }

    public static async Task<MappingProfile> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Profile file was not found.", path);
        }

        await using FileStream stream = File.OpenRead(path);
        MappingProfile? profile = await JsonSerializer.DeserializeAsync<MappingProfile>(stream, Options, cancellationToken);
        return profile ?? throw new InvalidDataException("Profile file did not contain a profile.");
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new JsonDerivedTypeConverter<MappedAction>(
            new Dictionary<string, Type>
            {
                ["keyboardKey"] = typeof(MappedAction.KeyboardKey),
                ["keyChord"] = typeof(MappedAction.KeyChord),
                ["mouseAction"] = typeof(MappedAction.MouseAction),
                ["mediaKey"] = typeof(MappedAction.MediaKey),
                ["macro"] = typeof(MappedAction.Macro),
                ["disabled"] = typeof(MappedAction.Disabled),
                ["passThrough"] = typeof(MappedAction.PassThrough)
            }));
        return options;
    }
}
```

Create helper in the same file below `ProfileStore`:

```csharp
internal sealed class JsonDerivedTypeConverter<TBase> : JsonConverter<TBase>
{
    private readonly IReadOnlyDictionary<string, Type> _typeByName;
    private readonly IReadOnlyDictionary<Type, string> _nameByType;

    public JsonDerivedTypeConverter(IReadOnlyDictionary<string, Type> typeByName)
    {
        _typeByName = typeByName;
        _nameByType = typeByName.ToDictionary(pair => pair.Value, pair => pair.Key);
    }

    public override TBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        if (!document.RootElement.TryGetProperty("type", out JsonElement typeElement))
        {
            throw new JsonException("Missing derived type discriminator.");
        }

        string? typeName = typeElement.GetString();
        if (typeName is null || !_typeByName.TryGetValue(typeName, out Type? runtimeType))
        {
            throw new JsonException($"Unknown derived type discriminator '{typeName}'.");
        }

        string json = document.RootElement.GetRawText();
        return (TBase)(JsonSerializer.Deserialize(json, runtimeType, options)
            ?? throw new JsonException("Derived value was null."));
    }

    public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
    {
        Type runtimeType = value?.GetType() ?? throw new JsonException("Cannot serialize null derived value.");
        if (!_nameByType.TryGetValue(runtimeType, out string? typeName))
        {
            throw new JsonException($"Unsupported derived type '{runtimeType.FullName}'.");
        }

        JsonElement element = JsonSerializer.SerializeToElement(value, runtimeType, options);
        writer.WriteStartObject();
        writer.WriteString("type", typeName);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            property.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}
```

- [ ] **Step 4: Run storage tests**

Run:

```powershell
dotnet test tests/MicroKeyStudio.Storage.Tests/MicroKeyStudio.Storage.Tests.csproj
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/MicroKeyStudio.Storage tests/MicroKeyStudio.Storage.Tests
git commit -m "feat: add JSON profile storage"
```

---

### Task 4: BLE Transport Boundary and Windows Implementation

**Files:**
- Create: `src/MicroKeyStudio.Ble/IBleTransport.cs`
- Create: `src/MicroKeyStudio.Ble/MicroBleDevice.cs`
- Create: `src/MicroKeyStudio.Ble/WindowsBleTransport.cs`

**Interfaces:**
- Consumes: `PacketSequence`, `MicroProtocolConstants`.
- Produces: `IBleTransport.ScanAsync`, `IBleTransport.ConnectAsync`, `IBleTransport.WriteSequenceAsync`, `IBleTransport.DisconnectAsync`.

- [ ] **Step 1: Define transport interface**

Create `src/MicroKeyStudio.Ble/IBleTransport.cs`:

```csharp
using MicroKeyStudio.Protocol.Packets;

namespace MicroKeyStudio.Ble;

public interface IBleTransport : IAsyncDisposable
{
    event EventHandler<byte[]>? NotificationReceived;

    Task<IReadOnlyList<MicroBleDevice>> ScanAsync(CancellationToken cancellationToken);

    Task ConnectAsync(MicroBleDevice device, CancellationToken cancellationToken);

    Task WriteSequenceAsync(PacketSequence sequence, TimeSpan delayBetweenWrites, CancellationToken cancellationToken);

    Task DisconnectAsync();
}
```

Create `src/MicroKeyStudio.Ble/MicroBleDevice.cs`:

```csharp
namespace MicroKeyStudio.Ble;

public sealed record MicroBleDevice(string Id, string Name, short? Rssi);
```

- [ ] **Step 2: Implement Windows BLE transport**

Create `src/MicroKeyStudio.Ble/WindowsBleTransport.cs`:

```csharp
using MicroKeyStudio.Protocol.Packets;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace MicroKeyStudio.Ble;

public sealed class WindowsBleTransport : IBleTransport
{
    private BluetoothLEDevice? _device;
    private GattCharacteristic? _writeCharacteristic;

    public event EventHandler<byte[]>? NotificationReceived;

    public async Task<IReadOnlyList<MicroBleDevice>> ScanAsync(CancellationToken cancellationToken)
    {
        var devices = new Dictionary<ulong, MicroBleDevice>();
        using var watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };

        watcher.Received += (_, args) =>
        {
            string name = args.Advertisement.LocalName;
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            if (name.Contains("8BitDo", StringComparison.OrdinalIgnoreCase) || name.StartsWith("80", StringComparison.OrdinalIgnoreCase))
            {
                devices[args.BluetoothAddress] = new MicroBleDevice(args.BluetoothAddress.ToString("X"), name, args.RawSignalStrengthInDBm);
            }
        };

        watcher.Start();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
        finally
        {
            watcher.Stop();
        }

        return devices.Values.OrderBy(device => device.Name).ToArray();
    }

    public async Task ConnectAsync(MicroBleDevice device, CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(device.Id, System.Globalization.NumberStyles.HexNumber, null, out ulong address))
        {
            throw new InvalidOperationException($"Device id '{device.Id}' is not a BLE address.");
        }

        _device = await BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(cancellationToken);
        if (_device is null)
        {
            throw new InvalidOperationException("Windows could not open the BLE device.");
        }

        GattDeviceServicesResult services = await _device.GetGattServicesForUuidAsync(
            MicroProtocolConstants.ServiceUuid,
            BluetoothCacheMode.Uncached).AsTask(cancellationToken);

        if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
        {
            throw new InvalidOperationException($"Micro service was not found. Status: {services.Status}");
        }

        GattDeviceService service = services.Services[0];
        GattCharacteristicsResult characteristics = await service.GetCharacteristicsForUuidAsync(
            MicroProtocolConstants.WriteCharacteristicUuid,
            BluetoothCacheMode.Uncached).AsTask(cancellationToken);

        if (characteristics.Status != GattCommunicationStatus.Success || characteristics.Characteristics.Count == 0)
        {
            throw new InvalidOperationException($"Micro write characteristic was not found. Status: {characteristics.Status}");
        }

        _writeCharacteristic = characteristics.Characteristics[0];
        _writeCharacteristic.ValueChanged += (_, args) =>
        {
            byte[] data = new byte[args.CharacteristicValue.Length];
            DataReader.FromBuffer(args.CharacteristicValue).ReadBytes(data);
            NotificationReceived?.Invoke(this, data);
        };

        await _writeCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask(cancellationToken);
    }

    public async Task WriteSequenceAsync(PacketSequence sequence, TimeSpan delayBetweenWrites, CancellationToken cancellationToken)
    {
        if (_writeCharacteristic is null)
        {
            throw new InvalidOperationException("Device is not connected.");
        }

        foreach (byte[] packet in sequence.Writes)
        {
            using var writer = new DataWriter();
            writer.WriteBytes(packet);
            GattCommunicationStatus status = await _writeCharacteristic.WriteValueAsync(
                writer.DetachBuffer(),
                GattWriteOption.WriteWithResponse).AsTask(cancellationToken);

            if (status != GattCommunicationStatus.Success)
            {
                throw new InvalidOperationException($"BLE write failed with status {status}.");
            }

            await Task.Delay(delayBetweenWrites, cancellationToken);
        }
    }

    public Task DisconnectAsync()
    {
        _writeCharacteristic = null;
        _device?.Dispose();
        _device = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
```

- [ ] **Step 3: Build BLE project**

Run:

```powershell
dotnet build src/MicroKeyStudio.Ble/MicroKeyStudio.Ble.csproj
```

Expected: PASS. If Windows SDK references require `net8.0-windows10.0.19041.0`, update the BLE project target framework exactly to that value.

- [ ] **Step 4: Commit**

```powershell
git add src/MicroKeyStudio.Ble
git commit -m "feat: add Windows BLE transport"
```

---

### Task 5: WPF App Shell and Developer Replay Screen

**Files:**
- Create/Modify: `src/MicroKeyStudio.App/App.xaml`
- Create/Modify: `src/MicroKeyStudio.App/MainWindow.xaml`
- Create/Modify: `src/MicroKeyStudio.App/MainWindow.xaml.cs`
- Create: `src/MicroKeyStudio.App/ViewModels/MainViewModel.cs`
- Create: `src/MicroKeyStudio.App/ViewModels/RelayCommand.cs`

**Interfaces:**
- Consumes: `IBleTransport`, `WindowsBleTransport`, `KnownMicroPackets.CreateCapturedSaveSequence()`.
- Produces: user-visible WPF app with scan, connect, replay captured sequence, and diagnostic log.

- [ ] **Step 1: Create command helper**

Create `src/MicroKeyStudio.App/ViewModels/RelayCommand.cs`:

```csharp
using System.Windows.Input;

namespace MicroKeyStudio.App.ViewModels;

public sealed class RelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isRunning = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

- [ ] **Step 2: Create view model**

Create `src/MicroKeyStudio.App/ViewModels/MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MicroKeyStudio.Ble;
using MicroKeyStudio.Protocol.Packets;

namespace MicroKeyStudio.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly IBleTransport _transport;
    private MicroBleDevice? _selectedDevice;
    private string _status = "Ready";
    private bool _isConnected;

    public MainViewModel() : this(new WindowsBleTransport())
    {
    }

    public MainViewModel(IBleTransport transport)
    {
        _transport = transport;
        _transport.NotificationReceived += (_, data) => AddLog($"Notify: {Convert.ToHexString(data)}");
        ScanCommand = new RelayCommand(ScanAsync);
        ConnectCommand = new RelayCommand(ConnectAsync, () => SelectedDevice is not null && !IsConnected);
        ReplayCapturedSequenceCommand = new RelayCommand(ReplayCapturedSequenceAsync, () => IsConnected);
        DisconnectCommand = new RelayCommand(DisconnectAsync, () => IsConnected);
    }

    public ObservableCollection<MicroBleDevice> Devices { get; } = new();

    public ObservableCollection<string> LogLines { get; } = new();

    public MicroBleDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            _selectedDevice = value;
            OnPropertyChanged();
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged();
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            _isConnected = value;
            OnPropertyChanged();
        }
    }

    public ICommand ScanCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand ReplayCapturedSequenceCommand { get; }
    public ICommand DisconnectCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task ScanAsync()
    {
        Status = "Scanning...";
        Devices.Clear();
        IReadOnlyList<MicroBleDevice> devices = await _transport.ScanAsync(CancellationToken.None);
        foreach (MicroBleDevice device in devices)
        {
            Devices.Add(device);
        }
        Status = $"Found {Devices.Count} device(s).";
    }

    private async Task ConnectAsync()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        Status = $"Connecting to {SelectedDevice.Name}...";
        await _transport.ConnectAsync(SelectedDevice, CancellationToken.None);
        IsConnected = true;
        Status = $"Connected to {SelectedDevice.Name}.";
    }

    private async Task ReplayCapturedSequenceAsync()
    {
        Status = "Writing captured sequence...";
        PacketSequence sequence = KnownMicroPackets.CreateCapturedSaveSequence();
        await _transport.WriteSequenceAsync(sequence, TimeSpan.FromMilliseconds(120), CancellationToken.None);
        Status = "Captured sequence written.";
    }

    private async Task DisconnectAsync()
    {
        await _transport.DisconnectAsync();
        IsConnected = false;
        Status = "Disconnected.";
    }

    private void AddLog(string message)
    {
        App.Current.Dispatcher.Invoke(() => LogLines.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}"));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public async ValueTask DisposeAsync()
    {
        await _transport.DisposeAsync();
    }
}
```

- [ ] **Step 3: Create WPF layout**

Replace `src/MicroKeyStudio.App/MainWindow.xaml` with:

```xml
<Window x:Class="MicroKeyStudio.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="MicroKey Studio" Height="720" Width="1080" MinHeight="620" MinWidth="900">
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="160" />
        </Grid.RowDefinitions>

        <DockPanel Grid.Row="0" LastChildFill="False">
            <TextBlock Text="MicroKey Studio" FontSize="24" FontWeight="SemiBold" DockPanel.Dock="Left" />
            <TextBlock Text="{Binding Status}" Margin="24,6,0,0" VerticalAlignment="Center" DockPanel.Dock="Left" />
        </DockPanel>

        <Grid Grid.Row="1" Margin="0,16,0,16">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="280" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Column="0">
                <Button Content="Scan" Command="{Binding ScanCommand}" Height="36" />
                <ListBox ItemsSource="{Binding Devices}" SelectedItem="{Binding SelectedDevice}" Margin="0,8,0,8" Height="260">
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <StackPanel>
                                <TextBlock Text="{Binding Name}" FontWeight="SemiBold" />
                                <TextBlock Text="{Binding Id}" FontSize="11" Foreground="Gray" />
                            </StackPanel>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
                <Button Content="Connect" Command="{Binding ConnectCommand}" Height="36" />
                <Button Content="Disconnect" Command="{Binding DisconnectCommand}" Height="36" Margin="0,8,0,0" />
            </StackPanel>

            <Grid Grid.Column="1" Margin="24,0,0,0">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto" />
                    <RowDefinition Height="*" />
                </Grid.RowDefinitions>
                <TextBlock Text="Developer Protocol Panel" FontSize="18" FontWeight="SemiBold" />
                <StackPanel Grid.Row="1" Margin="0,16,0,0">
                    <TextBlock TextWrapping="Wrap"
                               Text="Experimental hardware write. This replays a sanitized captured sequence to the connected 8BitDo Micro. Use only while testing with a recoverable mapping." />
                    <Button Content="Replay Captured Save Sequence"
                            Command="{Binding ReplayCapturedSequenceCommand}"
                            Height="40"
                            Width="260"
                            HorizontalAlignment="Left"
                            Margin="0,16,0,0" />
                </StackPanel>
            </Grid>
        </Grid>

        <ListBox Grid.Row="2" ItemsSource="{Binding LogLines}" />
    </Grid>
</Window>
```

- [ ] **Step 4: Wire MainWindow DataContext**

Replace `src/MicroKeyStudio.App/MainWindow.xaml.cs` with:

```csharp
using System.Windows;
using MicroKeyStudio.App.ViewModels;

namespace MicroKeyStudio.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    protected override async void OnClosed(EventArgs e)
    {
        await _viewModel.DisposeAsync();
        base.OnClosed(e);
    }
}
```

- [ ] **Step 5: Build app**

Run:

```powershell
dotnet build src/MicroKeyStudio.App/MicroKeyStudio.App.csproj
```

Expected: PASS.

- [ ] **Step 6: Manual UI smoke test**

Run:

```powershell
dotnet run --project src/MicroKeyStudio.App/MicroKeyStudio.App.csproj
```

Expected: WPF window opens, Scan button is visible, protocol panel is visible, and app closes cleanly.

- [ ] **Step 7: Commit**

```powershell
git add src/MicroKeyStudio.App
git commit -m "feat: add WPF developer replay shell"
```

---

### Task 6: README and Phase 1 Verification Notes

**Files:**
- Create: `README.md`
- Create: `docs/protocol-notes.md`
- Create: `docs/capture-analysis.md`

**Interfaces:**
- Consumes: public design docs and sanitized protocol facts.
- Produces: GitHub-ready public documentation without private values.

- [ ] **Step 1: Create README**

Create `README.md`:

```markdown
# MicroKey Studio

MicroKey Studio is an unofficial Windows key mapping tool for the 8BitDo Micro.

The project is built with C#/.NET 8 and WPF. Its goal is device-level key mapping: settings are written to the controller so they persist after the app closes.

## Status

Early development. The current milestone focuses on BLE connection, protocol replay, and safe documentation of the reverse-engineering process.

## Safety

This is an experimental community project. Writing unsupported configuration data to a device can change its behavior. Use test mappings first and keep the official mobile app available for recovery/reset.

## Architecture

- `MicroKeyStudio.App`: WPF UI.
- `MicroKeyStudio.Ble`: Windows BLE transport.
- `MicroKeyStudio.Protocol`: protocol constants, packet fixtures, mapping model.
- `MicroKeyStudio.Storage`: local profile storage.

## Protocol Notes

Observed public Micro app-mode UUIDs:

- Service: `0000FF10-0000-1000-8000-00805F9B34FB`
- Write/notify characteristic: `0000FF13-0000-1000-8000-00805F9B34FB`

Raw captures, APKs, device addresses, and phone identifiers are intentionally excluded from this repository.
```

- [ ] **Step 2: Create protocol notes**

Create `docs/protocol-notes.md`:

```markdown
# Protocol Notes

This document contains sanitized protocol observations for MicroKey Studio.

## Observed BLE UUIDs

- Service UUID: `0000FF10-0000-1000-8000-00805F9B34FB`
- Write/notify characteristic UUID: `0000FF13-0000-1000-8000-00805F9B34FB`

## Observed App Flow

1. The official app discovers the Micro in BLE app mode.
2. It connects and configures MTU.
3. It resolves the Micro service and characteristic.
4. It subscribes to notifications.
5. It sends report-enable and configuration packets.
6. It sends save/commit packets.

## Notes

The first implementation replays a sanitized known-good packet sequence behind a developer-only UI action. Future work will replace replay with generated packets from the decoded mapping model.
```

- [ ] **Step 3: Create capture analysis notes**

Create `docs/capture-analysis.md`:

```markdown
# Capture Analysis

The Android official app logs outgoing BLE payloads with `writeBleHidData:`. Local development used ADB logcat to inspect those payloads.

Private data excluded from public docs:

- Phone serial numbers.
- Bluetooth MAC addresses.
- Raw logcat output.
- APK files and decompiled APK trees.

Public findings:

- The official app sends byte arrays through a Java BLE helper after native packet construction.
- The Micro configuration path includes native functions named similarly to `writeMicroCustomConfig`, `readMicroCustomConfig`, `setMicroReportEnable`, and `readVersion_Micro`.
- The first public app milestone uses sanitized packet fixtures for a hardware replay test.
```

- [ ] **Step 4: Verify private values are absent**

Run:

```powershell
rg -n "phone-serial|bluetooth-address|absolute-private-path|raw-logcat|raw-apk" README.md docs
```

Expected: no matches in public files. If exact private paths or identifiers appear, remove them before committing.

- [ ] **Step 5: Run full tests and build**

Run:

```powershell
dotnet test MicroKeyStudio.sln
dotnet build MicroKeyStudio.sln
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add README.md docs/protocol-notes.md docs/capture-analysis.md
git commit -m "docs: add public project overview and protocol notes"
```

---

## Self-Review

Spec coverage:

- C#/.NET WPF architecture: covered by Tasks 1 and 5.
- BLE service and characteristic handling: covered by Task 4.
- Captured packet replay: covered by Tasks 2 and 5.
- Local profiles: covered by Task 3.
- Public/private data separation: covered by Task 6 and existing `.gitignore`.
- Full official-app-level features: intentionally deferred to later implementation plans after the hardware write path is verified.

Placeholder scan:

- The plan contains no placeholder markers.
- Future work is described as roadmap scope, not as an implementation step inside Phase 1.

Type consistency:

- `PacketSequence`, `KnownMicroPackets`, `MicroProtocolConstants`, `MappingProfile`, `MappedAction`, `MicroButton`, `IBleTransport`, and `MicroBleDevice` are defined before use.

## Execution Recommendation

Use subagent-driven development if multiple independent agents are available. Otherwise use inline execution and complete Task 1 through Task 6 in order, with a commit after each task.
