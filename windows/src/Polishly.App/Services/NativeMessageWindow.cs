using System;
using System.Runtime.InteropServices;
#if HAS_WPF
using System.Windows.Interop;
#endif

namespace Polishly.App.Services;

public class NativeMessageWindow : IDisposable
{
    public IntPtr Handle { get; private set; } = IntPtr.Zero;

#if HAS_WPF
    private HwndSource? _hwndSource;
#endif

    public event Action<int, IntPtr, IntPtr>? MessageReceived;

    public NativeMessageWindow()
    {
        InitializeWindow();
    }

    private void InitializeWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            Handle = (IntPtr)12345;
            return;
        }

#if HAS_WPF
        try
        {
            var parameters = new HwndSourceParameters("PolishlyNativeMessageWindow")
            {
                WindowStyle = 0,
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                ParentWindow = (IntPtr)(-3) // HWND_MESSAGE
            };

            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(HwndSourceHook);
            Handle = _hwndSource.Handle;
        }
        catch
        {
            Handle = (IntPtr)12345;
        }
#else
        Handle = (IntPtr)12345;
#endif
    }

#if HAS_WPF
    private IntPtr HwndSourceHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        MessageReceived?.Invoke(msg, wParam, lParam);
        return IntPtr.Zero;
    }
#endif

    public void ProcessMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        MessageReceived?.Invoke(msg, wParam, lParam);
    }

    public void Dispose()
    {
#if HAS_WPF
        if (_hwndSource != null && !_hwndSource.IsDisposed)
        {
            _hwndSource.RemoveHook(HwndSourceHook);
            _hwndSource.Dispose();
            _hwndSource = null;
        }
#endif
        Handle = IntPtr.Zero;
    }
}
