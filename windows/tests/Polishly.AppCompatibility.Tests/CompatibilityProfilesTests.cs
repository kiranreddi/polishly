using Polishly.Core.Capabilities;
using Xunit;

namespace Polishly.AppCompatibility.Tests;

public class CompatibilityProfilesTests
{
    private readonly AppCapabilityRules _capabilityRules = new();

    [Theory]
    [InlineData("notepad", true)]
    [InlineData("ms-teams", false)]
    [InlineData("slack", false)]
    [InlineData("code", false)]
    public void CompatibilityProfiles_VerifyAutomaticTriggerSupport(string processName, bool expectedAutomaticSupport)
    {
        var profile = _capabilityRules.GetProfile(processName);
        Assert.Equal(expectedAutomaticSupport, profile.AutomaticTriggerSupported);
    }

    [Theory]
    [InlineData("ms-teams")]
    [InlineData("slack")]
    [InlineData("chrome")]
    public void CompatibilityProfiles_VerifyClipboardFallbackRequired(string processName)
    {
        var profile = _capabilityRules.GetProfile(processName);
        Assert.True(profile.RequireClipboardFallback);
    }
}
