namespace Polishly.Core.Diff;

public enum DiffType
{
    Unchanged,
    Addition,
    Deletion
}

public record DiffSegment(DiffType Type, string Text);
