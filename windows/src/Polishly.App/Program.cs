using System;
using System.Runtime.InteropServices;
using System.Threading;
using Polishly.App.Services;
using Polishly.App.ViewModels;
using Polishly.App.Views;
using Polishly.Core;
using Polishly.Core.Capabilities;
using Polishly.Core.Diff;
using Polishly.Core.Models;
using Polishly.Core.StateMachine;
using Polishly.Providers.Demo;
using Polishly.Providers;

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
    private static IAppSettingsStore? _settingsStore;
    private static AppSettings _appSettings = new();
    private static Mutex? _singleInstanceMutex;
#if HAS_WPF
    private static SettingsWindow? _settingsWindow;
#endif
    private static readonly StartupRegistrationService StartupRegistration = new();
    private static int _rewriteWorkflowActive;

    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("=== Polishly Windows Companion App Starting ===");
        if (OperatingSystem.IsWindows())
        {
            try
            {
                Win32Native.SetProcessDpiAwarenessContext(
                    Win32Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch
            {
                // A manifest also declares Per-Monitor V2. Windows can reject this
                // call if another component already established the same context.
            }
        }

        bool demoRewrite = args.Any(a => string.Equals(a, "--demo-rewrite", StringComparison.OrdinalIgnoreCase));
        string? providerSmoke = null;
        string? demoText = null;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--text", StringComparison.OrdinalIgnoreCase))
            {
                demoText = args[i + 1];
                break;
            }
            if (string.Equals(args[i], "--provider-smoke", StringComparison.OrdinalIgnoreCase))
            {
                providerSmoke = args[i + 1];
            }
        }

        // 1. Dependency Composition & Service Registration
        _capabilityRules = new Polishly.Core.Capabilities.AppCapabilityRules();
        _settingsStore = new JsonAppSettingsStore();
        _appSettings = _settingsStore.LoadAsync().GetAwaiter().GetResult();

        _windowTracker = new WindowTracker();
        _captureEngine = new UIAutomationCapture(
            _windowTracker, _capabilityRules, _appSettings.BlockedApplications);
        _clipboardTransaction = new GuardedClipboardTransaction();
        _injectorEngine = new TextInjector(_clipboardTransaction, _capabilityRules);
        _credentialManager = new CredentialManager();
        _stateMachine = new Polishly.Core.StateMachine.RewriteStateMachine();
        _settingsViewModel = CreateSettingsViewModel();

        if (!string.IsNullOrWhiteSpace(providerSmoke))
        {
            RunProviderSmokeAndExit(providerSmoke).GetAwaiter().GetResult();
            return;
        }

        if (demoRewrite)
        {
            // The CLI demo verifies orchestration without reading or replacing
            // any interactive desktop content, including on Windows CI.
            _clipboardTransaction = new GuardedClipboardTransaction(() => 1u);
            _injectorEngine = new TextInjector(
                _clipboardTransaction, _capabilityRules);
            _captureEngine.TestFallbackText = string.IsNullOrWhiteSpace(demoText)
                ? "I think we should push the meeting to next week because several people are out."
                : demoText;
            RunDemoRewriteAndExit().GetAwaiter().GetResult();
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            _singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                name: @"Local\Polishly.WindowsCompanion",
                createdNew: out bool isFirstInstance);
            if (!isFirstInstance)
            {
                Console.WriteLine("[Polishly] Another instance is already running.");
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                return;
            }
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
            RegisterConfiguredHotkey(messageHwnd);
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
            app.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
            ThemeService.Apply(_appSettings.Theme);
            if (!_appSettings.OnboardingCompleted)
            {
                ShowOnboarding();
            }
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
            Console.WriteLine(
                $"[Polishly] Captured {selection.SelectedText.Length} characters from '{selection.TargetContext.ProcessName}'.");

            _stateMachine.Transition(RewriteEvent.CaptureSuccess);

            var diffEngine = new Polishly.Core.Diff.WordDiffEngine();
            var popupVm = new PopupViewModel(_stateMachine, diffEngine);
            using var popupVmLifetime = popupVm;
            popupVm.Reset(selection.SelectedText);
            popupVm.TargetWindowHandle = selection.TargetContext.WindowHandle;
            popupVm.IsVisible = true;

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
            }
            Console.WriteLine();

            popupVm.CompleteStream();

            Console.WriteLine(
                $"[Polishly] Local diff contains {popupVm.DiffSegments.Count} segments.");

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
            Console.WriteLine(injectResult.Success
                ? "[Polishly] Demo rewrite completed successfully."
                : "[Polishly] Demo rewrite failed safely.");
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
        string providerId = _appSettings.ActiveProviderId;
        string? apiKey = null;
        if (_credentialManager != null && providerId != "demo")
        {
            apiKey = await _credentialManager.GetApiKeyAsync(providerId);
        }

        string? model = _appSettings.ProviderPreferences.GetValueOrDefault(providerId);
        return ProviderFactory.Create(providerId, apiKey, model);
    }

    private static async Task RunProviderSmokeAndExit(string providerId)
    {
        string apiKey = Environment.GetEnvironmentVariable("POLISHLY_PROVIDER_API_KEY")
                        ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Console.Error.WriteLine("[Polishly] Provider smoke key is missing.");
            Environment.ExitCode = 2;
            return;
        }

        try
        {
            string model = ProviderFactory.GetModels(providerId).FirstOrDefault()
                           ?? throw new InvalidOperationException("Unknown provider.");
            IAiProvider provider = ProviderFactory.Create(providerId, apiKey, model);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            ValidationResult validation = await provider.ValidateCredentialsAsync(
                apiKey, timeout.Token);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    validation.ErrorMessage ?? "Credential validation failed.");
            }

            var request = new Polishly.Core.Models.RewriteRequest(
                "This sentence need polish.",
                Polishly.Core.Models.RewriteMode.Improve,
                null);
            int outputCharacters = 0;
            await foreach (RewriteToken token in provider.StreamRewriteAsync(
                               request, timeout.Token))
            {
                outputCharacters += token.Text.Length;
            }

            if (outputCharacters == 0)
            {
                throw new InvalidOperationException("Provider returned no rewrite text.");
            }

            Console.WriteLine(
                $"[Polishly] {providerId} credential and rewrite smoke test passed.");
            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[Polishly] {providerId} smoke test failed: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static async void ExecuteRewriteWorkflow(
        string? customInstruction = null,
        SelectionContext? preservedSelection = null,
        Polishly.Core.Models.RewriteMode requestedMode =
            Polishly.Core.Models.RewriteMode.Improve)
    {
        if (_stateMachine == null || _captureEngine == null || _injectorEngine == null) return;
        if (_trayIconService != null && _trayIconService.IsPaused)
        {
            Console.WriteLine("[Polishly] Rewrite requested while paused; ignoring.");
            return;
        }
        if (Interlocked.Exchange(ref _rewriteWorkflowActive, 1) == 1)
        {
            Console.WriteLine("[Polishly] A rewrite is already active.");
            return;
        }

        Console.WriteLine("[Polishly] Global Hotkey Triggered — Executing Rewrite Workflow...");
        bool popupPresented = false;
        using var workflowCancellation = new CancellationTokenSource();
        try
        {
            _stateMachine.Transition(RewriteEvent.TriggerHotkey);
            var selection = preservedSelection ??
                            await _captureEngine.CaptureSelectionAsync(
                                workflowCancellation.Token);
            Console.WriteLine(
                $"[Polishly] Captured {selection.SelectedText.Length} characters from '{selection.TargetContext.ProcessName}'.");

            _stateMachine.Transition(RewriteEvent.CaptureSuccess);

            var diffEngine = new Polishly.Core.Diff.WordDiffEngine();
            var popupVm = new PopupViewModel(_stateMachine, diffEngine);
            popupVm.Reset(selection.SelectedText);
            popupVm.TargetWindowHandle = selection.TargetContext.WindowHandle;
            popupVm.SelectedMode = requestedMode;

#if HAS_WPF
            PopupWindow? popupWin = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                popupWin = new PopupWindow(popupVm);
                popupWin.Closed += (_, _) => popupVm.Dispose();

                popupWin.Show();
                popupPresented = true;
                var placement = new NativePopupPlacementService(
                    popupWin, selection.TargetContext, selection.SelectionBounds);
                placement.Position();
                popupWin.SizeChanged += (s, e) => placement.RepositionForCurrentSize();
            }

            popupVm.RequestClose += (s, e) =>
            {
                popupWin?.Close();
                if (_stateMachine?.CurrentState !=
                    Polishly.Core.StateMachine.RewriteState.Replacing)
                {
                    workflowCancellation.Cancel();
                    CompleteRewriteWorkflow();
                }
            };

            popupVm.RequestPaste += async (s, rewrittenText) =>
            {
                if (_injectorEngine != null)
                {
                    bool replacementSucceeded = false;
                    try
                    {
                        var injectResult = await _injectorEngine.InjectTextAsync(
                            selection.TargetContext,
                            rewrittenText,
                            workflowCancellation.Token);
                        Console.WriteLine($"[Polishly] Safe replacement result: Success={injectResult.Success}, Method={injectResult.MethodUsed}");
                        _stateMachine?.Transition(injectResult.Success ? RewriteEvent.ReplaceSuccess : RewriteEvent.ReplaceFailed,
                            injectResult.ErrorMessage);
                        replacementSucceeded = injectResult.Success;
                    }
                    catch (Exception ex)
                    {
                        _stateMachine?.Transition(RewriteEvent.ReplaceFailed, ex.Message);
                    }

                    if (replacementSucceeded)
                    {
                        popupWin?.Close();
                        CompleteRewriteWorkflow();
                    }
                }
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
                workflowCancellation.Cancel();
                popupWin?.Close();
                var reviseVm = new ReviseInstructionViewModel
                {
                    TargetWindowHandle = selection.TargetContext.WindowHandle
                };
                var reviseWin = new ReviseInstructionView(reviseVm);
                bool submitted = false;
                reviseVm.InstructionSubmitted += (sender, prompt) =>
                {
                    submitted = true;
                    CompleteRewriteWorkflow();
                    ExecuteRewriteWorkflow(
                        prompt,
                        selection,
                        Polishly.Core.Models.RewriteMode.Custom);
                };
                reviseWin.Closed += (_, _) =>
                {
                    if (!submitted)
                    {
                        CompleteRewriteWorkflow();
                    }
                };
                reviseWin.ShowDialog();
            };

            popupVm.RequestRetry += (s, e) =>
            {
                workflowCancellation.Cancel();
                CompleteRewriteWorkflow();
                ExecuteRewriteWorkflow(
                    customInstruction,
                    selection,
                    requestedMode);
            };

            popupVm.RequestModeChange += (s, mode) =>
            {
                workflowCancellation.Cancel();
                popupWin?.Close();
                CompleteRewriteWorkflow();
                ExecuteRewriteWorkflow(null, selection, mode);
            };
#endif

            var provider = await ResolveProviderAsync();
            var mode = string.IsNullOrEmpty(customInstruction)
                ? requestedMode
                : Polishly.Core.Models.RewriteMode.Custom;
            var req = new Polishly.Core.Models.RewriteRequest(
                InputText: selection.SelectedText,
                Mode: mode,
                CustomInstruction: customInstruction
            );

            _stateMachine.Transition(RewriteEvent.StartStreaming);

            await foreach (var token in provider.StreamRewriteAsync(
                               req,
                               workflowCancellation.Token))
            {
                popupVm.AppendStreamingToken(token.Text);
            }

            popupVm.CompleteStream();
#if !HAS_WPF
            CompleteRewriteWorkflow();
#endif
        }
        catch (OperationCanceledException) when (workflowCancellation.IsCancellationRequested)
        {
            // A deliberate Retry, Revise, mode change, or dismissal owns the
            // next UI state. Do not surface cancellation as a provider failure.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Polishly] Rewrite workflow error: {ex.Message}");
            _stateMachine?.Transition(RewriteEvent.Error, ex.Message);
            if (!popupPresented)
            {
                _trayIconService?.ShowTrayNotification(
                    "Rewrite unavailable",
                    ex.Message);
                CompleteRewriteWorkflow();
            }
        }
    }

    private static void OpenSettingsWindow()
    {
        Console.WriteLine("[Polishly] Opening Settings Window...");
#if HAS_WPF
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _settingsViewModel ??= CreateSettingsViewModel();
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow(_settingsViewModel);
                _settingsWindow.Closed += (_, _) => _settingsWindow = null;
                _settingsWindow.Show();
            }
            else
            {
                if (_settingsWindow.WindowState == System.Windows.WindowState.Minimized)
                {
                    _settingsWindow.WindowState = System.Windows.WindowState.Normal;
                }
                _settingsWindow.Activate();
            }
        }
#endif
    }

    private static SettingsViewModel CreateSettingsViewModel()
    {
        var viewModel = new SettingsViewModel(
            _credentialManager, _settingsStore, _appSettings);
        viewModel.SettingsSaved += async (_, settings) =>
        {
            settings.OnboardingCompleted = _appSettings.OnboardingCompleted;
            _appSettings = settings;
            ThemeService.Apply(settings.Theme);
            try
            {
                await StartupRegistration.ApplyAsync(settings.LaunchAtStartup);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Polishly] Startup registration failed: {ex.Message}");
                _trayIconService?.ShowTrayNotification(
                    "Startup preference not applied",
                    "Windows did not allow Polishly to change its startup preference.");
            }

            if (_windowTracker != null && _capabilityRules != null)
            {
                _captureEngine = new UIAutomationCapture(
                    _windowTracker, _capabilityRules, settings.BlockedApplications);
            }

            if (_messageWindow != null)
            {
                RegisterConfiguredHotkey(_messageWindow.Handle);
            }
        };
        return viewModel;
    }

    private static void RegisterConfiguredHotkey(IntPtr messageHwnd)
    {
        if (_hotkeyListener == null) return;
        _hotkeyListener.Unregister();
        if (!HotkeyShortcutParser.TryParse(
                _appSettings.HotkeyShortcut, out ParsedHotkey parsed, out string? error))
        {
            string message = error ?? "The configured hotkey is invalid.";
            Console.Error.WriteLine($"[Polishly] {message}");
            _trayIconService?.ShowTrayNotification("Polishly hotkey unavailable", message);
            return;
        }

        if (!_hotkeyListener.Register(messageHwnd, parsed.Modifiers, parsed.VirtualKey))
        {
            string message =
                $"The global shortcut {_appSettings.HotkeyShortcut} is already in use. Choose another shortcut in Settings.";
            Console.Error.WriteLine($"[Polishly] {message}");
            _trayIconService?.ShowTrayNotification("Polishly hotkey unavailable", message);
            return;
        }

        Console.WriteLine($"[Polishly] Registered global hotkey {_appSettings.HotkeyShortcut}.");
    }

#if HAS_WPF
    private static void ShowOnboarding()
    {
        var viewModel = new OnboardingViewModel(
            _settingsViewModel ?? CreateSettingsViewModel());
        var window = new OnboardingWindow(viewModel);
        viewModel.OnboardingCompleted += (_, _) =>
        {
            _appSettings.OnboardingCompleted = true;
            window.Close();
        };
        window.Show();
    }
#endif


    private static void ShutdownApp()
    {
        Console.WriteLine("[Polishly] Shutting down Polishly Windows Companion...");
        _hotkeyListener?.Dispose();
        _trayIconService?.Dispose();
        _messageWindow?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        Environment.Exit(0);
    }

    private static void CompleteRewriteWorkflow() =>
        Interlocked.Exchange(ref _rewriteWorkflowActive, 0);
}
