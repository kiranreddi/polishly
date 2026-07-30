#if HAS_WPF
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
#endif

namespace Polishly.App.Services;

public sealed class ThemeService
{
    public string CurrentTheme { get; private set; } = "System";

    public void ApplyTheme(string themeName)
    {
        CurrentTheme = themeName;
        Apply(themeName);
    }

#if HAS_WPF
    public static void Apply(string theme)
    {
        Application? app = Application.Current;
        if (app == null) return;

        if (SystemParameters.HighContrast)
        {
            Set(app, SystemColors.WindowBrush, SystemColors.ControlBrush,
                SystemColors.WindowTextBrush, SystemColors.GrayTextBrush,
                SystemColors.HighlightBrush, SystemColors.ControlTextBrush);
            return;
        }

        bool light = theme.Equals("Light", StringComparison.OrdinalIgnoreCase) ||
                     (theme.Equals("System", StringComparison.OrdinalIgnoreCase) &&
                      SystemPrefersLight());
        if (light)
        {
            Set(app,
                Brush("#F7F8FA"), Brush("#FFFFFF"), Brush("#171A21"),
                Brush("#606979"), Brush("#007F73"), Brush("#B42318"));
        }
        else
        {
            Set(app,
                Brush("#17191F"), Brush("#22252D"), Brush("#F3F4F6"),
                Brush("#A8AFBD"), Brush("#14B8A6"), Brush("#FF7B72"));
        }
    }

    private static void Set(
        Application app, Brush background, Brush surface, Brush text,
        Brush muted, Brush accent, Brush danger)
    {
        app.Resources["Polishly.Background"] = background;
        app.Resources["Polishly.Surface"] = surface;
        app.Resources["Polishly.Text"] = text;
        app.Resources["Polishly.Muted"] = muted;
        app.Resources["Polishly.Accent"] = accent;
        app.Resources["Polishly.Danger"] = danger;
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }

    private static bool SystemPrefersLight()
    {
        try
        {
            object? value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return Convert.ToInt32(value) != 0;
        }
        catch
        {
            return true;
        }
    }
#else
    public static void Apply(string theme) { }
#endif
}
