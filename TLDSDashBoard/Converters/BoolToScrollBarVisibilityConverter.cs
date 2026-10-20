using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace TLDSDashBoard.Converters;

/// <summary>bool을 ScrollBarVisibility로 변환: true면 Auto, false면 Disabled. 호출한 쪽에서 의도적으로
/// 확대했을 때(예: GraphDetailSearchView의 세로 확대)만 ScrollViewer의 세로 스크롤바가 나타나게 하려는
/// 용도다. 기본 한 화면 크기에서 서브픽셀 반올림 오차 때문에 "Auto" 스크롤바가 깜빡 나타나면 Viewbox로
/// 늘어난 차트의 폭이 몇 픽셀 줄어 차트 크기가 눈에 띄게 바뀌는 문제가 있었다.</summary>
public sealed class BoolToScrollBarVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
