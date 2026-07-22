using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Polishly.WindowsIntegration.Native;

namespace Polishly.App.Services;


public class TrayIconService : IDisposable
{
    public const uint NIM_ADD = 0x00000000;
    public const uint NIM_MODIFY = 0x00000001;
    public const uint NIM_DELETE = 0x00000002;
    public const uint NIF_ICON = 0x00000002;
    public const uint NIF_TIP = 0x00000004;
    public const uint NIF_INFO = 0x00000010;

    public const uint WM_TRAYICON = 0x0401;

    private bool _isVisible;
    private bool _isPaused;
    private IntPtr _windowHandle = IntPtr.Zero;

    public bool IsVisible => _isVisible;
    public bool IsPaused => _isPaused;

    public IReadOnlyList<string> ContextMenuItems { get; } = new[] { "Rewrite", "Pause", "Settings", "Exit" };

    public event EventHandler? RewriteRequested;
    public event EventHandler<bool>? PauseToggled;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public void Initialize(IntPtr hWnd = default)
    {
        _windowHandle = hWnd;
        _isVisible = true;

        if (OperatingSystem.IsWindows())
        {
            NOTIFYICONDATA nid = CreateNotifyData(_windowHandle, "Polishly Companion", "Polishly AI Assistant is active.");
            Shell_NotifyIcon(NIM_ADD, ref nid);
        }
    }

    public void ShowContextMenu()
    {
        if (!OperatingSystem.IsWindows()) return;

        IntPtr hMenu = Win32Native.CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        try
        {
            Win32Native.AppendMenu(hMenu, Win32Native.MF_STRING, 1001, "Rewrite Selection (Ctrl+Shift+P)");
            Win32Native.AppendMenu(hMenu, Win32Native.MF_STRING, 1002, _isPaused ? "Resume Polishly" : "Pause Polishly");
            Win32Native.AppendMenu(hMenu, Win32Native.MF_STRING, 1003, "Settings...");
            Win32Native.AppendMenu(hMenu, Win32Native.MF_STRING, 1004, "Exit");

            Win32Native.GetCursorPos(out var pt);
            if (_windowHandle != IntPtr.Zero)
            {
                Win32Native.SetForegroundWindow(_windowHandle);
            }

            uint cmd = Win32Native.TrackPopupMenuEx(hMenu, Win32Native.TPM_LEFTALIGN | Win32Native.TPM_RETURNCMD, pt.X, pt.Y, _windowHandle, IntPtr.Zero);
            switch (cmd)
            {
                case 1001:
                    TriggerContextMenuAction("rewrite");
                    break;
                case 1002:
                    TriggerContextMenuAction("pause");
                    break;
                case 1003:
                    TriggerContextMenuAction("settings");
                    break;
                case 1004:
                    TriggerContextMenuAction("exit");
                    break;
            }
        }
        finally
        {
            Win32Native.DestroyMenu(hMenu);
        }
    }

    public void ShowTrayNotification(string title, string message)
    {
        if (!_isVisible) return;

        if (OperatingSystem.IsWindows())
        {
            NOTIFYICONDATA nid = CreateNotifyData(_windowHandle, title, message);
            nid.uFlags |= NIF_INFO;
            nid.szInfo = message;
            nid.szInfoTitle = title;
            Shell_NotifyIcon(NIM_MODIFY, ref nid);
        }
    }

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        PauseToggled?.Invoke(this, _isPaused);
    }

    public void TriggerContextMenuAction(string actionName)
    {
        switch (actionName?.ToLowerInvariant())
        {
            case "rewrite":
                RewriteRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "pause":
                TogglePause();
                break;
            case "settings":
                SettingsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "exit":
                ExitRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    public void Dispose()
    {
        if (_isVisible)
        {
            _isVisible = false;
            if (OperatingSystem.IsWindows())
            {
                NOTIFYICONDATA nid = CreateNotifyData(_windowHandle, "Polishly Companion", string.Empty);
                Shell_NotifyIcon(NIM_DELETE, ref nid);
            }
        }

        GC.SuppressFinalize(this);
    }

    private static NOTIFYICONDATA CreateNotifyData(IntPtr hWnd, string title, string tip)
    {
        var nid = new NOTIFYICONDATA();
        nid.cbSize = (uint)Marshal.SizeOf(nid);
        nid.hWnd = hWnd;
        nid.uID = 1;
        nid.uFlags = NIF_ICON | NIF_TIP;
        nid.uCallbackMessage = WM_TRAYICON;
        nid.szTip = tip;
        return nid;
    }


    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);
}

public class NavigationService
{
    public void OpenSettings() { }
    public void OpenOnboarding() { }
    public void ShowPopup() { }
}

public class ThemeService
{
    public string CurrentTheme { get; set; } = "System";

    public void ApplyTheme(string themeName)
    {
        CurrentTheme = themeName;
    }
}
