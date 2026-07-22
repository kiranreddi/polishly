using Polishly.Core.Models;
using Polishly.WindowsIntegration.Security;
using Xunit;

namespace Polishly.Platform.Tests;

public class SensitiveFieldFeatureTests
{
    private readonly SensitiveFieldDetector _detector = new();

    [Fact]
    public void IsSensitiveField_ElevatedProcess_ReturnsSensitiveStatus()
    {
        var window = new TargetWindow((IntPtr)100, 1, "notepad", "Elevated Notepad", true);
        var status = _detector.IsSensitiveField(window);

        Assert.True(status.IsSensitive);
        Assert.Contains("elevated", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("1password")]
    [InlineData("keepassxc")]
    [InlineData("bitwarden")]
    [InlineData("dashlane")]
    [InlineData("lastpass")]
    public void IsSensitiveField_PasswordManagers_ReturnsSensitiveStatus(string processName)
    {
        var window = new TargetWindow((IntPtr)200, 2, processName, "Password Vault", false);
        var status = _detector.IsSensitiveField(window);

        Assert.True(status.IsSensitive);
        Assert.Contains("blocklist", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("cmd")]
    [InlineData("powershell")]
    public void IsSensitiveField_CliTerminals_ReturnsSensitiveStatus(string processName)
    {
        var window = new TargetWindow((IntPtr)300, 3, processName, "Command Prompt", false);
        var status = _detector.IsSensitiveField(window);

        Assert.True(status.IsSensitive);
        Assert.Contains("blocklist", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Enter Password - App")]
    [InlineData("User Credentials Prompt")]
    public void IsSensitiveField_SensitiveWindowTitles_ReturnsSensitiveStatus(string title)
    {
        var window = new TargetWindow((IntPtr)400, 4, "custom_app", title, false);
        var status = _detector.IsSensitiveField(window);

        Assert.True(status.IsSensitive);
        Assert.Contains("title", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsSensitiveField_NormalApplication_ReturnsSafeStatus()
    {
        var window = new TargetWindow((IntPtr)500, 5, "notepad", "Document1 - Notepad", false);
        var status = _detector.IsSensitiveField(window);

        Assert.False(status.IsSensitive);
        Assert.Equal(SensitiveFieldStatus.Safe.IsSensitive, status.IsSensitive);
    }
}
