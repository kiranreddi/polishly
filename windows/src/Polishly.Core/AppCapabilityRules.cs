namespace Polishly.Core;

public class AppCapabilityRules
{
    private static readonly Dictionary<string, AppCapabilityProfile> KnownProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["notepad"] = new AppCapabilityProfile(
            ProcessName: "notepad",
            SupportsUIAutomation: true,
            RequiresClipboardFallback: false,
            SupportsSelectionBounds: true,
            SupportsAutoTrigger: true,
            IsSensitive: false,
            TargetCategory: "TextEditor",
            Notes: "Native Windows Notepad with full UI Automation support."
        ),
        ["teams"] = new AppCapabilityProfile(
            ProcessName: "teams",
            SupportsUIAutomation: false,
            RequiresClipboardFallback: true,
            SupportsSelectionBounds: false,
            SupportsAutoTrigger: false,
            IsSensitive: false,
            TargetCategory: "Chat",
            Notes: "Microsoft Teams web/Electron client requiring clipboard fallback."
        ),
        ["ms-teams"] = new AppCapabilityProfile(
            ProcessName: "ms-teams",
            SupportsUIAutomation: false,
            RequiresClipboardFallback: true,
            SupportsSelectionBounds: false,
            SupportsAutoTrigger: false,
            IsSensitive: false,
            TargetCategory: "Chat",
            Notes: "Microsoft Teams (new client) requiring clipboard fallback."
        ),
        ["outlook"] = new AppCapabilityProfile(
            ProcessName: "outlook",
            SupportsUIAutomation: true,
            RequiresClipboardFallback: false,
            SupportsSelectionBounds: true,
            SupportsAutoTrigger: true,
            IsSensitive: true,
            TargetCategory: "Email",
            Notes: "Microsoft Outlook desktop app with rich text support."
        ),
        ["winword"] = new AppCapabilityProfile(
            ProcessName: "winword",
            SupportsUIAutomation: true,
            RequiresClipboardFallback: false,
            SupportsSelectionBounds: true,
            SupportsAutoTrigger: true,
            IsSensitive: false,
            TargetCategory: "DocumentEditor",
            Notes: "Microsoft Word desktop app with full UI Automation and selection bounds."
        ),
        ["slack"] = new AppCapabilityProfile(
            ProcessName: "slack",
            SupportsUIAutomation: false,
            RequiresClipboardFallback: true,
            SupportsSelectionBounds: false,
            SupportsAutoTrigger: false,
            IsSensitive: false,
            TargetCategory: "Chat",
            Notes: "Slack desktop client requiring clipboard fallback."
        ),
        ["chrome"] = new AppCapabilityProfile(
            ProcessName: "chrome",
            SupportsUIAutomation: true,
            RequiresClipboardFallback: true,
            SupportsSelectionBounds: false,
            SupportsAutoTrigger: false,
            IsSensitive: false,
            TargetCategory: "Browser",
            Notes: "Google Chrome browser with partial UI Automation accessibility support."
        ),
        ["msedge"] = new AppCapabilityProfile(
            ProcessName: "msedge",
            SupportsUIAutomation: true,
            RequiresClipboardFallback: true,
            SupportsSelectionBounds: false,
            SupportsAutoTrigger: false,
            IsSensitive: false,
            TargetCategory: "Browser",
            Notes: "Microsoft Edge browser with partial UI Automation support."
        ),
        ["code"] = new AppCapabilityProfile(
            ProcessName: "code",
            SupportsUIAutomation: true,
            RequiresClipboardFallback: false,
            SupportsSelectionBounds: true,
            SupportsAutoTrigger: true,
            IsSensitive: false,
            TargetCategory: "IDE",
            Notes: "Visual Studio Code with custom accessibility bounds support."
        ),
        ["onenote"] = new AppCapabilityProfile(
            ProcessName: "onenote",
            SupportsUIAutomation: true,
            RequiresClipboardFallback: false,
            SupportsSelectionBounds: true,
            SupportsAutoTrigger: true,
            IsSensitive: false,
            TargetCategory: "Notes",
            Notes: "Microsoft OneNote with UI Automation support."
        )
    };

    public AppCapabilityProfile GetProfile(string? processName)
    {
        var normalized = NormalizeProcessName(processName);

        if (!string.IsNullOrEmpty(normalized) && KnownProfiles.TryGetValue(normalized, out var profile))
        {
            return profile;
        }

        return CreateDefaultProfile(normalized);
    }

    public static string NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        var cleaned = processName.Trim();

        if (cleaned.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[..^4];
        }

        return cleaned.ToLowerInvariant();
    }

    private static AppCapabilityProfile CreateDefaultProfile(string processName)
    {
        return new AppCapabilityProfile(
            ProcessName: string.IsNullOrEmpty(processName) ? "unknown" : processName,
            SupportsUIAutomation: false,
            RequiresClipboardFallback: true,
            SupportsSelectionBounds: false,
            SupportsAutoTrigger: false,
            IsSensitive: false,
            TargetCategory: "Generic",
            Notes: "Conservative default profile for unknown or unclassified applications."
        );
    }
}
