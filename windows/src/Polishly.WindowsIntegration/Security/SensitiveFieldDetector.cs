using Polishly.Core.Models;

namespace Polishly.WindowsIntegration.Security;

public class SensitiveFieldDetector : ISensitiveFieldDetector
{
    private static readonly HashSet<string> SensitiveProcessBlocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "1password", "keepass", "keepassxc", "bitwarden", "dashlane", "lastpass", "cmd", "powershell"
    };

    public SensitiveFieldStatus IsSensitiveField(TargetWindow window, object? automationElement = null)
    {
        if (window.IsElevated)
        {
            return new SensitiveFieldStatus(true, "Target application process is running elevated.");
        }

        if (SensitiveProcessBlocklist.Contains(window.ProcessName))
        {
            return new SensitiveFieldStatus(true, $"Process '{window.ProcessName}' is in the sensitive application blocklist.");
        }

        if (window.Title.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            window.Title.Contains("Credential", StringComparison.OrdinalIgnoreCase))
        {
            return new SensitiveFieldStatus(true, "Window title indicates sensitive or password field.");
        }

        return SensitiveFieldStatus.Safe;
    }
}
