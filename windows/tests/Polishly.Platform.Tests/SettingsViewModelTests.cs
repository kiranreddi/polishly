using Xunit;
using Polishly.App.Services;
using Polishly.App.ViewModels;
using Polishly.Core.Models;
using Polishly.WindowsIntegration.Security;

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

    [Fact]
    public async Task SettingsViewModel_Save_PersistsProviderAndCredential()
    {
        var credentialStore = new TestCredentialStore();
        var settingsStore = new TestSettingsStore();
        var vm = new SettingsViewModel(credentialStore, settingsStore, null);
        vm.ActiveProviderId = "openai";
        vm.ApiKey = "sk-test-key-123456";

        var saved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.SettingsSaved += (_, _) => saved.TrySetResult(true);

        vm.Save();

        await saved.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("sk-test-key-123456", credentialStore.SavedKey);
        Assert.Equal("openai", settingsStore.SavedSettings?.ActiveProviderId);
    }

    [Fact]
    public async Task AppSettingsStore_RoundTripsNonSecretSettings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"polishly-settings-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonAppSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                ActiveProviderId = "openai",
                Theme = "Dark",
                HotkeyShortcut = "Ctrl+Alt+K",
                LaunchAtStartup = true
            });

            var loaded = await store.LoadAsync();
            Assert.Equal("openai", loaded.ActiveProviderId);
            Assert.Equal("Dark", loaded.Theme);
            Assert.Equal("Ctrl+Alt+K", loaded.HotkeyShortcut);
            Assert.True(loaded.LaunchAtStartup);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class TestSettingsStore : IAppSettingsStore
    {
        public AppSettings? SavedSettings { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken ct = default) =>
            Task.FromResult(new AppSettings());

        public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            SavedSettings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class TestCredentialStore : ICredentialStore
    {
        public string? SavedKey { get; private set; }

        public Task SaveApiKeyAsync(string providerId, string apiKey, CancellationToken ct = default)
        {
            SavedKey = apiKey;
            return Task.CompletedTask;
        }

        public Task<string?> GetApiKeyAsync(string providerId, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);

        public Task DeleteApiKeyAsync(string providerId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
