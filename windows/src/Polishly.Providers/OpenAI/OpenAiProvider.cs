using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Polishly.Core.Models;
using Polishly.Core.Prompts;
using Polishly.Providers.Abstractions;

namespace Polishly.Providers.OpenAI;

public class OpenAiProvider : IAiProvider
{
    private readonly string? _apiKey;
    private readonly HttpClient _httpClient;
    private readonly PromptBuilder _promptBuilder = new();

    public ProviderConfig Config { get; }

    public OpenAiProvider(
        string? apiKey = null,
        string? model = null,
        string? baseUrl = null,
        HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _httpClient = httpClient ?? new HttpClient();

        Config = new ProviderConfig(
            ProviderId: "openai",
            DisplayName: "OpenAI GPT-4o",
            Model: model ?? "gpt-4o",
            BaseUrl: baseUrl ?? "https://api.openai.com/v1",
            IsEnabled: true
        );
    }

    public async IAsyncEnumerable<RewriteToken> StreamRewriteAsync(
        RewriteRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new HttpRequestException("OpenAI API key is missing or empty.", null, HttpStatusCode.Unauthorized);
        }

        string prompt = _promptBuilder.BuildPrompt(request);
        string endpoint = $"{Config.BaseUrl?.TrimEnd('/')}/chat/completions";

        var payload = new
        {
            model = Config.Model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            stream = true
        };

        var jsonBody = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new HttpRequestException("401 Unauthorized: Invalid OpenAI API key.", null, HttpStatusCode.Unauthorized);
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new HttpRequestException("429 Too Many Requests: OpenAI rate limit exceeded.", null, HttpStatusCode.TooManyRequests);
        }

        if (!response.IsSuccessStatusCode)
        {
            string err = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new HttpRequestException($"OpenAI request failed with status code {(int)response.StatusCode}: {err}", null, response.StatusCode);
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

                string? tokenText = ExtractContentFromOpenAiJson(data);
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
            string endpoint = $"{Config.BaseUrl?.TrimEnd('/')}/models";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return new ValidationResult(true, null);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new ValidationResult(false, "API key is invalid (401 Unauthorized).");
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

    private static string? ExtractContentFromOpenAiJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("content", out var contentProp) &&
                    contentProp.ValueKind == JsonValueKind.String)
                {
                    return contentProp.GetString();
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
