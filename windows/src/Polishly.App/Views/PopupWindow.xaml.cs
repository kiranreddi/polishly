using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Polishly.App.ViewModels;

namespace Polishly.App.Views;

public partial class PopupWindow : Window
{
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_NOACTIVATE = 0x08000000L;
    public const long WS_EX_TOOLWINDOW = 0x00000080L;

    public static long GetNonActivatingExStyleFlags() => WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;

    public PopupWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    public PopupWindow(PopupViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void InitializeComponent()
    {
    }

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
        }
#endif
    }

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
