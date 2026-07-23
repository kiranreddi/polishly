using Polishly.Core.Capabilities;
using Polishly.Core.Models;
using Polishly.WindowsIntegration.Clipboard;

namespace Polishly.WindowsIntegration.Injection;

public class TextInjector : IInjectorEngine
{
    private readonly IClipboardTransaction _clipboardTransaction;
    private readonly AppCapabilityRules _capabilityRules;

    public TextInjector(IClipboardTransaction clipboardTransaction, AppCapabilityRules capabilityRules)
    {
        _clipboardTransaction = clipboardTransaction;
        _capabilityRules = capabilityRules;
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

        var profile = _capabilityRules.GetProfile(context.ProcessName);

        if (profile.PreferredInjection == InjectionMethod.UIAutomationSetText)
        {
            return new InjectionResult(
                Success: true,
                MethodUsed: InjectionMethod.UIAutomationSetText
            );
        }

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
            ErrorMessage: clipboardResult.ErrorMessage ?? "Guarded paste failed, fallback to copy."
        );
    }
}
