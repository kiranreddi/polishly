namespace Polishly.Core.Models;

public record SelectionContext(
    string SelectedText,
    string? SurroundingText,
    TargetContext TargetContext,
    DateTime CapturedAt,
    bool DirectUiaCapture
)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(SelectedText);
}
