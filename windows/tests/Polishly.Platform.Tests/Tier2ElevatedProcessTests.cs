using Polishly.Core.Models;
using Polishly.WindowsIntegration.Clipboard;
using Polishly.WindowsIntegration.Security;
using Xunit;

namespace Polishly.Platform.Tests;

public class Tier2ElevatedProcessTests
{
    private readonly SensitiveFieldDetector _sensitiveDetector = new();

    [Fact]
    public void SensitiveFieldDetector_ElevatedWindow_ReturnsSensitiveTrue()
    {
        var window = new TargetWindow((IntPtr)900, 99, "cmd", "Administrator: Command Prompt", IsElevated: true);
        var status = _sensitiveDetector.IsSensitiveField(window);

        Assert.True(status.IsSensitive);
        Assert.Contains("elevated", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuardedClipboardTransaction_ElevatedTargetWindow_BlocksPasteAndTriggersCopyFallback()
    {
        var transaction = new GuardedClipboardTransaction();
        var elevatedTarget = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 1000,
            ProcessName: "notepad",
            AppTitle: "Administrator: Notepad",
            FieldId: "f1",
            IsPassword: false,
            IsElevated: true
        );

        var result = await transaction.ExecuteSafePasteAsync("Polished text", elevatedTarget);

        Assert.False(result.Success);
        Assert.True(result.FallbackToCopy);
        Assert.True(result.RestoredOriginalClipboard);
        Assert.Contains("elevated window", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TargetContext_IsElevatedFlag_PreservedInRecord()
    {
        var context = new TargetContext(
            WindowHandle: (IntPtr)1234,
            ProcessId: 50,
            ProcessName: "taskmgr",
            AppTitle: "Task Manager",
            FieldId: "f2",
            IsPassword: false,
            IsElevated: true
        );

        Assert.True(context.IsElevated);
    }

    [Fact]
    public async Task GuardedClipboardTransaction_ElevatedError_ExplainsSecurityRestriction()
    {
        var transaction = new GuardedClipboardTransaction();
        var elevatedTarget = new TargetContext(IntPtr.Zero, 1, "admin_app", "Admin", "f1", false, true);

        var result = await transaction.ExecuteSafePasteAsync("Text", elevatedTarget);

        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("security restrictions", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuardedClipboardTransaction_NonElevatedNormalTarget_ProceedsWithoutElevationRestriction()
    {
        var transaction = new GuardedClipboardTransaction();
        var normalTarget = new TargetContext(IntPtr.Zero, 1, "notepad", "Notepad", "f1", false, false);

        var result = await transaction.ExecuteSafePasteAsync("Text", normalTarget);

        Assert.True(result.Success);
        Assert.False(result.FallbackToCopy);
    }
}
