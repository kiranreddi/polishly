using Polishly.Core.Models;

namespace Polishly.Core;

public static class PromptBuilder
{
    public static string BuildSystemPrompt(RewriteMode mode, string? customInstruction = null)
    {
        var basePrompt = mode switch
        {
            RewriteMode.Improve => "You are an expert editor and writing assistant. Improve the provided text for clarity, grammar, and natural flow while strictly preserving the original tone, intent, and meaning.",
            RewriteMode.Concise => "You are an expert editor. Rewrite the provided text to be concise, clear, and direct. Remove fluff and wordiness while retaining all essential information and intent.",
            RewriteMode.Friendly => "You are a warm, helpful writing assistant. Rewrite the provided text in a friendly, approachable, and engaging tone while keeping the core message clear.",
            RewriteMode.Expand => "You are a creative and detailed writing assistant. Expand the provided text with additional clarity, detail, and structure while preserving the original intent.",
            RewriteMode.Custom => "You are a versatile writing assistant. Follow the custom user instructions carefully to rewrite the provided text.",
            _ => "You are an expert writing assistant. Rewrite the provided text cleanly and accurately."
        };

        if (!string.IsNullOrWhiteSpace(customInstruction))
        {
            basePrompt += $" Additional instruction: {customInstruction.Trim()}";
        }

        basePrompt += "\nImportant Output Rules:\n1. Return ONLY the rewritten text.\n2. Do NOT add preamble, markdown code blocks, quotes, or conversational responses unless requested.\n3. Preserve spacing and line breaks where relevant.";

        return basePrompt;
    }

    public static string BuildUserPrompt(string selectedText, RewriteMode mode, string? customInstruction = null)
    {
        var textToRewrite = selectedText ?? string.Empty;

        if (mode == RewriteMode.Custom && !string.IsNullOrWhiteSpace(customInstruction))
        {
            return $"Instruction: {customInstruction.Trim()}\n\nText to rewrite:\n{textToRewrite}";
        }

        if (!string.IsNullOrWhiteSpace(customInstruction))
        {
            return $"Rewrite Mode: {mode}. Additional Instruction: {customInstruction.Trim()}\n\nText to rewrite:\n{textToRewrite}";
        }

        return $"Rewrite Mode: {mode}\n\nText to rewrite:\n{textToRewrite}";
    }
}
