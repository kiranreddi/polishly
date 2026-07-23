using Xunit;

namespace Polishly.Core.Tests;

public class AppCapabilityRulesTests
{
    private readonly AppCapabilityRules _rules = new();

    [Theory]
    [InlineData("notepad", "notepad")]
    [InlineData("NOTEPAD.EXE", "notepad")]
    [InlineData("  teams.exe  ", "teams")]
    [InlineData("ms-teams", "ms-teams")]
    [InlineData("OUTLOOK.EXE", "outlook")]
    [InlineData("winword.exe", "winword")]
    [InlineData("Slack.exe", "slack")]
    [InlineData("chrome.exe", "chrome")]
    [InlineData("msedge.exe", "msedge")]
    [InlineData("Code.exe", "code")]
    [InlineData("onenote.exe", "onenote")]
    public void GetProfile_KnownApps_ReturnsConfiguredProfile(string inputName, string expectedProcessName)
    {
        var profile = _rules.GetProfile(inputName);

        Assert.NotNull(profile);
        Assert.Equal(expectedProcessName, profile.ProcessName);
        Assert.NotEmpty(profile.TargetCategory);
        Assert.NotEmpty(profile.Notes);
    }

    [Theory]
    [InlineData("unknown_app.exe")]
    [InlineData("custom_editor")]
    [InlineData((string?)null)]
    [InlineData("")]
    public void GetProfile_UnknownApps_ReturnsConservativeDefaultProfile(string? inputName)
    {
        var profile = _rules.GetProfile(inputName);

        Assert.NotNull(profile);
        Assert.False(profile.SupportsUIAutomation);
        Assert.True(profile.RequiresClipboardFallback);
        Assert.False(profile.SupportsAutoTrigger);
        Assert.Equal("Generic", profile.TargetCategory);
    }

    [Fact]
    public void SensitiveApp_Outlook_IsMarkedSensitive()
    {
        var profile = _rules.GetProfile("outlook.exe");
        Assert.True(profile.IsSensitive);
        Assert.Equal("Email", profile.TargetCategory);
    }

    [Fact]
    public void NormalizeProcessName_StripsExtensionAndTrims()
    {
        Assert.Equal("notepad", AppCapabilityRules.NormalizeProcessName("  Notepad.EXE  "));
        Assert.Equal("code", AppCapabilityRules.NormalizeProcessName("code.exe"));
        Assert.Equal(string.Empty, AppCapabilityRules.NormalizeProcessName(null));
    }
}
