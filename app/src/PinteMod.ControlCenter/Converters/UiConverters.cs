using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Simulation;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Converters;

public sealed class DurationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TimeSpan duration
            ? DurationDisplay.Format(duration)
            : "--:--";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class StatusBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = GetResourceKey(value);

        return Application.Current.TryFindResource(key) as Brush ?? Brushes.DodgerBlue;
    }

    public static string GetResourceKey(object? value) =>
        value switch
        {
            ServiceHealth.Healthy or EventSeverity.Success or RankedStatus.Ranked or PlayerLifeState.Alive => "SuccessBrush",
            ServiceHealth.Warning or EventSeverity.Warning or RankedStatus.Unranked or PlayerLifeState.Downed => "WarningBrush",
            ServiceHealth.Offline or ServiceHealth.Error or EventSeverity.Danger or SimulationStatus.Rejected or PlayerLifeState.Dead => "DangerBrush",
            ServiceHealth.Unknown or RankedStatus.Unknown or PlayerLifeState.Unknown => "TextSecondaryBrush",
            ServiceDeclaredState.Running or ServiceDeclaredState.Monitoring or ServiceDeclaredState.Connected or ServiceDeclaredState.Active => "SuccessBrush",
            ServiceDeclaredState.Paused or ServiceDeclaredState.Configured => "WarningBrush",
            ServiceDeclaredState.Stopped or ServiceDeclaredState.Error => "DangerBrush",
            ServiceDeclaredState.Unknown => "TextSecondaryBrush",
            LocalReadStatus.Success => "SuccessBrush",
            LocalReadStatus.Empty or LocalReadStatus.Invalid or LocalReadStatus.UnsupportedSchema or LocalReadStatus.AccessDenied or LocalReadStatus.IoError => "WarningBrush",
            LocalReadStatus.NotAttempted or LocalReadStatus.Missing => "TextSecondaryBrush",
            DataFreshness.Fresh => "SuccessBrush",
            DataFreshness.Stale => "WarningBrush",
            DataFreshness.Expired or DataFreshness.Unknown => "TextSecondaryBrush",
            DataProvenance.LocalFile => "AccentBrightBrush",
            DataProvenance.MemoryCache => "WarningBrush",
            DataProvenance.Simulation => "AccentBrightBrush",
            DataProvenance.Unavailable => "TextSecondaryBrush",
            SimulationStatus.Simulated => "AccentBrightBrush",
            _ => "AccentBrightBrush"
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class StatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            ServiceHealth.Healthy => "CONNECTÉ",
            ServiceHealth.Warning => "ATTENTION",
            ServiceHealth.Offline => "HORS LIGNE",
            ServiceHealth.Error => "ERREUR",
            ServiceHealth.Unknown => "INCONNU",
            ServiceDeclaredState.Running => "EN COURS",
            ServiceDeclaredState.Monitoring => "SUPERVISION",
            ServiceDeclaredState.Connected => "CONNECTÉ",
            ServiceDeclaredState.Active => "ACTIF",
            ServiceDeclaredState.Paused => "EN PAUSE",
            ServiceDeclaredState.Configured => "CONFIGURÉ",
            ServiceDeclaredState.Stopped => "ARRÊTÉ",
            ServiceDeclaredState.Error => "ERREUR",
            ServiceDeclaredState.Unknown => "INCONNU",
            LocalReadStatus.NotAttempted => "NON TENTÉE",
            LocalReadStatus.Success => "RÉUSSIE",
            LocalReadStatus.Missing => "ABSENTE",
            LocalReadStatus.Empty => "VIDE",
            LocalReadStatus.Invalid => "INVALIDE",
            LocalReadStatus.UnsupportedSchema => "SCHÉMA INCOMPATIBLE",
            LocalReadStatus.AccessDenied => "ACCÈS REFUSÉ",
            LocalReadStatus.IoError => "ERREUR DE LECTURE",
            DataFreshness.Fresh => "FRAÎCHE",
            DataFreshness.Stale => "RETARDÉE",
            DataFreshness.Expired => "EXPIRÉE",
            DataFreshness.Unknown => "INCONNUE",
            RankedStatus.Ranked => "RANKED",
            RankedStatus.Unranked => "UNRANKED",
            RankedStatus.Unknown => "INCONNU",
            PlayerLifeState.Alive => "EN VIE",
            PlayerLifeState.Downed => "À TERRE",
            PlayerLifeState.Dead => "MORT",
            PlayerLifeState.Spectator => "SPECTATEUR",
            PlayerLifeState.Unknown => "INCONNU",
            _ => value?.ToString()?.ToUpperInvariant() ?? "INCONNU"
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is not null;
        if (string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class InvertedBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class SidebarWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double width && width <= 1100 ? new GridLength(82) : new GridLength(224);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class WidthToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var parts = (parameter?.ToString() ?? "1100").Split(':', StringSplitOptions.RemoveEmptyEntries);
        var threshold = double.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 1100;
        var visible = value is double width && width > threshold;
        if (parts.Length > 1 && string.Equals(parts[1], "Invert", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
