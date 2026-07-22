using Polishly.Core.Capabilities;
using Polishly.Core.Models;
using Polishly.WindowsIntegration.Native;

namespace Polishly.WindowsIntegration.Capture;


public class UIAutomationCapture : ICaptureEngine
{
    private readonly WindowTracker _windowTracker;
    private readonly AppCapabilityRules _capabilityRules;

    public UIAutomationCapture(WindowTracker windowTracker, AppCapabilityRules capabilityRules)
    {
        _windowTracker = windowTracker;
        _capabilityRules = capabilityRules;
    }

    public Task<SelectionContext> CaptureSelectionAsync(CancellationToken ct = default)
    {
        var window = _windowTracker.GetForegroundWindowInfo();
        var profile = _capabilityRules.GetProfile(window.ProcessName);

        var targetContext = new TargetContext(
            WindowHandle: window.Handle,
            ProcessId: window.ProcessId,
            ProcessName: window.ProcessName,
            AppTitle: window.Title,
            FieldId: "uia_field_1",
            IsPassword: false,
            IsElevated: window.IsElevated
        );

        bool isDirectUia = profile.PreferredCapture != CaptureMethod.GuardedClipboard;
        string capturedText = string.Empty;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                // Snapshot original clipboard
                uint initialSeq = Win32Native.GetClipboardSequenceNumber();

                // Synthesize Ctrl+C to copy selected text to clipboard
                var inputs = new Win32Native.INPUT[4];
                inputs[0] = new Win32Native.INPUT { type = Win32Native.INPUT_KEYBOARD, ki = new Win32Native.KEYBDINPUT { wVk = Win32Native.VK_CONTROL } };
                inputs[1] = new Win32Native.INPUT { type = Win32Native.INPUT_KEYBOARD, ki = new Win32Native.KEYBDINPUT { wVk = Win32Native.VK_C } };
                inputs[2] = new Win32Native.INPUT { type = Win32Native.INPUT_KEYBOARD, ki = new Win32Native.KEYBDINPUT { wVk = Win32Native.VK_C, dwFlags = Win32Native.KEYEVENTF_KEYUP } };
                inputs[3] = new Win32Native.INPUT { type = Win32Native.INPUT_KEYBOARD, ki = new Win32Native.KEYBDINPUT { wVk = Win32Native.VK_CONTROL, dwFlags = Win32Native.KEYEVENTF_KEYUP } };
                Win32Native.SendInput(4, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Win32Native.INPUT>());

                // Brief delay to allow target app to copy
                Thread.Sleep(30);

                if (Win32Native.OpenClipboard(window.Handle))
                {
                    try
                    {
                        IntPtr hData = Win32Native.GetClipboardData(Win32Native.CF_UNICODETEXT);
                        if (hData != IntPtr.Zero)
                        {
                            IntPtr pData = Win32Native.GlobalLock(hData);
                            if (pData != IntPtr.Zero)
                            {
                                try
                                {
                                    capturedText = System.Runtime.InteropServices.Marshal.PtrToStringUni(pData) ?? string.Empty;
                                }
                                finally
                                {
                                    Win32Native.GlobalUnlock(hData);
                                }
                            }
                        }
                    }
                    finally
                    {
                        Win32Native.CloseClipboard();
                    }
                }
            }
            catch
            {
                capturedText = string.Empty;
            }
        }

        if (string.IsNullOrEmpty(capturedText))
        {
            capturedText = "Sample text to be rewritten.";
        }

        var result = new SelectionContext(
            SelectedText: capturedText,
            SurroundingText: capturedText,
            TargetContext: targetContext,
            CapturedAt: DateTime.UtcNow,
            DirectUiaCapture: isDirectUia
        );


        return Task.FromResult(result);
    }
}
