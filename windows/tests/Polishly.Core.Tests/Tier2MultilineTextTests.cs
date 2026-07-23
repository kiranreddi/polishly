using Polishly.Core.Diff;
using Polishly.Core.Models;
using Polishly.Core.Prompts;
using Polishly.Providers.Demo;
using Xunit;

namespace Polishly.Core.Tests;

public class Tier2MultilineTextTests
{
    private readonly Polishly.Core.Prompts.PromptBuilder _promptBuilder = new();
    private readonly Polishly.Core.Diff.WordDiffEngine _diffEngine = new();

    [Fact]
    public void PromptBuilder_MultilineCrlfInput_PreservesLineBreaksInPrompt()
    {
        string multiline = "First line.\r\nSecond line.\r\nThird line.";
        var req = new RewriteRequest(multiline, RewriteMode.Improve);

        string prompt = _promptBuilder.BuildPrompt(req);

        Assert.Contains("First line.", prompt);
        Assert.Contains("Second line.", prompt);
        Assert.Contains("Third line.", prompt);
        Assert.Contains("\r\n", prompt);
    }

    [Fact]
    public void WordDiffEngine_MultilineParagraphDiff_PreservesParagraphStructure()
    {
        string original = "Paragraph 1: Initial draft.\n\nParagraph 2: Second point.";
        string revised = "Paragraph 1: Polished draft.\n\nParagraph 2: Second point.";

        var diff = _diffEngine.ComputeDiff(original, revised);

        Assert.Contains(diff, d => d.Type == DiffType.Deletion && d.Text.Contains("Initial"));
        Assert.Contains(diff, d => d.Type == DiffType.Addition && d.Text.Contains("Polished"));
        Assert.Contains(diff, d => d.Type == DiffType.Unchanged && d.Text.Contains("Paragraph 2"));
    }

    [Fact]
    public async Task DemoProvider_MultilineInput_StreamsTokensPreservingLines()
    {
        var provider = new DemoProvider();
        string multilineInput = "Line 1\nLine 2\nLine 3";
        var request = new RewriteRequest(multilineInput, RewriteMode.Improve);

        var tokens = new List<string>();
        await foreach (var token in provider.StreamRewriteAsync(request))
        {
            tokens.Add(token.Text);
        }

        string fullOutput = string.Concat(tokens);
        Assert.Contains("Line 1", fullOutput);
        Assert.Contains("Line 2", fullOutput);
        Assert.Contains("Line 3", fullOutput);
    }

    [Fact]
    public void WordDiffEngine_NormalizedCrlfVsLf_DiffsCleanlyWithoutCorruptingLines()
    {
        string crlf = "Header\r\nItem A\r\nItem B";
        string lf = "Header\nItem A\nItem B";

        var diff = _diffEngine.ComputeDiff(crlf, lf);

        Assert.NotNull(diff);
        string reconstructedLf = string.Concat(diff.Where(d => d.Type != DiffType.Deletion).Select(d => d.Text));
        Assert.Equal(lf, reconstructedLf);
    }

    [Fact]
    public void WordDiffEngine_MultilineAlignment_RetainsIndentationsAndWhitespace()
    {
        string original = "  func main() {\n    println(\"hello\");\n  }";
        string revised = "  func main() {\n    println(\"world\");\n  }";

        var diff = _diffEngine.ComputeDiff(original, revised);

        Assert.Contains(diff, d => d.Type == DiffType.Deletion && d.Text.Contains("hello"));
        Assert.Contains(diff, d => d.Type == DiffType.Addition && d.Text.Contains("world"));
    }
}
