using System.Text.RegularExpressions;

namespace Polishly.Core;

public class WordDiffEngine
{
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}]+|[^\p{L}\p{N}\s]+|\s+", RegexOptions.Compiled);

    public IReadOnlyList<DiffSegment> ComputeDiff(string originalText, string newText)
    {
        originalText ??= string.Empty;
        newText ??= string.Empty;

        if (originalText.Length == 0 && newText.Length == 0)
        {
            return Array.Empty<DiffSegment>();
        }

        if (originalText == newText)
        {
            return new[] { new DiffSegment(DiffSegmentType.Unchanged, originalText) };
        }

        if (originalText.Length == 0)
        {
            return new[] { new DiffSegment(DiffSegmentType.Added, newText) };
        }

        if (newText.Length == 0)
        {
            return new[] { new DiffSegment(DiffSegmentType.Deleted, originalText) };
        }

        var tokensA = Tokenize(originalText);
        var tokensB = Tokenize(newText);

        int prefixIndex = 0;
        while (prefixIndex < tokensA.Count && prefixIndex < tokensB.Count && tokensA[prefixIndex] == tokensB[prefixIndex])
        {
            prefixIndex++;
        }

        int suffixA = tokensA.Count - 1;
        int suffixB = tokensB.Count - 1;
        while (suffixA >= prefixIndex && suffixB >= prefixIndex && tokensA[suffixA] == tokensB[suffixB])
        {
            suffixA--;
            suffixB--;
        }

        var rawSegments = new List<DiffSegment>();
        if (prefixIndex > 0)
        {
            rawSegments.Add(new DiffSegment(DiffSegmentType.Unchanged, string.Concat(tokensA.Take(prefixIndex))));
        }

        var middleA = tokensA.Skip(prefixIndex).Take(suffixA - prefixIndex + 1).ToList();
        var middleB = tokensB.Skip(prefixIndex).Take(suffixB - prefixIndex + 1).ToList();

        if (middleA.Count > 0 || middleB.Count > 0)
        {
            var middleDiff = ComputeLcsDiff(middleA, middleB);
            rawSegments.AddRange(middleDiff);
        }

        if (suffixA < tokensA.Count - 1)
        {
            rawSegments.Add(new DiffSegment(DiffSegmentType.Unchanged, string.Concat(tokensA.Skip(suffixA + 1))));
        }

        return MergeAdjacentSegments(rawSegments);
    }

    private static List<DiffSegment> ComputeLcsDiff(List<string> tokensA, List<string> tokensB)
    {
        if (tokensA.Count == 0 && tokensB.Count == 0) return new List<DiffSegment>();
        if (tokensA.Count == 0) return new List<DiffSegment> { new(DiffSegmentType.Added, string.Concat(tokensB)) };
        if (tokensB.Count == 0) return new List<DiffSegment> { new(DiffSegmentType.Deleted, string.Concat(tokensA)) };

        var n = tokensA.Count;
        var m = tokensB.Count;

        var dp = new int[n + 1, m + 1];

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                if (tokensA[i - 1] == tokensB[j - 1])
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }

        var tempSegments = new List<DiffSegment>();
        var currA = n;
        var currB = m;

        while (currA > 0 || currB > 0)
        {
            if (currA > 0 && currB > 0 && tokensA[currA - 1] == tokensB[currB - 1])
            {
                tempSegments.Add(new DiffSegment(DiffSegmentType.Unchanged, tokensA[currA - 1]));
                currA--;
                currB--;
            }
            else if (currB > 0 && (currA == 0 || dp[currA, currB - 1] >= dp[currA - 1, currB]))
            {
                tempSegments.Add(new DiffSegment(DiffSegmentType.Added, tokensB[currB - 1]));
                currB--;
            }
            else if (currA > 0 && (currB == 0 || dp[currA, currB - 1] < dp[currA - 1, currB]))
            {
                tempSegments.Add(new DiffSegment(DiffSegmentType.Deleted, tokensA[currA - 1]));
                currA--;
            }
        }

        tempSegments.Reverse();
        return tempSegments;
    }

    private static List<string> Tokenize(string text)
    {
        var matches = TokenRegex.Matches(text);
        var tokens = new List<string>(matches.Count);
        foreach (Match match in matches)
        {
            tokens.Add(match.Value);
        }
        return tokens;
    }

    private static List<DiffSegment> MergeAdjacentSegments(List<DiffSegment> segments)
    {
        if (segments.Count == 0)
        {
            return segments;
        }

        var merged = new List<DiffSegment>();
        var currentType = segments[0].Type;
        var currentText = new System.Text.StringBuilder(segments[0].Text);

        for (var i = 1; i < segments.Count; i++)
        {
            if (segments[i].Type == currentType)
            {
                currentText.Append(segments[i].Text);
            }
            else
            {
                merged.Add(new DiffSegment(currentType, currentText.ToString()));
                currentType = segments[i].Type;
                currentText.Clear();
                currentText.Append(segments[i].Text);
            }
        }

        merged.Add(new DiffSegment(currentType, currentText.ToString()));
        return merged;
    }
}
