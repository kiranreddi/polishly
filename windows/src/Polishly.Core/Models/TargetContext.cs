namespace Polishly.Core.Models;

public record TargetContext(
    IntPtr WindowHandle,
    int ProcessId,
    string ProcessName,
    string AppTitle,
    string FieldId,
    bool IsPassword,
    bool IsElevated
)
{
    public static TargetContext Unknown => new(
        WindowHandle: IntPtr.Zero,
        ProcessId: 0,
        ProcessName: "unknown",
        AppTitle: "Unknown Window",
        FieldId: "unknown_field",
        IsPassword: false,
        IsElevated: false
    );
}
