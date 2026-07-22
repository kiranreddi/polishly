namespace Polishly.Core.Capabilities;

public enum CaptureMethod
{
    UIAutomationTextPattern,
    UIAutomationSelection,
    GuardedClipboard
}

public enum InjectionMethod
{
    UIAutomationSetText,
    GuardedPasteTransaction,
    CopyToClipboardOnly
}

public record AppProfile(
    string ProcessName,
    string DisplayName,
    CaptureMethod PreferredCapture,
    InjectionMethod PreferredInjection,
    bool AutomaticTriggerSupported,
    bool SelectionBoundsSupported,
    bool RequireClipboardFallback
);
