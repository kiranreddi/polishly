using Polishly.App.Services;
using Polishly.App.ViewModels;
using Polishly.Core.Models;
using Polishly.Providers;
using Polishly.WindowsIntegration.Hotkey;
using Xunit;

namespace Polishly.Platform.Tests;

public class WindowsPlanCompletionTests
{
    [Theory]
    [InlineData("Ctrl+Shift+P", 0x0006u, 0x50u)]
    [InlineData("Alt+Space", 0x0001u, 0x20u)]
    [InlineData("Win+F12", 0x0008u, 0x7Bu)]
    public void HotkeyParser_ParsesSupportedShortcuts(
        string shortcut, uint modifiers, uint virtualKey)
    {
        bool parsed = HotkeyShortcutParser.TryParse(
            shortcut, out ParsedHotkey result, out string? error);

        Assert.True(parsed, error);
        Assert.Equal(modifiers, result.Modifiers);
        Assert.Equal(virtualKey, result.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("P")]
    [InlineData("Ctrl+Mouse1")]
    [InlineData("Meta+P")]
    public void HotkeyParser_RejectsUnsafeOrUnsupportedShortcuts(string shortcut)
    {
        Assert.False(HotkeyShortcutParser.TryParse(shortcut, out _, out _));
    }

    [Fact]
    public async Task SettingsStore_PersistsPreferencesWithoutCredentialMaterial()
    {
        string directory = Path.Combine(
            Path.GetTempPath(), $"polishly-settings-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new JsonAppSettingsStore(path);
            var settings = new AppSettings
            {
                ActiveProviderId = "openai",
                Theme = "Dark",
                HotkeyShortcut = "Alt+Space",
                LaunchAtStartup = true,
                OnboardingCompleted = true
            };
            settings.ProviderPreferences["openai"] = "gpt-4.1-mini";

            await store.SaveAsync(settings);
            AppSettings loaded = await store.LoadAsync();
            string json = await File.ReadAllTextAsync(path);

            Assert.Equal("openai", loaded.ActiveProviderId);
            Assert.Equal("gpt-4.1-mini", loaded.ProviderPreferences["openai"]);
            Assert.True(loaded.OnboardingCompleted);
            Assert.DoesNotContain("apiKey", json);
            Assert.DoesNotContain("sk-", json);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("openai", "gpt-4.1-mini")]
    [InlineData("anthropic", "claude-3-5-haiku-latest")]
    [InlineData("groq", "llama-3.3-70b-versatile")]
    [InlineData("cerebras", "llama-3.3-70b")]
    public void ProviderFactory_ValidatesSupportedModels(string provider, string model)
    {
        Assert.True(ProviderFactory.IsKnownModel(provider, model));
        Assert.False(ProviderFactory.IsKnownModel(provider, "invented-model"));
    }

    [Fact]
    public void PopupPositioner_PreservesNegativeMonitorCoordinatesAt175Percent()
    {
        var workArea = new ScreenRect(-2560, -220, 2560, 1440);
        var selection = new ScreenRect(-2200, 980, 420, 30);
        var positioner = new PopupPositioner(
            PopupPositioner.DpiToScale(168));

        ScreenPoint result = positioner.CalculatePosition(
            selection, workArea, 700, 350);

        Assert.InRange(result.X, workArea.Left, workArea.Right - 700);
        Assert.InRange(result.Y, workArea.Top, workArea.Bottom - 350);
    }

    [Fact]
    public void ScreenBounds_RejectsNonFiniteOrEmptyGeometry()
    {
        Assert.True(new ScreenBounds(-100, 50, 300, 20).IsUsable);
        Assert.False(new ScreenBounds(0, 0, 0, 20).IsUsable);
        Assert.False(new ScreenBounds(double.NaN, 0, 20, 20).IsUsable);
    }
}
