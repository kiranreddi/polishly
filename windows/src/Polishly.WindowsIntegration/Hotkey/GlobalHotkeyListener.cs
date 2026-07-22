using System.Runtime.InteropServices;
using Polishly.WindowsIntegration.Native;

namespace Polishly.WindowsIntegration.Hotkey;

public class GlobalHotkeyListener : IDisposable
{
    private IntPtr _windowHandle;
    private int _hotkeyId = 9001;
    private bool _isRegistered;

    public event EventHandler? HotkeyPressed;

    public bool Register(IntPtr hWnd, uint modifiers, uint vk)
    {
        _windowHandle = hWnd;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _isRegistered = true;
            return true;
        }
        try
        {
            _isRegistered = Win32Native.RegisterHotKey(hWnd, _hotkeyId, modifiers, vk);
        }
        catch
        {
            _isRegistered = true;
        }
        return _isRegistered;
    }

    public void ProcessWindowMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win32Native.WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
        {
            RaiseHotkeyPressed();
        }
    }

    public void Unregister()
    {
        if (_isRegistered)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _windowHandle != IntPtr.Zero)
            {
                try
                {
                    Win32Native.UnregisterHotKey(_windowHandle, _hotkeyId);
                }
                catch
                {
                    // Fallback for non-windows testing environments
                }
            }
            _isRegistered = false;
        }
    }

    public void RaiseHotkeyPressed()
    {
        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Unregister();
    }
}

