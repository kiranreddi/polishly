using Microsoft.Win32;

namespace Polishly.App.Services;

public sealed class StartupRegistrationService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Polishly";

    public void Apply(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return;

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
                                ?? throw new InvalidOperationException(
                                    "Windows startup settings could not be opened.");
        if (enabled)
        {
            string executable = Environment.ProcessPath
                                ?? throw new InvalidOperationException(
                                    "Polishly executable path is unavailable.");
            key.SetValue(ValueName, $"\"{executable}\" --startup", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) is string value &&
               !string.IsNullOrWhiteSpace(value);
    }
}
