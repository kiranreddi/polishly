using System.Runtime.InteropServices;
using Polishly.Core.Models;
using Polishly.WindowsIntegration.Native;
#if HAS_WPF
using System.Windows.Automation;
#endif

namespace Polishly.WindowsIntegration.Clipboard;

public class GuardedClipboardTransaction : IClipboardTransaction
{
    private readonly Func<uint>? _getClipboardSequenceFunc;
    private readonly ClipboardSnapshotService _clipboard = new();

    public GuardedClipboardTransaction(Func<uint>? getClipboardSequenceFunc = null)
    {
        _getClipboardSequenceFunc = getClipboardSequenceFunc;
    }

    public Task<uint> GetSequenceNumberAsync(CancellationToken ct = default)
    {
        uint sequence = _getClipboardSequenceFunc?.Invoke() ??
                        (OperatingSystem.IsWindows()
                            ? Win32Native.GetClipboardSequenceNumber()
                            : 0);
        return Task.FromResult(sequence);
    }

    public async Task<ClipboardTransactionResult> ExecuteSafePasteAsync(
        string textToPaste,
        TargetContext targetContext,
        CancellationToken ct = default)
    {
        if (targetContext.IsPassword)
        {
            return Failure("Automatic paste is blocked in password fields.", restored: true);
        }

        if (targetContext.IsElevated)
        {
            return Failure(
                "Automatic paste is blocked in an elevated window due to Windows security restrictions.",
                restored: true);
        }

        if (_getClipboardSequenceFunc != null)
        {
            return await ExecuteSimulationAsync(ct);
        }

        if (!OperatingSystem.IsWindows())
        {
            if (targetContext.WindowHandle != IntPtr.Zero)
            {
                return Failure(
                    "Target window lost focus before paste transaction could execute.",
                    restored: true);
            }
            return new ClipboardTransactionResult(true, true, false);
        }

        string? targetError = ValidateExactTarget(targetContext);
        if (targetError != null)
        {
            return Failure(targetError, restored: true);
        }

        ClipboardSnapshotService.Snapshot snapshot;
        try
        {
            snapshot = _clipboard.Capture();
        }
        catch (Exception ex)
        {
            return Failure($"Clipboard could not be safely materialized: {ex.Message}", restored: false);
        }

        uint sequenceBeforeWrite = Win32Native.GetClipboardSequenceNumber();
        uint sequenceAfterWrite;
        try
        {
            _clipboard.SetUnicodeText(textToPaste);
            sequenceAfterWrite = Win32Native.GetClipboardSequenceNumber();
            if (sequenceAfterWrite == sequenceBeforeWrite)
            {
                try
                {
                    _clipboard.Restore(snapshot);
                    return Failure(
                        "Windows did not confirm Polishly clipboard ownership.",
                        restored: true);
                }
                catch (Exception restoreError)
                {
                    return Failure(
                        $"Windows did not confirm Polishly clipboard ownership and restore failed: {restoreError.Message}",
                        restored: false);
                }
            }
        }
        catch (Exception ex)
        {
            return Failure($"Polishly could not write the temporary clipboard text: {ex.Message}", restored: false);
        }

        targetError = ValidateExactTarget(targetContext);
        if (targetError != null)
        {
            return RestoreThenFail(snapshot, sequenceAfterWrite, targetError);
        }

        uint sent = Win32Native.SendCtrlShortcut(Win32Native.VK_V);
        if (sent != 4)
        {
            return RestoreThenFail(
                snapshot,
                sequenceAfterWrite,
                $"Windows rejected the paste shortcut ({Marshal.GetLastWin32Error()}).");
        }

        await Task.Delay(80, ct);

        uint sequenceAfterPaste = Win32Native.GetClipboardSequenceNumber();
        if (sequenceAfterPaste != sequenceAfterWrite)
        {
            return new ClipboardTransactionResult(
                Success: false,
                RestoredOriginalClipboard: false,
                FallbackToCopy: false,
                ErrorMessage:
                    "The clipboard changed during replacement. Polishly left the newer clipboard untouched.");
        }

        try
        {
            _clipboard.Restore(snapshot);
        }
        catch (Exception ex)
        {
            return new ClipboardTransactionResult(
                Success: false,
                RestoredOriginalClipboard: false,
                FallbackToCopy: false,
                ErrorMessage:
                    $"Text was pasted, but the original clipboard could not be restored: {ex.Message}");
        }

        return new ClipboardTransactionResult(
            Success: true,
            RestoredOriginalClipboard: true,
            FallbackToCopy: false);
    }

    private async Task<ClipboardTransactionResult> ExecuteSimulationAsync(CancellationToken ct)
    {
        uint before = _getClipboardSequenceFunc!.Invoke();
        await Task.Delay(10, ct);
        uint after = _getClipboardSequenceFunc.Invoke();
        if (before != 0 && after != before)
        {
            return Failure(
                "Clipboard sequence number mismatch detected; concurrent modification aborted the transaction.",
                restored: false);
        }

        return new ClipboardTransactionResult(true, true, false);
    }

    private ClipboardTransactionResult RestoreThenFail(
        ClipboardSnapshotService.Snapshot snapshot,
        uint ownedSequence,
        string error)
    {
        if (Win32Native.GetClipboardSequenceNumber() != ownedSequence)
        {
            return Failure(
                $"{error} The clipboard also changed, so newer clipboard content was left untouched.",
                restored: false);
        }

        try
        {
            _clipboard.Restore(snapshot);
            return Failure(error, restored: true);
        }
        catch (Exception ex)
        {
            return Failure($"{error} Clipboard restore also failed: {ex.Message}", restored: false);
        }
    }

    private static ClipboardTransactionResult Failure(string error, bool restored) =>
        new(false, restored, true, error);

    private static string? ValidateExactTarget(TargetContext target)
    {
        IntPtr foreground = Win32Native.GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground != target.WindowHandle)
        {
            return "The original window lost focus. The rewrite was copied instead.";
        }

        Win32Native.GetWindowThreadProcessId(foreground, out uint processId);
        if (processId != target.ProcessId)
        {
            return "The foreground process no longer matches the captured source.";
        }

#if HAS_WPF
        if (string.IsNullOrWhiteSpace(target.AutomationRuntimeId))
        {
            return "The original field identity is unavailable; automatic paste is not safe.";
        }

        try
        {
            AutomationElement? focused = AutomationElement.FocusedElement;
            if (focused == null || focused.Current.IsPassword)
            {
                return "The original editable field is no longer focused or is sensitive.";
            }

            string runtimeId = string.Join(".", focused.GetRuntimeId());
            if (!string.Equals(runtimeId, target.AutomationRuntimeId, StringComparison.Ordinal))
            {
                return "The focused field changed after capture. Automatic paste was cancelled.";
            }
        }
        catch
        {
            return "The original field could not be revalidated. Automatic paste was cancelled.";
        }
#endif

        return null;
    }
}
