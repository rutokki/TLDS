using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace TLDSDashBoard.Converters;

/// <summary>Converts a bool to ScrollBarVisibility: Auto when true, Disabled when false. Used so a
/// ScrollViewer's vertical scrollbar only ever appears once the caller has deliberately zoomed in (e.g.
/// GraphDetailSearchView's 세로 확대) instead of an "Auto" scrollbar flickering into existence from a
/// sub-pixel rounding overshoot at the normal fit-one-screen size — that flicker was stealing a few
/// pixels of width from the Viewbox-stretched charts and visibly resizing them.</summary>
public sealed class BoolToScrollBarVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
