using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TLDSDashBoard.Converters;

/// <summary>Visible when the bound value is non-null (and, for strings, non-empty); Collapsed otherwise.</summary>
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
