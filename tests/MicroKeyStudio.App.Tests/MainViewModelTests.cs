using MicroKeyStudio.App.ViewModels;
using MicroKeyStudio.Ble;
using MicroKeyStudio.Protocol.Actions;
using MicroKeyStudio.Protocol.Buttons;
using MicroKeyStudio.Protocol.Configuration;
using MicroKeyStudio.Protocol.Packets;
using MicroKeyStudio.Protocol.Profiles;
using MicroKeyStudio.Storage;

namespace MicroKeyStudio.App.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task Editing_a_mapping_blocks_device_save_until_applied_or_canceled()
    {
        using var transport = new FakeBleTransport();
        transport.QueueCollectedNotificationBatch(CreateValidConfigPages());
        var viewModel = CreateSaveViewModel(transport, new FakeConfirmationService());
        await ConnectAsync(viewModel);
        Assert.True(viewModel.SaveToDeviceCommand.CanExecute(null));
        int notifications = 0;
        viewModel.SaveToDeviceCommand.CanExecuteChanged += (_, _) => notifications++;

        viewModel.SelectMapping(viewModel.LeftMappings[0]);
        viewModel.PendingAction = "F1";

        Assert.False(viewModel.SaveToDeviceCommand.CanExecute(null));
        await ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();
        Assert.Empty(transport.WriteInvocations);

        await ((RelayCommand)viewModel.CancelMappingEditCommand).ExecuteAsync();
        Assert.True(viewModel.SaveToDeviceCommand.CanExecute(null));
        Assert.True(notifications > 0);

        viewModel.SelectMapping(viewModel.LeftMappings[0]);
        viewModel.PendingAction = "F2";
        await ((RelayCommand)viewModel.ApplySelectedMappingCommand).ExecuteAsync();
        Assert.False(viewModel.IsMappingEditorOpen);
        Assert.True(viewModel.SaveToDeviceCommand.CanExecute(null));
        Assert.Equal("F2", viewModel.LeftMappings[0].Action);
        Assert.Empty(transport.WriteInvocations);
    }

    [Fact]
    public void Default_mapping_display_has_left_and_right_button_rows()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());

        Assert.Equal(8, viewModel.LeftMappings.Count);
        Assert.Equal(8, viewModel.RightMappings.Count);
        Assert.Empty(viewModel.DeviceProfiles);
        Assert.Equal("프로필 0개", viewModel.ProfileSummaryText);
        Assert.Contains(viewModel.LeftMappings, row => row.Button == "L2" && row.Action == "Win+Z");
        Assert.Contains(viewModel.RightMappings, row => row.Button == "R2" && row.Action == "Win+S");
        Assert.Equal("Slot 09", viewModel.LeftMappings.Single(row => row.Id == "L2").SourceSlotText);
        Assert.Equal("Slot 03", viewModel.RightMappings.Single(row => row.Id == "A").SourceSlotText);
    }

    [Fact]
    public void Settings_offer_korean_and_english_language_choices()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());

        Assert.Equal(new[] { "한국어", "English" }, viewModel.Languages);
        Assert.Equal("한국어", viewModel.SelectedLanguage);

        viewModel.SelectedLanguage = "English";

        Assert.Equal("English", viewModel.SelectedLanguage);
    }

    [Fact]
    public void Language_selection_updates_labels_and_button_names()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());

        Assert.Equal("기기 찾기", viewModel.Text.FindDevice);
        Assert.Equal("위", viewModel.LeftMappings.Single(mapping => mapping.Id == "Up").Button);

        viewModel.SelectedLanguage = "English";

        Assert.Equal("Find device", viewModel.Text.FindDevice);
        Assert.Equal("Up", viewModel.LeftMappings.Single(mapping => mapping.Id == "Up").Button);
    }

    [Fact]
    public void Editor_and_device_save_labels_are_localized_in_both_languages()
    {
        var expectedLabels = new Dictionary<string, (string Korean, string English)>
        {
            ["Back"] = ("뒤로", "Back"),
            ["PhysicalKeyboardCapture"] = ("실제 키보드 입력", "Physical keyboard capture"),
            ["CaptureInstructions"] = ("아래 영역을 누른 뒤 원하는 키 또는 키 조합을 누르세요.", "Click below, then press a key or key chord."),
            ["FreeformAction"] = ("자유 입력 동작", "Freeform action"),
            ["Cancel"] = ("취소", "Cancel"),
            ["Apply"] = ("PC 프로필에 적용", "Apply to PC profile"),
            ["FooterText"] = (
                "기기 매핑을 저장하면 저장 직전 백업을 만들고 저장 후 전체 설정을 다시 읽어 검증합니다.",
                "Saving device mappings creates a pre-save backup and verifies the complete settings with a fresh readback."),
            ["SaveToDevice"] = ("기기에 저장", "Save to device"),
            ["RestoreOriginalSettings"] = ("최근 백업 복원", "Restore latest backup"),
            ["Confirmation"] = ("확인", "Confirmation"),
            ["UnsupportedMapping"] = ("지원하지 않는 매핑", "Unsupported mapping"),
            ["ReadbackMismatch"] = ("다시 읽은 값이 일치하지 않습니다", "Readback mismatch"),
            ["VerifiedSuccess"] = ("검증 완료", "Verified success")
        };

        foreach ((string propertyName, (string korean, string english)) in expectedLabels)
        {
            var property = typeof(UiText).GetProperty(propertyName);

            Assert.NotNull(property);
            Assert.Equal(korean, property.GetValue(UiText.Korean));
            Assert.Equal(english, property.GetValue(UiText.English));
        }
    }

    [Fact]
    public async Task Sleep_setting_is_enabled_after_a_complete_device_read_and_reflects_the_flag()
    {
        using var transport = new FakeBleTransport();
        transport.QueueCollectedNotificationBatch(CreateValidConfigPages());
        var viewModel = CreateSaveViewModel(transport, new FakeConfirmationService());

        Assert.False(viewModel.IsSleepSettingSupported);
        Assert.False(viewModel.IsSleepDisabled);

        await ConnectAsync(viewModel);

        Assert.True(viewModel.IsSleepSettingSupported);
        Assert.False(viewModel.IsSleepDisabled);
        Assert.Contains("저장", viewModel.SleepSettingStatusText);

        viewModel.SelectedLanguage = "English";

        Assert.Contains("save", viewModel.SleepSettingStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scan_is_disabled_while_a_device_is_connected()
    {
        using var transport = new FakeBleTransport();
        transport.QueueCollectedNotificationBatch(CreateValidConfigPages());
        var viewModel = CreateSaveViewModel(transport, new FakeConfirmationService());

        await ConnectAsync(viewModel);

        Assert.False(viewModel.ScanCommand.CanExecute(null));
    }

    [Fact]
    public async Task Unknown_sleep_value_stays_read_only_and_is_preserved_during_a_mapping_save()
    {
        using var transport = new FakeBleTransport();
        byte[] unknownPayload = GetPayload(CreateValidConfigPages());
        unknownPayload[MicroConfigSnapshot.DisableSleepOffset] = 0x7f;
        byte[][] unknownPages = CreateConfigPages(unknownPayload);
        transport.QueueCollectedNotificationBatch(unknownPages);
        transport.QueueCollectedNotificationBatch(unknownPages);
        transport.QueueCollectedNotificationBatch(unknownPages);
        var viewModel = CreateSaveViewModel(transport, new FakeConfirmationService());
        viewModel.SelectedLanguage = "English";

        await ConnectAsync(viewModel);

        Assert.False(viewModel.IsSleepSettingSupported);
        await ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();

        PacketSequence written = Assert.Single(transport.WriteInvocations).Sequence!;
        Assert.Equal(0x7f, GetSavePayload(written)[MicroConfigSnapshot.DisableSleepOffset]);
        Assert.Contains("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reset_changes_restores_the_last_device_read_without_writing_to_the_device()
    {
        using var transport = new FakeBleTransport();
        transport.QueueCollectedNotificationBatch(CreateValidConfigPages());
        var confirmation = new FakeConfirmationService();
        var viewModel = CreateSaveViewModel(transport, confirmation);

        Assert.False(viewModel.ResetMappingsCommand.CanExecute(null));
        await ConnectAsync(viewModel);
        ButtonMappingDisplay mapping = viewModel.RightMappings.Single(item => item.Id == "X");
        Assert.Equal("Ctrl+W", mapping.Action);
        viewModel.SelectMapping(mapping);
        viewModel.PendingAction = "F1";
        viewModel.ApplySelectedMapping();
        viewModel.IsSleepDisabled = true;
        Assert.True(viewModel.ResetMappingsCommand.CanExecute(null));

        await ((RelayCommand)viewModel.ResetMappingsCommand).ExecuteAsync();

        Assert.Equal("Ctrl+W", mapping.Action);
        Assert.Equal("Ctrl+W", viewModel.SelectedProfile!.ActionsByButtonId["X"]);
        Assert.False(viewModel.IsSleepDisabled);
        Assert.False(viewModel.ResetMappingsCommand.CanExecute(null));
        Assert.Equal(1, confirmation.CallCount);
        Assert.Empty(transport.WriteInvocations);
        Assert.Contains("되돌렸", viewModel.Status);
    }

    [Fact]
    public async Task Reset_changes_cancellation_preserves_the_edited_mapping()
    {
        using var transport = new FakeBleTransport();
        transport.QueueCollectedNotificationBatch(CreateValidConfigPages());
        var confirmation = new FakeConfirmationService(result: false);
        var viewModel = CreateSaveViewModel(transport, confirmation);
        await ConnectAsync(viewModel);
        ButtonMappingDisplay mapping = viewModel.LeftMappings.Single(item => item.Id == "L");
        viewModel.SelectMapping(mapping);
        viewModel.PendingAction = "F2";
        viewModel.ApplySelectedMapping();

        await ((RelayCommand)viewModel.ResetMappingsCommand).ExecuteAsync();

        Assert.Equal("F2", mapping.Action);
        Assert.Equal("F2", viewModel.SelectedProfile!.ActionsByButtonId["L"]);
        Assert.Empty(transport.WriteInvocations);
    }

    [Fact]
    public void Mapping_editor_selecting_a_row_opens_with_a_draft_and_cancel_discards_it()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());
        ButtonMappingDisplay row = viewModel.LeftMappings.Single(mapping => mapping.Id == "L");
        var profile = new DeviceProfileDisplay(
            "profile-1",
            "Shortcuts",
            new Dictionary<string, string> { ["L"] = "Win+Z" });
        viewModel.DeviceProfiles.Add(profile);
        viewModel.SelectedProfile = profile;

        viewModel.SelectMapping(row);
        Assert.Same(row, viewModel.SelectedMapping);
        Assert.True(viewModel.IsMappingEditorOpen);
        Assert.Equal("L", viewModel.EditorTitleText);
        Assert.Equal("Win+Z", viewModel.PendingAction);

        viewModel.PendingAction = "Ctrl+C";
        viewModel.CancelMappingEditCommand.Execute(null);

        Assert.Equal("Win+Z", row.Action);
        Assert.Equal("Win+Z", profile.ActionsByButtonId["L"]);
        Assert.Equal("Win+Z", viewModel.PendingAction);
        Assert.False(viewModel.IsMappingEditorOpen);
    }

    [Fact]
    public void Key_picker_selection_updates_the_editor_draft_only()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());
        ButtonMappingDisplay row = viewModel.LeftMappings.Single(mapping => mapping.Id == "L");

        viewModel.SelectMapping(row);
        viewModel.SelectedKeyCategory = viewModel.KeyCategories.Single(category => category.Id == "numeric-keypad");
        KeyOptionDisplay option = viewModel.AvailableKeyOptions.Single(key => key.Action == "Keypad 0");
        viewModel.SelectKeyOptionCommand.Execute(option);

        Assert.Equal("Win+Z", row.Action);
        Assert.Equal("Keypad 0", viewModel.PendingAction);
    }

    [Fact]
    public void Language_selection_updates_key_picker_category_names()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());

        Assert.Contains(viewModel.KeyCategories, category => category.DisplayName == "숫자 키패드");

        viewModel.SelectedLanguage = "English";

        Assert.Contains(viewModel.KeyCategories, category => category.DisplayName == "Numeric Keypad");
    }

    [Fact]
    public void Key_picker_uses_fixed_physical_keyboard_rows()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());
        var expectedRows = new Dictionary<string, string[][]>
        {
            ["letters"] =
            [
                ["Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P"],
                ["A", "S", "D", "F", "G", "H", "J", "K", "L"],
                ["Z", "X", "C", "V", "B", "N", "M"]
            ],
            ["numeric-keypad"] =
            [
                ["Num Lock", "Keypad /", "Keypad *", "Keypad -"],
                ["Keypad 7", "Keypad 8", "Keypad 9", "Keypad +"],
                ["Keypad 4", "Keypad 5", "Keypad 6"],
                ["Keypad 1", "Keypad 2", "Keypad 3", "Keypad Enter"],
                ["Keypad 0", "Keypad ."]
            ],
            ["function-keys"] =
            [
                ["F1", "F2", "F3", "F4", "F5", "F6"],
                ["F7", "F8", "F9", "F10", "F11", "F12"],
                ["F13", "F14", "F15", "F16", "F17", "F18"],
                ["F19", "F20", "F21", "F22", "F23", "F24"]
            ],
            ["number-symbol"] =
            [
                ["`", "1", "2", "3", "4", "5", "6"],
                ["7", "8", "9", "0", "-", "=", "["],
                ["]", "\\", ";", "'", ",", ".", "/"]
            ],
            ["shift-symbol"] =
            [
                ["Shift+`", "Shift+1", "Shift+2", "Shift+3", "Shift+4", "Shift+5", "Shift+6"],
                ["Shift+7", "Shift+8", "Shift+9", "Shift+0", "Shift+-", "Shift+=", "Shift+["],
                ["Shift+]", "Shift+\\", "Shift+;", "Shift+'", "Shift+,", "Shift+.", "Shift+/"]
            ],
            ["others"] =
            [
                ["Print Screen", "Scroll Lock", "Pause", "Insert", "Home", "Page Up"],
                ["Ctrl", "Shift", "Win", "Caps Lock", "Delete", "End", "Page Down"],
                ["Enter", "Alt", "Esc", "Up"],
                ["Tab", "Backspace", "Left", "Down", "Right"],
                ["Space", "None"]
            ]
        };

        foreach ((string categoryId, string[][] rows) in expectedRows)
        {
            KeyCategoryDisplay category = viewModel.KeyCategories.Single(item => item.Id == categoryId);
            Assert.Equal(rows.Length, category.Rows.Count);
            for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                Assert.Equal(rows[rowIndex], category.Rows[rowIndex].Options.Select(option => option.Action));
            }
        }
    }

    [Fact]
    public void Shift_symbol_picker_exposes_every_us_keyboard_symbol()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());
        KeyCategoryDisplay category = viewModel.KeyCategories.Single(item => item.Id == "shift-symbol");

        Assert.Equal("~!@#$%^&*()_+{}|:\"<>?", string.Concat(category.Options.Select(option => option.Label)));
        Assert.All(category.Options, option => Assert.StartsWith("Shift+", option.Action));
    }

    [Fact]
    public void Key_picker_includes_standard_control_and_navigation_keys()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());
        HashSet<string> actions = viewModel.KeyCategories
            .SelectMany(category => category.Options)
            .Select(option => option.Action)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] expectedActions =
        [
            "Tab", "Backspace", "Enter", "Esc", "Space", "Caps Lock",
            "Print Screen", "Scroll Lock", "Pause", "Insert", "Home", "Page Up",
            "Delete", "End", "Page Down", "Up", "Down", "Left", "Right",
            "Ctrl", "Shift", "Alt", "Win", "Num Lock", "Keypad Enter", "None"
        ];

        Assert.All(expectedActions, action => Assert.Contains(action, actions));
        Assert.All(Enumerable.Range(1, 24), index => Assert.Contains($"F{index}", actions));
        Assert.All(actions, action => Assert.True(MicroHidCodec.TryEncode(action, out _, out _), action));
    }

    [Fact]
    public void Every_key_picker_action_is_unique_and_encodable()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());
        KeyOptionDisplay[] options = viewModel.KeyCategories.SelectMany(category => category.Options).ToArray();

        Assert.Equal(options.Length, options.Select(option => option.Action).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Single(options, option => option.Action == "Win");
        foreach (KeyOptionDisplay option in options)
        {
            Assert.True(
                MicroHidCodec.TryEncode(option.Action, out _, out string? error),
                $"{option.Action}: {error}");
        }
    }

    [Fact]
    public async Task Freeform_mapping_input_saves_key_chords()
    {
        using var transport = new FakeBleTransport();
        string libraryPath = CreateTempLibraryPath();
        var viewModel = new MainViewModel(transport, libraryPath)
        {
            ProfileNameDraft = "Shortcuts"
        };
        await ((RelayCommand)viewModel.CreateProfileCommand).ExecuteAsync();
        ButtonMappingDisplay row = viewModel.RightMappings.Single(mapping => mapping.Id == "A");

        viewModel.SelectMapping(row);
        viewModel.PendingAction = "Ctrl+C";
        await ((RelayCommand)viewModel.ApplySelectedMappingCommand).ExecuteAsync();

        ProfileLibrary saved = await ProfileStore.LoadLibraryAsync(libraryPath, CancellationToken.None);
        MappedAction action = saved.Profiles.Single().Mappings[MicroButton.A];
        var chord = Assert.IsType<MappedAction.KeyChord>(action);
        Assert.Equal(new[] { "Ctrl", "C" }, chord.Keys);
        Assert.False(viewModel.IsMappingEditorOpen);
    }

    [Fact]
    public async Task Mapping_editor_apply_persists_then_closes_after_a_successful_save()
    {
        using var transport = new FakeBleTransport();
        string libraryPath = CreateTempLibraryPath();
        var viewModel = new MainViewModel(transport, libraryPath)
        {
            ProfileNameDraft = "Shortcuts"
        };
        await ((RelayCommand)viewModel.CreateProfileCommand).ExecuteAsync();
        ButtonMappingDisplay row = viewModel.LeftMappings.Single(mapping => mapping.Id == "L");

        viewModel.SelectMapping(row);
        viewModel.PendingAction = "Ctrl+C";
        await ((RelayCommand)viewModel.ApplySelectedMappingCommand).ExecuteAsync();

        ProfileLibrary saved = await ProfileStore.LoadLibraryAsync(libraryPath, CancellationToken.None);
        MappedAction action = saved.Profiles.Single().Mappings[MicroButton.L];
        var chord = Assert.IsType<MappedAction.KeyChord>(action);
        Assert.Equal(new[] { "Ctrl", "C" }, chord.Keys);
        Assert.False(viewModel.IsMappingEditorOpen);
    }

    [Fact]
    public async Task Mapping_editor_save_failure_keeps_the_editor_open_with_its_draft()
    {
        using var transport = new FakeBleTransport();
        string libraryPath = Path.Combine(Path.GetTempPath(), $"microkey-app-library-{Guid.NewGuid():N}");
        Directory.CreateDirectory(libraryPath);
        try
        {
            var viewModel = new MainViewModel(transport, libraryPath);
            var profile = new DeviceProfileDisplay(
                "profile-1",
                "Shortcuts",
                new Dictionary<string, string> { ["L"] = "Win+Z" });
            viewModel.DeviceProfiles.Add(profile);
            viewModel.SelectedProfile = profile;
            ButtonMappingDisplay row = viewModel.LeftMappings.Single(mapping => mapping.Id == "L");

            viewModel.SelectMapping(row);
            viewModel.PendingAction = "Ctrl+C";
            await ((RelayCommand)viewModel.ApplySelectedMappingCommand).ExecuteAsync();

            Assert.True(viewModel.IsMappingEditorOpen);
            Assert.Equal("Ctrl+C", viewModel.PendingAction);
        }
        finally
        {
            Directory.Delete(libraryPath, recursive: true);
        }
    }

    [Fact]
    public async Task Mapping_editor_cancel_after_a_failed_save_restores_the_committed_mapping()
    {
        using var transport = new FakeBleTransport();
        string libraryPath = Path.Combine(Path.GetTempPath(), $"microkey-app-library-{Guid.NewGuid():N}");
        Directory.CreateDirectory(libraryPath);
        try
        {
            var viewModel = new MainViewModel(transport, libraryPath);
            var profile = new DeviceProfileDisplay(
                "profile-1",
                "Shortcuts",
                new Dictionary<string, string> { ["L"] = "Win+Z" });
            viewModel.DeviceProfiles.Add(profile);
            viewModel.SelectedProfile = profile;
            ButtonMappingDisplay row = viewModel.LeftMappings.Single(mapping => mapping.Id == "L");

            viewModel.SelectMapping(row);
            viewModel.PendingAction = "Ctrl+C";
            await ((RelayCommand)viewModel.ApplySelectedMappingCommand).ExecuteAsync();
            viewModel.CancelMappingEditCommand.Execute(null);

            Assert.Equal("Win+Z", row.Action);
            Assert.Equal("Win+Z", profile.ActionsByButtonId["L"]);
            Assert.Equal("Win+Z", viewModel.PendingAction);
            Assert.False(viewModel.IsMappingEditorOpen);
        }
        finally
        {
            Directory.Delete(libraryPath, recursive: true);
        }
    }

    [Fact]
    public void Mapping_editor_apply_without_a_selected_profile_leaves_the_draft_uncommitted()
    {
        using var transport = new FakeBleTransport();
        string libraryPath = CreateTempLibraryPath();
        var viewModel = new MainViewModel(transport, libraryPath);
        ButtonMappingDisplay row = viewModel.LeftMappings.Single(mapping => mapping.Id == "L");

        viewModel.SelectMapping(row);
        viewModel.PendingAction = "Ctrl+C";
        viewModel.ApplySelectedMappingCommand.Execute(null);

        Assert.False(viewModel.ApplySelectedMappingCommand.CanExecute(null));
        Assert.Equal("Win+Z", row.Action);
        Assert.Equal("Ctrl+C", viewModel.PendingAction);
        Assert.True(viewModel.IsMappingEditorOpen);
        Assert.False(File.Exists(libraryPath));
    }

    [Fact]
    public void Editor_title_raises_property_changed_when_language_changes()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());
        ButtonMappingDisplay row = viewModel.LeftMappings.Single(mapping => mapping.Id == "Up");
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.SelectMapping(row);
        changedProperties.Clear();
        viewModel.SelectedLanguage = "English";

        Assert.Equal("Up", viewModel.EditorTitleText);
        Assert.Contains(nameof(MainViewModel.EditorTitleText), changedProperties);
    }

    [Fact]
    public void Micro_service_uuid_can_be_selected_from_full_service_list()
    {
        Guid? selected = GattUuidSelector.SelectMicroServiceUuid(new[]
        {
            Guid.Parse("00001801-0000-1000-8000-00805f9b34fb"),
            Guid.Parse("00001800-0000-1000-8000-00805f9b34fb"),
            Guid.Parse("0000ff10-0000-1000-8000-00805f9b34fb")
        });

        Assert.Equal(Guid.Parse("0000ff10-0000-1000-8000-00805f9b34fb"), selected);
    }

    [Fact]
    public void Micro_service_uuid_selection_returns_null_when_missing()
    {
        Guid? selected = GattUuidSelector.SelectMicroServiceUuid(new[]
        {
            Guid.Parse("00001801-0000-1000-8000-00805f9b34fb"),
            Guid.Parse("00001800-0000-1000-8000-00805f9b34fb")
        });

        Assert.Null(selected);
    }

    [Fact]
    public async Task Connect_loads_device_stored_mapping_into_generated_profile_name()
    {
        using var transport = new FakeBleTransport();
        string libraryPath = CreateTempLibraryPath();
        QueueValidConfig(transport);
        var viewModel = new MainViewModel(transport, libraryPath)
        {
            SelectedDevice = new MicroBleDevice("AABBCCDDEEFF", "8BitDo Micro", -40)
        };

        await ((RelayCommand)viewModel.ConnectCommand).ExecuteAsync();

        Assert.Equal("Captured Micro load sequence", transport.LastCollectedSequence?.Name);
        Assert.Contains(viewModel.RawConfigValues, value => value.Slot == 5 && value.DisplayText == "Ctrl+W");
        Assert.Contains(viewModel.RawConfigValues, value => value.Slot == 16 && value.DisplayText == "Esc");
        Assert.Single(viewModel.DeviceProfiles);
        Assert.Equal("Device Profile 1", viewModel.SelectedProfile?.Name);
        Assert.True(viewModel.SelectedProfile?.IsLoaded);
        Assert.Equal("Device Profile 1", viewModel.SelectedProfile?.DisplayName);
        Assert.Equal("프로필 1개: Device Profile 1", viewModel.ProfileSummaryText);
        Assert.Equal("Up", viewModel.LeftMappings.Single(mapping => mapping.Id == "Up").Action);
        Assert.Equal("Ctrl+W", viewModel.RightMappings.Single(mapping => mapping.Id == "X").Action);
        Assert.Equal("Enter", viewModel.RightMappings.Single(mapping => mapping.Id == "A").Action);
        Assert.NotNull(viewModel.LastDiagnosticFilePath);
        Assert.Contains("설정 페이지 4개", viewModel.Status);
        Assert.Contains("이름은 아직", viewModel.Status);

        ProfileLibrary saved = await ProfileStore.LoadLibraryAsync(libraryPath, CancellationToken.None);
        Assert.Single(saved.Profiles);
        Assert.Equal("current-device", saved.SelectedProfileId);
        Assert.Equal("Device Profile 1", saved.Profiles[0].Name);
    }

    [Fact]
    public async Task Local_profile_library_is_loaded_and_device_read_preserves_existing_profiles()
    {
        using var transport = new FakeBleTransport();
        string libraryPath = CreateTempLibraryPath();
        await ProfileStore.SaveLibraryAsync(
            new ProfileLibrary(
                1,
                "profile-2",
                new[]
                {
                    new MappingProfile(
                        "profile-1",
                        "Default Profile",
                        1,
                        new Dictionary<MicroButton, MappedAction>
                        {
                            [MicroButton.Up] = new MappedAction.KeyboardKey(".")
                        }),
                    new MappingProfile(
                        "profile-2",
                        "profile1",
                        1,
                        new Dictionary<MicroButton, MappedAction>
                        {
                            [MicroButton.X] = new MappedAction.KeyChord(new[] { "Shift", "'" })
                        })
                }),
            libraryPath,
            CancellationToken.None);
        QueueValidConfig(transport);
        var viewModel = new MainViewModel(transport, libraryPath)
        {
            SelectedDevice = new MicroBleDevice("AABBCCDDEEFF", "8BitDo Micro", -40)
        };

        Assert.Equal(2, viewModel.DeviceProfiles.Count);
        Assert.Equal("profile1", viewModel.SelectedProfile?.Name);

        await ((RelayCommand)viewModel.ConnectCommand).ExecuteAsync();

        Assert.Equal(3, viewModel.DeviceProfiles.Count);
        Assert.Contains(viewModel.DeviceProfiles, profile => profile.Name == "Default Profile");
        Assert.Contains(viewModel.DeviceProfiles, profile => profile.Name == "profile1");
        Assert.Equal("Device Profile 1", viewModel.SelectedProfile?.Name);

        ProfileLibrary saved = await ProfileStore.LoadLibraryAsync(libraryPath, CancellationToken.None);
        Assert.Equal(3, saved.Profiles.Count);
        Assert.Equal("Device Profile 1", saved.Profiles.Single(profile => profile.Id == "current-device").Name);
    }

    [Fact]
    public async Task Local_profile_can_be_created_renamed_and_deleted()
    {
        using var transport = new FakeBleTransport();
        string libraryPath = CreateTempLibraryPath();
        var viewModel = new MainViewModel(transport, libraryPath)
        {
            ProfileNameDraft = "OBS Editing"
        };

        await ((RelayCommand)viewModel.CreateProfileCommand).ExecuteAsync();
        Assert.Single(viewModel.DeviceProfiles);
        Assert.Equal("OBS Editing", viewModel.SelectedProfile?.Name);
        Assert.Equal("OBS Editing", viewModel.ProfileNameDraft);

        viewModel.ProfileNameDraft = "OBS Shortcuts";
        await ((RelayCommand)viewModel.RenameProfileCommand).ExecuteAsync();
        Assert.Equal("OBS Shortcuts", viewModel.SelectedProfile?.Name);
        Assert.Equal("OBS Shortcuts", viewModel.ProfileNameDraft);

        await ((RelayCommand)viewModel.DeleteProfileCommand).ExecuteAsync();
        Assert.Empty(viewModel.DeviceProfiles);

        ProfileLibrary saved = await ProfileStore.LoadLibraryAsync(libraryPath, CancellationToken.None);
        Assert.Empty(saved.Profiles);
    }

    [Fact]
    public async Task Loading_selected_profile_applies_its_button_actions()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());
        var profile = new DeviceProfileDisplay("Test Profile", new Dictionary<string, string>
        {
            ["Up"] = "A",
            ["X"] = "B"
        });

        viewModel.DeviceProfiles.Add(profile);
        viewModel.SelectedProfile = profile;

        Assert.Equal("Test Profile", viewModel.ProfileNameDraft);
        Assert.Equal(".", viewModel.LeftMappings.Single(mapping => mapping.Id == "Up").Action);
        Assert.Equal("Shift+'", viewModel.RightMappings.Single(mapping => mapping.Id == "X").Action);

        await ((RelayCommand)viewModel.LoadSelectedProfileCommand).ExecuteAsync();

        Assert.Equal("A", viewModel.LeftMappings.Single(mapping => mapping.Id == "Up").Action);
        Assert.Equal("B", viewModel.RightMappings.Single(mapping => mapping.Id == "X").Action);
    }

    [Fact]
    public void Create_profile_is_disabled_for_the_selected_profile_name()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());
        var profile = new DeviceProfileDisplay("Device Profile 1", new Dictionary<string, string>
        {
            ["Up"] = "I"
        });

        viewModel.DeviceProfiles.Add(profile);
        viewModel.SelectedProfile = profile;

        Assert.Equal("Device Profile 1", viewModel.ProfileNameDraft);
        Assert.False(viewModel.CreateProfileCommand.CanExecute(null));

        viewModel.ProfileNameDraft = "Work shortcuts";

        Assert.True(viewModel.CreateProfileCommand.CanExecute(null));
    }

    [Fact]
    public async Task Saved_selected_profile_is_shown_when_profile_library_loads()
    {
        using var transport = new FakeBleTransport();
        string libraryPath = CreateTempLibraryPath();
        await ProfileStore.SaveLibraryAsync(
            new ProfileLibrary(
                1,
                "profile-1",
                new[]
                {
                    new MappingProfile(
                        "profile-1",
                        "Default Profile",
                        1,
                        new Dictionary<MicroButton, MappedAction>
                        {
                            [MicroButton.Up] = new MappedAction.KeyboardKey("A"),
                            [MicroButton.X] = new MappedAction.KeyboardKey("B")
                        })
                }),
            libraryPath,
            CancellationToken.None);

        var viewModel = new MainViewModel(transport, libraryPath);

        Assert.Equal("Default Profile", viewModel.ProfileNameDraft);
        Assert.Equal("A", viewModel.LeftMappings.Single(mapping => mapping.Id == "Up").Action);
        Assert.Equal("B", viewModel.RightMappings.Single(mapping => mapping.Id == "X").Action);
    }

    [Fact]
    public async Task Constructor_completes_on_wpf_style_synchronization_context()
    {
        string libraryPath = CreateTempLibraryPath();
        await ProfileStore.SaveLibraryAsync(
            new ProfileLibrary(
                1,
                "profile-1",
                new[]
                {
                    new MappingProfile(
                        "profile-1",
                        "Default Profile",
                        1,
                        new Dictionary<MicroButton, MappedAction>
                        {
                            [MicroButton.Up] = new MappedAction.KeyboardKey("A")
                        })
                }),
            libraryPath,
            CancellationToken.None);

        using var completed = new ManualResetEventSlim();
        Exception? exception = null;
        MainViewModel? viewModel = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
            try
            {
                viewModel = new MainViewModel(new FakeBleTransport(), libraryPath);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                completed.Set();
            }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(completed.Wait(TimeSpan.FromSeconds(2)), "MainViewModel construction deadlocked before the WPF window could be shown.");
        Assert.Null(exception);
        Assert.NotNull(viewModel);
        Assert.Equal("Default Profile", viewModel.ProfileNameDraft);
    }

    [Fact]
    public async Task Loading_unread_profile_candidate_keeps_current_mappings_and_explains_status()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());
        var profile = new DeviceProfileDisplay("Device Profile 2", new Dictionary<string, string>(), IsLoaded: false);

        viewModel.DeviceProfiles.Add(profile);
        viewModel.SelectedProfile = profile;

        await ((RelayCommand)viewModel.LoadSelectedProfileCommand).ExecuteAsync();

        Assert.Equal(".", viewModel.LeftMappings.Single(mapping => mapping.Id == "Up").Action);
        Assert.Contains("아직 읽지 못했습니다", viewModel.Status);
    }

    [Fact]
    public async Task Failed_device_load_records_diagnostics_path()
    {
        using var transport = new FakeBleTransport();
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath())
        {
            SelectedDevice = new MicroBleDevice("AABBCCDDEEFF", "8BitDo Micro", -40)
        };

        await ((RelayCommand)viewModel.ConnectCommand).ExecuteAsync();

        Assert.NotNull(viewModel.LastDiagnosticFilePath);
        Assert.Empty(viewModel.DeviceProfiles);
        Assert.Equal("프로필 0개", viewModel.ProfileSummaryText);
        Assert.Contains("저장 설정 응답", viewModel.Status);
    }

    [Fact]
    public async Task Connect_failure_records_diagnostics_path()
    {
        using var transport = new FakeBleTransport();
        transport.ThrowOnConnect(new InvalidOperationException("Micro service was not found. Status: Success"));
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath())
        {
            SelectedDevice = new MicroBleDevice("AABBCCDDEEFF", "8BitDo Micro", -40)
        };

        await ((RelayCommand)viewModel.ConnectCommand).ExecuteAsync();

        Assert.NotNull(viewModel.LastDiagnosticFilePath);
        Assert.Contains("Micro service was not found", viewModel.Status);
    }

    [Fact]
    public async Task Scan_auto_selects_and_loads_when_one_device_is_found()
    {
        using var transport = new FakeBleTransport();
        var device = new MicroBleDevice("AABBCCDDEEFF", "8BitDo Micro", -40);
        transport.QueueScanResults(device);
        QueueValidConfig(transport);
        var viewModel = new MainViewModel(transport, CreateTempLibraryPath());

        await ((RelayCommand)viewModel.ScanCommand).ExecuteAsync();

        Assert.Same(device, viewModel.SelectedDevice);
        Assert.Equal(device, transport.LastConnectedDevice);
        Assert.Single(viewModel.DeviceProfiles);
        Assert.Equal("Device Profile 1", viewModel.SelectedProfile?.Name);
        Assert.Equal("Up", viewModel.LeftMappings.Single(mapping => mapping.Id == "Up").Action);
        Assert.Contains("설정 페이지 4개", viewModel.Status);
    }

    [Fact]
    public void Save_to_device_is_disabled_while_disconnected()
    {
        using var transport = new FakeBleTransport();
        var confirmation = new FakeConfirmationService();
        var viewModel = CreateSaveViewModel(transport, confirmation);

        Assert.False(viewModel.SaveToDeviceCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_to_device_is_disabled_after_an_incomplete_read()
    {
        using var transport = new FakeBleTransport();
        byte[][] pages = CreateValidConfigPages();
        transport.QueueCollectedNotificationBatch(pages.Take(3).ToArray());
        var confirmation = new FakeConfirmationService();
        var viewModel = CreateSaveViewModel(transport, confirmation);

        await ConnectAsync(viewModel);

        Assert.True(viewModel.IsConnected);
        Assert.False(viewModel.SaveToDeviceCommand.CanExecute(null));
        Assert.Null(transport.LastWrittenSequence);
    }

    [Fact]
    public async Task Save_to_device_is_disabled_for_an_unsupported_action()
    {
        using var transport = new FakeBleTransport();
        transport.QueueCollectedNotificationBatch(CreateValidConfigPages());
        var confirmation = new FakeConfirmationService();
        var viewModel = CreateSaveViewModel(transport, confirmation);
        await ConnectAsync(viewModel);
        viewModel.LeftMappings.Single(mapping => mapping.Id == "L").Action = "Mouse click";

        Assert.False(viewModel.SaveToDeviceCommand.CanExecute(null));

        await ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();

        Assert.Single(transport.CollectedSequences);
        Assert.Null(transport.LastWrittenSequence);
        Assert.Equal(0, confirmation.CallCount);
    }

    [Fact]
    public async Task Save_to_device_declined_confirmation_still_creates_a_baseline_backup()
    {
        using var transport = new FakeBleTransport();
        byte[][] baselinePages = CreateValidConfigPages();
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.QueueCollectedNotificationBatch(baselinePages);
        var confirmation = new FakeConfirmationService(result: false);
        var viewModel = CreateSaveViewModel(transport, confirmation);
        await ConnectAsync(viewModel);
        viewModel.LeftMappings.Single(mapping => mapping.Id == "L").Action = "Keypad 1";

        await ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();

        Assert.Null(transport.LastWrittenSequence);
        Assert.NotNull(viewModel.LastBackupPath);
        Assert.True(File.Exists(viewModel.LastBackupPath));
        MicroConfigBackup backup = await MicroConfigBackupStore.LoadAsync(viewModel.LastBackupPath, CancellationToken.None);
        Assert.Equal(GetPayload(baselinePages), backup.Payload);
    }

    [Fact]
    public async Task Save_to_device_writes_generated_packets_backs_up_baseline_and_verifies_fresh_readback()
    {
        using var transport = new FakeBleTransport();
        byte[][] initialPages = CreateValidConfigPages();
        byte[] initialPayload = GetPayload(initialPages);
        byte[] freshPayload = initialPayload.ToArray();
        new byte[] { 0x04, 0x00, 0x00, 0x00 }.CopyTo(freshPayload, 7 * 4);
        freshPayload[178] = 0xab;
        byte[][] freshPages = CreateConfigPages(freshPayload);
        byte[] expectedPayload = freshPayload.ToArray();
        new byte[] { 0x59, 0x00, 0x00, 0x00 }.CopyTo(expectedPayload, 7 * 4);
        transport.QueueCollectedNotificationBatch(initialPages);
        transport.QueueCollectedNotificationBatch(freshPages);
        transport.QueueCollectedNotificationBatch(CreateConfigPages(expectedPayload));
        var confirmation = new FakeConfirmationService();
        var viewModel = CreateSaveViewModel(transport, confirmation);
        viewModel.SelectedLanguage = "English";
        await ConnectAsync(viewModel);
        viewModel.LeftMappings.Single(mapping => mapping.Id == "L").Action = "Keypad 1";

        await ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();

        Assert.NotNull(transport.LastWrittenSequence);
        FakeBleInvocation write = Assert.Single(transport.WriteInvocations);
        Assert.True(write.IsConnected);
        Assert.Equal(TimeSpan.FromMilliseconds(120), write.DelayBetweenWrites);
        Assert.False(write.CancellationToken.CanBeCanceled);
        Assert.Equal("Micro save sequence", write.Sequence?.Name);
        Assert.Equal(5, write.Sequence?.Writes.Count);
        uint[] offsets = { 0, 45, 90, 135 };
        for (int index = 0; index < offsets.Length; index++)
        {
            byte[] page = write.Sequence!.Writes[index];
            Assert.Equal(62, page.Length);
            Assert.Equal(0x04, page[0]);
            Assert.Equal(0x01, page[1]);
            Assert.Equal(offsets[index], BitConverter.ToUInt32(page, 13));
        }

        Assert.Equal(
            Hex("04 06 00 5b 00 00 00 ff ff 00 00 00 00 00 00 00 00"),
            write.Sequence!.Writes[^1]);
        Assert.DoesNotContain(transport.WriteInvocations, invocation => invocation.Sequence?.Name == "Captured Micro save sequence");
        Assert.Equal(
            new[]
            {
                FakeBleInvocationKind.Connect,
                FakeBleInvocationKind.Collect,
                FakeBleInvocationKind.Collect,
                FakeBleInvocationKind.Write,
                FakeBleInvocationKind.Collect
            },
            transport.Invocations.Select(invocation => invocation.Kind));
        Assert.NotNull(viewModel.LastBackupPath);
        Assert.True(File.Exists(viewModel.LastBackupPath));
        Assert.Contains("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(viewModel.RawConfigValues, value => value.Slot == 7 && value.DisplayText == "Keypad 1");
        Assert.Contains("L: A -> Keypad 1", confirmation.LastMessage);
        Assert.DoesNotContain("Ctrl -> Keypad 1", confirmation.LastMessage);

        MicroConfigBackup backup = await MicroConfigBackupStore.LoadAsync(viewModel.LastBackupPath, CancellationToken.None);
        Assert.NotEqual(initialPayload, freshPayload);
        Assert.Equal(freshPayload, backup.Payload);
        Assert.NotEqual(expectedPayload, backup.Payload);
        Assert.Equal(expectedPayload, GetSavePayload(write.Sequence));
        Assert.Equal(0xab, GetSavePayload(write.Sequence)[178]);
    }

    [Fact]
    public async Task Save_to_device_writes_disable_sleep_and_verifies_its_fresh_readback()
    {
        using var transport = new FakeBleTransport();
        byte[][] baselinePages = CreateValidConfigPages();
        byte[] expectedPayload = GetPayload(baselinePages);
        expectedPayload[MicroConfigSnapshot.DisableSleepOffset] = 0x01;
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.QueueCollectedNotificationBatch(CreateConfigPages(expectedPayload));
        var confirmation = new FakeConfirmationService();
        var viewModel = CreateSaveViewModel(transport, confirmation);
        viewModel.SelectedLanguage = "English";
        await ConnectAsync(viewModel);

        viewModel.IsSleepDisabled = true;
        await ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();

        PacketSequence written = Assert.Single(transport.WriteInvocations).Sequence!;
        Assert.Equal(0x01, GetSavePayload(written)[MicroConfigSnapshot.DisableSleepOffset]);
        Assert.True(viewModel.IsSleepDisabled);
        Assert.Contains("Disable Sleep: Off -> On", confirmation.LastMessage);
        Assert.Contains("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        MicroConfigBackup backup = await MicroConfigBackupStore.LoadAsync(viewModel.LastBackupPath!, CancellationToken.None);
        Assert.Equal(0x00, backup.Payload[MicroConfigSnapshot.DisableSleepOffset]);
    }

    [Fact]
    public async Task Save_to_device_reports_mismatch_when_disable_sleep_readback_does_not_change()
    {
        using var transport = new FakeBleTransport();
        byte[][] baselinePages = CreateValidConfigPages();
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.QueueCollectedNotificationBatch(baselinePages);
        var viewModel = CreateSaveViewModel(transport, new FakeConfirmationService());
        viewModel.SelectedLanguage = "English";
        await ConnectAsync(viewModel);

        viewModel.IsSleepDisabled = true;
        await ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();

        Assert.Contains("mismatch", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.IsSleepDisabled);
    }

    [Fact]
    public async Task Save_to_device_write_exception_retains_the_backup_path()
    {
        using var transport = new FakeBleTransport();
        byte[][] baselinePages = CreateValidConfigPages();
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.ThrowOnWrite(new InvalidOperationException("write failed"));
        var viewModel = CreateSaveViewModel(transport, new FakeConfirmationService());
        await ConnectAsync(viewModel);
        viewModel.LeftMappings.Single(mapping => mapping.Id == "L").Action = "Keypad 1";

        await ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();

        Assert.NotNull(viewModel.LastBackupPath);
        Assert.True(File.Exists(viewModel.LastBackupPath));
        Assert.Contains("write failed", viewModel.Status);
        Assert.Equal(2, transport.CollectedSequences.Count);
        FakeBleInvocation write = Assert.Single(transport.WriteInvocations);
        Assert.True(write.IsConnected);
        Assert.Equal(TimeSpan.FromMilliseconds(120), write.DelayBetweenWrites);
        Assert.False(write.CancellationToken.CanBeCanceled);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.SaveToDeviceCommand.CanExecute(null));
        Assert.Equal(FakeBleInvocationKind.Write, transport.Invocations[^1].Kind);
    }

    [Fact]
    public async Task Save_to_device_incomplete_readback_reports_a_mismatch()
    {
        using var transport = new FakeBleTransport();
        byte[][] baselinePages = CreateValidConfigPages();
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.QueueCollectedNotificationBatch(baselinePages.Take(3).ToArray());
        var viewModel = CreateSaveViewModel(transport, new FakeConfirmationService());
        viewModel.SelectedLanguage = "English";
        await ConnectAsync(viewModel);
        viewModel.LeftMappings.Single(mapping => mapping.Id == "L").Action = "Keypad 1";

        await ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();

        Assert.Contains("mismatch", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.SaveToDeviceCommand.CanExecute(null));
    }

    [Fact]
    public async Task Save_to_device_wrong_slot_readback_reports_a_mismatch()
    {
        using var transport = new FakeBleTransport();
        byte[][] baselinePages = CreateValidConfigPages();
        byte[] wrongPayload = GetPayload(baselinePages);
        new byte[] { 0x59, 0x00, 0x00, 0x00 }.CopyTo(wrongPayload, 8 * 4);
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.QueueCollectedNotificationBatch(CreateConfigPages(wrongPayload));
        var viewModel = CreateSaveViewModel(transport, new FakeConfirmationService());
        viewModel.SelectedLanguage = "English";
        await ConnectAsync(viewModel);
        viewModel.LeftMappings.Single(mapping => mapping.Id == "L").Action = "Keypad 1";

        await ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();

        Assert.Contains("mismatch", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Save_to_device_reentry_is_blocked_by_is_busy()
    {
        using var transport = new FakeBleTransport();
        byte[][] baselinePages = CreateValidConfigPages();
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.QueueCollectedNotificationBatch(baselinePages);
        var confirmation = new FakeConfirmationService();
        confirmation.BlockNextConfirmation();
        var viewModel = CreateSaveViewModel(transport, confirmation);
        await ConnectAsync(viewModel);
        viewModel.LeftMappings.Single(mapping => mapping.Id == "L").Action = "Keypad 1";

        Task firstSave = ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();
        await confirmation.WaitForRequestAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.SaveToDeviceCommand.CanExecute(null));
        await ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();
        Assert.Equal(1, confirmation.CallCount);

        confirmation.CompleteConfirmation(result: false);
        await firstSave;
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Save_to_device_blocks_other_state_mutating_commands_while_confirmation_is_in_flight()
    {
        using var transport = new FakeBleTransport();
        byte[][] baselinePages = CreateValidConfigPages();
        transport.QueueCollectedNotificationBatch(baselinePages);
        transport.QueueCollectedNotificationBatch(baselinePages);
        var confirmation = new FakeConfirmationService();
        confirmation.BlockNextConfirmation();
        var viewModel = CreateSaveViewModel(transport, confirmation);
        await ConnectAsync(viewModel);
        ButtonMappingDisplay lMapping = viewModel.LeftMappings.Single(mapping => mapping.Id == "L");
        ButtonMappingDisplay rMapping = viewModel.RightMappings.Single(mapping => mapping.Id == "R");
        lMapping.Action = "Keypad 1";
        viewModel.SelectMapping(lMapping);
        await ((RelayCommand)viewModel.CancelMappingEditCommand).ExecuteAsync();
        viewModel.PendingAction = "Keypad 2";
        viewModel.ProfileNameDraft = "Busy Profile";
        int profileCount = viewModel.DeviceProfiles.Count;
        var commandsToRefresh = new[]
        {
            viewModel.ScanCommand,
            viewModel.ConnectCommand,
            viewModel.DisconnectCommand,
            viewModel.LoadSelectedProfileCommand,
            viewModel.CreateProfileCommand,
            viewModel.RenameProfileCommand,
            viewModel.DeleteProfileCommand,
            viewModel.SaveToDeviceCommand,
            viewModel.RestoreDeviceBackupCommand,
            viewModel.ResetMappingsCommand,
            viewModel.SelectMappingCommand,
            viewModel.ApplySelectedMappingCommand,
            viewModel.CancelMappingEditCommand,
            viewModel.SelectKeyOptionCommand
        };
        var refreshedCommands = new HashSet<object>();
        foreach (var command in commandsToRefresh)
        {
            var observedCommand = command;
            command.CanExecuteChanged += (_, _) => refreshedCommands.Add(observedCommand);
        }

        Task save = ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();
        await confirmation.WaitForRequestAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(commandsToRefresh.Length, refreshedCommands.Count);

        var commandStates = new[]
        {
            ("scan", viewModel.ScanCommand.CanExecute(null)),
            ("connect", viewModel.ConnectCommand.CanExecute(null)),
            ("disconnect", viewModel.DisconnectCommand.CanExecute(null)),
            ("load profile", viewModel.LoadSelectedProfileCommand.CanExecute(null)),
            ("create profile", viewModel.CreateProfileCommand.CanExecute(null)),
            ("rename profile", viewModel.RenameProfileCommand.CanExecute(null)),
            ("delete profile", viewModel.DeleteProfileCommand.CanExecute(null)),
            ("save", viewModel.SaveToDeviceCommand.CanExecute(null)),
            ("restore", viewModel.RestoreDeviceBackupCommand.CanExecute(null)),
            ("reset", viewModel.ResetMappingsCommand.CanExecute(null)),
            ("select mapping", viewModel.SelectMappingCommand.CanExecute(rMapping)),
            ("apply mapping", viewModel.ApplySelectedMappingCommand.CanExecute(null)),
            ("cancel mapping", viewModel.CancelMappingEditCommand.CanExecute(null)),
            ("select key", viewModel.SelectKeyOptionCommand.CanExecute(new KeyOptionDisplay("2", "Keypad 2")))
        };

        await ((RelayCommand)viewModel.DisconnectCommand).ExecuteAsync();
        await ((RelayCommand)viewModel.LoadSelectedProfileCommand).ExecuteAsync();
        await ((RelayCommand)viewModel.CreateProfileCommand).ExecuteAsync();
        await ((RelayCommand)viewModel.ApplySelectedMappingCommand).ExecuteAsync();
        viewModel.SelectMappingCommand.Execute(rMapping);
        viewModel.SelectKeyOptionCommand.Execute(new KeyOptionDisplay("3", "Keypad 3"));
        await ((RelayCommand)viewModel.CancelMappingEditCommand).ExecuteAsync();

        confirmation.CompleteConfirmation(result: false);
        await save;

        Assert.All(commandStates, state => Assert.False(state.Item2, $"{state.Item1} must be disabled while save is busy."));
        Assert.Empty(transport.WriteInvocations);
        Assert.DoesNotContain(transport.Invocations, invocation => invocation.Kind == FakeBleInvocationKind.Disconnect);
        Assert.Equal(profileCount, viewModel.DeviceProfiles.Count);
        Assert.Equal("Keypad 1", lMapping.Action);
        Assert.Same(lMapping, viewModel.SelectedMapping);
        Assert.False(viewModel.IsMappingEditorOpen);
    }

    [Fact]
    public async Task Save_to_device_freezes_confirmation_and_packet_target_during_fresh_read()
    {
        using var transport = new FakeBleTransport();
        byte[][] initialPages = CreateValidConfigPages();
        byte[] freshPayload = GetPayload(initialPages);
        new byte[] { 0x04, 0x00, 0x00, 0x00 }.CopyTo(freshPayload, 7 * 4);
        byte[][] freshPages = CreateConfigPages(freshPayload);
        byte[] expectedPayload = freshPayload.ToArray();
        new byte[] { 0x59, 0x00, 0x00, 0x00 }.CopyTo(expectedPayload, 7 * 4);
        transport.QueueCollectedNotificationBatch(initialPages);
        transport.QueueBlockedCollectedNotificationBatch(freshPages);
        transport.QueueCollectedNotificationBatch(CreateConfigPages(expectedPayload));
        var confirmation = new FakeConfirmationService();
        var viewModel = CreateSaveViewModel(transport, confirmation);
        viewModel.SelectedLanguage = "English";
        await ConnectAsync(viewModel);
        ButtonMappingDisplay lMapping = viewModel.LeftMappings.Single(mapping => mapping.Id == "L");
        lMapping.Action = "Keypad 1";
        viewModel.SelectMapping(lMapping);

        await ((RelayCommand)viewModel.CancelMappingEditCommand).ExecuteAsync();
        Task save = ((RelayCommand)viewModel.SaveToDeviceCommand).ExecuteAsync();
        await transport.WaitForBlockedCollectionAsync().WaitAsync(TimeSpan.FromSeconds(2));
        try
        {
            viewModel.PendingAction = "Keypad 2";
            await ((RelayCommand)viewModel.ApplySelectedMappingCommand).ExecuteAsync();
        }
        finally
        {
            transport.ReleaseBlockedCollection();
        }

        await save;

        Assert.Equal("Keypad 1", lMapping.Action);
        Assert.Contains("L: A -> Keypad 1", confirmation.LastMessage);
        Assert.DoesNotContain("Keypad 2", confirmation.LastMessage);
        PacketSequence written = Assert.Single(transport.WriteInvocations).Sequence!;
        Assert.Equal(expectedPayload, GetSavePayload(written));
    }

    [Fact]
    public async Task Save_to_device_rejects_a_stale_snapshot_after_reconnect_read_is_incomplete()
    {
        using var transport = new FakeBleTransport();
        byte[][] baselinePages = CreateValidConfigPages();
        transport.QueueCollectedNotificationBatch(baselinePages);
        var viewModel = CreateSaveViewModel(transport, new FakeConfirmationService());
        await ConnectAsync(viewModel);
        Assert.True(viewModel.SaveToDeviceCommand.CanExecute(null));

        await ((RelayCommand)viewModel.DisconnectCommand).ExecuteAsync();
        transport.QueueCollectedNotificationBatch(baselinePages.Take(3).ToArray());
        await ConnectAsync(viewModel);

        Assert.True(viewModel.IsConnected);
        Assert.False(viewModel.SaveToDeviceCommand.CanExecute(null));
    }

    [Fact]
    public async Task Restore_device_backup_is_unavailable_when_disconnected_or_the_last_backup_is_missing()
    {
        using var transport = new FakeBleTransport();
        byte[] backupPayload = GetPayload(CreateValidConfigPages());
        string backupDirectory = CreateTempBackupDirectory();
        string backupPath = await MicroConfigBackupStore.SaveAsync(
            backupPayload,
            backupDirectory,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        var confirmation = new FakeConfirmationService();
        var viewModel = CreateRestoreViewModel(transport, confirmation, backupDirectory, backupPath);

        Assert.False(viewModel.RestoreDeviceBackupCommand.CanExecute(null));

        transport.QueueCollectedNotificationBatch(CreateValidConfigPages());
        await ConnectAsync(viewModel);
        Assert.True(viewModel.RestoreDeviceBackupCommand.CanExecute(null));

        File.Delete(backupPath);
        Assert.False(viewModel.RestoreDeviceBackupCommand.CanExecute(null));
        await ((RelayCommand)viewModel.RestoreDeviceBackupCommand).ExecuteAsync();

        Assert.Empty(transport.WriteInvocations);
        Assert.Equal(0, confirmation.CallCount);
        Assert.Equal(backupPath, viewModel.LastBackupPath);
    }

    [Fact]
    public async Task Construction_uses_newest_embedded_timestamp_not_filesystem_write_time_and_preserves_restore_gating()
    {
        using var transport = new FakeBleTransport();
        string backupDirectory = CreateTempBackupDirectory();
        byte[] olderPayload = GetPayload(CreateValidConfigPages());
        byte[] newestPayload = olderPayload.ToArray();
        newestPayload[178] = 0xab;
        string olderPath = await MicroConfigBackupStore.SaveAsync(
            olderPayload,
            backupDirectory,
            new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        string newestPath = await MicroConfigBackupStore.SaveAsync(
            newestPayload,
            backupDirectory,
            new DateTimeOffset(2026, 8, 16, 11, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        File.SetLastWriteTimeUtc(olderPath, new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(newestPath, new DateTime(2026, 8, 16, 9, 0, 0, DateTimeKind.Utc));
        byte[] olderContents = await File.ReadAllBytesAsync(olderPath);
        byte[] newestContents = await File.ReadAllBytesAsync(newestPath);

        var viewModel = new MainViewModel(
            transport,
            new FakeConfirmationService(),
            CreateTempLibraryPath(),
            backupDirectory);

        Assert.Equal(newestPath, viewModel.LastBackupPath);
        Assert.False(viewModel.RestoreDeviceBackupCommand.CanExecute(null));
        Assert.Equal(olderContents, await File.ReadAllBytesAsync(olderPath));
        Assert.Equal(newestContents, await File.ReadAllBytesAsync(newestPath));
        Assert.Equal(new[] { olderPath, newestPath }, Directory.GetFiles(backupDirectory).OrderBy(path => path));

        viewModel.SelectedDevice = new MicroBleDevice("AABBCCDDEEFF", "8BitDo Micro", -40);
        transport.QueueCollectedNotificationBatch(CreateValidConfigPages());
        await ConnectAsync(viewModel);

        Assert.True(viewModel.RestoreDeviceBackupCommand.CanExecute(null));
    }

    [Fact]
    public async Task Construction_skips_a_malformed_newest_backup_and_uses_the_next_strict_backup()
    {
        using var transport = new FakeBleTransport();
        string backupDirectory = CreateTempBackupDirectory();
        string validPath = await MicroConfigBackupStore.SaveAsync(
            GetPayload(CreateValidConfigPages()),
            backupDirectory,
            new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        string malformedPath = Path.Combine(backupDirectory, "micro-config-backup-20260816-110000000-malformed.json");
        await File.WriteAllTextAsync(malformedPath, "{\"schema\":1,\"timestamp\":\"2026-08-16T11:00:00Z\",\"payload\":\"AA==\"}");
        File.SetLastWriteTimeUtc(validPath, new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(malformedPath, new DateTime(2026, 8, 16, 11, 0, 0, DateTimeKind.Utc));
        byte[] malformedContents = await File.ReadAllBytesAsync(malformedPath);

        var viewModel = new MainViewModel(
            transport,
            new FakeConfirmationService(),
            CreateTempLibraryPath(),
            backupDirectory);

        Assert.Equal(validPath, viewModel.LastBackupPath);
        Assert.False(viewModel.RestoreDeviceBackupCommand.CanExecute(null));
        Assert.Equal(malformedContents, await File.ReadAllBytesAsync(malformedPath));
    }

    [Fact]
    public async Task Construction_uses_ordinal_path_order_when_strict_backups_have_equal_embedded_timestamps()
    {
        using var transport = new FakeBleTransport();
        string backupDirectory = CreateTempBackupDirectory();
        DateTimeOffset createdAt = new(2026, 8, 16, 10, 0, 0, TimeSpan.Zero);
        string firstSavedPath = await MicroConfigBackupStore.SaveAsync(
            GetPayload(CreateValidConfigPages()),
            backupDirectory,
            createdAt,
            CancellationToken.None);
        string secondSavedPath = await MicroConfigBackupStore.SaveAsync(
            GetPayload(CreateValidConfigPages()),
            backupDirectory,
            createdAt,
            CancellationToken.None);
        string alphaPath = Path.Combine(backupDirectory, "micro-config-backup-alpha.json");
        string omegaPath = Path.Combine(backupDirectory, "micro-config-backup-omega.json");
        File.Move(firstSavedPath, alphaPath);
        File.Move(secondSavedPath, omegaPath);
        File.SetLastWriteTimeUtc(alphaPath, new DateTime(2026, 8, 16, 9, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(omegaPath, new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc));

        var viewModel = new MainViewModel(
            transport,
            new FakeConfirmationService(),
            CreateTempLibraryPath(),
            backupDirectory);

        Assert.Equal(alphaPath, viewModel.LastBackupPath);
    }

    [Fact]
    public async Task Construction_excludes_nonmatching_json_files_even_when_they_contain_newer_strict_backups()
    {
        using var transport = new FakeBleTransport();
        string backupDirectory = CreateTempBackupDirectory();
        string matchingPath = await MicroConfigBackupStore.SaveAsync(
            GetPayload(CreateValidConfigPages()),
            backupDirectory,
            new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        string savedNonmatchingPath = await MicroConfigBackupStore.SaveAsync(
            GetPayload(CreateValidConfigPages()),
            backupDirectory,
            new DateTimeOffset(2026, 8, 16, 11, 0, 0, TimeSpan.Zero),
            CancellationToken.None);
        string nonmatchingPath = Path.Combine(backupDirectory, "not-a-micro-config-backup.json");
        File.Move(savedNonmatchingPath, nonmatchingPath);
        byte[] nonmatchingContents = await File.ReadAllBytesAsync(nonmatchingPath);

        var viewModel = new MainViewModel(
            transport,
            new FakeConfirmationService(),
            CreateTempLibraryPath(),
            backupDirectory);

        Assert.Equal(matchingPath, viewModel.LastBackupPath);
        Assert.Equal(nonmatchingContents, await File.ReadAllBytesAsync(nonmatchingPath));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("not-a-directory")]
    [InlineData("no-valid-backup")]
    public async Task Construction_survives_missing_inaccessible_or_no_valid_backup_directory(string scenario)
    {
        using var transport = new FakeBleTransport();
        string backupDirectory = CreateTempBackupDirectory();
        if (scenario == "not-a-directory")
        {
            await File.WriteAllTextAsync(backupDirectory, "not a directory");
        }
        else if (scenario == "no-valid-backup")
        {
            Directory.CreateDirectory(backupDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(backupDirectory, "micro-config-backup-20260816-110000000-invalid.json"),
                "{\"schema\":1,\"timestamp\":\"2026-08-16T11:00:00Z\",\"payload\":\"AA==\"}");
        }

        var viewModel = new MainViewModel(
            transport,
            new FakeConfirmationService(),
            CreateTempLibraryPath(),
            backupDirectory);

        Assert.Null(viewModel.LastBackupPath);
        Assert.False(viewModel.RestoreDeviceBackupCommand.CanExecute(null));
    }

    [Fact]
    public async Task Restore_device_backup_declined_confirmation_sends_nothing_and_retains_the_original_backup()
    {
        using var transport = new FakeBleTransport();
        byte[] backupPayload = GetPayload(CreateValidConfigPages());
        var confirmation = new FakeConfirmationService(result: false);
        (MainViewModel viewModel, string backupPath, string backupDirectory) =
            await CreateConnectedRestoreViewModelAsync(transport, confirmation, backupPayload);
        viewModel.SelectedLanguage = "English";
        byte[] originalBackup = await File.ReadAllBytesAsync(backupPath);

        await ((RelayCommand)viewModel.RestoreDeviceBackupCommand).ExecuteAsync();

        Assert.Empty(transport.WriteInvocations);
        Assert.Equal(1, confirmation.CallCount);
        Assert.Contains("restore", confirmation.LastMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(backupPath, confirmation.LastMessage);
        Assert.DoesNotContain("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(backupPath, viewModel.LastBackupPath);
        Assert.Equal(originalBackup, await File.ReadAllBytesAsync(backupPath));
        Assert.Single(Directory.GetFiles(backupDirectory));
    }

    [Fact]
    public async Task Restore_device_backup_writes_exact_payload_packets_and_verifies_the_complete_readback()
    {
        using var transport = new FakeBleTransport();
        byte[] backupPayload = GetPayload(CreateValidConfigPages());
        new byte[] { 0x59, 0x00, 0x00, 0x00 }.CopyTo(backupPayload, 7 * 4);
        backupPayload[178] = 0xab;
        var confirmation = new FakeConfirmationService();
        (MainViewModel viewModel, string backupPath, string backupDirectory) =
            await CreateConnectedRestoreViewModelAsync(transport, confirmation, backupPayload);
        viewModel.SelectedLanguage = "English";
        byte[] originalBackup = await File.ReadAllBytesAsync(backupPath);
        transport.QueueCollectedNotificationBatch(CreateConfigPages(backupPayload));

        await ((RelayCommand)viewModel.RestoreDeviceBackupCommand).ExecuteAsync();

        FakeBleInvocation write = Assert.Single(transport.WriteInvocations);
        Assert.True(write.IsConnected);
        Assert.Equal(TimeSpan.FromMilliseconds(120), write.DelayBetweenWrites);
        Assert.False(write.CancellationToken.CanBeCanceled);
        Assert.Equal("Micro save sequence", write.Sequence?.Name);
        Assert.Equal(5, write.Sequence?.Writes.Count);
        Assert.Equal(backupPayload, GetSavePayload(write.Sequence!));
        Assert.Equal(0xab, GetSavePayload(write.Sequence!)[178]);
        uint[] offsets = { 0, 45, 90, 135 };
        for (int index = 0; index < offsets.Length; index++)
        {
            byte[] page = write.Sequence!.Writes[index];
            Assert.Equal(62, page.Length);
            Assert.Equal(0x04, page[0]);
            Assert.Equal(0x01, page[1]);
            Assert.Equal(offsets[index], BitConverter.ToUInt32(page, 13));
        }

        Assert.Equal(
            Hex("04 06 00 5b 00 00 00 ff ff 00 00 00 00 00 00 00 00"),
            write.Sequence!.Writes[^1]);
        Assert.DoesNotContain(transport.WriteInvocations, invocation => invocation.Sequence?.Name == "Captured Micro save sequence");
        Assert.Equal(
            new[]
            {
                FakeBleInvocationKind.Connect,
                FakeBleInvocationKind.Collect,
                FakeBleInvocationKind.Write,
                FakeBleInvocationKind.Collect
            },
            transport.Invocations.Select(invocation => invocation.Kind));
        Assert.Contains("restored", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(backupPath, viewModel.LastBackupPath);
        Assert.Equal(originalBackup, await File.ReadAllBytesAsync(backupPath));
        Assert.Single(Directory.GetFiles(backupDirectory));
        Assert.Contains(viewModel.RawConfigValues, value => value.Slot == 7 && value.DisplayText == "Keypad 1");
    }

    [Fact]
    public async Task Restore_device_backup_rejects_a_malformed_last_backup_without_writing()
    {
        using var transport = new FakeBleTransport();
        string backupDirectory = CreateTempBackupDirectory();
        Directory.CreateDirectory(backupDirectory);
        string backupPath = Path.Combine(backupDirectory, "malformed.json");
        await File.WriteAllTextAsync(backupPath, "{\"schema\":1,\"timestamp\":\"2026-08-16T00:00:00Z\",\"payload\":\"AA==\"}");
        var confirmation = new FakeConfirmationService();
        MainViewModel viewModel = CreateRestoreViewModel(transport, confirmation, backupDirectory, backupPath);
        transport.QueueCollectedNotificationBatch(CreateValidConfigPages());
        await ConnectAsync(viewModel);
        viewModel.SelectedLanguage = "English";

        await ((RelayCommand)viewModel.RestoreDeviceBackupCommand).ExecuteAsync();

        Assert.Empty(transport.WriteInvocations);
        Assert.Equal(0, confirmation.CallCount);
        Assert.DoesNotContain("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exactly 180 bytes", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(backupPath, viewModel.LastBackupPath);
    }

    [Fact]
    public async Task Restore_device_backup_write_failure_retains_the_backup_and_does_not_read_back()
    {
        using var transport = new FakeBleTransport();
        byte[] backupPayload = GetPayload(CreateValidConfigPages());
        var confirmation = new FakeConfirmationService();
        (MainViewModel viewModel, string backupPath, _) =
            await CreateConnectedRestoreViewModelAsync(transport, confirmation, backupPayload);
        transport.ThrowOnWrite(new InvalidOperationException("restore write failed"));
        viewModel.SelectedLanguage = "English";

        await ((RelayCommand)viewModel.RestoreDeviceBackupCommand).ExecuteAsync();

        Assert.Single(transport.WriteInvocations);
        Assert.Single(transport.CollectedSequences);
        Assert.Contains("restore write failed", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(backupPath, viewModel.LastBackupPath);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Restore_device_backup_incomplete_readback_never_reports_success()
    {
        using var transport = new FakeBleTransport();
        byte[] backupPayload = GetPayload(CreateValidConfigPages());
        (MainViewModel viewModel, string backupPath, _) =
            await CreateConnectedRestoreViewModelAsync(transport, new FakeConfirmationService(), backupPayload);
        transport.QueueCollectedNotificationBatch(CreateConfigPages(backupPayload).Take(3).ToArray());
        viewModel.SelectedLanguage = "English";

        await ((RelayCommand)viewModel.RestoreDeviceBackupCommand).ExecuteAsync();

        Assert.Single(transport.WriteInvocations);
        Assert.Contains("mismatch", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(backupPath, viewModel.LastBackupPath);
    }

    [Fact]
    public async Task Restore_device_backup_mismatch_in_an_unknown_payload_byte_never_reports_success()
    {
        using var transport = new FakeBleTransport();
        byte[] backupPayload = GetPayload(CreateValidConfigPages());
        backupPayload[178] = 0xab;
        byte[] wrongReadback = backupPayload.ToArray();
        wrongReadback[178] = 0xac;
        (MainViewModel viewModel, string backupPath, _) =
            await CreateConnectedRestoreViewModelAsync(transport, new FakeConfirmationService(), backupPayload);
        transport.QueueCollectedNotificationBatch(CreateConfigPages(wrongReadback));
        viewModel.SelectedLanguage = "English";

        await ((RelayCommand)viewModel.RestoreDeviceBackupCommand).ExecuteAsync();

        Assert.Single(transport.WriteInvocations);
        Assert.Contains("mismatch", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("verified", viewModel.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(backupPath, viewModel.LastBackupPath);
    }

    [Fact]
    public async Task Restore_device_backup_uses_is_busy_to_block_reentry_and_refresh_commands()
    {
        using var transport = new FakeBleTransport();
        byte[] backupPayload = GetPayload(CreateValidConfigPages());
        var confirmation = new FakeConfirmationService();
        confirmation.BlockNextConfirmation();
        (MainViewModel viewModel, string backupPath, _) =
            await CreateConnectedRestoreViewModelAsync(transport, confirmation, backupPayload);
        var refreshedCommands = new HashSet<object>();
        var commands = new[]
        {
            viewModel.SaveToDeviceCommand,
            viewModel.RestoreDeviceBackupCommand,
            viewModel.ResetMappingsCommand,
            viewModel.DisconnectCommand
        };
        foreach (var command in commands)
        {
            var observedCommand = command;
            command.CanExecuteChanged += (_, _) => refreshedCommands.Add(observedCommand);
        }

        Task firstRestore = ((RelayCommand)viewModel.RestoreDeviceBackupCommand).ExecuteAsync();
        await confirmation.WaitForRequestAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(viewModel.IsBusy);
        Assert.Equal(commands.Length, refreshedCommands.Count);
        Assert.All(commands, command => Assert.False(command.CanExecute(null)));
        await ((RelayCommand)viewModel.RestoreDeviceBackupCommand).ExecuteAsync();
        Assert.Equal(1, confirmation.CallCount);
        Assert.Empty(transport.WriteInvocations);

        confirmation.CompleteConfirmation(result: false);
        await firstRestore;

        Assert.False(viewModel.IsBusy);
        Assert.Equal(backupPath, viewModel.LastBackupPath);
    }

    private static void QueueValidConfig(FakeBleTransport transport)
    {
        transport.QueueCollectedNotifications(CreateValidConfigPages());
    }

    private static byte[][] CreateValidConfigPages()
    {
        return new[]
        {
            Hex("04 00 02 00 2d 00 9d 95 b4 00 00 00 00 00 00 00 8c b8 00 00 11 09 20 20 11 09 20 20 28 00 00 00 2a 00 00 00 e0 1a 00 00 e0 e1 17 00 e0 00 00 00 e2 00 00 00 e0 06 00 00 e0 19 00 00 00"),
            Hex("04 00 02 00 2d 00 69 07 b4 00 00 00 2d 00 00 00 00 00 00 00 00 00 00 4e 00 00 00 4b 00 00 00 e2 3d 00 00 29 00 00 00 52 00 00 00 51 00 00 00 50 00 00 00 4f 00 00 00 00 00 00 00 00 00"),
            Hex("04 00 02 00 2d 00 30 cf b4 00 00 00 5a 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00"),
            Hex("04 00 02 00 2d 00 30 cf b4 00 00 00 87 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00")
        };
    }

    private static MainViewModel CreateSaveViewModel(
        FakeBleTransport transport,
        FakeConfirmationService confirmation)
    {
        return new MainViewModel(
            transport,
            confirmation,
            CreateTempLibraryPath(),
            Path.Combine(Path.GetTempPath(), $"microkey-app-backups-{Guid.NewGuid():N}"))
        {
            SelectedDevice = new MicroBleDevice("AABBCCDDEEFF", "8BitDo Micro", -40)
        };
    }

    private static async Task<(MainViewModel ViewModel, string BackupPath, string BackupDirectory)>
        CreateConnectedRestoreViewModelAsync(
            FakeBleTransport transport,
            FakeConfirmationService confirmation,
            byte[] backupPayload)
    {
        string backupDirectory = CreateTempBackupDirectory();
        string backupPath = await MicroConfigBackupStore.SaveAsync(
            backupPayload,
            backupDirectory,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        MainViewModel viewModel = CreateRestoreViewModel(transport, confirmation, backupDirectory, backupPath);
        transport.QueueCollectedNotificationBatch(CreateValidConfigPages());
        await ConnectAsync(viewModel);
        return (viewModel, backupPath, backupDirectory);
    }

    private static MainViewModel CreateRestoreViewModel(
        FakeBleTransport transport,
        FakeConfirmationService confirmation,
        string backupDirectory,
        string backupPath)
    {
        var viewModel = new MainViewModel(
            transport,
            confirmation,
            CreateTempLibraryPath(),
            backupDirectory)
        {
            SelectedDevice = new MicroBleDevice("AABBCCDDEEFF", "8BitDo Micro", -40)
        };
        typeof(MainViewModel).GetProperty(nameof(MainViewModel.LastBackupPath))!.SetValue(viewModel, backupPath);
        return viewModel;
    }

    private static string CreateTempBackupDirectory()
    {
        return Path.Combine(Path.GetTempPath(), $"microkey-app-backups-{Guid.NewGuid():N}");
    }

    private static Task ConnectAsync(MainViewModel viewModel)
    {
        return ((RelayCommand)viewModel.ConnectCommand).ExecuteAsync();
    }

    private static byte[] GetPayload(byte[][] pages)
    {
        Assert.True(MicroConfigSnapshot.TryCreate(pages, out MicroConfigSnapshot? snapshot));
        Assert.NotNull(snapshot);
        return snapshot.CopyPayload();
    }

    private static byte[][] CreateConfigPages(byte[] payload)
    {
        uint[] offsets = { 0x00, 0x2d, 0x5a, 0x87 };
        return offsets.Select(offset =>
        {
            byte[] packet = new byte[61];
            packet[0] = 0x04;
            packet[1] = 0x00;
            packet[2] = 0x02;
            packet[4] = 45;
            BitConverter.GetBytes(offset).CopyTo(packet, 12);
            payload.AsSpan((int)offset, 45).CopyTo(packet.AsSpan(16));
            return packet;
        }).ToArray();
    }

    private static byte[] GetSavePayload(PacketSequence sequence)
    {
        return sequence.Writes
            .Take(4)
            .OrderBy(page => BitConverter.ToUInt32(page, 13))
            .SelectMany(page => page.Skip(17).Take(45))
            .ToArray();
    }

    private static string CreateTempLibraryPath()
    {
        return Path.Combine(Path.GetTempPath(), $"microkey-app-library-{Guid.NewGuid():N}.json");
    }

    private static byte[] Hex(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => Convert.ToByte(part, 16))
            .ToArray();
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
        }
    }
}
