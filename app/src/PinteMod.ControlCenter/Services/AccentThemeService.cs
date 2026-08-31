using System.Windows;
using System.Windows.Media;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Services;

public sealed record AccentThemeOption(
    string Key,
    string DisplayName,
    Color AccentColor,
    Color BrightColor,
    Color SoftColor)
{
    public Brush PreviewBrush { get; } = CreateFrozenBrush(AccentColor);

    private static Brush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

public static class AccentThemeService
{
    public static IReadOnlyList<AccentThemeOption> Options { get; } =
    [
        Create("blue", "Bleu PinteMod", "#168DFF", "#42B7FF", "#102A42"),
        Create("cyan", "Cyan électrique", "#00A8CC", "#4DDFFF", "#0B3440"),
        Create("indigo", "Indigo", "#5A67FF", "#8B95FF", "#1B214A"),
        Create("violet", "Violet", "#8B5CF6", "#B794FF", "#2A1D46"),
        Create("pink", "Rose néon", "#D946EF", "#F08CFF", "#421548"),
        Create("teal", "Turquoise", "#0FAF9A", "#4DE1CA", "#0D3836")
    ];

    public static AccentThemeOption Resolve(string? key) =>
        Options.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.Ordinal))
        ?? Options[0];

    public static void Apply(string? key)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        // Startup can resume while WPF is still finalising its application
        // resources on slower/remote Windows sessions.  The default XAML theme
        // is already valid, so a cosmetic accent must never prevent the window
        // from opening.
        if (!application.Dispatcher.CheckAccess())
        {
            application.Dispatcher.Invoke(() => Apply(key));
            return;
        }

        try
        {
            var option = Resolve(OperatorAccentTheme.NormalizeOrDefault(key));
            application.Resources["AccentBrush"] = new SolidColorBrush(option.AccentColor);
            application.Resources["AccentBrightBrush"] = new SolidColorBrush(option.BrightColor);
            application.Resources["AccentSoftBrush"] = new SolidColorBrush(option.SoftColor);
        }
        catch (InvalidOperationException)
        {
            // Keep the default accent from App.xaml. It is preferable to a
            // failed cosmetic choice on a remote/compatibility WPF session.
        }
    }

    private static AccentThemeOption Create(
        string key,
        string displayName,
        string accent,
        string bright,
        string soft) =>
        new(
            key,
            displayName,
            (Color)ColorConverter.ConvertFromString(accent),
            (Color)ColorConverter.ConvertFromString(bright),
            (Color)ColorConverter.ConvertFromString(soft));
}
