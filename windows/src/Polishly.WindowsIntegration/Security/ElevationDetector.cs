using System;
using System.Runtime.InteropServices;
using Polishly.WindowsIntegration.Native;

namespace Polishly.WindowsIntegration.Security;

public class ElevationDetector
{
    public bool IsElevatedProcess(int processId)
    {
        if (processId <= 0) return false;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        IntPtr hProcess = IntPtr.Zero;
        IntPtr hToken = IntPtr.Zero;
        try
        {
            hProcess = Win32Native.OpenProcess(Win32Native.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)processId);
            if (hProcess == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode == 5 || errorCode == 0x05)
                {
                    return true;
                }
                return false;
            }

            if (!Win32Native.OpenProcessToken(hProcess, Win32Native.TOKEN_QUERY, out hToken))
            {
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode == 5 || errorCode == 0x05)
                {
                    return true;
                }
                return false;
            }

            int elevationSize = Marshal.SizeOf<Win32Native.TOKEN_ELEVATION>();
            IntPtr elevationPtr = Marshal.AllocHGlobal(elevationSize);
            try
            {
                if (Win32Native.GetTokenInformation(hToken, Win32Native.TokenElevation, elevationPtr, (uint)elevationSize, out _))
                {
                    var elevation = Marshal.PtrToStructure<Win32Native.TOKEN_ELEVATION>(elevationPtr);
                    return elevation.TokenIsElevated != 0;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(elevationPtr);
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            if (hToken != IntPtr.Zero) Win32Native.CloseHandle(hToken);
            if (hProcess != IntPtr.Zero) Win32Native.CloseHandle(hProcess);
        }

        return false;
    }
}

