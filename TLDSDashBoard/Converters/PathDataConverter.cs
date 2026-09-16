using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TLDSDashBoard.Converters;

/// <summary>Converts an "M x,y L x,y ..." path-data string (as produced by PathBuilder) into a Geometry for binding to Path.Data.</summary>
public sealed class PathDataConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s)) return Geometry.Empty;
        try
        {
            return Geometry.Parse(s);
        }
        catch
        {
            return Geometry.Empty;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
