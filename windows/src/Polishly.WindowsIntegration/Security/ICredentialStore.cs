namespace Polishly.WindowsIntegration.Security;

public interface ICredentialStore
{
    Task SaveApiKeyAsync(string providerId, string apiKey, CancellationToken ct = default);
    Task<string?> GetApiKeyAsync(string providerId, CancellationToken ct = default);
    Task DeleteApiKeyAsync(string providerId, CancellationToken ct = default);
}
