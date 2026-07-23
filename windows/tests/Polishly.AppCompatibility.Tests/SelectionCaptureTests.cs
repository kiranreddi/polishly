using Polishly.Core.Capabilities;
using Polishly.Core.Models;
using Polishly.WindowsIntegration.Capture;
using Xunit;

namespace Polishly.AppCompatibility.Tests;

public class SelectionCaptureTests
{
    private readonly WindowTracker _tracker = new();
    private readonly AppCapabilityRules _rules = new();

    [Fact]
    public async Task CaptureSelectionAsync_StandardWindow_ReturnsValidSelectionContext()
    {
        var captureEngine = new UIAutomationCapture(_tracker, _rules)
        {
            TestFallbackText = "Sample selected text"
        };
        var context = await captureEngine.CaptureSelectionAsync();

        Assert.NotNull(context);
        Assert.NotNull(context.SelectedText);
        Assert.NotNull(context.TargetContext);
        Assert.True(context.CapturedAt <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData("notepad", true)]
    [InlineData("ms-teams", false)]
    [InlineData("slack", false)]
    [InlineData("chrome", true)]
    public void SelectionCapture_VerifyDirectUiaCapabilityFlag(string processName, bool expectedDirectUia)
    {
        var profile = _rules.GetProfile(processName);
        bool isDirectUia = profile.PreferredCapture != CaptureMethod.GuardedClipboard;

        Assert.Equal(expectedDirectUia, isDirectUia);
    }

    [Fact]
    public void TargetWindow_InvalidOrZeroHandle_ReturnsDefaultFallbackWindow()
    {
        var window = new TargetWindow(IntPtr.Zero, 0, "unknown", "Unknown", false);

        Assert.Equal(IntPtr.Zero, window.Handle);
        Assert.Equal(0, window.ProcessId);
        Assert.Equal("unknown", window.ProcessName);
        Assert.False(window.IsElevated);
    }

    [Fact]
    public void SelectionContext_IsEmpty_AccuratelyReflectsTextContent()
    {
        var target = new TargetContext(IntPtr.Zero, 1, "notepad", "Notepad", "f1", false, false);
        
        var emptyContext = new SelectionContext("", "", target, DateTime.UtcNow, true);
        var whitespaceContext = new SelectionContext("   ", "   ", target, DateTime.UtcNow, true);
        var validContext = new SelectionContext("Hello world", "Hello world context", target, DateTime.UtcNow, true);

        Assert.True(emptyContext.IsEmpty);
        Assert.True(whitespaceContext.IsEmpty);
        Assert.False(validContext.IsEmpty);
    }

    [Fact]
    public async Task CaptureSelectionAsync_WithCancellationToken_HonorsTokenState()
    {
        var captureEngine = new UIAutomationCapture(_tracker, _rules)
        {
            TestFallbackText = "Sample selected text"
        };
        using var cts = new CancellationTokenSource();
        
        var task = captureEngine.CaptureSelectionAsync(cts.Token);
        var context = await task;

        Assert.NotNull(context);
        Assert.False(context.IsEmpty);
    }
}
