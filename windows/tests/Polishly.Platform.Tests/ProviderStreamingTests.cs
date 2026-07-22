using System.Net;
using Polishly.Core.Models;
using Polishly.Providers.Abstractions;
using Polishly.Providers.Anthropic;
using Polishly.Providers.Cerebras;
using Polishly.Providers.Demo;
using Polishly.Providers.Groq;
using Polishly.Providers.OpenAI;
using Xunit;

namespace Polishly.Platform.Tests;

public class ProviderStreamingTests
{
    [Fact]
    public async Task OpenAiProvider_StreamRewriteAsync_ParsesSseTokensSuccessfully()
    {
        var sseLines = new[]
        {
            "data: {\"choices\":[{\"delta\":{\"content\":\"Hello\"}}]}",
            "data: {\"choices\":[{\"delta\":{\"content\":\" world\"}}]}",
            "data: {\"choices\":[{\"delta\":{\"content\":\"!\"}}]}",
            "data: [DONE]"
        };

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
            Assert.Equal("sk-test-key", req.Headers.Authorization?.Parameter);
            return MockHttpMessageHandler.CreateSseResponse(HttpStatusCode.OK, sseLines);
        });

        var client = new HttpClient(handler);
        var provider = new OpenAiProvider("sk-test-key", "gpt-4o", "https://api.openai.com/v1", client);

        var request = new RewriteRequest("Test text", RewriteMode.Improve);
        var tokens = new List<RewriteToken>();

        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            tokens.Add(token);
        }

        Assert.Equal(3, tokens.Count);
        Assert.Equal("Hello", tokens[0].Text);
        Assert.False(tokens[0].IsFinal);

        Assert.Equal(" world", tokens[1].Text);
        Assert.False(tokens[1].IsFinal);

        Assert.Equal("!", tokens[2].Text);
        Assert.True(tokens[2].IsFinal);
    }

    [Fact]
    public async Task OpenAiProvider_StreamRewriteAsync_401Unauthorized_ThrowsHttpRequestException()
    {
        var handler = new MockHttpMessageHandler(_ =>
            MockHttpMessageHandler.CreateJsonResponse(HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Invalid key\"}}"));

        var client = new HttpClient(handler);
        var provider = new OpenAiProvider("sk-invalid-key", httpClient: client);
        var request = new RewriteRequest("Test text", RewriteMode.Improve);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in provider.StreamRewriteAsync(request))
            {
            }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task OpenAiProvider_StreamRewriteAsync_429RateLimit_ThrowsHttpRequestException()
    {
        var handler = new MockHttpMessageHandler(_ =>
            MockHttpMessageHandler.CreateJsonResponse(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"Rate limit reached\"}}"));

        var client = new HttpClient(handler);
        var provider = new OpenAiProvider("sk-valid-key", httpClient: client);
        var request = new RewriteRequest("Test text", RewriteMode.Improve);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in provider.StreamRewriteAsync(request))
            {
            }
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Contains("429", ex.Message);
    }

    [Fact]
    public async Task OpenAiProvider_ValidateCredentialsAsync_HandlesStatusCodesCorrectly()
    {
        // Test Empty Key
        var providerEmpty = new OpenAiProvider();
        var emptyRes = await providerEmpty.ValidateCredentialsAsync("");
        Assert.False(emptyRes.IsValid);
        Assert.NotNull(emptyRes.ErrorMessage);

        // Test 200 OK
        var handler200 = new MockHttpMessageHandler(_ => MockHttpMessageHandler.CreateJsonResponse(HttpStatusCode.OK, "{}"));
        var provider200 = new OpenAiProvider(httpClient: new HttpClient(handler200));
        var validRes = await provider200.ValidateCredentialsAsync("sk-valid");
        Assert.True(validRes.IsValid);
        Assert.Null(validRes.ErrorMessage);

        // Test 401
        var handler401 = new MockHttpMessageHandler(_ => MockHttpMessageHandler.CreateJsonResponse(HttpStatusCode.Unauthorized, "{}"));
        var provider401 = new OpenAiProvider(httpClient: new HttpClient(handler401));
        var invalidRes = await provider401.ValidateCredentialsAsync("sk-invalid");
        Assert.False(invalidRes.IsValid);

        // Test 429
        var handler429 = new MockHttpMessageHandler(_ => MockHttpMessageHandler.CreateJsonResponse(HttpStatusCode.TooManyRequests, "{}"));
        var provider429 = new OpenAiProvider(httpClient: new HttpClient(handler429));
        var rateLimitRes = await provider429.ValidateCredentialsAsync("sk-ratelimited");
        Assert.False(rateLimitRes.IsValid);
    }

    [Fact]
    public async Task AnthropicProvider_StreamRewriteAsync_ParsesSseTokensSuccessfully()
    {
        var sseLines = new[]
        {
            "event: content_block_delta",
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Claude\"}}",
            "event: content_block_delta",
            "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\" says hi\"}}",
            "data: [DONE]"
        };

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.True(req.Headers.Contains("x-api-key"));
            Assert.Equal("sk-ant-test", req.Headers.GetValues("x-api-key").First());
            Assert.Equal("2023-06-01", req.Headers.GetValues("anthropic-version").First());
            return MockHttpMessageHandler.CreateSseResponse(HttpStatusCode.OK, sseLines);
        });

        var client = new HttpClient(handler);
        var provider = new AnthropicProvider("sk-ant-test", "claude-3-5-sonnet-20241022", "https://api.anthropic.com/v1", client);

        var request = new RewriteRequest("Hello", RewriteMode.Improve);
        var tokens = new List<RewriteToken>();

        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            tokens.Add(token);
        }

        Assert.Equal(2, tokens.Count);
        Assert.Equal("Claude", tokens[0].Text);
        Assert.False(tokens[0].IsFinal);

        Assert.Equal(" says hi", tokens[1].Text);
        Assert.True(tokens[1].IsFinal);
    }

    [Fact]
    public async Task AnthropicProvider_StreamRewriteAsync_401And429_ErrorHandling()
    {
        var handler401 = new MockHttpMessageHandler(_ => MockHttpMessageHandler.CreateJsonResponse(HttpStatusCode.Unauthorized, "{}"));
        var provider401 = new AnthropicProvider("sk-ant-bad", httpClient: new HttpClient(handler401));
        var ex401 = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in provider401.StreamRewriteAsync(new RewriteRequest("text", RewriteMode.Improve))) { }
        });
        Assert.Equal(HttpStatusCode.Unauthorized, ex401.StatusCode);

        var handler429 = new MockHttpMessageHandler(_ => MockHttpMessageHandler.CreateJsonResponse(HttpStatusCode.TooManyRequests, "{}"));
        var provider429 = new AnthropicProvider("sk-ant-rate", httpClient: new HttpClient(handler429));
        var ex429 = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in provider429.StreamRewriteAsync(new RewriteRequest("text", RewriteMode.Improve))) { }
        });
        Assert.Equal(HttpStatusCode.TooManyRequests, ex429.StatusCode);
    }

    [Fact]
    public async Task GroqProvider_StreamRewriteAsync_ParsesSseTokensSuccessfully()
    {
        var sseLines = new[]
        {
            "data: {\"choices\":[{\"delta\":{\"content\":\"Groq\"}}]}",
            "data: {\"choices\":[{\"delta\":{\"content\":\" fast\"}}]}",
            "data: [DONE]"
        };

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
            Assert.Equal("gsk-test", req.Headers.Authorization?.Parameter);
            return MockHttpMessageHandler.CreateSseResponse(HttpStatusCode.OK, sseLines);
        });

        var client = new HttpClient(handler);
        var provider = new GroqProvider("gsk-test", httpClient: client);

        var request = new RewriteRequest("Speed test", RewriteMode.Concise);
        var tokens = new List<RewriteToken>();

        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            tokens.Add(token);
        }

        Assert.Equal(2, tokens.Count);
        Assert.Equal("Groq", tokens[0].Text);
        Assert.Equal(" fast", tokens[1].Text);
        Assert.True(tokens[1].IsFinal);
    }

    [Fact]
    public async Task CerebrasProvider_StreamRewriteAsync_ParsesSseTokensSuccessfully()
    {
        var sseLines = new[]
        {
            "data: {\"choices\":[{\"delta\":{\"content\":\"Cerebras\"}}]}",
            "data: {\"choices\":[{\"delta\":{\"content\":\" wafer\"}}]}",
            "data: [DONE]"
        };

        var handler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
            Assert.Equal("csk-test", req.Headers.Authorization?.Parameter);
            return MockHttpMessageHandler.CreateSseResponse(HttpStatusCode.OK, sseLines);
        });

        var client = new HttpClient(handler);
        var provider = new CerebrasProvider("csk-test", httpClient: client);

        var request = new RewriteRequest("Wafer scale", RewriteMode.Expand);
        var tokens = new List<RewriteToken>();

        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            tokens.Add(token);
        }

        Assert.Equal(2, tokens.Count);
        Assert.Equal("Cerebras", tokens[0].Text);
        Assert.Equal(" wafer", tokens[1].Text);
        Assert.True(tokens[1].IsFinal);
    }

    [Theory]
    [InlineData(RewriteMode.Improve, "Improved: Hello")]
    [InlineData(RewriteMode.Concise, "Concise: Hello")]
    [InlineData(RewriteMode.Friendly, "Friendly: Hello")]
    [InlineData(RewriteMode.Expand, "Elaborated: Hello")]
    public async Task DemoProvider_StreamRewriteAsync_ReturnsCannedTransformations(RewriteMode mode, string expectedStart)
    {
        var provider = new DemoProvider();
        var request = new RewriteRequest("Hello", mode);
        var tokens = new List<RewriteToken>();

        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            tokens.Add(token);
        }

        Assert.NotEmpty(tokens);
        Assert.True(tokens.Last().IsFinal);

        string fullOutput = string.Concat(tokens.Select(t => t.Text));
        Assert.StartsWith(expectedStart, fullOutput);
    }

    [Fact]
    public void CustomModelAndEndpoint_ReflectedInConfig()
    {
        var customOpenAi = new OpenAiProvider("key", "gpt-4-turbo", "https://custom.openai.proxy/v1");
        Assert.Equal("gpt-4-turbo", customOpenAi.Config.Model);
        Assert.Equal("https://custom.openai.proxy/v1", customOpenAi.Config.BaseUrl);

        var customAnthropic = new AnthropicProvider("key", "claude-3-opus-20240229", "https://custom.anthropic.proxy/v1");
        Assert.Equal("claude-3-opus-20240229", customAnthropic.Config.Model);
        Assert.Equal("https://custom.anthropic.proxy/v1", customAnthropic.Config.BaseUrl);
    }
}
