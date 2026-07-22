using System;
using System.Collections.Generic;
using System.Reflection;
using Polishly.Core.Models;

namespace Polishly.WindowsIntegration.Security;

public class SensitiveFieldDetector : ISensitiveFieldDetector
{
    private static readonly HashSet<string> SensitiveProcessBlocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "1password", "keepass", "keepassxc", "bitwarden", "dashlane", "lastpass", "cmd", "powershell"
    };

    public SensitiveFieldStatus IsSensitiveField(TargetWindow window, object? automationElement = null)
    {
        if (window.IsElevated)
        {
            return new SensitiveFieldStatus(true, "Target application process is running elevated.");
        }

        if (SensitiveProcessBlocklist.Contains(window.ProcessName))
        {
            return new SensitiveFieldStatus(true, $"Process '{window.ProcessName}' is in the sensitive application blocklist.");
        }

        // Inspect AutomationElement for IsPassword property if supplied
        if (automationElement != null)
        {
            try
            {
                var isPasswordProp = automationElement.GetType().GetProperty("IsPassword", BindingFlags.Public | BindingFlags.Instance);
                if (isPasswordProp != null)
                {
                    var val = isPasswordProp.GetValue(automationElement);
                    if (val is bool isPass && isPass)
                    {
                        return new SensitiveFieldStatus(true, "Focused element is marked as a password field (IsPassword=true).");
                    }
                }

                var currentProp = automationElement.GetType().GetProperty("Current", BindingFlags.Public | BindingFlags.Instance);
                if (currentProp != null)
                {
                    var currentObj = currentProp.GetValue(automationElement);
                    if (currentObj != null)
                    {
                        var isPassInner = currentObj.GetType().GetProperty("IsPassword")?.GetValue(currentObj);
                        if (isPassInner is bool isPass && isPass)
                        {
                            return new SensitiveFieldStatus(true, "UI Automation element property Current.IsPassword is true.");
                        }

                        var controlTypeInner = currentObj.GetType().GetProperty("ControlType")?.GetValue(currentObj)?.ToString();
                        if (controlTypeInner != null && controlTypeInner.Contains("Password", StringComparison.OrdinalIgnoreCase))
                        {
                            return new SensitiveFieldStatus(true, "UI Automation ControlType indicates password field.");
                        }
                    }
                }
            }
            catch
            {
                // Fall back to title inspection if automation element reflection fails
            }
        }

        if (window.Title.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            window.Title.Contains("Credential", StringComparison.OrdinalIgnoreCase))
        {
            return new SensitiveFieldStatus(true, "Window title indicates sensitive or password field.");
        }

        return SensitiveFieldStatus.Safe;
    }
}

