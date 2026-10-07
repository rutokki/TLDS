using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TLDSDashBoard.Converters;

/// <summary>Visible when the bound count is 0 (or null), Collapsed otherwise — drives the "no results yet"
/// empty-state message layered over each 조회 page's DataGrid/chart (bind to <c>Results.Count</c> etc.).</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || (value is int n && n == 0) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
