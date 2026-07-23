using Polishly.Core.Models;

namespace Polishly.Providers.Abstractions;

public record ProviderConfig(
    string ProviderId,
    string DisplayName,
    string Model,
    string? BaseUrl = null,
    bool IsEnabled = true
);

public record RewriteToken(
    string Text,
    bool IsFinal = false
);

public record ValidationResult(
    bool IsValid,
    string? ErrorMessage = null
);

public interface IAiProvider
{
    ProviderConfig Config { get; }
    IAsyncEnumerable<RewriteToken> StreamRewriteAsync(RewriteRequest request, CancellationToken ct = default);
    Task<ValidationResult> ValidateCredentialsAsync(string apiKey, CancellationToken ct = default);
}
