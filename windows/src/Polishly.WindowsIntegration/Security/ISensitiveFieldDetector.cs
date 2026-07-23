using Polishly.Core.Models;

namespace Polishly.WindowsIntegration.Security;

public record SensitiveFieldStatus(
    bool IsSensitive,
    string Reason
)
{
    public static SensitiveFieldStatus Safe => new(false, "Field is safe");
}

public interface ISensitiveFieldDetector
{
    SensitiveFieldStatus IsSensitiveField(TargetWindow window, object? automationElement = null);
}
