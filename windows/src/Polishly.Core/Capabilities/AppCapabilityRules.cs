namespace Polishly.Core.Capabilities;

public class AppCapabilityRules
{
    private static readonly Dictionary<string, AppProfile> KnownProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["notepad"] = new AppProfile(
            ProcessName: "notepad",
            DisplayName: "Windows Notepad",
            PreferredCapture: CaptureMethod.UIAutomationTextPattern,
            PreferredInjection: InjectionMethod.UIAutomationSetText,
            AutomaticTriggerSupported: true,
            SelectionBoundsSupported: true,
            RequireClipboardFallback: false
        ),
        ["ms-teams"] = new AppProfile(
            ProcessName: "ms-teams",
            DisplayName: "Microsoft Teams",
            PreferredCapture: CaptureMethod.GuardedClipboard,
            PreferredInjection: InjectionMethod.GuardedPasteTransaction,
            AutomaticTriggerSupported: false,
            SelectionBoundsSupported: false,
            RequireClipboardFallback: true
        ),
        ["outlook"] = new AppProfile(
            ProcessName: "outlook",
            DisplayName: "Outlook Desktop",
            PreferredCapture: CaptureMethod.UIAutomationTextPattern,
            PreferredInjection: InjectionMethod.GuardedPasteTransaction,
            AutomaticTriggerSupported: true,
            SelectionBoundsSupported: true,
            RequireClipboardFallback: true
        ),
        ["winword"] = new AppProfile(
            ProcessName: "winword",
            DisplayName: "Microsoft Word",
            PreferredCapture: CaptureMethod.UIAutomationTextPattern,
            PreferredInjection: InjectionMethod.UIAutomationSetText,
            AutomaticTriggerSupported: true,
            SelectionBoundsSupported: true,
            RequireClipboardFallback: false
        ),
        ["slack"] = new AppProfile(
            ProcessName: "slack",
            DisplayName: "Slack",
            PreferredCapture: CaptureMethod.GuardedClipboard,
            PreferredInjection: InjectionMethod.GuardedPasteTransaction,
            AutomaticTriggerSupported: false,
            SelectionBoundsSupported: false,
            RequireClipboardFallback: true
        ),
        ["chrome"] = new AppProfile(
            ProcessName: "chrome",
            DisplayName: "Google Chrome",
            PreferredCapture: CaptureMethod.UIAutomationTextPattern,
            PreferredInjection: InjectionMethod.GuardedPasteTransaction,
            AutomaticTriggerSupported: false,
            SelectionBoundsSupported: true,
            RequireClipboardFallback: true
        ),
        ["msedge"] = new AppProfile(
            ProcessName: "msedge",
            DisplayName: "Microsoft Edge",
            PreferredCapture: CaptureMethod.UIAutomationTextPattern,
            PreferredInjection: InjectionMethod.GuardedPasteTransaction,
            AutomaticTriggerSupported: false,
            SelectionBoundsSupported: true,
            RequireClipboardFallback: true
        ),
        ["code"] = new AppProfile(
            ProcessName: "code",
            DisplayName: "Visual Studio Code",
            PreferredCapture: CaptureMethod.GuardedClipboard,
            PreferredInjection: InjectionMethod.GuardedPasteTransaction,
            AutomaticTriggerSupported: false,
            SelectionBoundsSupported: false,
            RequireClipboardFallback: true
        )
    };

    public static AppProfile DefaultProfile(string processName) => new(
        ProcessName: processName.ToLowerInvariant(),
        DisplayName: processName,
        PreferredCapture: CaptureMethod.GuardedClipboard,
        PreferredInjection: InjectionMethod.GuardedPasteTransaction,
        AutomaticTriggerSupported: false,
        SelectionBoundsSupported: false,
        RequireClipboardFallback: true
    );

    public AppProfile GetProfile(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return DefaultProfile("generic");
        }

        var normalizedName = processName.ToLowerInvariant().Replace(".exe", "");
        return KnownProfiles.TryGetValue(normalizedName, out var profile)
            ? profile
            : DefaultProfile(normalizedName);
    }
}
