namespace Polishly.Core.Models;

public class AppSettings
{
    public string ActiveProviderId { get; set; } = "demo";
    public string Theme { get; set; } = "System";
    public string HotkeyShortcut { get; set; } = "Ctrl+Shift+P";
    public bool LaunchAtStartup { get; set; } = false;
    public bool AutoTriggerEnabled { get; set; } = false;
    public Dictionary<string, string> ProviderPreferences { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(ActiveProviderId)) return false;
        if (string.IsNullOrWhiteSpace(HotkeyShortcut)) return false;
        if (Theme != "Light" && Theme != "Dark" && Theme != "System") return false;
        return true;
    }
}
