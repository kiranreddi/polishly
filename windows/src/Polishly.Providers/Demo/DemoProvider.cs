using System.Runtime.CompilerServices;
using Polishly.Core.Models;
using Polishly.Providers.Abstractions;

namespace Polishly.Providers.Demo;

public class DemoProvider : IAiProvider
{
    public ProviderConfig Config { get; } = new(
        ProviderId: "demo",
        DisplayName: "Demo Local Mode",
        Model: "demo-v1",
        IsEnabled: true
    );

    public async IAsyncEnumerable<RewriteToken> StreamRewriteAsync(
        RewriteRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        string prefix = request.Mode switch
        {
            RewriteMode.Improve => "Improved: ",
            RewriteMode.Concise => "Concise: ",
            RewriteMode.Friendly => "Friendly: ",
            RewriteMode.Expand => "Elaborated: ",
            _ => "Polished: "
        };

        string outputText = prefix + request.InputText;
        if (!string.IsNullOrWhiteSpace(request.CustomInstruction))
        {
            outputText += $" [{request.CustomInstruction}]";
        }

        string[] words = outputText.Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(15, ct).ConfigureAwait(false);
            string tokenText = (i == 0 ? "" : " ") + words[i];
            bool isFinal = i == words.Length - 1;
            yield return new RewriteToken(tokenText, isFinal);
        }
    }

    public Task<ValidationResult> ValidateCredentialsAsync(string apiKey, CancellationToken ct = default)
    {
        return Task.FromResult(new ValidationResult(true, null));
    }
}
