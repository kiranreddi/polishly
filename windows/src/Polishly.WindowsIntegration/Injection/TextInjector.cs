using Polishly.Core.Capabilities;
using Polishly.Core.Models;
using Polishly.WindowsIntegration.Clipboard;
#if HAS_WPF
using System.Windows.Automation;
#endif

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
            // UIA calls can block the WPF dispatcher while the target app is
            // servicing its automation provider, so keep the direct attempt
            // off the companion's UI thread.
            bool setDirectly = await Task.Run(() => TrySetFocusedText(newText));
            if (setDirectly)
            {
                return new InjectionResult(
                    Success: true,
                    MethodUsed: InjectionMethod.UIAutomationSetText
                );
            }
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

    private static bool TrySetFocusedText(string text)
    {
#if HAS_WPF
        try
        {
            var focusedElement = AutomationElement.FocusedElement;
            if (focusedElement != null &&
                focusedElement.TryGetCurrentPattern(ValuePattern.Pattern, out object valuePatternObject) &&
                valuePatternObject is ValuePattern valuePattern &&
                !valuePattern.Current.IsReadOnly)
            {
                valuePattern.SetValue(text);
                return true;
            }
        }
        catch
        {
            // Fall back to the guarded clipboard path when the target does
            // not expose a writable ValuePattern or UIA is unavailable.
        }
#endif
        return false;
    }
}
