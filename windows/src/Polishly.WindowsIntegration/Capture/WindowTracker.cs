using System.Diagnostics;
using System.Text;
using Polishly.Core.Models;
using Polishly.WindowsIntegration.Native;

namespace Polishly.WindowsIntegration.Capture;

public class WindowTracker
{
    private readonly Security.ElevationDetector _elevationDetector = new();

    public TargetWindow GetForegroundWindowInfo()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new TargetWindow(IntPtr.Zero, 0, "unknown", "Unknown", false);
        }

        try
        {
            var hWnd = Win32Native.GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
            {
                return new TargetWindow(IntPtr.Zero, 0, "unknown", "Unknown", false);
            }

            Win32Native.GetWindowThreadProcessId(hWnd, out var processId);
            string processName = "unknown";
            bool isElevated = _elevationDetector.IsElevatedProcess((int)processId);

            try
            {
                using var proc = Process.GetProcessById((int)processId);
                processName = proc.ProcessName;
            }
            catch
            {
                // Process lookup failed or access denied
            }

            var sb = new StringBuilder(256);
            Win32Native.GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            return new TargetWindow(hWnd, (int)processId, processName, title, isElevated);
        }
        catch
        {
            return new TargetWindow(IntPtr.Zero, 0, "unknown", "Unknown", false);
        }
    }
}
