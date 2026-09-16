using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Converters;

/// <summary>Maps an AlarmSeverity to one of the Critical/Warning/Info text or background brushes in Colors.xaml — pass ConverterParameter="Bg" for the background, anything else (or omit it) for the text color.</summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AlarmSeverity severity) return Brushes.Transparent;
        bool bg = parameter as string == "Bg";

        string key = severity switch
        {
            AlarmSeverity.Critical => bg ? "CriticalBgBrush" : "CriticalTextBrush",
            AlarmSeverity.Warning => bg ? "WarningBgBrush" : "WarningTextBrush",
            _ => bg ? "InfoBgBrush" : "InfoTextBrush",
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
