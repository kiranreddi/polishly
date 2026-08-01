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
            // Cross-platform headless stub: emulate a Notepad-like target for local demos/tests.
            return new TargetWindow(IntPtr.Zero, 0, "notepad", "Untitled - Notepad", false);
        }

        try
        {
            var hWnd = Win32Native.GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
            {
                return new TargetWindow(
                    IntPtr.Zero, 0, "unknown", "Unknown", IsElevated: true);
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
            // Failure to identify the source is a security boundary. Mark it
            // conservatively so capture refuses to transmit selected text.
            return new TargetWindow(
                IntPtr.Zero, 0, "unknown", "Unknown", IsElevated: true);
        }
    }
}
