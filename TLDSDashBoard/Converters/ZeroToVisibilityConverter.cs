using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TLDSDashBoard.Converters;

/// <summary>바인딩된 개수가 0(또는 null)이면 Visible, 아니면 Collapsed — 각 조회 페이지의 DataGrid/차트
/// 위에 겹쳐 둔 "아직 결과 없음" 빈 상태 안내 문구를 제어한다(<c>Results.Count</c> 등에 바인딩).</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || (value is int n && n == 0) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
