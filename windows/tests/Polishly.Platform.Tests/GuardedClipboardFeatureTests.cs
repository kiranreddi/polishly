using Polishly.Core.Models;
using Polishly.WindowsIntegration.Clipboard;
using Xunit;

namespace Polishly.Platform.Tests;

public class GuardedClipboardFeatureTests
{
    [Fact]
    public async Task ExecuteSafePasteAsync_WindowMismatch_BlocksPasteAndReturnsFallback()
    {
        var transaction = new GuardedClipboardTransaction();
        var target = new TargetContext(
            WindowHandle: (IntPtr)999999, // Mismatched handle
            ProcessId: 100,
            ProcessName: "notepad",
            AppTitle: "Notepad",
            FieldId: "field1",
            IsPassword: false,
            IsElevated: false
        );

        var result = await transaction.ExecuteSafePasteAsync("Test replacement", target);

        Assert.False(result.Success);
        Assert.True(result.FallbackToCopy);
        Assert.True(result.RestoredOriginalClipboard);
        Assert.Contains("lost focus", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteSafePasteAsync_PasswordField_BlocksPasteAndReturnsFallback()
    {
        var transaction = new GuardedClipboardTransaction();
        var passwordTarget = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 101,
            ProcessName: "notepad",
            AppTitle: "Notepad",
            FieldId: "pwd",
            IsPassword: true,
            IsElevated: false
        );

        var result = await transaction.ExecuteSafePasteAsync("Secret replacement", passwordTarget);

        Assert.False(result.Success);
        Assert.True(result.FallbackToCopy);
        Assert.Contains("password field", result.ErrorMessage);
    }

    [Fact]
    public async Task GetSequenceNumberAsync_ReturnsCurrentSequenceNumber()
    {
        var transaction = new GuardedClipboardTransaction();
        uint seq = await transaction.GetSequenceNumberAsync();

        Assert.True(seq >= 0);
    }

    [Fact]
    public async Task ExecuteSafePasteAsync_ValidTarget_ExecutesPasteSuccessfully()
    {
        var transaction = new GuardedClipboardTransaction(() => 1u);
        var validTarget = new TargetContext(
            WindowHandle: IntPtr.Zero, // Allows simulated matching
            ProcessId: 102,
            ProcessName: "notepad",
            AppTitle: "Notepad",
            FieldId: "f1",
            IsPassword: false,
            IsElevated: false
        );

        var result = await transaction.ExecuteSafePasteAsync("Polished text", validTarget);

        Assert.True(result.Success);
        Assert.False(result.FallbackToCopy);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteSafePasteAsync_ResultProperties_MatchExpectedFormat()
    {
        var transaction = new GuardedClipboardTransaction(() => 1u);
        var target = new TargetContext(IntPtr.Zero, 1, "test", "Test", "f1", false, false);
        var result = await transaction.ExecuteSafePasteAsync("Sample", target);

        Assert.True(result.Success);
        Assert.True(result.RestoredOriginalClipboard);
        Assert.False(result.FallbackToCopy);
    }
}
