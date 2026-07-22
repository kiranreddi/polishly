using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Polishly.App.Services;

public class TrayIconService : IDisposable
{
    public const uint NIM_ADD = 0x00000000;
    public const uint NIM_MODIFY = 0x00000001;
    public const uint NIM_DELETE = 0x00000002;
    public const uint NIF_ICON = 0x00000002;
    public const uint NIF_TIP = 0x00000004;
    public const uint NIF_INFO = 0x00000010;

    private bool _isVisible;
    private bool _isPaused;

    public bool IsVisible => _isVisible;
    public bool IsPaused => _isPaused;

    public IReadOnlyList<string> ContextMenuItems { get; } = new[] { "Rewrite", "Pause", "Settings", "Exit" };

    public event EventHandler? RewriteRequested;
    public event EventHandler<bool>? PauseToggled;
    public event EventHandler? SettingsRequested;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        _isVisible = true;

        if (OperatingSystem.IsWindows())
        {
            NOTIFYICONDATA nid = CreateNotifyData("Polishly Companion", "Polishly AI Assistant is active.");
            Shell_NotifyIcon(NIM_ADD, ref nid);
        }
    }

    public void ShowTrayNotification(string title, string message)
    {
        if (!_isVisible) return;

        if (OperatingSystem.IsWindows())
        {
            NOTIFYICONDATA nid = CreateNotifyData(title, message);
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
                NOTIFYICONDATA nid = CreateNotifyData("Polishly Companion", string.Empty);
                Shell_NotifyIcon(NIM_DELETE, ref nid);
            }
        }

        GC.SuppressFinalize(this);
    }

    private static NOTIFYICONDATA CreateNotifyData(string title, string tip)
    {
        var nid = new NOTIFYICONDATA();
        nid.cbSize = (uint)Marshal.SizeOf(nid);
        nid.uFlags = NIF_ICON | NIF_TIP;
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
