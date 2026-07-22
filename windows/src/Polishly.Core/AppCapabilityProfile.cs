namespace Polishly.Core;

public record AppCapabilityProfile(
    string ProcessName,
    bool SupportsUIAutomation,
    bool RequiresClipboardFallback,
    bool SupportsSelectionBounds,
    bool SupportsAutoTrigger,
    bool IsSensitive,
    string TargetCategory,
    string Notes
);
