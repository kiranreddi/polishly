using Polishly.Providers.Abstractions;
using Polishly.Providers.Anthropic;
using Polishly.Providers.Cerebras;
using Polishly.Providers.Groq;
using Polishly.Providers.OpenAI;
using Polishly.WindowsIntegration.Security;
using Xunit;

namespace Polishly.Platform.Tests;

public class Tier2AuthCredentialErrorTests
{
    private readonly CredentialManager _credentialStore = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateCredentialsAsync_MissingOrEmptyApiKey_ReturnsInvalidResult(string emptyKey)
    {
        IAiProvider provider = new OpenAiProvider();
        var result = await provider.ValidateCredentialsAsync(emptyKey);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("cannot be empty", result.ErrorMessage);
    }

    [Fact]
    public async Task GetApiKeyAsync_MissingProviderCredential_ReturnsNull()
    {
        string nonexistentProvider = "missing_prov_" + Guid.NewGuid().ToString("N");
        string? key = await _credentialStore.GetApiKeyAsync(nonexistentProvider);

        Assert.Null(key);
    }

    [Fact]
    public async Task Provider_ValidateCredentials_ProvidesClearGuidanceOnMissingKey()
    {
        IAiProvider provider = new AnthropicProvider();
        var result = await provider.ValidateCredentialsAsync("");

        Assert.False(result.IsValid);
        Assert.Contains("API key cannot be empty", result.ErrorMessage);
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("groq")]
    [InlineData("cerebras")]
    public async Task ValidateCredentialsAsync_ExpiredOrInvalidFormat_RejectsKey(string providerId)
    {
        IAiProvider provider = providerId switch
        {
            "openai" => new OpenAiProvider(),
            "anthropic" => new AnthropicProvider(),
            "groq" => new GroqProvider(),
            "cerebras" => new CerebrasProvider(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerId))
        };

        var emptyValidation = await provider.ValidateCredentialsAsync("   ");
        Assert.False(emptyValidation.IsValid);
    }

    [Fact]
    public async Task CredentialStore_EmptyProviderId_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _credentialStore.GetApiKeyAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => _credentialStore.SaveApiKeyAsync("  ", "key"));
    }
}
