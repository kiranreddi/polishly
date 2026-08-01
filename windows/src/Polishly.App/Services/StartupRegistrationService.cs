using Microsoft.Win32;
#if HAS_WPF
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
#endif

namespace Polishly.App.Services;

public sealed class StartupRegistrationService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Polishly";
    private const string StartupTaskId = "PolishlyStartup";
#if HAS_WPF
    private const int AppModelErrorNoPackage = 15700;
#endif

    public async Task ApplyAsync(bool enabled)
    {
        await Task.CompletedTask;
        if (!OperatingSystem.IsWindows()) return;

#if HAS_WPF
        if (IsPackaged())
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            if (!enabled)
            {
                if (startupTask.State is StartupTaskState.Enabled or
                    StartupTaskState.EnabledByPolicy)
                {
                    startupTask.Disable();
                }
                return;
            }

            if (startupTask.State == StartupTaskState.DisabledByUser)
            {
                throw new InvalidOperationException(
                    "Windows previously disabled Polishly at startup. Re-enable it in Settings > Apps > Startup.");
            }
            if (startupTask.State == StartupTaskState.DisabledByPolicy)
            {
                throw new InvalidOperationException(
                    "Your Windows policy prevents Polishly from starting at sign-in.");
            }
            if (startupTask.State != StartupTaskState.Enabled &&
                startupTask.State != StartupTaskState.EnabledByPolicy)
            {
                StartupTaskState result = await startupTask.RequestEnableAsync();
                if (result != StartupTaskState.Enabled &&
                    result != StartupTaskState.EnabledByPolicy)
                {
                    throw new InvalidOperationException(
                        "Windows did not enable Polishly at sign-in. Check Settings > Apps > Startup.");
                }
            }
            return;
        }
#endif

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

    public async Task<bool> IsEnabledAsync()
    {
        await Task.CompletedTask;
        if (!OperatingSystem.IsWindows()) return false;
#if HAS_WPF
        if (IsPackaged())
        {
            StartupTask startupTask = await StartupTask.GetAsync(StartupTaskId);
            return startupTask.State is StartupTaskState.Enabled or
                StartupTaskState.EnabledByPolicy;
        }
#endif
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) is string value &&
               !string.IsNullOrWhiteSpace(value);
    }

#if HAS_WPF
    private static bool IsPackaged()
    {
        int length = 0;
        int result = GetCurrentPackageFullName(ref length, null);
        return result != AppModelErrorNoPackage;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(
        ref int packageFullNameLength,
        System.Text.StringBuilder? packageFullName);
#endif
}
