using Polishly.Providers.Abstractions;

namespace Polishly.Providers;

public static class ProviderFactory
{
    private static readonly IReadOnlyDictionary<string, string[]> Models =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["demo"] = new[] { "local-demo" },
            ["openai"] = new[] { "gpt-4.1-mini", "gpt-4.1", "gpt-4o-mini" },
            ["anthropic"] = new[] { "claude-3-5-haiku-latest", "claude-3-7-sonnet-latest" },
            ["groq"] = new[] { "llama-3.3-70b-versatile", "llama-3.1-8b-instant" },
            ["cerebras"] = new[] { "llama-3.3-70b", "llama3.1-8b" }
        };

    public static IReadOnlyList<string> GetModels(string providerId) =>
        Models.TryGetValue(providerId, out string[]? models)
            ? models
            : Array.Empty<string>();

    public static bool IsKnownModel(string providerId, string? model) =>
        !string.IsNullOrWhiteSpace(model) &&
        GetModels(providerId).Contains(model, StringComparer.OrdinalIgnoreCase);

    public static IAiProvider Create(
        string providerId,
        string? apiKey,
        string? model = null,
        HttpClient? httpClient = null)
    {
        string normalized = providerId.Trim().ToLowerInvariant();
        string selectedModel = IsKnownModel(normalized, model)
            ? model!
            : GetModels(normalized).FirstOrDefault() ?? string.Empty;

        return normalized switch
        {
            "openai" => new OpenAI.OpenAiProvider(apiKey, selectedModel, httpClient: httpClient),
            "anthropic" => new Anthropic.AnthropicProvider(apiKey, selectedModel, httpClient: httpClient),
            "groq" => new Groq.GroqProvider(apiKey, selectedModel, httpClient: httpClient),
            "cerebras" => new Cerebras.CerebrasProvider(apiKey, selectedModel, httpClient: httpClient),
            "demo" => new Demo.DemoProvider(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(providerId), providerId, "Unknown rewrite provider.")
        };
    }
}
