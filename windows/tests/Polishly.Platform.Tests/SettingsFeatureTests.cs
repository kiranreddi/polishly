using Polishly.Core.Models;
using Xunit;

namespace Polishly.Platform.Tests;

public class SettingsFeatureTests
{
    [Fact]
    public void AppSettings_DefaultValues_MatchExpectedDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal("demo", settings.ActiveProviderId);
        Assert.Equal("System", settings.Theme);
        Assert.Equal("Ctrl+Shift+P", settings.HotkeyShortcut);
        Assert.False(settings.LaunchAtStartup);
        Assert.False(settings.AutoTriggerEnabled);
        Assert.NotNull(settings.ProviderPreferences);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("System")]
    public void AppSettings_ThemeSetting_UpdatesAndValidates(string theme)
    {
        var settings = new AppSettings { Theme = theme };

        Assert.Equal(theme, settings.Theme);
        Assert.True(settings.IsValid());
    }

    [Fact]
    public void AppSettings_ActiveProviderAndPreferences_StoreCorrectly()
    {
        var settings = new AppSettings
        {
            ActiveProviderId = "openai",
            ProviderPreferences = new Dictionary<string, string>
            {
                ["openai_model"] = "gpt-4o",
                ["anthropic_model"] = "claude-3-5-sonnet"
            }
        };

        Assert.Equal("openai", settings.ActiveProviderId);
        Assert.Equal("gpt-4o", settings.ProviderPreferences["openai_model"]);
        Assert.True(settings.IsValid());
    }

    [Fact]
    public void AppSettings_TogglesStartupAndAutoTrigger()
    {
        var settings = new AppSettings
        {
            LaunchAtStartup = true,
            AutoTriggerEnabled = true
        };

        Assert.True(settings.LaunchAtStartup);
        Assert.True(settings.AutoTriggerEnabled);
        Assert.True(settings.IsValid());
    }

    [Theory]
    [InlineData("", "System", "Ctrl+Shift+P", false)]
    [InlineData("openai", "InvalidTheme", "Ctrl+Shift+P", false)]
    [InlineData("openai", "Dark", "", false)]
    [InlineData("openai", "Dark", "Ctrl+Shift+P", true)]
    public void AppSettings_IsValid_ValidatesRequiredFields(
        string providerId, string theme, string hotkey, bool expectedValid)
    {
        var settings = new AppSettings
        {
            ActiveProviderId = providerId,
            Theme = theme,
            HotkeyShortcut = hotkey
        };

        Assert.Equal(expectedValid, settings.IsValid());
    }
}
