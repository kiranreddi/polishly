using Polishly.Core.Diff;
using Polishly.Core.Models;
using Polishly.Core.Prompts;
using Xunit;

namespace Polishly.Core.Tests;

public class Tier2EmptyStringTests
{
    private readonly Polishly.Core.Prompts.PromptBuilder _builder = new();
    private readonly Polishly.Core.Diff.WordDiffEngine _diffEngine = new();

    [Fact]
    public void SelectionContext_EmptyStringSelection_FlagsIsEmptyTrue()
    {
        var target = new TargetContext(IntPtr.Zero, 10, "notepad", "Notepad", "f1", false, false);
        var context = new SelectionContext("", "", target, DateTime.UtcNow, true);

        Assert.True(context.IsEmpty);
        Assert.Equal("", context.SelectedText);
    }

    [Fact]
    public void PromptBuilder_EmptyInputText_BuildsValidPromptSkeleton()
    {
        var req = new RewriteRequest("", RewriteMode.Improve);
        string prompt = _builder.BuildPrompt(req);

        Assert.NotNull(prompt);
        Assert.Contains(PromptFixture.SystemInstruction, prompt);
        Assert.Contains(PromptFixture.ImproveDirective, prompt);
    }

    [Fact]
    public void WordDiffEngine_BothStringsEmpty_ReturnsEmptyDiffList()
    {
        var diff = _diffEngine.ComputeDiff("", "");

        Assert.Empty(diff);
    }

    [Fact]
    public void WordDiffEngine_NonEmptyToEmpty_ReturnsHundredPercentDeletion()
    {
        string original = "Original non-empty text content.";
        var diff = _diffEngine.ComputeDiff(original, "");

        Assert.Single(diff);
        Assert.Equal(DiffType.Deletion, diff[0].Type);
        Assert.Equal(original, diff[0].Text);
    }

    [Fact]
    public void WordDiffEngine_EmptyToNonEmpty_ReturnsHundredPercentAddition()
    {
        string revised = "Brand new output content.";
        var diff = _diffEngine.ComputeDiff("", revised);

        Assert.Single(diff);
        Assert.Equal(DiffType.Addition, diff[0].Type);
        Assert.Equal(revised, diff[0].Text);
    }
}
