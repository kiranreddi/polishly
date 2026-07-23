using Polishly.Core.Models;
using Polishly.WindowsIntegration.Security;
using Xunit;

namespace Polishly.Platform.Tests;

public class SensitiveFieldDetectorTests
{
    private readonly SensitiveFieldDetector _detector = new();

    [Theory]
    [InlineData("1password")]
    [InlineData("keepassxc")]
    [InlineData("bitwarden")]
    public void IsSensitiveField_BlocklistedApplications_ReturnsSensitiveStatus(string processName)
    {
        var window = new TargetWindow((IntPtr)1, 10, processName, "Password Manager", false);
        var status = _detector.IsSensitiveField(window);

        Assert.True(status.IsSensitive);
        Assert.Contains("blocklist", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsSensitiveField_ElevatedProcess_ReturnsSensitiveStatus()
    {
        var window = new TargetWindow((IntPtr)2, 20, "notepad", "Admin Notepad", true);
        var status = _detector.IsSensitiveField(window);

        Assert.True(status.IsSensitive);
        Assert.Contains("elevated", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsSensitiveField_NormalApplication_ReturnsSafeStatus()
    {
        var window = new TargetWindow((IntPtr)3, 30, "notepad", "Untitled - Notepad", false);
        var status = _detector.IsSensitiveField(window);

        Assert.False(status.IsSensitive);
    }
}
