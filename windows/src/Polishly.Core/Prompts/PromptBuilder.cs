using Polishly.Core.Models;

namespace Polishly.Core.Prompts;

public class PromptBuilder
{
    public string BuildPrompt(RewriteRequest request)
    {
        var directive = request.Mode switch
        {
            RewriteMode.Improve => PromptFixture.ImproveDirective,
            RewriteMode.Concise => PromptFixture.ConciseDirective,
            RewriteMode.Friendly => PromptFixture.FriendlyDirective,
            RewriteMode.Expand => PromptFixture.ExpandDirective,
            RewriteMode.Custom => request.CustomInstruction ?? PromptFixture.ImproveDirective,
            _ => PromptFixture.ImproveDirective
        };

        if (request.Mode != RewriteMode.Custom && !string.IsNullOrWhiteSpace(request.CustomInstruction))
        {
            directive += $" Additional instruction: {request.CustomInstruction}";
        }

        return $"{PromptFixture.SystemInstruction}\n\nTask: {directive}\n\nInput Text:\n{request.InputText}";
    }
}
