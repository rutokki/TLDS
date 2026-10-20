using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TLDSDashBoard.Converters;

/// <summary>"#RRGGBB"/"#AARRGGBB" 형식의 16진수 문자열을 고정(frozen)된 SolidColorBrush로 변환한다.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex)!;
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                // 기본값으로 넘어감
            }
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
