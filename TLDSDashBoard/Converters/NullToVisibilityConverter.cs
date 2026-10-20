using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TLDSDashBoard.Converters;

/// <summary>바인딩 값이 null이 아니면(문자열은 비어 있지 않아야) Visible, 아니면 Collapsed.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool has = value switch
        {
            null => false,
            string s => !string.IsNullOrEmpty(s),
            _ => true
        };
        return has ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
