using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TLDSDashBoard.Converters;

/// <summary>Converts a bool to FontWeight: SemiBold when true, Normal when false.</summary>
public sealed class BoldWhenTrueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? FontWeights.SemiBold : FontWeights.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
