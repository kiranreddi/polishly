using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Polishly.App.Services;
using Polishly.Core.Models;
using Polishly.Providers;
using Polishly.WindowsIntegration.Hotkey;
using Polishly.WindowsIntegration.Security;

namespace Polishly.App.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ICredentialStore? _credentialStore;
    private readonly IAppSettingsStore? _settingsStore;
    private readonly AppSettings _baseSettings;
    private string _activeProviderId = "demo";
    private string _activeModel = "local-demo";
    private string _apiKey = string.Empty;
    private string _hotkeyShortcut = "Ctrl+Shift+P";
    private string _theme = "System";
    private bool _launchAtStartup = true;
    private string _validationStatus = "Valid";
    private string _connectionStatus = string.Empty;
    private string _newBlockedAppName = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<AppSettings>? SettingsSaved;

    public ObservableCollection<string> AvailableProviders { get; } =
        new(new[] { "demo", "openai", "anthropic", "groq", "cerebras" });
    public ObservableCollection<string> AvailableModels { get; } = new();
    public ObservableCollection<string> AvailableThemes { get; } =
        new(new[] { "System", "Light", "Dark" });
    public ObservableCollection<string> BlockedApplications { get; } = new();

    public string ActiveProviderId
    {
        get => _activeProviderId;
        set
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "demo" : value.ToLowerInvariant();
            if (_activeProviderId == normalized) return;
            _activeProviderId = normalized;
            RefreshModels();
            OnPropertyChanged();
            _ = LoadApiKeyForProviderAsync(_activeProviderId);
        }
    }

    public string ActiveModel
    {
        get => _activeModel;
        set
        {
            if (_activeModel == value) return;
            _activeModel = value ?? string.Empty;
            OnPropertyChanged();
            ValidateLocalConfiguration();
        }
    }

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (_apiKey == value) return;
            _apiKey = value ?? string.Empty;
            OnPropertyChanged();
            ValidateLocalConfiguration();
        }
    }

    public string HotkeyShortcut
    {
        get => _hotkeyShortcut;
        set
        {
            if (_hotkeyShortcut == value) return;
            _hotkeyShortcut = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string Theme
    {
        get => _theme;
        set
        {
            if (_theme == value) return;
            _theme = value ?? "System";
            OnPropertyChanged();
        }
    }

    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set
        {
            if (_launchAtStartup == value) return;
            _launchAtStartup = value;
            OnPropertyChanged();
        }
    }

    public string ValidationStatus
    {
        get => _validationStatus;
        private set
        {
            if (_validationStatus == value) return;
            _validationStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsApiKeyValid));
        }
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set
        {
            if (_connectionStatus == value) return;
            _connectionStatus = value;
            OnPropertyChanged();
        }
    }

    public bool IsApiKeyValid => ValidationStatus == "Valid";

    public string NewBlockedAppName
    {
        get => _newBlockedAppName;
        set
        {
            if (_newBlockedAppName == value) return;
            _newBlockedAppName = value ?? string.Empty;
            OnPropertyChanged();
            ((RelayCommand)AddBlocklistCommand).RaiseCanExecuteChanged();
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand ValidateApiKeyCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand RemoveApiKeyCommand { get; }
    public ICommand AddBlocklistCommand { get; }
    public ICommand RemoveBlocklistCommand { get; }

    public SettingsViewModel() : this(null, null, null) { }
    public SettingsViewModel(ICredentialStore? credentialStore)
        : this(credentialStore, null, null) { }

    public SettingsViewModel(
        ICredentialStore? credentialStore,
        IAppSettingsStore? settingsStore,
        AppSettings? initialSettings)
    {
        _credentialStore = credentialStore;
        _settingsStore = settingsStore;
        _baseSettings = initialSettings ?? new AppSettings();

        SaveCommand = new RelayCommand(Save);
        ValidateApiKeyCommand = new RelayCommand(() => ValidateLocalConfiguration());
        TestConnectionCommand = new RelayCommand(TestConnection);
        RemoveApiKeyCommand = new RelayCommand(RemoveApiKey);
        AddBlocklistCommand = new RelayCommand(AddBlockedApplication, CanAddBlockedApplication);
        RemoveBlocklistCommand = new RelayCommand<string>(RemoveBlockedApplication);

        Apply(initialSettings);
        RefreshModels(initialSettings);
        _ = LoadApiKeyForProviderAsync(ActiveProviderId);
    }

    public async Task LoadApiKeyForProviderAsync(string providerId)
    {
        _apiKey = string.Empty;
        if (_credentialStore != null && providerId != "demo")
        {
            try
            {
                _apiKey = await _credentialStore.GetApiKeyAsync(providerId) ?? string.Empty;
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"Credential Manager error: {ex.Message}";
            }
        }
        OnPropertyChanged(nameof(ApiKey));
        ValidateLocalConfiguration();
    }

    public bool ValidateApiKey(string providerId, string apiKey)
    {
        if (providerId.Equals("demo", StringComparison.OrdinalIgnoreCase))
        {
            ValidationStatus = "Valid";
            return true;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ValidationStatus = "API key required";
            return false;
        }

        if (apiKey.Length < 8)
        {
            ValidationStatus = "API key too short";
            return false;
        }

        ValidationStatus = "Valid";
        return true;
    }

    public bool CanAddBlockedApplication() =>
        !string.IsNullOrWhiteSpace(NewBlockedAppName) &&
        !BlockedApplications.Contains(
            NormalizeApplication(NewBlockedAppName), StringComparer.OrdinalIgnoreCase);

    public void AddBlockedApplication()
    {
        if (!CanAddBlockedApplication()) return;
        BlockedApplications.Add(NormalizeApplication(NewBlockedAppName));
        NewBlockedAppName = string.Empty;
    }

    public void RemoveBlockedApplication(string? appName)
    {
        if (!string.IsNullOrWhiteSpace(appName))
        {
            BlockedApplications.Remove(appName);
        }
    }

    public AppSettings BuildSettings()
    {
        var settings = new AppSettings
        {
            ActiveProviderId = ActiveProviderId,
            Theme = Theme,
            HotkeyShortcut = HotkeyShortcut,
            LaunchAtStartup = LaunchAtStartup,
            AutoTriggerEnabled = _baseSettings.AutoTriggerEnabled,
            OnboardingCompleted = _baseSettings.OnboardingCompleted,
            BlockedApplications = BlockedApplications
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
        foreach (var preference in _baseSettings.ProviderPreferences)
        {
            settings.ProviderPreferences[preference.Key] = preference.Value;
        }
        settings.ProviderPreferences[ActiveProviderId] = ActiveModel;
        return settings;
    }

    public async void Save()
    {
        if (!ValidateLocalConfiguration()) return;
        if (!HotkeyShortcutParser.TryParse(HotkeyShortcut, out _, out string? hotkeyError))
        {
            ValidationStatus = hotkeyError ?? "Invalid hotkey";
            return;
        }

        try
        {
            if (_credentialStore != null && ActiveProviderId != "demo" &&
                !string.IsNullOrWhiteSpace(ApiKey))
            {
                await _credentialStore.SaveApiKeyAsync(ActiveProviderId, ApiKey);
            }

            AppSettings settings = BuildSettings();
            if (_settingsStore != null)
            {
                await _settingsStore.SaveAsync(settings);
            }

            SettingsSaved?.Invoke(this, settings);
        }
        catch (Exception ex)
        {
            ValidationStatus = $"Save failed: {ex.Message}";
        }
    }

    private bool ValidateLocalConfiguration()
    {
        if (!ValidateApiKey(ActiveProviderId, ApiKey)) return false;
        if (!ProviderFactory.IsKnownModel(ActiveProviderId, ActiveModel))
        {
            ValidationStatus = "Choose a supported model";
            return false;
        }
        return true;
    }

    private async void TestConnection()
    {
        if (!ValidateLocalConfiguration()) return;
        if (ActiveProviderId == "demo")
        {
            ConnectionStatus = "Demo mode is ready and stays on this PC.";
            return;
        }

        ConnectionStatus = "Testing connection…";
        try
        {
            var provider = ProviderFactory.Create(ActiveProviderId, ApiKey, ActiveModel);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var result = await provider.ValidateCredentialsAsync(ApiKey, timeout.Token);
            ConnectionStatus = result.IsValid
                ? "Connected successfully"
                : result.ErrorMessage ?? "Connection failed";
        }
        catch (OperationCanceledException)
        {
            ConnectionStatus = "Connection test timed out";
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Connection failed: {ex.Message}";
        }
    }

    private async void RemoveApiKey()
    {
        if (_credentialStore == null || ActiveProviderId == "demo") return;
        try
        {
            await _credentialStore.DeleteApiKeyAsync(ActiveProviderId);
            ApiKey = string.Empty;
            ConnectionStatus = "Saved key removed";
        }
        catch (Exception ex)
        {
            ConnectionStatus = $"Key removal failed: {ex.Message}";
        }
    }

    private void Apply(AppSettings? settings)
    {
        if (settings == null)
        {
            foreach (string app in new AppSettings().BlockedApplications)
                BlockedApplications.Add(app);
            return;
        }

        _activeProviderId = settings.ActiveProviderId;
        _hotkeyShortcut = settings.HotkeyShortcut;
        _theme = settings.Theme;
        _launchAtStartup = settings.LaunchAtStartup;
        foreach (string app in settings.BlockedApplications)
            BlockedApplications.Add(NormalizeApplication(app));
    }

    private void RefreshModels(AppSettings? settings = null)
    {
        settings ??= _baseSettings;
        AvailableModels.Clear();
        foreach (string model in ProviderFactory.GetModels(ActiveProviderId))
            AvailableModels.Add(model);

        string? preferred = settings?.ProviderPreferences.GetValueOrDefault(ActiveProviderId);
        _activeModel = ProviderFactory.IsKnownModel(ActiveProviderId, preferred)
            ? preferred!
            : AvailableModels.FirstOrDefault() ?? string.Empty;
        OnPropertyChanged(nameof(ActiveModel));
    }

    private static string NormalizeApplication(string application)
    {
        string normalized = Path.GetFileName(application.Trim()).ToLowerInvariant();
        return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized + ".exe";
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
