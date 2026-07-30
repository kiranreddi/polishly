using Polishly.Core.Capabilities;
using Polishly.Core.Models;
using Polishly.WindowsIntegration.Clipboard;

namespace Polishly.WindowsIntegration.Injection;

public class TextInjector : IInjectorEngine
{
    private readonly IClipboardTransaction _clipboardTransaction;

    public TextInjector(IClipboardTransaction clipboardTransaction, AppCapabilityRules capabilityRules)
    {
        _clipboardTransaction = clipboardTransaction;
        _ = capabilityRules;
    }

    public async Task<InjectionResult> InjectTextAsync(TargetContext context, string newText, CancellationToken ct = default)
    {
        if (context.IsPassword || context.IsElevated)
        {
            return new InjectionResult(
                Success: false,
                MethodUsed: InjectionMethod.CopyToClipboardOnly,
                ErrorMessage: "Target context is elevated or sensitive password field."
            );
        }

        // TextPattern exposes the selected range but does not provide a safe
        // replacement API. ValuePattern.SetValue replaces the entire field, so it
        // must not be used for a partial selection. The guarded transaction is the
        // only generally safe path until a field-specific adapter proves otherwise.
        var clipboardResult = await _clipboardTransaction.ExecuteSafePasteAsync(newText, context, ct);
        if (clipboardResult.Success)
        {
            return new InjectionResult(
                Success: true,
                MethodUsed: InjectionMethod.GuardedPasteTransaction
            );
        }

        return new InjectionResult(
            Success: false,
            MethodUsed: InjectionMethod.CopyToClipboardOnly,
            ErrorMessage: clipboardResult.ErrorMessage ??
                          "Guarded paste failed. Use Copy to keep the rewrite without replacing text."
        );
    }
}
