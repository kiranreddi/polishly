using Xunit;
using Polishly.App.ViewModels;

namespace Polishly.Platform.Tests;

public class SettingsViewModelTests
{
    [Fact]
    public void SettingsViewModel_Initialization_DefaultValuesSet()
    {
        var vm = new SettingsViewModel();

        Assert.Equal("demo", vm.ActiveProviderId);
        Assert.Equal("Ctrl+Shift+P", vm.HotkeyShortcut);
        Assert.Equal("System", vm.Theme);
        Assert.True(vm.LaunchAtStartup);
        Assert.True(vm.IsApiKeyValid);
    }

    [Fact]
    public void SettingsViewModel_ValidateApiKey_DemoProviderAlwaysValid()
    {
        var vm = new SettingsViewModel();
        vm.ActiveProviderId = "demo";
        vm.ApiKey = "";

        bool isValid = vm.ValidateApiKey("demo", "");

        Assert.True(isValid);
        Assert.Equal("Valid", vm.ValidationStatus);
    }

    [Theory]
    [InlineData("openai", "", false)]
    [InlineData("openai", "short", false)]
    [InlineData("openai", "sk-1234567890abcdef", true)]
    [InlineData("anthropic", "sk-ant-api-01-key12345", true)]
    public void SettingsViewModel_ValidateApiKey_ValidatesKeyLength(string provider, string key, bool expectedValid)
    {
        var vm = new SettingsViewModel();
        vm.ActiveProviderId = provider;
        vm.ApiKey = key;

        bool result = vm.ValidateApiKey(provider, key);

        Assert.Equal(expectedValid, result);
    }

    [Fact]
    public void SettingsViewModel_HotkeyAndThemeConfiguration_UpdatesProperties()
    {
        var vm = new SettingsViewModel();
        vm.HotkeyShortcut = "Alt+Space";
        vm.Theme = "Dark";
        vm.LaunchAtStartup = false;

        Assert.Equal("Alt+Space", vm.HotkeyShortcut);
        Assert.Equal("Dark", vm.Theme);
        Assert.False(vm.LaunchAtStartup);
    }

    [Fact]
    public void SettingsViewModel_Blocklist_AddAndRemoveApplications()
    {
        var vm = new SettingsViewModel();
        vm.NewBlockedAppName = "notepad";

        Assert.True(vm.CanAddBlockedApplication());
        vm.AddBlockedApplication();

        Assert.Contains("notepad.exe", vm.BlockedApplications);
        Assert.Equal(string.Empty, vm.NewBlockedAppName);

        vm.RemoveBlockedApplication("notepad.exe");
        Assert.DoesNotContain("notepad.exe", vm.BlockedApplications);
    }
}
