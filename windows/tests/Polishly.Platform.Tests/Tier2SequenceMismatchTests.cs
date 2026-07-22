using Polishly.Core.Models;
using Polishly.WindowsIntegration.Clipboard;
using Xunit;

namespace Polishly.Platform.Tests;

public class Tier2SequenceMismatchTests
{
    [Fact]
    public async Task ExecuteSafePasteAsync_SequenceMismatch_AbortsPasteAndTriggersCopyFallback()
    {
        uint currentSeq = 100;
        // Simulate sequence number changing during paste execution
        Func<uint> sequenceGetter = () => ++currentSeq;

        var transaction = new GuardedClipboardTransaction(sequenceGetter);
        var target = new TargetContext(IntPtr.Zero, 1, "notepad", "Notepad", "f1", false, false);

        var result = await transaction.ExecuteSafePasteAsync("Polished text", target);

        Assert.False(result.Success);
        Assert.True(result.FallbackToCopy);
        Assert.False(result.RestoredOriginalClipboard);
        Assert.Contains("sequence number mismatch", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteSafePasteAsync_SequenceMismatch_LeavesErrorMessageDetailingConcurrentModification()
    {
        uint currentSeq = 50;
        Func<uint> sequenceGetter = () => ++currentSeq;

        var transaction = new GuardedClipboardTransaction(sequenceGetter);
        var target = new TargetContext(IntPtr.Zero, 1, "notepad", "Notepad", "f1", false, false);

        var result = await transaction.ExecuteSafePasteAsync("Text", target);

        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("concurrent modification", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteSafePasteAsync_MatchingSequenceNumber_SucceedsWithoutFallback()
    {
        Func<uint> sequenceGetter = () => 42; // Constant sequence number
        var transaction = new GuardedClipboardTransaction(sequenceGetter);
        var target = new TargetContext(IntPtr.Zero, 1, "notepad", "Notepad", "f1", false, false);

        var result = await transaction.ExecuteSafePasteAsync("Text", target);

        Assert.True(result.Success);
        Assert.False(result.FallbackToCopy);
        Assert.True(result.RestoredOriginalClipboard);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteSafePasteAsync_SequenceMismatch_DoesNotRestoreUserClipboardOverridingContent()
    {
        uint seqCall = 0;
        Func<uint> sequenceGetter = () =>
        {
            seqCall++;
            return seqCall == 1 ? 10u : 20u; // Different sequence on second call
        };

        var transaction = new GuardedClipboardTransaction(sequenceGetter);
        var target = new TargetContext(IntPtr.Zero, 1, "notepad", "Notepad", "f1", false, false);

        var result = await transaction.ExecuteSafePasteAsync("Replacement", target);

        Assert.False(result.RestoredOriginalClipboard);
        Assert.True(result.FallbackToCopy);
    }

    [Fact]
    public async Task GetSequenceNumberAsync_ReflectsSimulatedSequenceProvider()
    {
        Func<uint> sequenceGetter = () => 999u;
        var transaction = new GuardedClipboardTransaction(sequenceGetter);

        uint seq = await transaction.GetSequenceNumberAsync();

        Assert.Equal(999u, seq);
    }
}
