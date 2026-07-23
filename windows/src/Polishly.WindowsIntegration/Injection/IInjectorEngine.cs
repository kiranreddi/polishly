using Polishly.Core.Capabilities;
using Polishly.Core.Models;

namespace Polishly.WindowsIntegration.Injection;

public record InjectionResult(
    bool Success,
    InjectionMethod MethodUsed,
    string? ErrorMessage = null
);

public interface IInjectorEngine
{
    Task<InjectionResult> InjectTextAsync(TargetContext context, string newText, CancellationToken ct = default);
}
