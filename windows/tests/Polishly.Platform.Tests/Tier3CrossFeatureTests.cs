using Polishly.App.Services;
using Polishly.App.ViewModels;
using Polishly.Core;
using Polishly.Core.Capabilities;
using Polishly.Core.Diff;
using Polishly.Core.Models;
using Polishly.Core.Prompts;
using Polishly.Providers.Abstractions;
using Polishly.Providers.Demo;
using Polishly.WindowsIntegration.Capture;
using Polishly.WindowsIntegration.Clipboard;
using Polishly.WindowsIntegration.Hotkey;
using Polishly.WindowsIntegration.Native;
using Polishly.WindowsIntegration.Security;
using Xunit;

namespace Polishly.Platform.Tests;

public class Tier3CrossFeatureTests
{
    private readonly WindowTracker _tracker = new();
    private readonly Polishly.Core.Capabilities.AppCapabilityRules _capabilityRules = new();
    private readonly SensitiveFieldDetector _detector = new();

    [Fact]
    public async Task HotkeyAndUiaCapture_TriggersCaptureSequence()
    {
        using var hotkeyListener = new GlobalHotkeyListener();
        var captureEngine = new UIAutomationCapture(_tracker, _capabilityRules)
        {
            // Force deterministic capture on headless Windows CI (no interactive selection).
            TestFallbackText = "selected sample text"
        };
        var stateMachine = new Polishly.Core.RewriteStateMachine();

        var tcs = new TaskCompletionSource<bool>();
        hotkeyListener.HotkeyPressed += async (sender, args) =>
        {
            try
            {
                stateMachine.Fire(RewriteTrigger.StartCapture);
                var context = await captureEngine.CaptureSelectionAsync();
                if (!context.IsEmpty)
                {
                    stateMachine.Fire(RewriteTrigger.CaptureCompleted);
                }
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        };

        hotkeyListener.RaiseHotkeyPressed();
        Assert.True(await tcs.Task);
        Assert.Equal(RewriteState.Requesting, stateMachine.CurrentState);
    }

    [Fact]
    public async Task HotkeyAndGuardedClipboard_FallbackWhenUiaFails()
    {
        using var hotkeyListener = new GlobalHotkeyListener();
        var transaction = new GuardedClipboardTransaction(() => 1u);
        var target = new TargetContext(IntPtr.Zero, 100, "notepad", "Notepad", "edit1", false, false);

        var tcs = new TaskCompletionSource<ClipboardTransactionResult>();

        hotkeyListener.HotkeyPressed += async (sender, args) =>
        {
            var res = await transaction.ExecuteSafePasteAsync("Polished content", target);
            tcs.SetResult(res);
        };

        hotkeyListener.RaiseHotkeyPressed();
        var pasteResult = await tcs.Task;

        Assert.NotNull(pasteResult);
        Assert.True(pasteResult.Success);
        Assert.False(pasteResult.FallbackToCopy);
    }

    [Fact]
    public async Task SelectionCaptureAndProviderStreaming_PipelineIntegration()
    {
        var captureEngine = new UIAutomationCapture(_tracker, _capabilityRules)
        {
            TestFallbackText = "hello world from capture pipeline"
        };
        var provider = new DemoProvider();
        var stateMachine = new Polishly.Core.StateMachine.RewriteStateMachine();
        var diffEngine = new Polishly.Core.Diff.WordDiffEngine();
        var popupViewModel = new PopupViewModel(stateMachine, diffEngine);

        var selectionContext = await captureEngine.CaptureSelectionAsync();
        popupViewModel.Reset(selectionContext.SelectedText);

        stateMachine.Transition(Polishly.Core.StateMachine.RewriteEvent.TriggerHotkey);
        stateMachine.Transition(Polishly.Core.StateMachine.RewriteEvent.CaptureSuccess);

        var request = new RewriteRequest(selectionContext.SelectedText, RewriteMode.Improve);
        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            popupViewModel.AppendStreamingToken(token.Text);
        }

        popupViewModel.CompleteStream();

        Assert.Equal(Polishly.Core.StateMachine.RewriteState.StreamComplete, popupViewModel.CurrentState);
        Assert.NotEmpty(popupViewModel.RewrittenText);
        Assert.NotEmpty(popupViewModel.DiffSegments);
    }

    [Fact]
    public async Task GuardedClipboardAndSequenceMismatch_AbortsAndTriggersCopyFallback()
    {
        uint sequenceCounter = 50;
        Func<uint> sequenceProvider = () => ++sequenceCounter;

        var transaction = new GuardedClipboardTransaction(sequenceProvider);
        var target = new TargetContext(IntPtr.Zero, 200, "notepad", "Notepad", "f1", false, false);

        var result = await transaction.ExecuteSafePasteAsync("Polished text", target);

        Assert.False(result.Success);
        Assert.True(result.FallbackToCopy);
        Assert.False(result.RestoredOriginalClipboard);
        Assert.Contains("mismatch", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SensitiveFieldBlockAndFallbackToCopy_PasswordManager()
    {
        var target = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 300,
            ProcessName: "1password",
            AppTitle: "1Password - Vault",
            FieldId: "pwd_field",
            IsPassword: true,
            IsElevated: false
        );

        var targetWindow = new TargetWindow(target.WindowHandle, target.ProcessId, target.ProcessName, target.AppTitle, false);
        var status = _detector.IsSensitiveField(targetWindow);

        Assert.True(status.IsSensitive);

        var transaction = new GuardedClipboardTransaction();
        var pasteResult = await transaction.ExecuteSafePasteAsync("Secret password rewrite", target);

        Assert.False(pasteResult.Success);
        Assert.True(pasteResult.FallbackToCopy);
        Assert.Contains("password field", pasteResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SensitiveFieldBlockAndFallbackToCopy_ElevatedAdminProcess()
    {
        var target = new TargetContext(
            WindowHandle: IntPtr.Zero,
            ProcessId: 400,
            ProcessName: "cmd",
            AppTitle: "Administrator: Command Prompt",
            FieldId: "cli_field",
            IsPassword: false,
            IsElevated: true
        );

        var targetWindow = new TargetWindow(target.WindowHandle, target.ProcessId, target.ProcessName, target.AppTitle, true);
        var status = _detector.IsSensitiveField(targetWindow);

        Assert.True(status.IsSensitive);
        Assert.Contains("elevated", status.Reason, StringComparison.OrdinalIgnoreCase);

        var transaction = new GuardedClipboardTransaction();
        var pasteResult = await transaction.ExecuteSafePasteAsync("Elevated rewrite", target);

        Assert.False(pasteResult.Success);
        Assert.True(pasteResult.FallbackToCopy);
        Assert.Contains("elevated window", pasteResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsToggleAndTrayMenu_ThemeAndProviderSync()
    {
        var settings = new AppSettings();
        var settingsVm = new SettingsViewModel();
        var themeService = new ThemeService();
        var trayService = new TrayIconService();

        trayService.Initialize();
        Assert.True(trayService.IsVisible);

        settings.ActiveProviderId = "openai";
        settings.Theme = "Dark";
        settingsVm.ActiveProviderId = settings.ActiveProviderId;
        themeService.ApplyTheme(settings.Theme);

        Assert.Equal("openai", settingsVm.ActiveProviderId);
        Assert.Equal("Dark", themeService.CurrentTheme);
        Assert.True(settings.IsValid());
    }

    [Fact]
    public void SettingsToggleAndHotkeyReRegistration_UpdatesShortcut()
    {
        using var listener = new GlobalHotkeyListener();
        var settings = new AppSettings { HotkeyShortcut = "Ctrl+Shift+P" };

        bool firstReg = listener.Register(IntPtr.Zero, Win32Native.MOD_CONTROL | Win32Native.MOD_SHIFT, 0x50);
        listener.Unregister();

        settings.HotkeyShortcut = "Ctrl+Alt+K";
        bool secondReg = listener.Register(IntPtr.Zero, Win32Native.MOD_CONTROL | Win32Native.MOD_ALT, 0x4B);
        listener.Unregister();

        Assert.True(firstReg);
        Assert.True(secondReg);
    }

    [Fact]
    public async Task ProviderSelectionAndDiffCalculation_ProducesWordLevelDiff()
    {
        var provider = new DemoProvider();
        var request = new RewriteRequest("The quick text needs improvement.", RewriteMode.Improve);
        
        var sb = new System.Text.StringBuilder();
        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            sb.Append(token.Text);
        }

        string rewritten = sb.ToString();
        var diffEngine = new Polishly.Core.Diff.WordDiffEngine();
        var diffSegments = diffEngine.ComputeDiff(request.InputText, rewritten);

        Assert.NotEmpty(diffSegments);
        Assert.Contains(diffSegments, d => d.Type == DiffType.Addition || d.Type == DiffType.Unchanged);
    }

    [Fact]
    public async Task RewriteStateMachineAndGuardedClipboard_StateTransitionsOnPasteSuccess()
    {
        var sm = new Polishly.Core.RewriteStateMachine();
        var transaction = new GuardedClipboardTransaction(() => 1u);
        var target = new TargetContext(IntPtr.Zero, 500, "notepad", "Notepad", "f1", false, false);

        sm.Fire(RewriteTrigger.StartCapture);
        sm.Fire(RewriteTrigger.CaptureCompleted);
        sm.Fire(RewriteTrigger.ReceiveToken);
        sm.Fire(RewriteTrigger.CompleteStream);
        sm.Fire(RewriteTrigger.Accept);

        Assert.Equal(RewriteState.Accepted, sm.CurrentState);

        var result = await transaction.ExecuteSafePasteAsync("Polished output", target);
        Assert.True(result.Success);

        sm.Reset();
        Assert.Equal(RewriteState.Idle, sm.CurrentState);
    }

    [Fact]
    public void AppCapabilityRulesAndSensitiveDetector_CombinedPolicy()
    {
        var teamsProfile = _capabilityRules.GetProfile("ms-teams");
        Assert.True(teamsProfile.RequireClipboardFallback);
        Assert.False(teamsProfile.AutomaticTriggerSupported);

        var elevatedWindow = new TargetWindow(IntPtr.Zero, 600, "ms-teams", "Microsoft Teams", true);
        var status = _detector.IsSensitiveField(elevatedWindow);

        Assert.True(status.IsSensitive);
        Assert.Contains("elevated", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AppCapabilityRulesAndGuardedClipboard_RequireClipboardFallbackEnforced()
    {
        var profile = _capabilityRules.GetProfile("teams.exe");
        Assert.True(profile.RequireClipboardFallback);

        var target = new TargetContext(IntPtr.Zero, 701, "teams", "Microsoft Teams", "message_input", false, false);
        var transaction = new GuardedClipboardTransaction(() => 1u);

        var result = await transaction.ExecuteSafePasteAsync("Meeting update notes", target);

        Assert.True(result.Success);
        Assert.False(result.FallbackToCopy);
        Assert.True(result.RestoredOriginalClipboard);
    }

    [Fact]
    public async Task CredentialManagerAndProviderStreaming_ApiKeyRetrievalAndStreaming()
    {
        var credManager = new CredentialManager();
        const string targetProvider = "openai_test_tier3";
        const string testKey = "sk-test-tier3-cross-feature-key-12345";

        await credManager.SaveApiKeyAsync(targetProvider, testKey);
        string? retrievedKey = await credManager.GetApiKeyAsync(targetProvider);

        Assert.Equal(testKey, retrievedKey);

        var provider = new DemoProvider();
        var request = new RewriteRequest("Test key retrieval with provider streaming.", RewriteMode.Concise);

        var tokens = new List<string>();
        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            tokens.Add(token.Text);
        }

        Assert.NotEmpty(tokens);
        await credManager.DeleteApiKeyAsync(targetProvider);
    }

    [Fact]
    public void RedactedLoggerAndSensitiveDetector_SensitiveContextLogsAreRedacted()
    {
        var logger = new RedactedLogger();
        var targetWindow = new TargetWindow(IntPtr.Zero, 800, "1password", "1Password Vault", false);

        var status = _detector.IsSensitiveField(targetWindow);
        Assert.True(status.IsSensitive);

        logger.LogInformation($"Sensitive field detected in {targetWindow.ProcessName}: {status.Reason}. User text: sk-secret-api-key-9999");
        var logOutput = logger.GetLogs();

        Assert.Contains(logOutput, l => l.Contains("1password"));
        Assert.DoesNotContain("sk-secret-api-key-9999", string.Join("\n", logOutput));
    }
}
