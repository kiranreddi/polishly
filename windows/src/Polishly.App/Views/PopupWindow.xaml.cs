using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using Polishly.App.ViewModels;

namespace Polishly.App.Views;

public partial class PopupWindow : Window
{
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_NOACTIVATE = 0x08000000L;
    public const long WS_EX_TOOLWINDOW = 0x00000080L;
#if HAS_WPF
    private const int EscapeHotkeyId = 9002;
    private const int WmHotkey = 0x0312;
    private const int WhMouseLl = 14;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmRightButtonDown = 0x0204;
    private HwndSource? _source;
    private IntPtr _mouseHook;
    private LowLevelMouseProc? _mouseProc;
#endif

    public static long GetNonActivatingExStyleFlags() => WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;

    public PopupWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
#if HAS_WPF
        Loaded += (_, _) =>
        {
            if (SystemParameters.HighContrast)
            {
                PopupBorder.Background = SystemColors.WindowBrush;
                PopupBorder.BorderBrush = SystemColors.WindowTextBrush;
            }
        };
        PreviewKeyDown += OnPreviewKeyDown;
        Closed += (_, _) => RemoveInteractionHooks();
#endif
    }

    public PopupWindow(PopupViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

#if !HAS_WPF
    private void InitializeComponent()
    {
    }
#endif


    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyNonActivatingStyle();
    }

    public void ApplyNonActivatingStyle()
    {
#if HAS_WPF
        var helper = new WindowInteropHelper(this);
        IntPtr hWnd = helper.Handle;
        if (hWnd != IntPtr.Zero && OperatingSystem.IsWindows())
        {
            long exStyle = GetWindowLongPtrInternal(hWnd, GWL_EXSTYLE);
            exStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            SetWindowLongPtrInternal(hWnd, GWL_EXSTYLE, (IntPtr)exStyle);
            _source = HwndSource.FromHwnd(hWnd);
            _source?.AddHook(WindowHook);
            RegisterHotKey(hWnd, EscapeHotkeyId, 0, 0x1B);

            _mouseProc = MouseHook;
            _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, IntPtr.Zero, 0);
        }
#endif
    }

#if HAS_WPF
    private IntPtr WindowHook(
        IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == EscapeHotkeyId)
        {
            handled = true;
            (DataContext as PopupViewModel)?.HandleEscape();
        }
        return IntPtr.Zero;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            (DataContext as PopupViewModel)?.HandleEscape();
        }
    }

    private IntPtr MouseHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 &&
            (wParam.ToInt32() == WmLeftButtonDown ||
             wParam.ToInt32() == WmRightButtonDown))
        {
            var point = Marshal.PtrToStructure<MouseHookData>(lParam).Point;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out NativeRect rect) &&
                (point.X < rect.Left || point.X >= rect.Right ||
                 point.Y < rect.Top || point.Y >= rect.Bottom))
            {
                Dispatcher.BeginInvoke(() =>
                    (DataContext as PopupViewModel)?.HandleClickOutside());
            }
        }
        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void RemoveInteractionHooks()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero) UnregisterHotKey(hwnd, EscapeHotkeyId);
        if (_source != null)
        {
            _source.RemoveHook(WindowHook);
            _source = null;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        _mouseProc = null;
    }

    private delegate IntPtr LowLevelMouseProc(
        int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        IntPtr hWnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int idHook, LowLevelMouseProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);
#endif

    internal static long GetWindowLongPtrInternal(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr64(hWnd, nIndex).ToInt64();
        return GetWindowLong32(hWnd, nIndex);
    }

    internal static IntPtr SetWindowLongPtrInternal(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        return (IntPtr)SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32());
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
