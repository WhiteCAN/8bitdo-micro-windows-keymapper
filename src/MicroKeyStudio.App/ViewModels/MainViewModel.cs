using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MicroKeyStudio.App.Services;
using MicroKeyStudio.Ble;
using MicroKeyStudio.Protocol.Actions;
using MicroKeyStudio.Protocol.Buttons;
using MicroKeyStudio.Protocol.Configuration;
using MicroKeyStudio.Protocol.Packets;
using MicroKeyStudio.Protocol.Profiles;
using MicroKeyStudio.Storage;

namespace MicroKeyStudio.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private const string CurrentDeviceProfileId = "current-device";
    private const string CurrentDeviceProfileName = "Device Profile 1";
    private readonly IBleTransport _transport;
    private readonly IUserConfirmationService _confirmationService;
    private readonly string _profileLibraryPath;
    private readonly string _backupDirectory;
    private MicroBleDevice? _selectedDevice;
    private MicroConfigSnapshot? _currentSnapshot;
    private IReadOnlyList<byte[]> _lastReadConfigPages = Array.Empty<byte[]>();
    private string _status = "Ready to load device settings";
    private bool _isConnected;
    private string _selectedLanguage = "한국어";
    private ButtonMappingDisplay? _selectedMapping;
    private string _pendingAction = string.Empty;
    private UiText _text = UiText.Korean;
    private string? _lastDiagnosticFilePath;
    private DeviceProfileDisplay? _selectedProfile;
    private string _profileNameDraft = string.Empty;
    private KeyCategoryDisplay? _selectedKeyCategory;
    private bool _isMappingEditorOpen;
    private string? _lastBackupPath;
    private bool _isBusy;
    private bool _isSleepDisabled;

    public MainViewModel()
        : this(
            new WindowsBleTransport(),
            new MessageBoxConfirmationService(),
            GetDefaultProfileLibraryPath(),
            GetDefaultBackupDirectory())
    {
    }

    public MainViewModel(IBleTransport transport)
        : this(
            transport,
            new MessageBoxConfirmationService(),
            GetDefaultProfileLibraryPath(),
            GetDefaultBackupDirectory())
    {
    }

    public MainViewModel(IBleTransport transport, string profileLibraryPath)
        : this(
            transport,
            new MessageBoxConfirmationService(),
            profileLibraryPath,
            GetDefaultBackupDirectory())
    {
    }

    public MainViewModel(
        IBleTransport transport,
        IUserConfirmationService confirmationService,
        string profileLibraryPath,
        string backupDirectory)
    {
        _transport = transport;
        _confirmationService = confirmationService;
        _profileLibraryPath = profileLibraryPath;
        _backupDirectory = backupDirectory;
        _transport.NotificationReceived += (_, data) => AddLog($"Notify: {Convert.ToHexString(data)}");
        ScanCommand = new RelayCommand(ScanAsync, () => !IsBusy && !IsConnected);
        ConnectCommand = new RelayCommand(ConnectAsync, () => !IsBusy && SelectedDevice is not null && !IsConnected);
        LoadSelectedProfileCommand = new RelayCommand(LoadSelectedProfileAsync, () => !IsBusy && SelectedProfile is not null);
        CreateProfileCommand = new RelayCommand(CreateProfileAsync, CanCreateProfile);
        RenameProfileCommand = new RelayCommand(RenameProfileAsync, () => !IsBusy && SelectedProfile is not null && !string.IsNullOrWhiteSpace(ProfileNameDraft));
        DeleteProfileCommand = new RelayCommand(DeleteProfileAsync, () => !IsBusy && SelectedProfile is not null);
        SaveToDeviceCommand = new RelayCommand(SaveToDeviceAsync, CanSaveToDevice);
        RestoreDeviceBackupCommand = new RelayCommand(RestoreDeviceBackupAsync, CanRestoreDeviceBackup);
        ResetMappingsCommand = new RelayCommand(ResetMappingsAsync, CanResetMappings);
        DisconnectCommand = new RelayCommand(DisconnectAsync, () => !IsBusy && IsConnected);
        SelectMappingCommand = new RelayCommand<ButtonMappingDisplay>(SelectMapping, mapping => !IsBusy && mapping is not null);
        ApplySelectedMappingCommand = new RelayCommand(ApplySelectedMappingAsync, () => !IsBusy && SelectedMapping is not null && SelectedProfile is not null && !string.IsNullOrWhiteSpace(PendingAction));
        CancelMappingEditCommand = new RelayCommand(CancelMappingEditAsync, () => !IsBusy);
        SelectKeyOptionCommand = new RelayCommand<KeyOptionDisplay>(SelectKeyOption, option => !IsBusy && option is not null && SelectedMapping is not null);
        LastBackupPath = DiscoverLastBackupPath();
        BuildKeyPicker(isEnglish: false);
        LoadProfileLibrary();
    }

    public ObservableCollection<MicroBleDevice> Devices { get; } = new();

    public ObservableCollection<string> LogLines { get; } = new();

    public ObservableCollection<MicroKeyValue> RawConfigValues { get; } = new();

    public ObservableCollection<DeviceProfileDisplay> DeviceProfiles { get; } = new();

    public ObservableCollection<KeyCategoryDisplay> KeyCategories { get; } = new();

    public ObservableCollection<KeyOptionDisplay> AvailableKeyOptions { get; } = new();

    public IReadOnlyList<string> Languages { get; } = new[] { "한국어", "English" };

    public UiText Text
    {
        get => _text;
        private set
        {
            if (_text == value)
            {
                return;
            }

            _text = value;
            OnPropertyChanged();
        }
    }

    public string? LastDiagnosticFilePath
    {
        get => _lastDiagnosticFilePath;
        private set
        {
            if (_lastDiagnosticFilePath == value)
            {
                return;
            }

            _lastDiagnosticFilePath = value;
            OnPropertyChanged();
        }
    }

    public DeviceProfileDisplay? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (_selectedProfile == value)
            {
                return;
            }

            _selectedProfile = value;
            ProfileNameDraft = value?.Name ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProfileSummaryText));
            RaiseCommandStates();
        }
    }

    public string ProfileNameDraft
    {
        get => _profileNameDraft;
        set
        {
            if (_profileNameDraft == value)
            {
                return;
            }

            _profileNameDraft = value;
            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public ObservableCollection<ButtonMappingDisplay> LeftMappings { get; } = new()
    {
        new ButtonMappingDisplay("L2", "L2", "L2", "Win+Z", "Upper left shoulder", "왼쪽 위 숄더", 9),
        new ButtonMappingDisplay("L", "L", "L", "Win+Z", "Left shoulder", "왼쪽 숄더", 7),
        new ButtonMappingDisplay("Minus", "-", "-", "Space", "Minus", "마이너스", 13),
        new ButtonMappingDisplay("Up", "위", "Up", ".", "D-pad up", "십자키 위", 17),
        new ButtonMappingDisplay("Left", "왼쪽", "Left", "Win+A", "D-pad left", "십자키 왼쪽", 19),
        new ButtonMappingDisplay("Right", "오른쪽", "Right", "Win+T", "D-pad right", "십자키 오른쪽", 20),
        new ButtonMappingDisplay("Down", "아래", "Down", ",", "D-pad down", "십자키 아래", 18),
        new ButtonMappingDisplay("Star", "별", "Star", "Z", "Star", "별 버튼", 15)
    };

    public ObservableCollection<ButtonMappingDisplay> RightMappings { get; } = new()
    {
        new ButtonMappingDisplay("R2", "R2", "R2", "Win+S", "Upper right shoulder", "오른쪽 위 숄더", 10),
        new ButtonMappingDisplay("R", "R", "R", "Win+S", "Right shoulder", "오른쪽 숄더", 8),
        new ButtonMappingDisplay("Plus", "+", "+", "Shift+D", "Plus", "플러스", 14),
        new ButtonMappingDisplay("X", "X", "X", "Shift+'", "X button", "X 버튼", 5),
        new ButtonMappingDisplay("A", "A", "A", "Alt+'", "A button", "A 버튼", 3),
        new ButtonMappingDisplay("Y", "Y", "Y", "G", "Y button", "Y 버튼", 6),
        new ButtonMappingDisplay("B", "B", "B", "Esc", "B button", "B 버튼", 4),
        new ButtonMappingDisplay("Logo", "로고", "Logo", "Win+B", "Logo", "로고 버튼", 16)
    };

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage == value)
            {
                return;
            }

            _selectedLanguage = value;
            bool isEnglish = value == "English";
            Text = isEnglish ? UiText.English : UiText.Korean;
            foreach (ButtonMappingDisplay mapping in LeftMappings.Concat(RightMappings))
            {
                mapping.SetLanguage(isEnglish);
            }

            foreach (KeyCategoryDisplay category in KeyCategories)
            {
                category.SetLanguage(isEnglish);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedMappingButtonText));
            OnPropertyChanged(nameof(SelectedMappingDescriptionText));
            OnPropertyChanged(nameof(EditorTitleText));
            OnPropertyChanged(nameof(SleepSettingStatusText));
            OnPropertyChanged(nameof(RestoreRequirementText));
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(SaveRequirementText));
            Status = isEnglish ? "Language changed to English." : "언어가 한국어로 변경되었습니다.";
        }
    }

    public KeyCategoryDisplay? SelectedKeyCategory
    {
        get => _selectedKeyCategory;
        set
        {
            if (_selectedKeyCategory == value)
            {
                return;
            }

            _selectedKeyCategory = value;
            RefreshAvailableKeyOptions();
            OnPropertyChanged();
        }
    }

    public ButtonMappingDisplay? SelectedMapping
    {
        get => _selectedMapping;
        private set
        {
            if (_selectedMapping == value)
            {
                return;
            }

            if (_selectedMapping is not null)
            {
                _selectedMapping.IsSelected = false;
            }

            _selectedMapping = value;
            if (_selectedMapping is not null)
            {
                _selectedMapping.IsSelected = true;
                PendingAction = _selectedMapping.Action;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedMappingButtonText));
            OnPropertyChanged(nameof(SelectedMappingDescriptionText));
            OnPropertyChanged(nameof(EditorTitleText));
            RaiseCommandStates();
        }
    }

    public string? LastBackupPath
    {
        get => _lastBackupPath;
        private set
        {
            if (_lastBackupPath == value)
            {
                return;
            }

            _lastBackupPath = value;
            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public string PendingAction
    {
        get => _pendingAction;
        set
        {
            if (_pendingAction == value)
            {
                return;
            }

            _pendingAction = value;
            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public string SelectedMappingButtonText => SelectedMapping?.Button ?? Text.SelectMapping;

    public string SelectedMappingDescriptionText => SelectedMapping?.Description ?? Text.NoButtonSelected;

    public bool IsMappingEditorOpen
    {
        get => _isMappingEditorOpen;
        private set
        {
            if (_isMappingEditorOpen == value)
            {
                return;
            }

            _isMappingEditorOpen = value;
            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public string EditorTitleText => SelectedMapping?.Button ?? Text.SelectMapping;

    public bool IsSleepSettingSupported => IsConnected
        && !IsBusy
        && _currentSnapshot?.IsDisableSleepValueKnown == true;

    public bool IsSleepDisabled
    {
        get => _isSleepDisabled;
        set
        {
            if (_isSleepDisabled == value)
            {
                return;
            }

            _isSleepDisabled = value;
            OnPropertyChanged();
            RaiseCommandStates();
        }
    }

    public string SleepSettingStatusText => _currentSnapshot is null || !IsConnected
        ? (SelectedLanguage == "English"
            ? "Connect and load the complete device settings first."
            : "먼저 기기를 연결하고 전체 설정을 불러오세요.")
        : !_currentSnapshot.IsDisableSleepValueKnown
            ? (SelectedLanguage == "English"
                ? "The device returned an unsupported Disable Sleep value. It will be preserved."
                : "기기에서 지원되지 않는 슬립 값을 반환했습니다. 원본 값은 그대로 보존됩니다.")
        : IsBusy
            ? (SelectedLanguage == "English" ? "Wait for the current device operation." : "현재 기기 작업이 끝날 때까지 기다리세요.")
            : (SelectedLanguage == "English"
                ? "Change the setting, then use Save to device."
                : "설정을 변경한 뒤 기기에 저장을 누르세요.");

    public string ConnectionStatusText => IsConnected
        ? (SelectedLanguage == "English" ? "Device connected" : "기기 연결됨")
        : (SelectedLanguage == "English" ? "Device not connected · Local profile" : "기기 연결 안 됨 · PC 프로필");

    public string SaveRequirementText => IsBusy
        ? (SelectedLanguage == "English" ? "Wait for the current device operation." : "현재 기기 작업이 끝날 때까지 기다리세요.")
        : IsMappingEditorOpen
            ? (SelectedLanguage == "English" ? "Apply to the PC profile first. Save to the device from the overview." : "먼저 PC 프로필에 적용하세요. 기기 저장은 매핑 화면에서 진행합니다.")
            : !IsConnected || _currentSnapshot is null
                ? (SelectedLanguage == "English" ? "Connect a device and load its settings to save." : "기기에 저장하려면 기기를 연결하고 설정을 불러오세요.")
                : !CanSaveToDevice()
                    ? (SelectedLanguage == "English" ? "A mapping is unsupported. Choose a supported key combination." : "지원하지 않는 매핑이 있습니다. 지원하는 키 조합을 선택하세요.")
                    : (SelectedLanguage == "English" ? "PC profile changes are saved separately. Use Save to device to update the device." : "PC 프로필과 기기는 별도로 저장됩니다. 기기에 반영하려면 기기에 저장을 누르세요.");

    public string RestoreRequirementText => SelectedLanguage == "English"
        ? "Restores the latest pre-save backup. Requires a connected device and a valid backup."
        : "가장 최근 저장 직전 백업을 복원합니다. 연결된 기기와 유효한 백업이 필요합니다.";

    public string ProfileSummaryText => SelectedProfile is null
        ? string.Format(Text.ProfileCount, DeviceProfiles.Count)
        : string.Format(Text.ProfileCountWithSelection, DeviceProfiles.Count, SelectedProfile.Name);

    public MicroBleDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            _selectedDevice = value;
            OnPropertyChanged();
            RaiseCommandStates();
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
            OnPropertyChanged(nameof(IsSleepSettingSupported));
            OnPropertyChanged(nameof(SleepSettingStatusText));
            RaiseCommandStates();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSleepSettingSupported));
            OnPropertyChanged(nameof(SleepSettingStatusText));
            RaiseCommandStates();
        }
    }

    public ICommand ScanCommand { get; }

    public ICommand ConnectCommand { get; }

    public ICommand LoadSelectedProfileCommand { get; }

    public ICommand CreateProfileCommand { get; }

    public ICommand RenameProfileCommand { get; }

    public ICommand DeleteProfileCommand { get; }

    public ICommand SaveToDeviceCommand { get; }

    public ICommand RestoreDeviceBackupCommand { get; }

    public ICommand ResetMappingsCommand { get; }

    public ICommand DisconnectCommand { get; }

    public ICommand SelectMappingCommand { get; }

    public ICommand ApplySelectedMappingCommand { get; }

    public ICommand CancelMappingEditCommand { get; }

    public ICommand SelectKeyOptionCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task ScanAsync()
    {
        await RunWithStatusAsync(Text.Scanning, async () =>
        {
            Devices.Clear();
            IReadOnlyList<MicroBleDevice> devices = await _transport.ScanAsync(CancellationToken.None);
            foreach (MicroBleDevice device in devices)
            {
                Devices.Add(device);
            }

            if (Devices.Count > 0)
            {
                SelectedDevice = Devices[0];
                AddLog(string.Format(Text.AutoLoadingOnlyDevice, SelectedDevice.Name));
                await ConnectAsync();
                return;
            }

            Status = string.Format(Text.FoundDevices, Devices.Count);
        });
    }

    private async Task ConnectAsync()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        MicroBleDevice device = SelectedDevice;
        var notifications = new List<byte[]>();
        void CaptureNotification(object? _, byte[] data)
        {
            lock (notifications)
            {
                notifications.Add(data.ToArray());
            }
        }

        await RunWithStatusAsync(string.Format(Text.LoadingSettings, device.Name), async () =>
        {
            RawConfigValues.Clear();
            _currentSnapshot = null;
            OnPropertyChanged(nameof(IsSleepSettingSupported));
            OnPropertyChanged(nameof(SleepSettingStatusText));
            RaiseCommandStates();
            OnPropertyChanged(nameof(ProfileSummaryText));

            _transport.NotificationReceived += CaptureNotification;
            MicroConfigSnapshot? snapshot = null;
            try
            {
                await _transport.ConnectAsync(device, CancellationToken.None);
                IsConnected = true;
                snapshot = await ReadCurrentSnapshotAsync();
            }
            catch (Exception ex)
            {
                LastDiagnosticFilePath = WriteLoadDiagnostics(device, notifications, _lastReadConfigPages, ex);
                AddLog($"Diagnostics: {LastDiagnosticFilePath}");
                if (ex is IncompleteMicroConfigException)
                {
                    Status = string.Format(Text.SettingsNotReturned, device.Name);
                    OnPropertyChanged(nameof(ProfileSummaryText));
                    AddLog($"Profiles loaded: {DeviceProfiles.Count}; config pages: {_lastReadConfigPages.Count}/4; notifications: {notifications.Count}");
                    return;
                }

                throw;
            }
            finally
            {
                _transport.NotificationReceived -= CaptureNotification;
            }

            if (snapshot is not null)
            {
                _currentSnapshot = snapshot;
                RefreshRawConfigValues(snapshot);

                UpsertProfile(CreateProfileFromSnapshot(CurrentDeviceProfileId, CurrentDeviceProfileName, snapshot));
                SelectedProfile = DeviceProfiles.Single(profile => profile.Id == CurrentDeviceProfileId);
                ApplyProfile(SelectedProfile);
                await SaveProfileLibraryAsync();

                Status = string.Format(Text.LoadedConfigPages, _lastReadConfigPages.Count, device.Name);
                LastDiagnosticFilePath = WriteLoadDiagnostics(device, notifications, _lastReadConfigPages);
                AddLog($"Decoded {RawConfigValues.Count} raw keyboard slot(s).");
                AddLog($"Decoded profiles shown: {DeviceProfiles.Count}; selected: {SelectedProfile.Name}");
                AddLog($"Diagnostics: {LastDiagnosticFilePath}");
                RaiseCommandStates();
            }
        });
    }

    private async Task<MicroConfigSnapshot> ReadCurrentSnapshotAsync()
    {
        PacketSequence loadSequence = KnownMicroPackets.CreateCapturedLoadSequence();
        AddLog($"Load writes: {loadSequence.Writes.Count}");
        _lastReadConfigPages = Array.Empty<byte[]>();
        _lastReadConfigPages = await _transport.WriteSequenceAndCollectNotificationsAsync(
            loadSequence,
            MicroConfigSnapshot.IsConfigPage,
            expectedCount: 4,
            timeout: TimeSpan.FromSeconds(8),
            delayBetweenWrites: TimeSpan.FromMilliseconds(180),
            CancellationToken.None);
        AddLog($"Config pages received: {_lastReadConfigPages.Count}/4");

        if (!MicroConfigSnapshot.TryCreate(_lastReadConfigPages, out MicroConfigSnapshot? snapshot) || snapshot is null)
        {
            throw new IncompleteMicroConfigException();
        }

        return snapshot;
    }

    private Task LoadSelectedProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return Task.CompletedTask;
        }

        ApplyProfile(SelectedProfile);
        return Task.CompletedTask;
    }

    private bool CanSaveToDevice()
    {
        if (!IsConnected || IsBusy || IsMappingEditorOpen || _currentSnapshot is null)
        {
            return false;
        }

        try
        {
            if (_currentSnapshot.IsDisableSleepValueKnown)
            {
                MicroSavePacketBuilder.Build(_currentSnapshot, CreateActionsBySlot(), IsSleepDisabled);
            }
            else
            {
                MicroSavePacketBuilder.Build(_currentSnapshot, CreateActionsBySlot());
            }
            return true;
        }
        catch (MicroMappingEncodingException)
        {
            return false;
        }
    }

    private async Task SaveToDeviceAsync()
    {
        if (!CanSaveToDevice())
        {
            return;
        }

        IsBusy = true;
        try
        {
            IReadOnlyDictionary<int, string> actionsBySlot = CreateActionsBySlot();
            bool? disableSleep = _currentSnapshot?.IsDisableSleepValueKnown == true
                ? IsSleepDisabled
                : null;
            _currentSnapshot = null;
            OnPropertyChanged(nameof(IsSleepSettingSupported));
            OnPropertyChanged(nameof(SleepSettingStatusText));
            RaiseCommandStates();
            MicroConfigSnapshot baseline;
            try
            {
                baseline = await ReadCurrentSnapshotAsync();
            }
            catch (IncompleteMicroConfigException)
            {
                _currentSnapshot = null;
                Status = string.Format(Text.SettingsNotReturned, SelectedDevice?.Name ?? "Micro");
                return;
            }

            _currentSnapshot = baseline;
            MicroSaveBuildResult build = disableSleep.HasValue
                ? MicroSavePacketBuilder.Build(baseline, actionsBySlot, disableSleep.Value)
                : MicroSavePacketBuilder.Build(baseline, actionsBySlot);
            LastBackupPath = await MicroConfigBackupStore.SaveAsync(
                baseline.CopyPayload(),
                _backupDirectory,
                DateTimeOffset.Now,
                CancellationToken.None);

            MicroConfigSnapshot target = MicroConfigSnapshot.FromPayload(build.ExpectedPayload);
            string confirmationMessage = BuildSaveConfirmationMessage(baseline, target, LastBackupPath);
            if (!await _confirmationService.ConfirmAsync(Text.Confirmation, confirmationMessage))
            {
                Status = SelectedLanguage == "English" ? "Save canceled." : "기기 저장을 취소했습니다.";
                return;
            }

            _currentSnapshot = null;
            OnPropertyChanged(nameof(IsSleepSettingSupported));
            OnPropertyChanged(nameof(SleepSettingStatusText));
            RaiseCommandStates();
            await _transport.WriteSequenceAsync(
                build.Sequence,
                TimeSpan.FromMilliseconds(120),
                CancellationToken.None);

            MicroConfigSnapshot readback;
            try
            {
                readback = await ReadCurrentSnapshotAsync();
            }
            catch (IncompleteMicroConfigException)
            {
                Status = Text.ReadbackMismatch;
                return;
            }

            _currentSnapshot = readback;
            RefreshRawConfigValues(readback);
            if (!MicroSavePacketBuilder.MatchesSlots(readback, actionsBySlot, out IReadOnlyList<int> mismatchedSlots))
            {
                Status = $"{Text.ReadbackMismatch}: {string.Join(", ", mismatchedSlots.Select(slot => $"Slot {slot:00}"))}";
                return;
            }

            if (disableSleep.HasValue && readback.DisableSleep != disableSleep.Value)
            {
                Status = $"{Text.ReadbackMismatch}: Disable Sleep";
                return;
            }

            Status = Text.VerifiedSuccess;
        }
        catch (MicroMappingEncodingException exception)
        {
            Status = $"{Text.UnsupportedMapping}: {string.Join(", ", exception.InvalidSlots.Select(slot => $"Slot {slot:00}"))}";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
            AddLog($"Error: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRestoreDeviceBackup()
    {
        return !IsBusy
            && IsConnected
            && !string.IsNullOrWhiteSpace(LastBackupPath)
            && File.Exists(LastBackupPath);
    }

    private string? DiscoverLastBackupPath()
    {
        try
        {
            var validBackups = new List<(string Path, DateTimeOffset CreatedAt)>();
            foreach (string path in Directory.EnumerateFiles(
                _backupDirectory,
                "micro-config-backup-*.json",
                SearchOption.TopDirectoryOnly))
            {
                try
                {
                    MicroConfigBackup backup = MicroConfigBackupStore.LoadAsync(path, CancellationToken.None).GetAwaiter().GetResult();
                    validBackups.Add((path, backup.CreatedAt));
                }
                catch (Exception exception) when (
                    exception is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or ArgumentException)
                {
                    // Invalid or no-longer-readable candidates are not eligible for restoration.
                }
            }

            return validBackups
                .OrderByDescending(backup => backup.CreatedAt)
                .ThenBy(backup => backup.Path, StringComparer.Ordinal)
                .Select(backup => backup.Path)
                .FirstOrDefault();
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            // Backup discovery must not prevent the editor from starting.
        }

        return null;
    }

    private async Task RestoreDeviceBackupAsync()
    {
        if (!CanRestoreDeviceBackup())
        {
            return;
        }

        string backupPath = LastBackupPath!;
        IsBusy = true;
        try
        {
            MicroConfigBackup backup = await MicroConfigBackupStore.LoadAsync(backupPath, CancellationToken.None);
            MicroConfigSnapshot target = MicroConfigSnapshot.FromPayload(backup.Payload);
            string confirmationMessage = SelectedLanguage == "English"
                ? $"Restore the latest pre-save Micro backup?{Environment.NewLine}{Environment.NewLine}{backupPath}{Environment.NewLine}{Environment.NewLine}This will overwrite the current device settings."
                : $"Micro의 가장 최근 저장 직전 백업을 복원하시겠습니까?{Environment.NewLine}{Environment.NewLine}{backupPath}{Environment.NewLine}{Environment.NewLine}현재 기기 설정을 덮어씁니다.";
            if (!await _confirmationService.ConfirmAsync(Text.Confirmation, confirmationMessage))
            {
                Status = SelectedLanguage == "English" ? "Restore canceled." : "최근 백업 복원을 취소했습니다.";
                return;
            }

            PacketSequence sequence = MicroSavePacketBuilder.BuildFromPayload(target);
            _currentSnapshot = null;
            RaiseCommandStates();
            await _transport.WriteSequenceAsync(
                sequence,
                TimeSpan.FromMilliseconds(120),
                CancellationToken.None);

            MicroConfigSnapshot readback;
            try
            {
                readback = await ReadCurrentSnapshotAsync();
            }
            catch (IncompleteMicroConfigException)
            {
                Status = Text.ReadbackMismatch;
                return;
            }

            _currentSnapshot = readback;
            if (!readback.CopyPayload().AsSpan().SequenceEqual(backup.Payload))
            {
                Status = Text.ReadbackMismatch;
                return;
            }

            RefreshRawConfigValues(readback);
            Status = SelectedLanguage == "English"
                ? "Latest backup restored and verified."
                : "최근 백업을 복원하고 검증했습니다.";
        }
        catch (Exception exception)
        {
            Status = exception.Message;
            AddLog($"Error: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanResetMappings()
    {
        if (IsBusy || _currentSnapshot is null || SelectedProfile is null)
        {
            return false;
        }

        DeviceProfileDisplay baseline = CreateProfileFromSnapshot(
            CurrentDeviceProfileId,
            CurrentDeviceProfileName,
            _currentSnapshot);
        return LeftMappings.Concat(RightMappings).Any(mapping =>
            baseline.ActionsByButtonId.TryGetValue(mapping.Id, out string? action)
            && !string.Equals(mapping.Action, action, StringComparison.Ordinal))
            || IsSleepDisabled != _currentSnapshot.DisableSleep;
    }

    private async Task ResetMappingsAsync()
    {
        if (!CanResetMappings() || _currentSnapshot is null || SelectedProfile is null)
        {
            return;
        }

        string message = SelectedLanguage == "English"
            ? "Reset the screen and selected local profile to the last settings read from the device? This does not write to the device."
            : "화면과 선택한 PC 로컬 프로필을 기기에서 마지막으로 읽은 설정으로 되돌리시겠습니까? 이 작업은 기기에 쓰지 않습니다.";
        if (!await _confirmationService.ConfirmAsync(Text.Confirmation, message))
        {
            Status = SelectedLanguage == "English" ? "Reset canceled." : "변경사항 되돌리기를 취소했습니다.";
            return;
        }

        DeviceProfileDisplay baseline = CreateProfileFromSnapshot(
            CurrentDeviceProfileId,
            CurrentDeviceProfileName,
            _currentSnapshot);
        foreach (ButtonMappingDisplay mapping in LeftMappings.Concat(RightMappings))
        {
            if (!baseline.ActionsByButtonId.TryGetValue(mapping.Id, out string? action))
            {
                continue;
            }

            mapping.Action = action;
            SelectedProfile.SetAction(mapping.Id, action);
        }

        IsSleepDisabled = _currentSnapshot.DisableSleep;

        PendingAction = SelectedMapping?.Action ?? string.Empty;
        await SaveProfileLibraryAsync();
        Status = SelectedLanguage == "English"
            ? "Reset changes to the last device-read settings."
            : "변경사항을 기기에서 마지막으로 읽은 설정으로 되돌렸습니다.";
        RaiseCommandStates();
    }

    private IReadOnlyDictionary<int, string> CreateActionsBySlot()
    {
        return LeftMappings.Concat(RightMappings)
            .ToDictionary(mapping => mapping.SourceSlot, mapping => mapping.Action);
    }

    private string BuildSaveConfirmationMessage(
        MicroConfigSnapshot baseline,
        MicroConfigSnapshot target,
        string backupPath)
    {
        byte[] baselinePayload = baseline.CopyPayload();
        byte[] targetPayload = target.CopyPayload();
        var changes = LeftMappings.Concat(RightMappings)
            .Where(mapping => !baselinePayload.AsSpan(mapping.SourceSlot * 4, 4)
                .SequenceEqual(targetPayload.AsSpan(mapping.SourceSlot * 4, 4)))
            .Select(mapping => $"{mapping.Button}: {baseline.KeyboardValues[mapping.SourceSlot].DisplayText} -> {target.KeyboardValues[mapping.SourceSlot].DisplayText}")
            .ToList();
        if (baseline.DisableSleep != target.DisableSleep)
        {
            string before = baseline.DisableSleep ? "On" : "Off";
            string after = target.DisableSleep ? "On" : "Off";
            changes.Add($"Disable Sleep: {before} -> {after}");
        }

        string changeList = changes.Count > 0
            ? string.Join(Environment.NewLine, changes)
            : (SelectedLanguage == "English" ? "No mapping changes." : "변경된 매핑이 없습니다.");

        return SelectedLanguage == "English"
            ? $"The following Micro mappings will be written:{Environment.NewLine}{Environment.NewLine}{changeList}{Environment.NewLine}{Environment.NewLine}Backup: {backupPath}{Environment.NewLine}{Environment.NewLine}Continue?"
            : $"다음 Micro 매핑을 기기에 저장합니다:{Environment.NewLine}{Environment.NewLine}{changeList}{Environment.NewLine}{Environment.NewLine}백업: {backupPath}{Environment.NewLine}{Environment.NewLine}계속하시겠습니까?";
    }

    private async Task DisconnectAsync()
    {
        await RunWithStatusAsync("Disconnecting...", async () =>
        {
            await _transport.DisconnectAsync();
            _currentSnapshot = null;
            OnPropertyChanged(nameof(IsSleepSettingSupported));
            OnPropertyChanged(nameof(SleepSettingStatusText));
            IsConnected = false;
            Status = Text.Disconnected;
        });
    }

    public void SelectMapping(ButtonMappingDisplay? mapping)
    {
        if (mapping is null)
        {
            return;
        }

        SelectedMapping = mapping;
        PendingAction = mapping.Action;
        IsMappingEditorOpen = true;
    }

    public void ApplySelectedMapping()
    {
        if (SelectedMapping is null || string.IsNullOrWhiteSpace(PendingAction))
        {
            return;
        }

        SelectedMapping.Action = PendingAction.Trim();
        if (SelectedProfile is not null)
        {
            SelectedProfile.SetAction(SelectedMapping.Id, SelectedMapping.Action);
        }

        Status = string.Format(Text.UpdatedMapping, SelectedMapping.Button, SelectedMapping.Action);
        RaiseCommandStates();
    }

    private async Task ApplySelectedMappingAsync()
    {
        if (SelectedMapping is null || SelectedProfile is null || string.IsNullOrWhiteSpace(PendingAction))
        {
            return;
        }

        ButtonMappingDisplay mapping = SelectedMapping;
        DeviceProfileDisplay profile = SelectedProfile;
        string previousMappingAction = mapping.Action;
        bool profileHadAction = profile.ActionsByButtonId.TryGetValue(mapping.Id, out string? previousProfileAction);
        ApplySelectedMapping();
        try
        {
            await SaveProfileLibraryAsync();
            IsMappingEditorOpen = false;
        }
        catch (Exception ex)
        {
            mapping.Action = previousMappingAction;
            if (profileHadAction)
            {
                profile.SetAction(mapping.Id, previousProfileAction!);
            }
            else
            {
                profile.RemoveAction(mapping.Id);
            }

            Status = ex.Message;
            AddLog($"Error: {ex.Message}");
        }
    }

    private Task CancelMappingEditAsync()
    {
        if (SelectedMapping is not null)
        {
            PendingAction = SelectedMapping.Action;
        }

        IsMappingEditorOpen = false;
        return Task.CompletedTask;
    }

    private async Task CreateProfileAsync()
    {
        string name = ProfileNameDraft.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var actions = LeftMappings.Concat(RightMappings)
            .ToDictionary(mapping => mapping.Id, mapping => mapping.Action);
        var profile = new DeviceProfileDisplay(CreateProfileId(), name, actions);
        DeviceProfiles.Add(profile);
        SelectedProfile = profile;
        await SaveProfileLibraryAsync();
        Status = string.Format(Text.ProfileCreated, profile.Name);
    }

    private bool CanCreateProfile()
    {
        string name = ProfileNameDraft.Trim();
        return !IsBusy
            && !string.IsNullOrWhiteSpace(name)
            && !string.Equals(name, SelectedProfile?.Name, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RenameProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        string name = ProfileNameDraft.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        SelectedProfile.Name = name;
        OnPropertyChanged(nameof(ProfileSummaryText));
        await SaveProfileLibraryAsync();
        Status = string.Format(Text.ProfileRenamed, name);
    }

    private async Task DeleteProfileAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        DeviceProfileDisplay removed = SelectedProfile;
        DeviceProfiles.Remove(removed);
        SelectedProfile = DeviceProfiles.FirstOrDefault();
        await SaveProfileLibraryAsync();
        Status = string.Format(Text.ProfileDeleted, removed.Name);
    }

    private async Task RunWithStatusAsync(string startingStatus, Func<Task> action)
    {
        Status = startingStatus;
        AddLog(startingStatus);
        try
        {
            await action();
            AddLog(Status);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            AddLog($"Error: {ex.Message}");
        }
    }

    private void AddLog(string message)
    {
        string line = $"{DateTime.Now:HH:mm:ss} {message}";
        if (App.Current?.Dispatcher is null)
        {
            LogLines.Insert(0, line);
            return;
        }

        App.Current.Dispatcher.Invoke(() => LogLines.Insert(0, line));
    }

    private void RaiseCommandStates()
    {
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(SaveRequirementText));
        (ScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ConnectCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LoadSelectedProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CreateProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RenameProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveToDeviceCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RestoreDeviceBackupCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ResetMappingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DisconnectCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SelectMappingCommand as RelayCommand<ButtonMappingDisplay>)?.RaiseCanExecuteChanged();
        (ApplySelectedMappingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CancelMappingEditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SelectKeyOptionCommand as RelayCommand<KeyOptionDisplay>)?.RaiseCanExecuteChanged();
    }

    private void SelectKeyOption(KeyOptionDisplay? option)
    {
        if (option is null || SelectedMapping is null)
        {
            return;
        }

        PendingAction = option.Action;
    }

    private void BuildKeyPicker(bool isEnglish)
    {
        KeyCategories.Clear();
        KeyCategories.Add(new KeyCategoryDisplay(
            "letters",
            "Letters:A-Z",
            "문자 A-Z",
            new[]
            {
                KeyRow("Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P"),
                KeyRow("A", "S", "D", "F", "G", "H", "J", "K", "L"),
                KeyRow("Z", "X", "C", "V", "B", "N", "M")
            }));
        KeyCategories.Add(new KeyCategoryDisplay(
            "numeric-keypad",
            "Numeric Keypad",
            "숫자 키패드",
            new[]
            {
                KeyRow(
                    new KeyOptionDisplay("Num Lock", "Num Lock"),
                    new KeyOptionDisplay("/", "Keypad /"),
                    new KeyOptionDisplay("*", "Keypad *"),
                    new KeyOptionDisplay("-", "Keypad -")),
                KeyRow(
                    new KeyOptionDisplay("7", "Keypad 7"),
                    new KeyOptionDisplay("8", "Keypad 8"),
                    new KeyOptionDisplay("9", "Keypad 9"),
                    new KeyOptionDisplay("+", "Keypad +")),
                KeyRow(
                    new KeyOptionDisplay("4", "Keypad 4"),
                    new KeyOptionDisplay("5", "Keypad 5"),
                    new KeyOptionDisplay("6", "Keypad 6")),
                KeyRow(
                    new KeyOptionDisplay("1", "Keypad 1"),
                    new KeyOptionDisplay("2", "Keypad 2"),
                    new KeyOptionDisplay("3", "Keypad 3"),
                    new KeyOptionDisplay("Enter", "Keypad Enter")),
                KeyRow(
                    new KeyOptionDisplay("0", "Keypad 0"),
                    new KeyOptionDisplay(".", "Keypad ."))
            }));
        KeyCategories.Add(new KeyCategoryDisplay(
            "function-keys",
            "Function Keys:F1-F24",
            "기능 키:F1-F24",
            Enumerable.Range(0, 4)
                .Select(row => new KeyRowDisplay(
                    Enumerable.Range((row * 6) + 1, 6)
                        .Select(index => new KeyOptionDisplay($"F{index}", $"F{index}"))
                        .ToArray()))));
        KeyCategories.Add(new KeyCategoryDisplay(
            "number-symbol",
            "Number and Symbol Keys",
            "숫자/기호 키",
            new[]
            {
                KeyRow("`", "1", "2", "3", "4", "5", "6"),
                KeyRow("7", "8", "9", "0", "-", "=", "["),
                KeyRow("]", "\\", ";", "'", ",", ".", "/")
            }));
        KeyCategories.Add(new KeyCategoryDisplay(
            "shift-symbol",
            "Shift Symbols",
            "Shift 기호 키",
            new[]
            {
                ShiftSymbolRow("~!@#$%^", "`", "1", "2", "3", "4", "5", "6"),
                ShiftSymbolRow("&*()_+{", "7", "8", "9", "0", "-", "=", "["),
                ShiftSymbolRow("}|:\"<>?", "]", "\\", ";", "'", ",", ".", "/")
            }));
        KeyCategories.Add(new KeyCategoryDisplay(
            "others",
            "Others",
            "기타",
            new[]
            {
                KeyRow("Print Screen", "Scroll Lock", "Pause", "Insert", "Home", "Page Up"),
                KeyRow("Ctrl", "Shift", "Win", "Caps Lock", "Delete", "End", "Page Down"),
                KeyRow("Enter", "Alt", "Esc", "Up"),
                KeyRow(
                    new KeyOptionDisplay("Tab", "Tab"),
                    new KeyOptionDisplay("Back", "Backspace"),
                    new KeyOptionDisplay("Left", "Left"),
                    new KeyOptionDisplay("Down", "Down"),
                    new KeyOptionDisplay("Right", "Right")),
                KeyRow(
                    new KeyOptionDisplay("Space", "Space"),
                    new KeyOptionDisplay("Null", "None"))
            }));

        foreach (KeyCategoryDisplay category in KeyCategories)
        {
            category.SetLanguage(isEnglish);
        }

        SelectedKeyCategory = KeyCategories.FirstOrDefault();
    }

    private static KeyRowDisplay KeyRow(params string[] actions) =>
        new(actions.Select(action => new KeyOptionDisplay(action, action)).ToArray());

    private static KeyRowDisplay KeyRow(params KeyOptionDisplay[] options) => new(options);

    private static KeyRowDisplay ShiftSymbolRow(string labels, params string[] baseKeys) =>
        new(labels.Select((label, index) =>
            new KeyOptionDisplay(label.ToString(), $"Shift+{baseKeys[index]}")).ToArray());

    private void RefreshAvailableKeyOptions()
    {
        AvailableKeyOptions.Clear();
        if (SelectedKeyCategory is null)
        {
            return;
        }

        foreach (KeyOptionDisplay option in SelectedKeyCategory.Options)
        {
            AvailableKeyOptions.Add(option);
        }
    }

    private void RefreshRawConfigValues(MicroConfigSnapshot snapshot)
    {
        IsSleepDisabled = snapshot.DisableSleep;
        OnPropertyChanged(nameof(IsSleepSettingSupported));
        OnPropertyChanged(nameof(SleepSettingStatusText));
        RawConfigValues.Clear();
        foreach (MicroKeyValue value in snapshot.KeyboardValues.Take(24))
        {
            RawConfigValues.Add(value);
        }
    }

    private void ApplyProfile(DeviceProfileDisplay profile)
    {
        if (!profile.IsLoaded)
        {
            Status = string.Format(Text.ProfileNotLoaded, profile.Name);
            return;
        }

        foreach (ButtonMappingDisplay mapping in LeftMappings.Concat(RightMappings))
        {
            if (profile.ActionsByButtonId.TryGetValue(mapping.Id, out string? action))
            {
                mapping.Action = action;
            }
        }

        Status = string.Format(Text.LoadedProfile, profile.Name);
        RaiseCommandStates();
    }

    private static DeviceProfileDisplay CreateProfileFromSnapshot(string id, string name, MicroConfigSnapshot snapshot)
    {
        var slotByButtonId = new Dictionary<string, int>
        {
            ["A"] = 3,
            ["B"] = 4,
            ["X"] = 5,
            ["Y"] = 6,
            ["L"] = 7,
            ["R"] = 8,
            ["L2"] = 9,
            ["R2"] = 10,
            ["Minus"] = 13,
            ["Plus"] = 14,
            ["Star"] = 15,
            ["Logo"] = 16,
            ["Up"] = 17,
            ["Down"] = 18,
            ["Left"] = 19,
            ["Right"] = 20
        };

        Dictionary<string, string> actions = slotByButtonId
            .Where(pair => pair.Value < snapshot.KeyboardValues.Count)
            .ToDictionary(
                pair => pair.Key,
                pair => snapshot.KeyboardValues[pair.Value].DisplayText);

        return new DeviceProfileDisplay(id, name, actions);
    }

    private void LoadProfileLibrary()
    {
        ProfileLibrary library = ProfileStore.LoadLibraryAsync(_profileLibraryPath, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        foreach (MappingProfile profile in library.Profiles)
        {
            DeviceProfiles.Add(CreateProfileDisplay(profile));
        }

        SelectedProfile = DeviceProfiles.FirstOrDefault(profile => profile.Id == library.SelectedProfileId)
            ?? DeviceProfiles.FirstOrDefault();
        if (SelectedProfile is not null)
        {
            ApplyProfile(SelectedProfile);
        }
    }

    private async Task SaveProfileLibraryAsync()
    {
        string? selectedId = SelectedProfile?.Id;
        var library = new ProfileLibrary(
            1,
            selectedId,
            DeviceProfiles.Where(profile => profile.IsLoaded).Select(CreateMappingProfile).ToArray());

        await ProfileStore.SaveLibraryAsync(library, _profileLibraryPath, CancellationToken.None);
    }

    private void UpsertProfile(DeviceProfileDisplay profile)
    {
        DeviceProfileDisplay? existing = DeviceProfiles.FirstOrDefault(candidate => candidate.Id == profile.Id);
        if (existing is null)
        {
            DeviceProfiles.Add(profile);
            return;
        }

        int index = DeviceProfiles.IndexOf(existing);
        DeviceProfiles[index] = profile;
    }

    private static DeviceProfileDisplay CreateProfileDisplay(MappingProfile profile)
    {
        Dictionary<string, string> actions = profile.Mappings
            .Select(pair => new KeyValuePair<string, string>(ButtonIdFromMicroButton(pair.Key), DisplayMappedAction(pair.Value)))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        return new DeviceProfileDisplay(profile.Id, profile.Name, actions);
    }

    private static MappingProfile CreateMappingProfile(DeviceProfileDisplay profile)
    {
        Dictionary<MicroButton, MappedAction> mappings = profile.ActionsByButtonId
            .Select(pair => new KeyValuePair<MicroButton?, MappedAction>(MicroButtonFromButtonId(pair.Key), ParseMappedAction(pair.Value)))
            .Where(pair => pair.Key is not null)
            .ToDictionary(pair => pair.Key!.Value, pair => pair.Value);

        return new MappingProfile(profile.Id, profile.Name, 1, mappings);
    }

    private static string DisplayMappedAction(MappedAction action)
    {
        return action switch
        {
            MappedAction.KeyboardKey keyboardKey => keyboardKey.Key,
            MappedAction.KeyChord keyChord => string.Join("+", keyChord.Keys),
            MappedAction.MouseAction mouseAction => mouseAction.Action,
            MappedAction.MediaKey mediaKey => mediaKey.Key,
            MappedAction.Macro macro => macro.MacroId,
            MappedAction.Disabled => "Disabled",
            MappedAction.PassThrough => "PassThrough",
            _ => string.Empty
        };
    }

    private static MappedAction ParseMappedAction(string action)
    {
        string trimmed = action.Trim();
        if (trimmed.Contains('+', StringComparison.Ordinal))
        {
            return new MappedAction.KeyChord(trimmed.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return new MappedAction.KeyboardKey(trimmed);
    }

    private static MicroButton? MicroButtonFromButtonId(string id)
    {
        return id switch
        {
            "Logo" => MicroButton.Home,
            _ => Enum.TryParse(id, out MicroButton button) ? button : null
        };
    }

    private static string ButtonIdFromMicroButton(MicroButton button)
    {
        return button == MicroButton.Home ? "Logo" : button.ToString();
    }

    private static string CreateProfileId()
    {
        return $"profile-{Guid.NewGuid():N}";
    }

    private static string GetDefaultProfileLibraryPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MicroKeyStudio",
            "profiles.json");
    }

    private static string GetDefaultBackupDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MicroKeyStudio",
            "backups");
    }

    private string WriteLoadDiagnostics(
        MicroBleDevice device,
        IReadOnlyList<byte[]> notifications,
        IReadOnlyList<byte[]> configPages,
        Exception? exception = null)
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "MicroKeyStudio");
        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, "last-load-diagnostics.txt");
        IEnumerable<string> lines = new[]
        {
            $"Time: {DateTimeOffset.Now:O}",
            $"DeviceName: {device.Name}",
            $"ConfigPages: {configPages.Count}/4",
            $"Notifications: {notifications.Count}",
            $"ProfileCount: {DeviceProfiles.Count}",
            $"DecodedProfileCount: {DeviceProfiles.Count(profile => profile.IsLoaded)}",
            $"SelectedProfile: {SelectedProfile?.Name ?? "(none)"}",
            $"Error: {exception?.Message ?? "(none)"}",
            "NotificationsHex:"
        }.Concat(notifications.Select(packet => Convert.ToHexString(packet)));

        File.WriteAllLines(path, lines);
        return path;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public async ValueTask DisposeAsync()
    {
        await _transport.DisposeAsync();
    }

    private sealed class IncompleteMicroConfigException : Exception
    {
        public IncompleteMicroConfigException()
            : base("The device did not return a complete four-page Micro configuration.")
        {
        }
    }
}

public sealed class ButtonMappingDisplay : INotifyPropertyChanged
{
    private string _action;
    private bool _isSelected;
    private string _button;
    private string _description;

    public ButtonMappingDisplay(
        string id,
        string koreanButton,
        string englishButton,
        string action,
        string englishDescription,
        string koreanDescription,
        int sourceSlot)
    {
        Id = id;
        KoreanButton = koreanButton;
        EnglishButton = englishButton;
        _action = action;
        EnglishDescription = englishDescription;
        KoreanDescription = koreanDescription;
        SourceSlot = sourceSlot;
        _button = koreanButton;
        _description = koreanDescription;
    }

    public string Id { get; }

    public string KoreanButton { get; }

    public string EnglishButton { get; }

    public int SourceSlot { get; }

    public string SourceSlotText => $"Slot {SourceSlot:00}";

    public string Button
    {
        get => _button;
        private set
        {
            if (_button == value)
            {
                return;
            }

            _button = value;
            OnPropertyChanged();
        }
    }

    public string Action
    {
        get => _action;
        set
        {
            if (_action == value)
            {
                return;
            }

            _action = value;
            OnPropertyChanged();
        }
    }

    public string EnglishDescription { get; }

    public string KoreanDescription { get; }

    public string Description
    {
        get => _description;
        private set
        {
            if (_description == value)
            {
                return;
            }

            _description = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetLanguage(bool isEnglish)
    {
        Button = isEnglish ? EnglishButton : KoreanButton;
        Description = isEnglish ? EnglishDescription : KoreanDescription;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class DeviceProfileDisplay : INotifyPropertyChanged
{
    private readonly Dictionary<string, string> _actionsByButtonId;
    private string _name;

    public DeviceProfileDisplay(string name, IReadOnlyDictionary<string, string> actionsByButtonId, bool IsLoaded = true)
        : this($"profile-{Guid.NewGuid():N}", name, actionsByButtonId, IsLoaded)
    {
    }

    public DeviceProfileDisplay(string id, string name, IReadOnlyDictionary<string, string> actionsByButtonId, bool IsLoaded = true)
    {
        Id = id;
        _name = name;
        _actionsByButtonId = new Dictionary<string, string>(actionsByButtonId);
        this.IsLoaded = IsLoaded;
    }

    public string Id { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public IReadOnlyDictionary<string, string> ActionsByButtonId => _actionsByButtonId;

    public bool IsLoaded { get; }

    public string DisplayName => IsLoaded ? Name : $"{Name} (읽기 대기)";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetAction(string buttonId, string action)
    {
        _actionsByButtonId[buttonId] = action;
    }

    public void RemoveAction(string buttonId)
    {
        _actionsByButtonId.Remove(buttonId);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed class KeyCategoryDisplay : INotifyPropertyChanged
{
    private string _displayName;

    public KeyCategoryDisplay(
        string id,
        string englishName,
        string koreanName,
        IEnumerable<KeyRowDisplay> rows)
    {
        Id = id;
        EnglishName = englishName;
        KoreanName = koreanName;
        Rows = rows.ToArray();
        Options = Rows.SelectMany(row => row.Options).ToArray();
        _displayName = koreanName;
    }

    public string Id { get; }

    public string EnglishName { get; }

    public string KoreanName { get; }

    public IReadOnlyList<KeyRowDisplay> Rows { get; }

    public IReadOnlyList<KeyOptionDisplay> Options { get; }

    public string DisplayName
    {
        get => _displayName;
        private set
        {
            if (_displayName == value)
            {
                return;
            }

            _displayName = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetLanguage(bool isEnglish)
    {
        DisplayName = isEnglish ? EnglishName : KoreanName;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public sealed record KeyRowDisplay(IReadOnlyList<KeyOptionDisplay> Options);

public sealed record KeyOptionDisplay(string Label, string Action);

public sealed record UiText(
    string FindDevice,
    string LoadSavedSettings,
    string Disconnect,
    string ProfileAndButtons,
    string SidebarDescription,
    string Profile,
    string ProfileName,
    string CreateProfile,
    string RenameProfile,
    string DeleteProfile,
    string DeviceSettings,
    string DeviceHelp,
    string Settings,
    string Language,
    string SyncSafety,
    string SyncSafetyDescription,
    string ButtonMapping,
    string MappingHelp,
    string DisableSleep,
    string EditMapping,
    string EditMappingDescription,
    string SelectedButton,
    string SelectMapping,
    string Location,
    string NoButtonSelected,
    string Action,
    string KeyPicker,
    string KeyPickerDescription,
    string ApplyMapping,
    string DeviceSlots,
    string FooterText,
    string Reset,
    string Scanning,
    string FoundDevices,
    string LoadingSettings,
    string LoadedConfigPages,
    string SettingsNotReturned,
    string Disconnected,
    string UpdatedMapping,
    string LoadedProfile,
    string ProfileNotLoaded,
    string ProfileCreated,
    string ProfileRenamed,
    string ProfileDeleted,
    string ProfileCount,
    string ProfileCountWithSelection,
    string Back,
    string PhysicalKeyboardCapture,
    string CaptureInstructions,
    string FreeformAction,
    string Cancel,
    string Apply,
    string SaveToDevice,
    string RestoreOriginalSettings,
    string Confirmation,
    string UnsupportedMapping,
    string ReadbackMismatch,
    string VerifiedSuccess)
{
    public static UiText Korean { get; } = new(
        "기기 찾기",
        "프로필 불러오기",
        "접속 끊기",
        "프로필과 버튼",
        "프로필, 기기 불러오기, 언어 설정을 한 화면에서 관리합니다.",
        "프로필",
        "프로필 이름",
        "추가",
        "이름변경",
        "삭제",
        "기기 설정",
        "기기에서 읽은 매핑과 PC 로컬 프로필을 함께 표시합니다. 추가, 이름변경, 삭제는 PC에만 저장됩니다.",
        "설정",
        "언어",
        "동기화 안전",
        "기기 쓰기는 실험 기능입니다. 먼저 저장 설정을 불러온 뒤 사용하세요.",
        "버튼 매핑",
        "매핑 칩을 눌러 해당 버튼을 편집합니다.",
        "슬립 비활성화",
        "매핑 편집",
        "설정된 매핑 버튼을 누르면 여기서 값을 바꿀 수 있습니다.",
        "선택한 버튼",
        "매핑 선택",
        "위치",
        "선택한 버튼 없음",
        "동작",
        "키 선택",
        "공식 앱처럼 카테고리에서 고르거나, 아래 입력칸에 Ctrl+C 같은 동시 키를 직접 입력하세요.",
        "매핑 적용",
        "기기 슬롯",
        "기기 매핑을 저장하면 저장 직전 백업을 만들고 저장 후 전체 설정을 다시 읽어 검증합니다.",
        "불러온 값으로 되돌리기",
        "Micro 기기를 찾는 중...",
        "{0}개 기기를 찾았습니다. 저장 설정을 불러올 기기를 선택하세요.",
        "{0}에서 저장 설정을 불러오는 중...",
        "{1}에서 설정 페이지 {0}개로 기기 저장 프로필을 불러왔습니다. 이름은 아직 임시 이름입니다.",
        "{0}에 연결했지만 저장 설정 응답이 돌아오지 않았습니다.",
        "접속을 끊었습니다.",
        "{0}을(를) {1}(으)로 변경했습니다.",
        "{0} 프로필을 화면에 불러왔습니다.",
        "{0} 프로필은 아직 읽지 못했습니다. 공식 앱 로그에서 프로필별 읽기 패킷을 확인해야 합니다.",
        "{0} 프로필을 만들었습니다.",
        "{0}(으)로 이름을 변경했습니다.",
        "{0} 프로필을 삭제했습니다.",
        "프로필 {0}개",
        "프로필 {0}개: {1}",
        "뒤로",
        "실제 키보드 입력",
        "아래 영역을 누른 뒤 원하는 키 또는 키 조합을 누르세요.",
        "자유 입력 동작",
        "취소",
        "PC 프로필에 적용",
        "기기에 저장",
        "최근 백업 복원",
        "확인",
        "지원하지 않는 매핑",
        "다시 읽은 값이 일치하지 않습니다",
        "검증 완료")
    {
        AutoLoadingOnlyDevice = "{0} 기기를 찾아 프로필 목록을 불러옵니다."
    };

    public static UiText English { get; } = new(
        "Find device",
        "Load profile",
        "Disconnect",
        "Profile and Buttons",
        "Manage profiles, device loading, and language settings in one view.",
        "Profile",
        "Profile name",
        "Add",
        "Rename",
        "Delete",
        "Device Settings",
        "Shows the device-read mapping and local PC profiles together. Add, rename, and delete affect only the PC.",
        "Settings",
        "Language",
        "Sync Safety",
        "Device writes are experimental. Load saved settings first.",
        "Button Mapping",
        "Click a mapping chip to edit that button.",
        "Disable Sleep",
        "Edit Mapping",
        "Click a mapping button to change its value here.",
        "Selected button",
        "Select a mapping",
        "Location",
        "No button selected",
        "Action",
        "Key picker",
        "Choose from official-app-style categories, or type a chord such as Ctrl+C in the field below.",
        "Apply mapping",
        "Device Slots",
        "Saving device mappings creates a pre-save backup and verifies the complete settings with a fresh readback.",
        "Reset changes",
        "Scanning for Micro devices...",
        "Found {0} device(s). Select one to load saved settings.",
        "Loading saved settings from {0}...",
        "Loaded the device-stored profile from {0} config page(s) on {1}. The name is temporary for now.",
        "Connected to {0}, but saved settings were not returned.",
        "Disconnected.",
        "Updated {0} to {1}.",
        "Loaded {0} profile to the screen.",
        "{0} has not been read yet. Profile-specific read packets still need to be identified from official app logs.",
        "Created {0} profile.",
        "Renamed profile to {0}.",
        "Deleted {0} profile.",
        "{0} profile(s)",
        "{0} profile(s): {1}",
        "Back",
        "Physical keyboard capture",
        "Click below, then press a key or key chord.",
        "Freeform action",
        "Cancel",
        "Apply to PC profile",
        "Save to device",
        "Restore latest backup",
        "Confirmation",
        "Unsupported mapping",
        "Readback mismatch",
        "Verified success")
    {
        AutoLoadingOnlyDevice = "Found {0}; loading its profile list."
    };

    public string AutoLoadingOnlyDevice { get; init; } = "Found one device; loading saved settings automatically.";
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Predicate<T?>? _canExecute;

    public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke((T?)parameter) ?? true;
    }

    public void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _execute((T?)parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
