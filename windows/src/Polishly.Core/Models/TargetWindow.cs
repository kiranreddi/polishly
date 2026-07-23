namespace Polishly.Core.Models;

public record TargetWindow(
    IntPtr Handle,
    int ProcessId,
    string ProcessName,
    string Title,
    bool IsElevated
);
