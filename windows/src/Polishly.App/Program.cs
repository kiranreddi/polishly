using System;
using System.Runtime.InteropServices;
using System.Threading;
using Polishly.App.Services;
using Polishly.App.ViewModels;
using Polishly.App.Views;
using Polishly.Core;
using Polishly.Core.Capabilities;
using Polishly.Core.Diff;
using Polishly.Providers.Demo;
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
    private static RewriteStateMachine? _stateMachine;
    private static WindowTracker? _windowTracker;
    private static Polishly.Core.Capabilities.AppCapabilityRules? _capabilityRules;

    private static UIAutomationCapture? _captureEngine;
    private static GuardedClipboardTransaction? _clipboardTransaction;
    private static TextInjector? _injectorEngine;
    private static CredentialManager? _credentialManager;

    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("=== Polishly Windows Companion App Starting ===");

        // 1. Dependency Composition & Service Registration
        _capabilityRules = new Polishly.Core.Capabilities.AppCapabilityRules();

        _windowTracker = new WindowTracker();
        _captureEngine = new UIAutomationCapture(_windowTracker, _capabilityRules);
        _clipboardTransaction = new GuardedClipboardTransaction();
        _injectorEngine = new TextInjector(_clipboardTransaction, _capabilityRules);
        _credentialManager = new CredentialManager();
        _stateMachine = new RewriteStateMachine();

        // 2. Tray Service Initialization
        _trayIconService = new TrayIconService();
        _trayIconService.Initialize();

        _trayIconService.RewriteRequested += (s, e) => ExecuteRewriteWorkflow();
        _trayIconService.SettingsRequested += (s, e) => OpenSettingsWindow();
        _trayIconService.ExitRequested += (s, e) => ShutdownApp();

        // 3. Global Hotkey Registration (Ctrl+Shift+P)
        _hotkeyListener = new GlobalHotkeyListener();
        _hotkeyListener.HotkeyPressed += (s, e) => ExecuteRewriteWorkflow();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Register hotkey: MOD_CONTROL (0x0002) | MOD_SHIFT (0x0004), VK 'P' (0x50)
            _hotkeyListener.Register(IntPtr.Zero, Win32Native.MOD_CONTROL | Win32Native.MOD_SHIFT, 0x50);
            Console.WriteLine("[Polishly] Registered global hotkey Ctrl+Shift+P");
        }

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

    private static async void ExecuteRewriteWorkflow()
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
            _stateMachine.Fire(RewriteTrigger.StartCapture);
            var selection = await _captureEngine.CaptureSelectionAsync();
            Console.WriteLine($"[Polishly] Captured selection from '{selection.TargetContext.ProcessName}': \"{selection.SelectedText}\"");

            _stateMachine.Fire(RewriteTrigger.CaptureCompleted);
            _stateMachine.Fire(RewriteTrigger.StartRequest);

            var provider = new DemoProvider();
            var req = new Polishly.Core.Models.RewriteRequest(
                InputText: selection.SelectedText,
                Mode: Polishly.Core.Models.RewriteMode.Improve
            );

            var diffEngine = new Polishly.Core.Diff.WordDiffEngine();
            string fullRewrite = string.Empty;

            await foreach (var token in provider.StreamRewriteAsync(req))
            {
                fullRewrite += token.Text;
                _stateMachine.Fire(RewriteTrigger.ReceiveToken);
            }

            _stateMachine.Fire(RewriteTrigger.CompleteStream);
            var diff = diffEngine.ComputeDiff(selection.SelectedText, fullRewrite);
            Console.WriteLine($"[Polishly] Word-level diff computed ({diff.Count} segments). Rewritten text: \"{fullRewrite}\"");


            // Execute safe text injection
            _stateMachine.Fire(RewriteTrigger.Accept);
            var injectResult = await _injectorEngine.InjectTextAsync(selection.TargetContext, fullRewrite);
            Console.WriteLine($"[Polishly] Safe replacement result: Success={injectResult.Success}, Method={injectResult.MethodUsed}");

            _stateMachine.Reset();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Polishly] Rewrite workflow error: {ex.Message}");
            _stateMachine?.Fire(RewriteTrigger.Fail, ex.Message);
        }
    }

    private static void OpenSettingsWindow()
    {
        Console.WriteLine("[Polishly] Opening Settings Window...");
    }

    private static void ShutdownApp()
    {
        Console.WriteLine("[Polishly] Shutting down Polishly Windows Companion...");
        _hotkeyListener?.Dispose();
        _trayIconService?.Dispose();
        Environment.Exit(0);
    }
}

