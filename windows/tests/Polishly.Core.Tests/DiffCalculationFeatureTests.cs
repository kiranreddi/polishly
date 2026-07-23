using System.Linq;
using Polishly.Core;
using Polishly.Core.Diff;
using Xunit;

namespace Polishly.Core.Tests;

public class DiffCalculationFeatureTests
{
    private readonly WordDiffEngine _engine = new();

    [Fact]
    public void ComputeDiff_IdenticalTexts_ReturnsSingleUnchangedSegment()
    {
        string text = "The quick brown fox jumps over the lazy dog.";
        var diff = _engine.ComputeDiff(text, text);

        Assert.Single(diff);
        Assert.Equal(DiffSegmentType.Unchanged, diff[0].Type);
        Assert.Equal(text, diff[0].Text);
    }

    [Fact]
    public void ComputeDiff_PureWordAdditions_IdentifiesAdditionSegments()
    {
        string original = "I have a cat.";
        string revised = "I have a very cute cat.";

        var diff = _engine.ComputeDiff(original, revised);

        Assert.Contains(diff, d => d.Type == DiffSegmentType.Added && d.Text.Contains("very cute"));
        Assert.Contains(diff, d => d.Type == DiffSegmentType.Unchanged && d.Text.Contains("I have a"));
    }

    [Fact]
    public void ComputeDiff_PureWordDeletions_IdentifiesDeletionSegments()
    {
        string original = "She bought a red, blue, and green dress.";
        string revised = "She bought a dress.";

        var diff = _engine.ComputeDiff(original, revised);

        Assert.Contains(diff, d => d.Type == DiffSegmentType.Deleted && d.Text.Contains("red, blue, and green"));
        Assert.Contains(diff, d => d.Type == DiffSegmentType.Unchanged && d.Text.Contains("She bought a"));
    }

    [Fact]
    public void ComputeDiff_WordModifications_IdentifiesBothDeletionAndAddition()
    {
        string original = "This design is bad and slow.";
        string revised = "This design is excellent and fast.";

        var diff = _engine.ComputeDiff(original, revised);

        Assert.Contains(diff, d => d.Type == DiffSegmentType.Deleted && d.Text.Contains("bad"));
        Assert.Contains(diff, d => d.Type == DiffSegmentType.Added && d.Text.Contains("excellent"));
        Assert.Contains(diff, d => d.Type == DiffSegmentType.Deleted && d.Text.Contains("slow"));
        Assert.Contains(diff, d => d.Type == DiffSegmentType.Added && d.Text.Contains("fast"));
    }

    [Fact]
    public void ComputeDiff_WhitespaceAndPunctuationChanges_HandlesDifferencesWithoutLoss()
    {
        string original = "Hello, world!";
        string revised = "Hello world.";

        var diff = _engine.ComputeDiff(original, revised);

        string reconstructedOriginal = string.Concat(diff.Where(d => d.Type != DiffSegmentType.Added).Select(d => d.Text));
        string reconstructedRevised = string.Concat(diff.Where(d => d.Type != DiffSegmentType.Deleted).Select(d => d.Text));

        Assert.Equal(original, reconstructedOriginal);
        Assert.Equal(revised, reconstructedRevised);
    }
}
