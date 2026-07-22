namespace Polishly.Core.Models;

public record RewriteRequest(
    string InputText,
    RewriteMode Mode,
    string? CustomInstruction = null,
    string? TargetAppProcessName = null
);

public record RewriteResult(
    bool Success,
    string OriginalText,
    string RewrittenText,
    string? ErrorMessage = null
);
