using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.Converters;

/// <summary>AlarmSeverity를 Colors.xaml의 Critical/Warning/Info 글자색 또는 배경색 브러시 중 하나로 매핑한다 — 배경색은 ConverterParameter="Bg"를 넘기고, 그 외(또는 생략)는 글자색.</summary>
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
