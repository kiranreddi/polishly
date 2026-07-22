using Polishly.WindowsIntegration.Security;
using Xunit;

namespace Polishly.Platform.Tests;

public class CredentialManagerFeatureTests
{
    [Fact]
    public async Task SaveAndGetApiKeyAsync_Roundtrip_ReturnsStoredKey()
    {
        var store = new CredentialManager();
        string providerId = "test_prov_" + Guid.NewGuid().ToString("N");
        string apiKey = "sk-test-key-12345";

        await store.SaveApiKeyAsync(providerId, apiKey);
        string? retrieved = await store.GetApiKeyAsync(providerId);

        Assert.Equal(apiKey, retrieved);

        await store.DeleteApiKeyAsync(providerId);
        string? deleted = await store.GetApiKeyAsync(providerId);

        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteApiKeyAsync_RemovesKeyFromStore()
    {
        var store = new CredentialManager();
        string providerId = "del_prov_" + Guid.NewGuid().ToString("N");
        
        await store.SaveApiKeyAsync(providerId, "sk-to-delete");
        await store.DeleteApiKeyAsync(providerId);

        string? result = await store.GetApiKeyAsync(providerId);
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveApiKeyAsync_OverwriteExistingKey_UpdatesKey()
    {
        var store = new CredentialManager();
        string providerId = "overwrite_prov_" + Guid.NewGuid().ToString("N");

        await store.SaveApiKeyAsync(providerId, "sk-initial-key");
        await store.SaveApiKeyAsync(providerId, "sk-updated-key");

        string? retrieved = await store.GetApiKeyAsync(providerId);
        Assert.Equal("sk-updated-key", retrieved);

        await store.DeleteApiKeyAsync(providerId);
    }

    [Fact]
    public async Task GetApiKeyAsync_NonExistentProvider_ReturnsNull()
    {
        var store = new CredentialManager();
        string providerId = "non_existent_" + Guid.NewGuid().ToString("N");

        string? retrieved = await store.GetApiKeyAsync(providerId);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task CredentialManager_Validation_RejectsNullOrEmptyProviderId()
    {
        var store = new CredentialManager();

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveApiKeyAsync("", "sk-key"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.GetApiKeyAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => store.DeleteApiKeyAsync(""));
    }
}
