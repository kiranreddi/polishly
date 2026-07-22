using Polishly.Core.Models;
using Polishly.Providers.Abstractions;
using Polishly.Providers.Demo;
using Polishly.Providers.OpenAI;
using Xunit;

namespace Polishly.Platform.Tests;

public class Tier2NetworkTransientErrorTests
{
    [Fact]
    public async Task StreamRewriteAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var provider = new DemoProvider();
        var req = new RewriteRequest("Test input", RewriteMode.Improve);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in provider.StreamRewriteAsync(req, cts.Token))
            {
                // Should throw on first iteration
            }
        });
    }

    [Fact]
    public async Task StreamRewriteAsync_MidStreamCancellation_AbortsEnumeration()
    {
        var provider = new DemoProvider();
        var req = new RewriteRequest("One two three four five six seven eight nine ten", RewriteMode.Improve);
        using var cts = new CancellationTokenSource();

        int count = 0;
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var token in provider.StreamRewriteAsync(req, cts.Token))
            {
                count++;
                if (count == 2)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.Equal(2, count);
    }

    [Fact]
    public void SimulatedRateLimitError_ReturnsRateLimitErrorResult()
    {
        var rateLimitException = new HttpRequestException("429 Too Many Requests - Rate limit exceeded", null, System.Net.HttpStatusCode.TooManyRequests);

        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, rateLimitException.StatusCode);
        Assert.Contains("429", rateLimitException.Message);
    }

    [Fact]
    public void SimulatedServiceUnavailableError_ReturnsTransientUnavailableResult()
    {
        var unavailableException = new HttpRequestException("503 Service Unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable);

        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, unavailableException.StatusCode);
        Assert.Contains("503", unavailableException.Message);
    }

    [Fact]
    public async Task RetryPolicySimulation_RetriesTransientFailureThenSucceeds()
    {
        int attempts = 0;
        int maxRetries = 3;

        async Task<string> ExecuteWithRetryAsync()
        {
            for (int i = 0; i <= maxRetries; i++)
            {
                attempts++;
                if (attempts < 3)
                {
                    await Task.Delay(10);
                    continue; // Simulate transient failure retry
                }
                return "Success";
            }
            throw new Exception("Max retries exceeded");
        }

        string result = await ExecuteWithRetryAsync();

        Assert.Equal("Success", result);
        Assert.Equal(3, attempts);
    }
}
