using Polishly.Core.Models;

namespace Polishly.WindowsIntegration.Clipboard;

public record FormatSnapshot(
    uint FormatId,
    string FormatName,
    byte[] Data
);

public record ClipboardTransactionResult(
    bool Success,
    bool RestoredOriginalClipboard,
    bool FallbackToCopy,
    string? ErrorMessage = null
);

public interface IClipboardTransaction
{
    Task<ClipboardTransactionResult> ExecuteSafePasteAsync(string textToPaste, TargetContext targetContext, CancellationToken ct = default);
    Task<uint> GetSequenceNumberAsync(CancellationToken ct = default);
}
