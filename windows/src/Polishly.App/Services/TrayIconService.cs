using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Polishly.WindowsIntegration.Native;

namespace Polishly.App.Services;


public class TrayIconService : IDisposable
{
    public const uint NIM_ADD = 0x00000000;
    public const uint NIM_MODIFY = 0x00000001;
    public const uint NIM_DELETE = 0x00000002;
    public const uint NIF_MESSAGE = 0x00000001;
    public const uint NIF_ICON = 0x00000002;
    public const uint NIF_TIP = 0x00000004;
    public const uint NIF_INFO = 0x00000010;
    public const uint IMAGE_ICON = 1;
    public const uint LR_DEFAULTSIZE = 0x00000040;
    public const uint LR_LOADFROMFILE = 0x00000010;

    public const uint WM_TRAYICON = 0x0401;
    public const int IDI_APPLICATION = 32512;

    private bool _isVisible;
    private bool _isPaused;
    private IntPtr _windowHandle = IntPtr.Zero;
    private IntPtr _iconHandle = IntPtr.Zero;
    private bool _ownsIconHandle;

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

        if (OperatingSystem.IsWindows())
        {
            EnsureIconLoaded();
            NOTIFYICONDATA nid = CreateNotifyData(_windowHandle, "Polishly Companion", "Polishly AI Assistant is active.");
            _isVisible = Shell_NotifyIcon(NIM_ADD, ref nid);
        }
        else
        {
            _isVisible = true;
        }
    }


    public void ProcessWindowMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYICON)
        {
            int mouseMsg = lParam.ToInt32() & 0xFFFF;
            if (mouseMsg == 0x0205 || mouseMsg == 0x0202 || mouseMsg == 0x0203 || mouseMsg == 0x0206)
            {
                ShowContextMenu();
            }
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

        if (_ownsIconHandle && _iconHandle != IntPtr.Zero)
        {
            DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
            _ownsIconHandle = false;
        }

        GC.SuppressFinalize(this);
    }

    private void EnsureIconLoaded()
    {
        if (_iconHandle != IntPtr.Zero || !OperatingSystem.IsWindows()) return;

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Polishly.ico");
        if (File.Exists(iconPath))
        {
            _iconHandle = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
            _ownsIconHandle = _iconHandle != IntPtr.Zero;
        }

        if (_iconHandle == IntPtr.Zero)
        {
            _iconHandle = LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
        }
    }

    private NOTIFYICONDATA CreateNotifyData(IntPtr hWnd, string title, string tip)
    {
        EnsureIconLoaded();
        var nid = new NOTIFYICONDATA();
        nid.cbSize = (uint)Marshal.SizeOf(nid);
        nid.hWnd = hWnd;
        nid.uID = 1;
        nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        nid.uCallbackMessage = WM_TRAYICON;
        nid.hIcon = _iconHandle;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

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
