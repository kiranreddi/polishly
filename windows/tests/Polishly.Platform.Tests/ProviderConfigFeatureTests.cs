using Polishly.Providers.Abstractions;
using Polishly.Providers.Anthropic;
using Polishly.Providers.Cerebras;
using Polishly.Providers.Demo;
using Polishly.Providers.Groq;
using Polishly.Providers.OpenAI;
using Xunit;

namespace Polishly.Platform.Tests;

public class ProviderConfigFeatureTests
{
    [Fact]
    public void OpenAiProvider_Config_MatchesContract()
    {
        var provider = new OpenAiProvider("sk-valid-key");
        var config = provider.Config;

        Assert.Equal("openai", config.ProviderId);
        Assert.Equal("OpenAI GPT-4o", config.DisplayName);
        Assert.Equal("gpt-4o", config.Model);
        Assert.Equal("https://api.openai.com/v1", config.BaseUrl);
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public void AnthropicProvider_Config_MatchesContract()
    {
        var provider = new AnthropicProvider("sk-ant-key");
        var config = provider.Config;

        Assert.Equal("anthropic", config.ProviderId);
        Assert.Equal("claude-3-5-sonnet-20241022", config.Model);
        Assert.Equal("https://api.anthropic.com/v1", config.BaseUrl);
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public void GroqProvider_Config_MatchesContract()
    {
        var provider = new GroqProvider("gsk-test-key");
        var config = provider.Config;

        Assert.Equal("groq", config.ProviderId);
        Assert.Equal("llama-3.3-70b-versatile", config.Model);
        Assert.Equal("https://api.groq.com/openai/v1", config.BaseUrl);
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public void CerebrasProvider_Config_MatchesContract()
    {
        var provider = new CerebrasProvider("csk-test-key");
        var config = provider.Config;

        Assert.Equal("cerebras", config.ProviderId);
        Assert.Equal("llama-3.3-70b", config.Model);
        Assert.Equal("https://api.cerebras.ai/v1", config.BaseUrl);
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public async Task DemoProvider_ConfigAndValidation_MatchesContract()
    {
        var provider = new DemoProvider();
        var config = provider.Config;

        Assert.Equal("demo", config.ProviderId);
        Assert.Equal("demo-v1", config.Model);
        Assert.Null(config.BaseUrl);

        var result = await provider.ValidateCredentialsAsync("any-key-or-none");
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AllCloudProviders_ValidateCredentials_RejectsEmptyApiKey(string emptyKey)
    {
        IAiProvider[] providers = new IAiProvider[]
        {
            new OpenAiProvider(),
            new AnthropicProvider(),
            new GroqProvider(),
            new CerebrasProvider()
        };

        foreach (var p in providers)
        {
            var res = await p.ValidateCredentialsAsync(emptyKey);
            Assert.False(res.IsValid);
            Assert.NotNull(res.ErrorMessage);
        }
    }
}
