using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Converters;

/// <summary>
/// Maps an AlarmSeverity to its pill background/foreground brush. Pass ConverterParameter="Bg" or
/// "Fg" to pick which. Looks the brush up from Application resources (Themes/Colors.xaml) so the
/// palette stays centralized.
/// </summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AlarmSeverity severity) return Brushes.Gray;
        bool wantBg = string.Equals(parameter as string, "Bg", StringComparison.OrdinalIgnoreCase);

        string key = severity switch
        {
            AlarmSeverity.Critical => wantBg ? "CriticalBgBrush" : "CriticalTextBrush",
            AlarmSeverity.Warning => wantBg ? "WarningBgBrush" : "WarningTextBrush",
            _ => wantBg ? "InfoBgBrush" : "InfoTextBrush"
        };

        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
