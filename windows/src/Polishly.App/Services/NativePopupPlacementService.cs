using Polishly.Core.Models;
using Polishly.WindowsIntegration.Native;
#if HAS_WPF
using System.Windows;
using System.Windows.Interop;
#endif

namespace Polishly.App.Services;

/// <summary>
/// Converts UI Automation's physical screen coordinates into a monitor-aware,
/// non-activating popup placement. All calculations remain in physical pixels
/// until SetWindowPos, which avoids mixed-DPI and negative-coordinate drift.
/// </summary>
public sealed class NativePopupPlacementService
{
#if HAS_WPF
    private readonly Window _popup;
    private readonly TargetContext _target;
    private readonly ScreenBounds? _selectionBounds;
    private PopupPositioner? _positioner;
    private ScreenRect _anchor;
    private ScreenRect _workArea;

    public NativePopupPlacementService(
        Window popup,
        TargetContext target,
        ScreenBounds? selectionBounds)
    {
        _popup = popup;
        _target = target;
        _selectionBounds = selectionBounds;
    }

    public bool Position()
    {
        if (!OperatingSystem.IsWindows()) return false;

        var anchorRect = ResolveAnchor();
        var nativeAnchor = new Win32Native.RECT
        {
            Left = (int)Math.Floor(anchorRect.Left),
            Top = (int)Math.Floor(anchorRect.Top),
            Right = (int)Math.Ceiling(anchorRect.Right),
            Bottom = (int)Math.Ceiling(anchorRect.Bottom)
        };

        IntPtr monitor = Win32Native.MonitorFromRect(
            ref nativeAnchor,
            Win32Native.MONITOR_DEFAULTTONEAREST);
        var monitorInfo = new Win32Native.MONITORINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32Native.MONITORINFO>()
        };
        if (monitor == IntPtr.Zero || !Win32Native.GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        uint dpi = _target.WindowHandle != IntPtr.Zero
            ? Win32Native.GetDpiForWindow(_target.WindowHandle)
            : 96;
        double scale = PopupPositioner.DpiToScale((int)(dpi == 0 ? 96 : dpi));
        _positioner ??= new PopupPositioner(scale);
        _positioner.DpiScale = scale;
        _anchor = anchorRect;
        _workArea = new ScreenRect(
            monitorInfo.rcWork.Left,
            monitorInfo.rcWork.Top,
            monitorInfo.rcWork.Right - monitorInfo.rcWork.Left,
            monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top);

        double widthPixels = Math.Max(320, _popup.ActualWidth) * scale;
        double heightPixels = Math.Max(80, _popup.ActualHeight) * scale;
        ScreenPoint point = _positioner.CalculatePosition(
            _anchor, _workArea, widthPixels, heightPixels);
        return Apply(point);
    }

    public void RepositionForCurrentSize()
    {
        if (_positioner == null) return;
        uint dpi = _target.WindowHandle != IntPtr.Zero
            ? Win32Native.GetDpiForWindow(_target.WindowHandle)
            : 96;
        double scale = PopupPositioner.DpiToScale((int)(dpi == 0 ? 96 : dpi));
        ScreenPoint point = _positioner.RecalculateForStreamingHeight(
            _anchor,
            _workArea,
            Math.Max(320, _popup.ActualWidth) * scale,
            Math.Max(80, _popup.ActualHeight) * scale);
        Apply(point);
    }

    private bool Apply(ScreenPoint point)
    {
        IntPtr popupHandle = new WindowInteropHelper(_popup).Handle;
        return popupHandle != IntPtr.Zero &&
               Win32Native.SetWindowPos(
                   popupHandle,
                   IntPtr.Zero,
                   (int)Math.Round(point.X),
                   (int)Math.Round(point.Y),
                   0,
                   0,
                   Win32Native.SWP_NOSIZE |
                   Win32Native.SWP_NOZORDER |
                   Win32Native.SWP_NOACTIVATE);
    }

    private ScreenRect ResolveAnchor()
    {
        if (_selectionBounds is { IsUsable: true } selection)
        {
            return new ScreenRect(selection.Left, selection.Top, selection.Width, selection.Height);
        }

        if (_target.WindowHandle != IntPtr.Zero &&
            Win32Native.GetWindowRect(_target.WindowHandle, out var windowRect))
        {
            double width = Math.Max(1, windowRect.Right - windowRect.Left);
            double height = Math.Max(1, windowRect.Bottom - windowRect.Top);
            return new ScreenRect(
                windowRect.Left + Math.Min(24, width / 2),
                windowRect.Top + Math.Min(48, height / 2),
                Math.Max(1, width - 48),
                24);
        }

        if (Win32Native.GetCursorPos(out var cursor))
        {
            return new ScreenRect(cursor.X, cursor.Y, 1, 1);
        }

        throw new InvalidOperationException("No safe popup anchor is available.");
    }
#else
    public NativePopupPlacementService(object popup, TargetContext target, ScreenBounds? selectionBounds) { }
    public bool Position() => false;
    public void RepositionForCurrentSize() { }
#endif
}
