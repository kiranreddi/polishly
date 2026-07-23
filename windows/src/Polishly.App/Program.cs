using System;
using System.Runtime.InteropServices;
using System.Threading;
using Polishly.App.Services;
using Polishly.App.ViewModels;
using Polishly.App.Views;
using Polishly.Core;
using Polishly.Core.Capabilities;
using Polishly.Core.Diff;
using Polishly.Core.StateMachine;
using Polishly.Providers.Demo;

using Polishly.Providers.Abstractions;
using Polishly.WindowsIntegration.Capture;

using Polishly.WindowsIntegration.Clipboard;
using Polishly.WindowsIntegration.Hotkey;
using Polishly.WindowsIntegration.Injection;
using Polishly.WindowsIntegration.Native;
using Polishly.WindowsIntegration.Security;

namespace Polishly.App;

public static class Program
{
    private static TrayIconService? _trayIconService;
    private static GlobalHotkeyListener? _hotkeyListener;
    private static Polishly.Core.StateMachine.RewriteStateMachine? _stateMachine;
    private static WindowTracker? _windowTracker;
    private static Polishly.Core.Capabilities.AppCapabilityRules? _capabilityRules;

    private static UIAutomationCapture? _captureEngine;
    private static GuardedClipboardTransaction? _clipboardTransaction;
    private static TextInjector? _injectorEngine;
    private static CredentialManager? _credentialManager;
    private static NativeMessageWindow? _messageWindow;

    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("=== Polishly Windows Companion App Starting ===");

        bool demoRewrite = args.Any(a => string.Equals(a, "--demo-rewrite", StringComparison.OrdinalIgnoreCase));
        string? demoText = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--text", StringComparison.OrdinalIgnoreCase))
            {
                demoText = args[i + 1];
                break;
            }
        }

        // 1. Dependency Composition & Service Registration
        _capabilityRules = new Polishly.Core.Capabilities.AppCapabilityRules();

        _windowTracker = new WindowTracker();
        _captureEngine = new UIAutomationCapture(_windowTracker, _capabilityRules);
        _clipboardTransaction = new GuardedClipboardTransaction();
        _injectorEngine = new TextInjector(_clipboardTransaction, _capabilityRules);
        _credentialManager = new CredentialManager();
        _stateMachine = new Polishly.Core.StateMachine.RewriteStateMachine();

        if (demoRewrite)
        {
            _captureEngine.TestFallbackText = string.IsNullOrWhiteSpace(demoText)
                ? "I think we should push the meeting to next week because several people are out."
                : demoText;
            RunDemoRewriteAndExit().GetAwaiter().GetResult();
            return;
        }

        // 2. Native Message Window Initialization
        _messageWindow = new NativeMessageWindow();
        var messageHwnd = _messageWindow.Handle;

        // 3. Tray Service Initialization
        _trayIconService = new TrayIconService();
        _trayIconService.Initialize(messageHwnd);

        _trayIconService.RewriteRequested += (s, e) => ExecuteRewriteWorkflow();
        _trayIconService.SettingsRequested += (s, e) => OpenSettingsWindow();
        _trayIconService.ExitRequested += (s, e) => ShutdownApp();

        // 4. Global Hotkey Registration (Ctrl+Shift+P)
        _hotkeyListener = new GlobalHotkeyListener();
        _hotkeyListener.HotkeyPressed += (s, e) => ExecuteRewriteWorkflow();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Register hotkey: MOD_CONTROL (0x0002) | MOD_SHIFT (0x0004), VK 'P' (0x50)
            _hotkeyListener.Register(messageHwnd, Win32Native.MOD_CONTROL | Win32Native.MOD_SHIFT, 0x50);
            Console.WriteLine("[Polishly] Registered global hotkey Ctrl+Shift+P");
        }

        _messageWindow.MessageReceived += (msg, wParam, lParam) =>
        {
            _hotkeyListener?.ProcessWindowMessage(msg, wParam, lParam);
            _trayIconService?.ProcessWindowMessage(msg, wParam, lParam);
        };

        Console.WriteLine("[Polishly] Native Windows Companion Engine initialized.");
        Console.WriteLine("[Polishly] Running system tray background loop...");

#if HAS_WPF
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var app = new System.Windows.Application();
            app.Run();
            return;
        }
#endif

        // Background service execution loop for CLI/testing environments
        var keepAliveEvent = new ManualResetEvent(false);
        AppDomain.CurrentDomain.ProcessExit += (s, e) => keepAliveEvent.Set();
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; keepAliveEvent.Set(); };

        Console.WriteLine("[Polishly] Press Ctrl+C or send exit via Tray to terminate.");
        keepAliveEvent.WaitOne();

        ShutdownApp();
    }

    /// <summary>
    /// Headless end-to-end rewrite demo: capture → stream (DemoProvider) → word-diff → accept/inject → exit.
    /// Usage: Polishly.App --demo-rewrite [--text "selected text"]
    /// </summary>
    private static async Task RunDemoRewriteAndExit()
    {
        if (_stateMachine == null || _captureEngine == null || _injectorEngine == null)
        {
            Console.Error.WriteLine("[Polishly] Demo rewrite failed: services not initialized.");
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine("[Polishly] Demo rewrite mode — exercising capture → stream → diff → inject.");
        try
        {
            _stateMachine.Transition(RewriteEvent.TriggerHotkey);
            var selection = await _captureEngine.CaptureSelectionAsync();
            Console.WriteLine($"[Polishly] Captured from '{selection.TargetContext.ProcessName}': \"{selection.SelectedText}\"");

            _stateMachine.Transition(RewriteEvent.CaptureSuccess);

            var diffEngine = new Polishly.Core.Diff.WordDiffEngine();
            var popupVm = new PopupViewModel(_stateMachine, diffEngine);
            popupVm.Reset(selection.SelectedText);
            popupVm.TargetWindowHandle = selection.TargetContext.WindowHandle;

            var provider = await ResolveProviderAsync();
            var req = new Polishly.Core.Models.RewriteRequest(
                InputText: selection.SelectedText,
                Mode: Polishly.Core.Models.RewriteMode.Improve,
                CustomInstruction: null
            );

            _stateMachine.Transition(RewriteEvent.StartStreaming);
            Console.Write("[Polishly] Streaming: ");
            await foreach (var token in provider.StreamRewriteAsync(req))
            {
                Console.Write(token.Text);
                popupVm.AppendStreamingToken(token.Text);
                _stateMachine.Transition(RewriteEvent.ReceiveToken);
            }
            Console.WriteLine();

            popupVm.CompleteStream();
            _stateMachine.Transition(RewriteEvent.StreamFinished);

            Console.WriteLine($"[Polishly] Original : {popupVm.OriginalText}");
            Console.WriteLine($"[Polishly] Rewritten: {popupVm.RewrittenText}");
            Console.WriteLine("[Polishly] Diff segments:");
            foreach (var seg in popupVm.DiffSegments)
            {
                Console.WriteLine($"  [{seg.Type}] {seg.Text}");
            }

            var pasteDone = new TaskCompletionSource<Polishly.WindowsIntegration.Injection.InjectionResult>();
            popupVm.RequestPaste += async (s, rewrittenText) =>
            {
                try
                {
                    var result = await _injectorEngine.InjectTextAsync(selection.TargetContext, rewrittenText);
                    _stateMachine.Transition(result.Success ? RewriteEvent.ReplaceSuccess : RewriteEvent.ReplaceFailed,
                        result.ErrorMessage);
                    pasteDone.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    _stateMachine.Transition(RewriteEvent.ReplaceFailed, ex.Message);
                    pasteDone.TrySetException(ex);
                }
            };

            popupVm.Accept();
            var injectResult = await pasteDone.Task;

            Console.WriteLine($"[Polishly] Inject result: Success={injectResult.Success}, Method={injectResult.MethodUsed}");
            Console.WriteLine($"[Polishly] Final state: {_stateMachine.CurrentState}");
            Console.WriteLine("[Polishly] Demo rewrite completed successfully.");
            Environment.ExitCode = injectResult.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Polishly] Demo rewrite error: {ex.Message}");
            _stateMachine?.Transition(RewriteEvent.Error, ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private static SettingsViewModel? _settingsViewModel;

    private static async Task<IAiProvider> ResolveProviderAsync()
    {
        string providerId = _settingsViewModel?.ActiveProviderId ?? "demo";
        string? apiKey = null;
        if (_credentialManager != null && providerId != "demo")
        {
            apiKey = await _credentialManager.GetApiKeyAsync(providerId);
        }

        return providerId.ToLowerInvariant() switch
        {
            "openai" => new Polishly.Providers.OpenAI.OpenAiProvider(apiKey ?? string.Empty),
            "anthropic" => new Polishly.Providers.Anthropic.AnthropicProvider(apiKey ?? string.Empty),
            "groq" => new Polishly.Providers.Groq.GroqProvider(apiKey ?? string.Empty),
            "cerebras" => new Polishly.Providers.Cerebras.CerebrasProvider(apiKey ?? string.Empty),
            _ => new Polishly.Providers.Demo.DemoProvider()
        };
    }

    private static async void ExecuteRewriteWorkflow(string? customInstruction = null)
    {
        if (_stateMachine == null || _captureEngine == null || _injectorEngine == null) return;
        if (_trayIconService != null && _trayIconService.IsPaused)
        {
            Console.WriteLine("[Polishly] Rewrite requested while paused; ignoring.");
            return;
        }

        Console.WriteLine("[Polishly] Global Hotkey Triggered — Executing Rewrite Workflow...");
        try
        {
            _stateMachine.Transition(RewriteEvent.TriggerHotkey);
            var selection = await _captureEngine.CaptureSelectionAsync();
            Console.WriteLine($"[Polishly] Captured selection from '{selection.TargetContext.ProcessName}': \"{selection.SelectedText}\"");

            _stateMachine.Transition(RewriteEvent.CaptureSuccess);

            var diffEngine = new Polishly.Core.Diff.WordDiffEngine();
            var popupVm = new PopupViewModel(_stateMachine, diffEngine);
            popupVm.Reset(selection.SelectedText);
            popupVm.TargetWindowHandle = selection.TargetContext.WindowHandle;

#if HAS_WPF
            PopupWindow? popupWin = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                popupWin = new PopupWindow(popupVm);

                // Position popup window near target window using PopupPositioner
                var positioner = new PopupPositioner();
                var targetRect = new ScreenRect(100, 100, 400, 200);
                var workArea = new ScreenRect(0, 0, 1920, 1080);
                var pos = positioner.CalculatePosition(targetRect, workArea, popupWin.Width > 0 ? popupWin.Width : 400, popupWin.Height > 0 ? popupWin.Height : 250);
                popupWin.Left = pos.X;
                popupWin.Top = pos.Y;

                popupWin.Show();
            }

            popupVm.RequestClose += (s, e) =>
            {
                popupWin?.Close();
            };

            popupVm.RequestPaste += async (s, rewrittenText) =>
            {
                if (_injectorEngine != null)
                {
                    var injectResult = await _injectorEngine.InjectTextAsync(selection.TargetContext, rewrittenText);
                    Console.WriteLine($"[Polishly] Safe replacement result: Success={injectResult.Success}, Method={injectResult.MethodUsed}");
                    _stateMachine?.Transition(injectResult.Success ? RewriteEvent.ReplaceSuccess : RewriteEvent.ReplaceFailed,
                        injectResult.ErrorMessage);
                }
                popupWin?.Close();
            };

            popupVm.RequestCopy += (s, text) =>
            {
                if (OperatingSystem.IsWindows())
                {
                    try { System.Windows.Clipboard.SetText(text); } catch { }
                }
                popupWin?.Close();
            };

            popupVm.RequestRevise += (s, e) =>
            {
                popupWin?.Close();
                var reviseVm = new ReviseInstructionViewModel
                {
                    TargetWindowHandle = selection.TargetContext.WindowHandle
                };
                var reviseWin = new ReviseInstructionView(reviseVm);
                reviseVm.InstructionSubmitted += (sender, prompt) =>
                {
                    ExecuteRewriteWorkflow(prompt);
                };
                reviseWin.ShowDialog();
            };
#endif

            var provider = await ResolveProviderAsync();
            var mode = string.IsNullOrEmpty(customInstruction) ? Polishly.Core.Models.RewriteMode.Improve : Polishly.Core.Models.RewriteMode.Custom;
            var req = new Polishly.Core.Models.RewriteRequest(
                InputText: selection.SelectedText,
                Mode: mode,
                CustomInstruction: customInstruction
            );

            _stateMachine.Transition(RewriteEvent.StartStreaming);

            await foreach (var token in provider.StreamRewriteAsync(req))
            {
                popupVm.AppendStreamingToken(token.Text);
                _stateMachine.Transition(RewriteEvent.ReceiveToken);
            }

            popupVm.CompleteStream();
            _stateMachine.Transition(RewriteEvent.StreamFinished);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Polishly] Rewrite workflow error: {ex.Message}");
            _stateMachine?.Transition(RewriteEvent.Error, ex.Message);
        }
    }

    private static void OpenSettingsWindow()
    {
        Console.WriteLine("[Polishly] Opening Settings Window...");
#if HAS_WPF
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _settingsViewModel ??= new SettingsViewModel(_credentialManager);
            var settingsWin = new SettingsWindow(_settingsViewModel);
            settingsWin.Show();
        }
#endif
    }


    private static void ShutdownApp()
    {
        Console.WriteLine("[Polishly] Shutting down Polishly Windows Companion...");
        _hotkeyListener?.Dispose();
        _trayIconService?.Dispose();
        _messageWindow?.Dispose();
        Environment.Exit(0);
    }
}

