namespace Polishly.Core.Models;

public record SelectionContext(
    string SelectedText,
    string? SurroundingText,
    TargetContext TargetContext,
    DateTime CapturedAt,
    bool DirectUiaCapture,
    ScreenBounds? SelectionBounds = null
)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(SelectedText);
}

public readonly record struct ScreenBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public bool IsUsable => Width > 0 && Height > 0 &&
                            double.IsFinite(Left) && double.IsFinite(Top) &&
                            double.IsFinite(Width) && double.IsFinite(Height);
}
