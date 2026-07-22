using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Polishly.App.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private string _activeProviderId = "demo";
    private string _apiKey = string.Empty;
    private string _hotkeyShortcut = "Ctrl+Shift+P";
    private string _theme = "System";
    private bool _launchAtStartup = true;
    private string _validationStatus = string.Empty;
    private string _newBlockedAppName = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SettingsSaved;

    public ObservableCollection<string> AvailableProviders { get; } = new()
    {
        "demo", "openai", "anthropic", "groq", "cerebras"
    };

    public ObservableCollection<string> AvailableThemes { get; } = new()
    {
        "System", "Light", "Dark"
    };

    public ObservableCollection<string> BlockedApplications { get; } = new()
    {
        "kdbx.exe", "1password.exe"
    };

    public string ActiveProviderId
    {
        get => _activeProviderId;
        set
        {
            if (_activeProviderId != value)
            {
                _activeProviderId = value ?? "demo";
                OnPropertyChanged();
                ValidateApiKey(_activeProviderId, ApiKey);
            }
        }
    }

    public string ApiKey
    {
        get => _apiKey;
        set
        {
            if (_apiKey != value)
            {
                _apiKey = value ?? string.Empty;
                OnPropertyChanged();
                ValidateApiKey(ActiveProviderId, _apiKey);
            }
        }
    }

    public string HotkeyShortcut
    {
        get => _hotkeyShortcut;
        set
        {
            if (_hotkeyShortcut != value)
            {
                _hotkeyShortcut = value ?? "Ctrl+Shift+P";
                OnPropertyChanged();
            }
        }
    }

    public string Theme
    {
        get => _theme;
        set
        {
            if (_theme != value)
            {
                _theme = value ?? "System";
                OnPropertyChanged();
            }
        }
    }

    public bool LaunchAtStartup
    {
        get => _launchAtStartup;
        set
        {
            if (_launchAtStartup != value)
            {
                _launchAtStartup = value;
                OnPropertyChanged();
            }
        }
    }

    public string ValidationStatus
    {
        get => _validationStatus;
        private set
        {
            if (_validationStatus != value)
            {
                _validationStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsApiKeyValid));
            }
        }
    }

    public bool IsApiKeyValid => ValidationStatus == "Valid" || ActiveProviderId == "demo";

    public string NewBlockedAppName
    {
        get => _newBlockedAppName;
        set
        {
            if (_newBlockedAppName != value)
            {
                _newBlockedAppName = value ?? string.Empty;
                OnPropertyChanged();
                ((RelayCommand)AddBlocklistCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand ValidateApiKeyCommand { get; }
    public ICommand AddBlocklistCommand { get; }
    public ICommand RemoveBlocklistCommand { get; }

    public SettingsViewModel()
    {
        SaveCommand = new RelayCommand(Save);
        ValidateApiKeyCommand = new RelayCommand(() => ValidateApiKey(ActiveProviderId, ApiKey));
        AddBlocklistCommand = new RelayCommand(AddBlockedApplication, CanAddBlockedApplication);
        RemoveBlocklistCommand = new RelayCommand<string>(RemoveBlockedApplication);

        ValidateApiKey(ActiveProviderId, ApiKey);
    }

    public bool ValidateApiKey(string providerId, string apiKey)
    {
        if (providerId == "demo")
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

    public bool CanAddBlockedApplication()
    {
        return !string.IsNullOrWhiteSpace(NewBlockedAppName) && !BlockedApplications.Contains(NewBlockedAppName.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    public void AddBlockedApplication()
    {
        if (!CanAddBlockedApplication()) return;

        string app = NewBlockedAppName.Trim();
        if (!app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            app += ".exe";
        }

        BlockedApplications.Add(app.ToLowerInvariant());
        NewBlockedAppName = string.Empty;
    }

    public void RemoveBlockedApplication(string? appName)
    {
        if (string.IsNullOrEmpty(appName)) return;
        BlockedApplications.Remove(appName);
    }

    public void Save()
    {
        ValidateApiKey(ActiveProviderId, ApiKey);
        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
