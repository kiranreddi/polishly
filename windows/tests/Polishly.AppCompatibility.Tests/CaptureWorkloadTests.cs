using Polishly.Core.Capabilities;
using Polishly.Core.Models;
using Polishly.WindowsIntegration.Capture;
using Xunit;

namespace Polishly.AppCompatibility.Tests;

public class CaptureWorkloadTests
{
    private readonly WindowTracker _tracker = new();
    private readonly AppCapabilityRules _rules = new();

    [Fact]
    public async Task CaptureSelectionAsync_ProducesValidSelectionContext()
    {
        var captureEngine = new UIAutomationCapture(_tracker, _rules);

        var context = await captureEngine.CaptureSelectionAsync();

        Assert.NotNull(context);
        Assert.NotNull(context.SelectedText);
        Assert.NotNull(context.TargetContext);
        Assert.False(context.IsEmpty);
    }
}
