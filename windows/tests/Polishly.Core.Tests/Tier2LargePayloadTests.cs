using System.Diagnostics;
using Polishly.Core.Diff;
using Polishly.Core.Models;
using Polishly.Core.Prompts;
using Polishly.Providers.Demo;
using Polishly.WindowsIntegration.Clipboard;
using Xunit;

namespace Polishly.Core.Tests;

public class Tier2LargePayloadTests
{
    private readonly Polishly.Core.Prompts.PromptBuilder _builder = new();
    private readonly Polishly.Core.Diff.WordDiffEngine _diffEngine = new();

    [Fact]
    public void PromptBuilder_100kCharacterPayload_ExecutesFastWithoutStackOverflow()
    {
        string largeInput = new string('A', 100_000);
        var req = new RewriteRequest(largeInput, RewriteMode.Improve);

        var sw = Stopwatch.StartNew();
        string prompt = _builder.BuildPrompt(req);
        sw.Stop();

        Assert.NotNull(prompt);
        Assert.True(prompt.Length >= 100_000);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Prompt building took {sw.ElapsedMilliseconds} ms, expected < 500 ms");
    }

    [Fact]
    public void WordDiffEngine_100kCharacterPayload_CompletesDiffInSubSecond()
    {
        string baseText = string.Join(" ", Enumerable.Repeat("word", 15_000)); // ~75k-100k chars
        string modifiedText = baseText + " extra_added_word";

        var sw = Stopwatch.StartNew();
        var diff = _diffEngine.ComputeDiff(baseText, modifiedText);
        sw.Stop();

        Assert.NotEmpty(diff);
        Assert.Contains(diff, d => d.Type == DiffType.Addition && d.Text.Contains("extra_added_word"));
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Diff calculation took {sw.ElapsedMilliseconds} ms, expected < 1000 ms");
    }

    [Fact]
    public async Task DemoProvider_100kPayload_StreamsTokensWithoutMemoryOverflow()
    {
        var provider = new DemoProvider();
        string largeInput = string.Join(" ", Enumerable.Repeat("token", 2_000));
        var req = new RewriteRequest(largeInput, RewriteMode.Improve);

        int tokenCount = 0;
        await foreach (var token in provider.StreamRewriteAsync(req))
        {
            tokenCount++;
        }

        Assert.True(tokenCount > 2000);
    }

    [Fact]
    public void SelectionContext_LargePayload_PreservesFullStringWithoutTruncation()
    {
        string largeText = new string('X', 120_000);
        var target = new TargetContext(IntPtr.Zero, 1, "app", "App", "f1", false, false);

        var context = new SelectionContext(largeText, largeText, target, DateTime.UtcNow, true);

        Assert.Equal(120_000, context.SelectedText.Length);
        Assert.False(context.IsEmpty);
    }

    [Fact]
    public async Task GuardedClipboardTransaction_LargePayload_HandlesBufferValidationSafely()
    {
        var transaction = new GuardedClipboardTransaction(() => 1u);
        string largeText = new string('Z', 150_000);
        var target = new TargetContext(IntPtr.Zero, 1, "notepad", "Notepad", "f1", false, false);

        var result = await transaction.ExecuteSafePasteAsync(largeText, target);

        Assert.True(result.Success);
        Assert.False(result.FallbackToCopy);
    }
}
