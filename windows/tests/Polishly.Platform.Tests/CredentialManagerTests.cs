using Polishly.WindowsIntegration.Security;
using Xunit;

namespace Polishly.Platform.Tests;

public class CredentialManagerTests
{
    [Fact]
    public async Task SaveAndGetApiKey_ReturnsStoredKey()
    {
        var store = new CredentialManager();
        string providerId = "test_provider_" + Guid.NewGuid().ToString("N");
        string apiKey = "sk-test-key-12345";

        await store.SaveApiKeyAsync(providerId, apiKey);
        string? retrieved = await store.GetApiKeyAsync(providerId);

        Assert.Equal(apiKey, retrieved);

        await store.DeleteApiKeyAsync(providerId);
        string? deleted = await store.GetApiKeyAsync(providerId);

        Assert.Null(deleted);
    }
}
