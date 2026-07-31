using System.IO;
using System.Text.Json;
using Polishly.Core.Models;

namespace Polishly.App.Services;

public interface IAppSettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

/// <summary>
/// Persists non-secret companion settings locally. API keys remain in Windows
/// Credential Manager and are intentionally never written to this file.
/// </summary>
public sealed class AppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsPath;

    public AppSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Polishly",
            "settings.json"))
    {
    }

    public AppSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            // A damaged or inaccessible settings file should not prevent the
            // tray companion from starting with safe defaults.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
