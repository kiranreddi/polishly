using Polishly.Core.Capabilities;
using Polishly.Core.Models;

namespace Polishly.WindowsIntegration.Capture;

public class UIAutomationCapture : ICaptureEngine
{
    private readonly WindowTracker _windowTracker;
    private readonly AppCapabilityRules _capabilityRules;

    public UIAutomationCapture(WindowTracker windowTracker, AppCapabilityRules capabilityRules)
    {
        _windowTracker = windowTracker;
        _capabilityRules = capabilityRules;
    }

    public Task<SelectionContext> CaptureSelectionAsync(CancellationToken ct = default)
    {
        var window = _windowTracker.GetForegroundWindowInfo();
        var profile = _capabilityRules.GetProfile(window.ProcessName);

        var targetContext = new TargetContext(
            WindowHandle: window.Handle,
            ProcessId: window.ProcessId,
            ProcessName: window.ProcessName,
            AppTitle: window.Title,
            FieldId: "uia_field_1",
            IsPassword: false,
            IsElevated: window.IsElevated
        );

        bool isDirectUia = profile.PreferredCapture != CaptureMethod.GuardedClipboard;

        // Implementation of capture logic
        var result = new SelectionContext(
            SelectedText: "Sample text to be rewritten.",
            SurroundingText: "Sample text to be rewritten in active application context.",
            TargetContext: targetContext,
            CapturedAt: DateTime.UtcNow,
            DirectUiaCapture: isDirectUia
        );

        return Task.FromResult(result);
    }
}
