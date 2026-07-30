using Polishly.WindowsIntegration.Native;

namespace Polishly.WindowsIntegration.Hotkey;

public readonly record struct ParsedHotkey(uint Modifiers, uint VirtualKey);

public static class HotkeyShortcutParser
{
    public static bool TryParse(string? shortcut, out ParsedHotkey hotkey, out string? error)
    {
        hotkey = default;
        error = null;
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            error = "A global hotkey is required.";
            return false;
        }

        string[] parts = shortcut.Split(
            '+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            error = "Use at least one modifier, for example Ctrl+Shift+P.";
            return false;
        }

        uint modifiers = 0;
        foreach (string modifier in parts[..^1])
        {
            uint parsedModifier = modifier.ToLowerInvariant() switch
            {
                "ctrl" or "control" => Win32Native.MOD_CONTROL,
                "shift" => Win32Native.MOD_SHIFT,
                "alt" => Win32Native.MOD_ALT,
                "win" or "windows" => Win32Native.MOD_WIN,
                _ => 0
            };
            if (parsedModifier == 0)
            {
                error = $"Unsupported modifier '{modifier}'.";
                return false;
            }
            modifiers |= parsedModifier;
        }

        if (modifiers == 0)
        {
            error = "The shortcut must include Ctrl, Shift, Alt, or Win.";
            return false;
        }

        string key = parts[^1].ToUpperInvariant();
        uint virtualKey;
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            virtualKey = key[0];
        }
        else if (key == "SPACE")
        {
            virtualKey = 0x20;
        }
        else if (key.StartsWith('F') &&
                 int.TryParse(key[1..], out int functionNumber) &&
                 functionNumber is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + functionNumber - 1);
        }
        else
        {
            error = $"Unsupported key '{parts[^1]}'.";
            return false;
        }

        hotkey = new ParsedHotkey(modifiers, virtualKey);
        return true;
    }
}
