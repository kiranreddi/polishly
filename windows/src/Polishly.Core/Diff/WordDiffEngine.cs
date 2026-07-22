namespace Polishly.Core.Diff;

public class WordDiffEngine
{
    public IReadOnlyList<DiffSegment> ComputeDiff(string originalText, string revisedText)
    {
        if (originalText == revisedText)
        {
            return string.IsNullOrEmpty(originalText)
                ? Array.Empty<DiffSegment>()
                : new List<DiffSegment> { new(DiffType.Unchanged, originalText) };
        }

        var oldWords = SplitIntoTokens(originalText);
        var newWords = SplitIntoTokens(revisedText);

        // Trim common prefix tokens
        int prefixIndex = 0;
        while (prefixIndex < oldWords.Count && prefixIndex < newWords.Count && oldWords[prefixIndex] == newWords[prefixIndex])
        {
            prefixIndex++;
        }

        // Trim common suffix tokens
        int oldSuffix = oldWords.Count - 1;
        int newSuffix = newWords.Count - 1;
        while (oldSuffix >= prefixIndex && newSuffix >= prefixIndex && oldWords[oldSuffix] == newWords[newSuffix])
        {
            oldSuffix--;
            newSuffix--;
        }

        var result = new List<DiffSegment>();

        if (prefixIndex > 0)
        {
            result.Add(new DiffSegment(DiffType.Unchanged, string.Concat(oldWords.Take(prefixIndex))));
        }

        var middleOld = oldWords.Skip(prefixIndex).Take(oldSuffix - prefixIndex + 1).ToList();
        var middleNew = newWords.Skip(prefixIndex).Take(newSuffix - prefixIndex + 1).ToList();

        if (middleOld.Count > 0 || middleNew.Count > 0)
        {
            var middleDiff = ComputeLcsDiff(middleOld, middleNew);
            result.AddRange(middleDiff);
        }

        if (oldSuffix < oldWords.Count - 1)
        {
            result.Add(new DiffSegment(DiffType.Unchanged, string.Concat(oldWords.Skip(oldSuffix + 1))));
        }

        return MergeAdjacentSegments(result);
    }

    private static List<DiffSegment> ComputeLcsDiff(List<string> oldWords, List<string> newWords)
    {
        if (oldWords.Count == 0 && newWords.Count == 0) return new List<DiffSegment>();
        if (oldWords.Count == 0) return new List<DiffSegment> { new(DiffType.Addition, string.Concat(newWords)) };
        if (newWords.Count == 0) return new List<DiffSegment> { new(DiffType.Deletion, string.Concat(oldWords)) };

        var lcs = ComputeLcsMatrix(oldWords, newWords);
        var tempSegments = new List<DiffSegment>();

        int i = oldWords.Count;
        int j = newWords.Count;

        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0 && oldWords[i - 1] == newWords[j - 1])
            {
                tempSegments.Add(new DiffSegment(DiffType.Unchanged, oldWords[i - 1]));
                i--;
                j--;
            }
            else if (j > 0 && (i == 0 || lcs[i, j - 1] >= lcs[i - 1, j]))
            {
                tempSegments.Add(new DiffSegment(DiffType.Addition, newWords[j - 1]));
                j--;
            }
            else if (i > 0 && (j == 0 || lcs[i, j - 1] < lcs[i - 1, j]))
            {
                tempSegments.Add(new DiffSegment(DiffType.Deletion, oldWords[i - 1]));
                i--;
            }
        }

        tempSegments.Reverse();
        return tempSegments;
    }

    private static List<string> SplitIntoTokens(string text)
    {
        var tokens = new List<string>();
        if (string.IsNullOrEmpty(text)) return tokens;

        int start = 0;
        bool inWord = char.IsLetterOrDigit(text[0]);

        for (int i = 1; i < text.Length; i++)
        {
            bool currentInWord = char.IsLetterOrDigit(text[i]);
            if (currentInWord != inWord)
            {
                tokens.Add(text[start..i]);
                start = i;
                inWord = currentInWord;
            }
        }
        tokens.Add(text[start..]);
        return tokens;
    }

    private static int[,] ComputeLcsMatrix(List<string> a, List<string> b)
    {
        int m = a.Count;
        int n = b.Count;
        int[,] matrix = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (a[i - 1] == b[j - 1])
                {
                    matrix[i, j] = matrix[i - 1, j - 1] + 1;
                }
                else
                {
                    matrix[i, j] = Math.Max(matrix[i - 1, j], matrix[i, j - 1]);
                }
            }
        }

        return matrix;
    }

    private static List<DiffSegment> MergeAdjacentSegments(List<DiffSegment> raw)
    {
        var merged = new List<DiffSegment>();
        foreach (var seg in raw)
        {
            if (merged.Count > 0 && merged[^1].Type == seg.Type)
            {
                merged[^1] = new DiffSegment(seg.Type, merged[^1].Text + seg.Text);
            }
            else
            {
                merged.Add(seg);
            }
        }
        return merged;
    }
}

