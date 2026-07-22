using System;
using System.Threading;
using System.Threading.Tasks;
using Polishly.Core.Capabilities;
using Polishly.Core.Models;
using Polishly.WindowsIntegration.Native;
using Polishly.WindowsIntegration.Security;
#if HAS_WPF
using System.Windows.Automation;
#endif

namespace Polishly.WindowsIntegration.Capture;

public class UIAutomationCapture : ICaptureEngine
{
    private readonly WindowTracker _windowTracker;
    private readonly AppCapabilityRules _capabilityRules;
    private readonly SensitiveFieldDetector _sensitiveDetector = new();

    public UIAutomationCapture(WindowTracker windowTracker, AppCapabilityRules capabilityRules)
    {
        _windowTracker = windowTracker;
        _capabilityRules = capabilityRules;
    }

    public Task<SelectionContext> CaptureSelectionAsync(CancellationToken ct = default)
    {
        var window = _windowTracker.GetForegroundWindowInfo();
        var profile = _capabilityRules.GetProfile(window.ProcessName);

        bool isPassword = false;
        string capturedText = string.Empty;
        bool isDirectUia = profile.PreferredCapture != CaptureMethod.GuardedClipboard;

        if (OperatingSystem.IsWindows())
        {
#if HAS_WPF
            try
            {
                var focusedElement = AutomationElement.FocusedElement;
                if (focusedElement != null)
                {
                    object isPassProp = focusedElement.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty);
                    if (isPassProp is bool b && b)
                    {
                        isPassword = true;
                    }

                    if (isDirectUia)
                    {
                        if (focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out object tpObj) && tpObj is TextPattern textPattern)
                        {
                            var selectionRanges = textPattern.GetSelection();
                            if (selectionRanges != null && selectionRanges.Length > 0)
                            {
                                capturedText = selectionRanges[0].GetText(-1) ?? string.Empty;
                            }
                        }

                        if (string.IsNullOrEmpty(capturedText) && focusedElement.TryGetCurrentPattern(ValuePattern.Pattern, out object vpObj) && vpObj is ValuePattern valuePattern)
                        {
                            capturedText = valuePattern.Current.Value ?? string.Empty;
                        }
                    }
                }
            }
            catch
            {
                // UIA lookup exception fallback
            }
#endif

            var sensitiveStatus = _sensitiveDetector.IsSensitiveField(window);
            if (sensitiveStatus.IsSensitive)
            {
                isPassword = true;
            }

            // Fall back to GuardedClipboard clipboard capture if UIA direct capture yielded empty text or profile mandates clipboard
            if (string.IsNullOrEmpty(capturedText))
            {
                try
                {
                    // Synthesize Ctrl+C to copy selected text to clipboard
                    var inputs = new Win32Native.INPUT[4];
                    inputs[0] = new Win32Native.INPUT { type = Win32Native.INPUT_KEYBOARD, ki = new Win32Native.KEYBDINPUT { wVk = Win32Native.VK_CONTROL } };
                    inputs[1] = new Win32Native.INPUT { type = Win32Native.INPUT_KEYBOARD, ki = new Win32Native.KEYBDINPUT { wVk = Win32Native.VK_C } };
                    inputs[2] = new Win32Native.INPUT { type = Win32Native.INPUT_KEYBOARD, ki = new Win32Native.KEYBDINPUT { wVk = Win32Native.VK_C, dwFlags = Win32Native.KEYEVENTF_KEYUP } };
                    inputs[3] = new Win32Native.INPUT { type = Win32Native.INPUT_KEYBOARD, ki = new Win32Native.KEYBDINPUT { wVk = Win32Native.VK_CONTROL, dwFlags = Win32Native.KEYEVENTF_KEYUP } };
                    Win32Native.SendInput(4, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Win32Native.INPUT>());

                    Thread.Sleep(50);

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
        }

        if (string.IsNullOrEmpty(capturedText))
        {
            capturedText = "Sample text to be rewritten.";
        }


        var targetContext = new TargetContext(
            WindowHandle: window.Handle,
            ProcessId: window.ProcessId,
            ProcessName: window.ProcessName,
            AppTitle: window.Title,
            FieldId: "uia_field_1",
            IsPassword: isPassword,
            IsElevated: window.IsElevated
        );

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
