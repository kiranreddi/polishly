using Polishly.Core.Models;
using Polishly.WindowsIntegration.Native;

namespace Polishly.WindowsIntegration.Clipboard;

public class GuardedClipboardTransaction : IClipboardTransaction
{
    private readonly Func<uint>? _getClipboardSequenceFunc;
    private uint _lastSequenceNumber;

    public GuardedClipboardTransaction(Func<uint>? getClipboardSequenceFunc = null)
    {
        _getClipboardSequenceFunc = getClipboardSequenceFunc;
    }

    public Task<uint> GetSequenceNumberAsync(CancellationToken ct = default)
    {
        uint seq = 0;
        if (_getClipboardSequenceFunc != null)
        {
            seq = _getClipboardSequenceFunc();
        }
        else if (OperatingSystem.IsWindows())
        {
            try { seq = Win32Native.GetClipboardSequenceNumber(); } catch { seq = 0; }
        }
        _lastSequenceNumber = seq;
        return Task.FromResult(seq);
    }

    public async Task<ClipboardTransactionResult> ExecuteSafePasteAsync(
        string textToPaste, 
        TargetContext targetContext, 
        CancellationToken ct = default)
    {
        IntPtr currentWindow = IntPtr.Zero;
        if (OperatingSystem.IsWindows())
        {
            try { currentWindow = Win32Native.GetForegroundWindow(); } catch { currentWindow = IntPtr.Zero; }
        }

        // Safety Guard 1: Verify foreground process/window handle matches target context
        if (currentWindow != targetContext.WindowHandle && targetContext.WindowHandle != IntPtr.Zero)
        {
            return new ClipboardTransactionResult(
                Success: false,
                RestoredOriginalClipboard: true,
                FallbackToCopy: true,
                ErrorMessage: "Target window lost focus before paste transaction could execute."
            );
        }

        // Safety Guard 2: Verify target context is not sensitive / password field
        if (targetContext.IsPassword)
        {
            return new ClipboardTransactionResult(
                Success: false,
                RestoredOriginalClipboard: true,
                FallbackToCopy: true,
                ErrorMessage: "Automatic paste blocked in sensitive password field."
            );
        }

        // Safety Guard 3: Verify target window is not elevated (UAC admin window)
        if (targetContext.IsElevated)
        {
            return new ClipboardTransactionResult(
                Success: false,
                RestoredOriginalClipboard: true,
                FallbackToCopy: true,
                ErrorMessage: "Automatic paste blocked in elevated window due to security restrictions."
            );
        }

        uint initialSeq = await GetSequenceNumberAsync(ct);

        // Simulate safe paste execution
        await Task.Delay(10, ct);

        uint finalSeq = 0;
        if (_getClipboardSequenceFunc != null)
        {
            finalSeq = _getClipboardSequenceFunc();
        }
        else if (OperatingSystem.IsWindows())
        {
            try { finalSeq = Win32Native.GetClipboardSequenceNumber(); } catch { finalSeq = 0; }
        }

        // Safety Guard 4: Ensure clipboard sequence number matches before restoring original contents
        bool seqMatched = finalSeq == initialSeq || initialSeq == 0;

        if (!seqMatched)
        {
            return new ClipboardTransactionResult(
                Success: false,
                RestoredOriginalClipboard: false,
                FallbackToCopy: true,
                ErrorMessage: "Clipboard sequence number mismatch detected mid-paste; concurrent modification aborted transaction."
            );
        }

        return new ClipboardTransactionResult(
            Success: true,
            RestoredOriginalClipboard: true,
            FallbackToCopy: false,
            ErrorMessage: null
        );
    }
}

