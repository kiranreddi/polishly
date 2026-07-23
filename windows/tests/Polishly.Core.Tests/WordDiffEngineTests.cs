using Xunit;

namespace Polishly.Core.Tests;

public class WordDiffEngineTests
{
    private readonly WordDiffEngine _engine = new();

    [Fact]
    public void ComputeDiff_IdenticalStrings_ReturnsSingleUnchangedSegment()
    {
        const string text = "Hello world, this is a test.";
        var diff = _engine.ComputeDiff(text, text);

        Assert.Single(diff);
        Assert.Equal(DiffSegmentType.Unchanged, diff[0].Type);
        Assert.Equal(text, diff[0].Text);
    }

    [Fact]
    public void ComputeDiff_BothEmpty_ReturnsEmptyList()
    {
        var diff = _engine.ComputeDiff("", "");
        Assert.Empty(diff);
    }

    [Fact]
    public void ComputeDiff_NullInputs_HandledAsEmpty()
    {
        var diff = _engine.ComputeDiff(null!, null!);
        Assert.Empty(diff);
    }

    [Fact]
    public void ComputeDiff_OriginalEmpty_ReturnsAdded()
    {
        const string newText = "Brand new text.";
        var diff = _engine.ComputeDiff("", newText);

        Assert.Single(diff);
        Assert.Equal(DiffSegmentType.Added, diff[0].Type);
        Assert.Equal(newText, diff[0].Text);
    }

    [Fact]
    public void ComputeDiff_NewEmpty_ReturnsDeleted()
    {
        const string originalText = "Old deleted text.";
        var diff = _engine.ComputeDiff(originalText, "");

        Assert.Single(diff);
        Assert.Equal(DiffSegmentType.Deleted, diff[0].Type);
        Assert.Equal(originalText, diff[0].Text);
    }

    [Fact]
    public void ComputeDiff_SingleWordChange_ReturnsCorrectSegments()
    {
        const string original = "The quick brown fox";
        const string updated = "The fast brown fox";

        var diff = _engine.ComputeDiff(original, updated);

        Assert.NotEmpty(diff);
        var rebuiltOriginal = string.Concat(diff.Where(d => d.Type != DiffSegmentType.Added).Select(d => d.Text));
        var rebuiltNew = string.Concat(diff.Where(d => d.Type != DiffSegmentType.Deleted).Select(d => d.Text));

        Assert.Equal(original, rebuiltOriginal);
        Assert.Equal(updated, rebuiltNew);
    }

    [Fact]
    public void ComputeDiff_PunctuationAndWhitespace_Preserved()
    {
        const string original = "Hello, world!\nHow are you?";
        const string updated = "Hello, World!\nHow are you today?";

        var diff = _engine.ComputeDiff(original, updated);

        var rebuiltOriginal = string.Concat(diff.Where(d => d.Type != DiffSegmentType.Added).Select(d => d.Text));
        var rebuiltNew = string.Concat(diff.Where(d => d.Type != DiffSegmentType.Deleted).Select(d => d.Text));

        Assert.Equal(original, rebuiltOriginal);
        Assert.Equal(updated, rebuiltNew);
    }

    [Fact]
    public void ComputeDiff_UnicodeAndEmoji_HandledCorrectly()
    {
        const string original = "Great job! 👍";
        const string updated = "Awesome job! 🚀";

        var diff = _engine.ComputeDiff(original, updated);

        var rebuiltOriginal = string.Concat(diff.Where(d => d.Type != DiffSegmentType.Added).Select(d => d.Text));
        var rebuiltNew = string.Concat(diff.Where(d => d.Type != DiffSegmentType.Deleted).Select(d => d.Text));

        Assert.Equal(original, rebuiltOriginal);
        Assert.Equal(updated, rebuiltNew);
    }
}
