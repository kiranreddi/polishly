using System.Runtime.InteropServices;
using Polishly.Core.Capabilities;
using Polishly.Core.Models;
using Polishly.WindowsIntegration.Clipboard;
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
    private readonly SensitiveFieldDetector _sensitiveDetector;
    private readonly ClipboardSnapshotService _clipboard = new();

    public string? TestFallbackText { get; set; }

    public UIAutomationCapture(
        WindowTracker windowTracker,
        AppCapabilityRules capabilityRules,
        IEnumerable<string>? additionalBlockedApplications = null)
    {
        _windowTracker = windowTracker;
        _capabilityRules = capabilityRules;
        _sensitiveDetector = new SensitiveFieldDetector(additionalBlockedApplications);
    }

    public async Task<SelectionContext> CaptureSelectionAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask;
        if (!string.IsNullOrEmpty(TestFallbackText))
        {
            var testTarget = new TargetContext(
                IntPtr.Zero, 0, "notepad", "Untitled - Notepad", "test-field",
                false, false, "test-runtime-id", "Edit", TestFallbackText);
            return new SelectionContext(
                TestFallbackText, TestFallbackText, testTarget, DateTime.UtcNow, true,
                new ScreenBounds(100, 100, 320, 24));
        }

        var window = _windowTracker.GetForegroundWindowInfo();
        var profile = _capabilityRules.GetProfile(window.ProcessName);
        var sensitiveStatus = _sensitiveDetector.IsSensitiveField(window);
        if (sensitiveStatus.IsSensitive || window.IsElevated)
        {
            throw new InvalidOperationException(
                $"Selection capture blocked for '{window.ProcessName}': {sensitiveStatus.Reason}");
        }

        string capturedText = string.Empty;
        string? runtimeId = null;
        string? controlType = null;
        ScreenBounds? bounds = null;
        bool isPassword = false;
        bool directUiaCapture = false;

        if (OperatingSystem.IsWindows())
        {
#if HAS_WPF
            AutomationElement? focusedElement = null;
            try
            {
                focusedElement = AutomationElement.FocusedElement;
                if (focusedElement != null)
                {
                    isPassword = focusedElement.Current.IsPassword;
                    if (isPassword)
                    {
                        throw new InvalidOperationException(
                            "Selection capture blocked: the focused element is a password field.");
                    }

                    runtimeId = FormatRuntimeId(focusedElement.GetRuntimeId());
                    controlType = focusedElement.Current.ControlType?.ProgrammaticName;

                    if (profile.PreferredCapture != CaptureMethod.GuardedClipboard &&
                        focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out object pattern) &&
                        pattern is TextPattern textPattern)
                    {
                        var selectedRanges = textPattern.GetSelection();
                        var selectedRange = selectedRanges.FirstOrDefault();
                        if (selectedRange != null)
                        {
                            capturedText = selectedRange.GetText(-1) ?? string.Empty;
                            bounds = GetBounds(selectedRange);
                            directUiaCapture = !string.IsNullOrEmpty(capturedText);
                        }
                    }

                    if (bounds is null)
                    {
                        var rect = focusedElement.Current.BoundingRectangle;
                        if (!rect.IsEmpty && rect.Width > 0 && rect.Height > 0)
                        {
                            bounds = new ScreenBounds(rect.Left, rect.Top, rect.Width, rect.Height);
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                // UIA may be unavailable for an application. Clipboard capture remains
                // allowed only when the focused element identity was still obtained.
            }

            if (string.IsNullOrEmpty(capturedText))
            {
                if (focusedElement == null || string.IsNullOrWhiteSpace(runtimeId))
                {
                    throw new InvalidOperationException(
                        "Selection capture could not prove the focused field. No clipboard shortcut was sent.");
                }

                capturedText = await CaptureWithRestoredClipboardAsync(window.Handle, ct);
                directUiaCapture = false;
            }
#endif
        }

        if (string.IsNullOrEmpty(capturedText))
        {
            if (!OperatingSystem.IsWindows())
            {
                capturedText = TestFallbackText ?? "Sample selected text";
                runtimeId ??= "headless-field";
            }
            else
            {
                throw new InvalidOperationException(
                    "Selection capture failed: no selected text was returned by the active field.");
            }
        }

        string fieldId = runtimeId ?? $"window:{window.Handle.ToInt64():X}";
        var targetContext = new TargetContext(
            WindowHandle: window.Handle,
            ProcessId: window.ProcessId,
            ProcessName: window.ProcessName,
            AppTitle: window.Title,
            FieldId: fieldId,
            IsPassword: isPassword,
            IsElevated: window.IsElevated,
            AutomationRuntimeId: runtimeId,
            ControlType: controlType,
            OriginalSelectedText: capturedText);

        return new SelectionContext(
            capturedText,
            capturedText,
            targetContext,
            DateTime.UtcNow,
            directUiaCapture,
            bounds);
    }

#if HAS_WPF
    private async Task<string> CaptureWithRestoredClipboardAsync(IntPtr targetWindow, CancellationToken ct)
    {
        var snapshot = _clipboard.Capture();
        uint beforeCopy = Win32Native.GetClipboardSequenceNumber();

        if (Win32Native.GetForegroundWindow() != targetWindow)
        {
            throw new InvalidOperationException("The source window changed before selection capture.");
        }

        if (Win32Native.SendCtrlShortcut(Win32Native.VK_C) != 4)
        {
            throw new InvalidOperationException(
                $"Windows rejected the copy shortcut ({Marshal.GetLastWin32Error()}).");
        }

        uint afterCopy = beforeCopy;
        for (int attempt = 0; attempt < 12 && afterCopy == beforeCopy; attempt++)
        {
            await Task.Delay(25, ct);
            afterCopy = Win32Native.GetClipboardSequenceNumber();
        }

        if (afterCopy == beforeCopy)
        {
            throw new InvalidOperationException("The active application did not copy the selection.");
        }

        string selectedText = string.Empty;
        Exception? readError = null;
        try
        {
            selectedText = _clipboard.GetUnicodeText();
        }
        catch (Exception ex)
        {
            readError = ex;
        }

        if (Win32Native.GetClipboardSequenceNumber() != afterCopy)
        {
            throw new InvalidOperationException(
                "The clipboard changed during capture; Polishly left the newer clipboard untouched.");
        }

        try
        {
            _clipboard.Restore(snapshot);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Selection capture stopped because the original clipboard could not be restored.",
                ex);
        }

        if (readError != null)
        {
            throw new InvalidOperationException(
                "The copied selection could not be read as Unicode text.", readError);
        }
        if (string.IsNullOrEmpty(selectedText))
        {
            throw new InvalidOperationException("The copied selection did not contain Unicode text.");
        }

        return selectedText;
    }

    private static string FormatRuntimeId(int[]? runtimeId) =>
        runtimeId is { Length: > 0 } ? string.Join(".", runtimeId) : string.Empty;

    private static ScreenBounds? GetBounds(TextPatternRange range)
    {
        double[] rectangles = range.GetBoundingRectangles();
        if (rectangles.Length < 4)
        {
            return null;
        }

        double left = double.PositiveInfinity;
        double top = double.PositiveInfinity;
        double right = double.NegativeInfinity;
        double bottom = double.NegativeInfinity;
        for (int i = 0; i + 3 < rectangles.Length; i += 4)
        {
            double width = rectangles[i + 2];
            double height = rectangles[i + 3];
            if (width <= 0 || height <= 0) continue;
            left = Math.Min(left, rectangles[i]);
            top = Math.Min(top, rectangles[i + 1]);
            right = Math.Max(right, rectangles[i] + width);
            bottom = Math.Max(bottom, rectangles[i + 1] + height);
        }

        var result = new ScreenBounds(left, top, right - left, bottom - top);
        return result.IsUsable ? result : null;
    }
#endif
}
