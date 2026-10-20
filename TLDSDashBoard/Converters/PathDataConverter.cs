using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TLDSDashBoard.Converters;

/// <summary>"M x,y L x,y ..." 형식의 경로 데이터 문자열(PathBuilder가 만든 것)을 Path.Data에 바인딩할 Geometry로 변환한다.</summary>
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
