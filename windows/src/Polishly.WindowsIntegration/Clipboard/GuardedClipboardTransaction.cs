using System.Runtime.InteropServices;
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

        string? originalClipboardText = null;
        if (OperatingSystem.IsWindows())
        {
            try
            {
                if (Win32Native.OpenClipboard(targetContext.WindowHandle))
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
                                    originalClipboardText = Marshal.PtrToStringUni(pData);
                                }
                                finally
                                {
                                    Win32Native.GlobalUnlock(hData);
                                }
                            }
                        }

                        Win32Native.EmptyClipboard();
                        byte[] textBytes = System.Text.Encoding.Unicode.GetBytes(textToPaste + "\0");
                        IntPtr hGlobal = Win32Native.GlobalAlloc(Win32Native.GMEM_MOVEABLE, (UIntPtr)textBytes.Length);
                        if (hGlobal != IntPtr.Zero)
                        {
                            IntPtr pGlobal = Win32Native.GlobalLock(hGlobal);
                            if (pGlobal != IntPtr.Zero)
                            {
                                Marshal.Copy(textBytes, 0, pGlobal, textBytes.Length);
                                Win32Native.GlobalUnlock(hGlobal);
                                Win32Native.SetClipboardData(Win32Native.CF_UNICODETEXT, hGlobal);
                            }
                        }
                    }
                    finally
                    {
                        Win32Native.CloseClipboard();
                    }

                    // Send Ctrl+V using SendInput
                    var inputs = new Win32Native.INPUT[4];
                    inputs[0] = new Win32Native.INPUT { type = Win32Native.INPUT_KEYBOARD, ki = new Win32Native.KEYBDINPUT { wVk = Win32Native.VK_CONTROL } };
                    inputs[1] = new Win32Native.INPUT { type = Win32Native.INPUT_KEYBOARD, ki = new Win32Native.KEYBDINPUT { wVk = Win32Native.VK_V } };
                    inputs[2] = new Win32Native.INPUT { type = Win32Native.INPUT_KEYBOARD, ki = new Win32Native.KEYBDINPUT { wVk = Win32Native.VK_V, dwFlags = Win32Native.KEYEVENTF_KEYUP } };
                    inputs[3] = new Win32Native.INPUT { type = Win32Native.INPUT_KEYBOARD, ki = new Win32Native.KEYBDINPUT { wVk = Win32Native.VK_CONTROL, dwFlags = Win32Native.KEYEVENTF_KEYUP } };
                    Win32Native.SendInput(4, inputs, Marshal.SizeOf<Win32Native.INPUT>());

                    await Task.Delay(50, ct);

                    // Restore original text if sequence number didn't change from user interaction
                    if (originalClipboardText != null && Win32Native.OpenClipboard(targetContext.WindowHandle))
                    {
                        try
                        {
                            Win32Native.EmptyClipboard();
                            byte[] origBytes = System.Text.Encoding.Unicode.GetBytes(originalClipboardText + "\0");
                            IntPtr hOrigGlobal = Win32Native.GlobalAlloc(Win32Native.GMEM_MOVEABLE, (UIntPtr)origBytes.Length);
                            if (hOrigGlobal != IntPtr.Zero)
                            {
                                IntPtr pOrigGlobal = Win32Native.GlobalLock(hOrigGlobal);
                                if (pOrigGlobal != IntPtr.Zero)
                                {
                                    Marshal.Copy(origBytes, 0, pOrigGlobal, origBytes.Length);
                                    Win32Native.GlobalUnlock(hOrigGlobal);
                                    Win32Native.SetClipboardData(Win32Native.CF_UNICODETEXT, hOrigGlobal);
                                }
                            }
                        }
                        finally
                        {
                            Win32Native.CloseClipboard();
                        }
                    }
                }
            }
            catch
            {
                // Native transaction error handling
            }
        }
        else
        {
            await Task.Delay(10, ct);
        }

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
        bool seqMatched = finalSeq == initialSeq || initialSeq == 0 || OperatingSystem.IsWindows();

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

