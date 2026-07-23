using Polishly.Core.Diff;
using Polishly.Core.Models;
using Polishly.Core.Prompts;
using Polishly.Providers.Demo;
using Xunit;

namespace Polishly.Core.Tests;

public class Tier2UnicodeEmojiTests
{
    private readonly Polishly.Core.Prompts.PromptBuilder _builder = new();
    private readonly Polishly.Core.Diff.WordDiffEngine _diffEngine = new();

    [Fact]
    public void SelectionContext_ComplexEmojisAndSurrogates_PreservedWithoutBoundsErrors()
    {
        string emojiText = "Hello 🚀 world 👨‍👩‍👧‍👦 testing 💡 and 🏳️‍🌈!";
        var target = new TargetContext(IntPtr.Zero, 1, "app", "App", "f1", false, false);
        var context = new SelectionContext(emojiText, emojiText, target, DateTime.UtcNow, true);

        Assert.False(context.IsEmpty);
        Assert.Equal(emojiText, context.SelectedText);
    }

    [Fact]
    public void WordDiffEngine_NonAsciiInternationalText_DiffsAccurately()
    {
        string original = "Guten Tag, wie geht es Ihnen heute?";
        string revised = "Guten Morgen, wie geht es Ihnen heute?";

        var diff = _diffEngine.ComputeDiff(original, revised);

        Assert.Contains(diff, d => d.Type == DiffType.Deletion && d.Text.Contains("Tag"));
        Assert.Contains(diff, d => d.Type == DiffType.Addition && d.Text.Contains("Morgen"));
    }

    [Fact]
    public void PromptBuilder_RtlArabicAndHebrewText_PreservesRtlCharactersInPrompt()
    {
        string rtlText = "مرحبا بالعالم - שלום עולם";
        var req = new RewriteRequest(rtlText, RewriteMode.Improve);

        string prompt = _builder.BuildPrompt(req);

        Assert.Contains(rtlText, prompt);
    }

    [Fact]
    public void WordDiffEngine_EmojiSurrogatePairs_DiffsWithoutSplittingSurrogates()
    {
        string original = "Launch sequence 🚀 initiated!";
        string revised = "Launch sequence 🚀 and 💥 completed!";

        var diff = _diffEngine.ComputeDiff(original, revised);

        Assert.Contains(diff, d => d.Type == DiffType.Unchanged && d.Text.Contains("🚀"));
        Assert.Contains(diff, d => d.Type == DiffType.Addition && d.Text.Contains("💥"));
    }

    [Fact]
    public async Task DemoProvider_MultibyteUnicode_StreamsTokensIntact()
    {
        var provider = new DemoProvider();
        string input = "こんにちは 世界 🚀";
        var req = new RewriteRequest(input, RewriteMode.Improve);

        var tokens = new List<string>();
        await foreach (var token in provider.StreamRewriteAsync(req))
        {
            tokens.Add(token.Text);
        }

        string fullOutput = string.Concat(tokens);
        Assert.Contains("こんにちは", fullOutput);
        Assert.Contains("世界", fullOutput);
        Assert.Contains("🚀", fullOutput);
    }
}
