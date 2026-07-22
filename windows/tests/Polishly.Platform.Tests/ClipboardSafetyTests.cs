using Polishly.Core.Models;
using Polishly.WindowsIntegration.Clipboard;
using Xunit;

namespace Polishly.Platform.Tests;

public class ClipboardSafetyTests
{
    private readonly GuardedClipboardTransaction _transaction = new();

    [Fact]
    public async Task ExecuteSafePasteAsync_PasswordFieldTarget_BlocksPasteAndReturnsFallback()
    {
        var passwordTarget = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 100,
            ProcessName: "notepad",
            AppTitle: "Notepad",
            FieldId: "pwd_field",
            IsPassword: true,
            IsElevated: false
        );

        var result = await _transaction.ExecuteSafePasteAsync("Replacement text", passwordTarget);

        Assert.False(result.Success);
        Assert.True(result.FallbackToCopy);
        Assert.Contains("password field", result.ErrorMessage);
    }
}
