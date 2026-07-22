using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Polishly.Core.Models;
using Polishly.Core.Prompts;
using Polishly.Providers.Abstractions;

namespace Polishly.Providers.Anthropic;

public class AnthropicProvider : IAiProvider
{
    private readonly string? _apiKey;
    private readonly HttpClient _httpClient;
    private readonly PromptBuilder _promptBuilder = new();

    public ProviderConfig Config { get; }

    public AnthropicProvider(
        string? apiKey = null,
        string? model = null,
        string? baseUrl = null,
        HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _httpClient = httpClient ?? new HttpClient();

        Config = new ProviderConfig(
            ProviderId: "anthropic",
            DisplayName: "Anthropic Claude 3.5 Sonnet",
            Model: model ?? "claude-3-5-sonnet-20241022",
            BaseUrl: baseUrl ?? "https://api.anthropic.com/v1",
            IsEnabled: true
        );
    }

    public async IAsyncEnumerable<RewriteToken> StreamRewriteAsync(
        RewriteRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new HttpRequestException("Anthropic API key is missing or empty.", null, HttpStatusCode.Unauthorized);
        }

        string prompt = _promptBuilder.BuildPrompt(request);
        string endpoint = $"{Config.BaseUrl?.TrimEnd('/')}/messages";

        var payload = new
        {
            model = Config.Model,
            max_tokens = 4096,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            stream = true
        };

        var jsonBody = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Add("x-api-key", _apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new HttpRequestException("401 Unauthorized: Invalid Anthropic API key.", null, HttpStatusCode.Unauthorized);
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new HttpRequestException("429 Too Many Requests: Anthropic rate limit exceeded.", null, HttpStatusCode.TooManyRequests);
        }

        if (!response.IsSuccessStatusCode)
        {
            string err = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new HttpRequestException($"Anthropic request failed with status code {(int)response.StatusCode}: {err}", null, response.StatusCode);
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        RewriteToken? pendingToken = null;

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("data: "))
            {
                string data = line.Substring(6).Trim();
                if (data == "[DONE]")
                {
                    break;
                }

                string? tokenText = ExtractContentFromAnthropicJson(data);
                if (!string.IsNullOrEmpty(tokenText))
                {
                    if (pendingToken != null)
                    {
                        yield return pendingToken;
                    }
                    pendingToken = new RewriteToken(tokenText, false);
                }
            }
        }

        if (pendingToken != null)
        {
            yield return new RewriteToken(pendingToken.Text, true);
        }
        else
        {
            yield return new RewriteToken("", true);
        }
    }

    public async Task<ValidationResult> ValidateCredentialsAsync(string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ValidationResult(false, "API key cannot be empty.");
        }

        try
        {
            string endpoint = $"{Config.BaseUrl?.TrimEnd('/')}/messages";
            var payload = new
            {
                model = Config.Model,
                max_tokens = 1,
                messages = new[]
                {
                    new { role = "user", content = "hi" }
                }
            };

            var jsonBody = JsonSerializer.Serialize(payload);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            httpRequest.Headers.Add("x-api-key", apiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");
            httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return new ValidationResult(true, null);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                return new ValidationResult(false, "API key is invalid (401/403 Unauthorized).");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new ValidationResult(false, "Rate limit exceeded (429 Too Many Requests).");
            }

            return new ValidationResult(false, $"Validation failed with status {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return new ValidationResult(false, $"Validation error: {ex.Message}");
        }
    }

    private static string? ExtractContentFromAnthropicJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("type", out var typeProp) &&
                typeProp.GetString() == "content_block_delta")
            {
                if (doc.RootElement.TryGetProperty("delta", out var deltaProp) &&
                    deltaProp.TryGetProperty("text", out var textProp) &&
                    textProp.ValueKind == JsonValueKind.String)
                {
                    return textProp.GetString();
                }
            }
        }
        catch
        {
            // Ignore non-json or malformed SSE lines
        }
        return null;
    }
}
