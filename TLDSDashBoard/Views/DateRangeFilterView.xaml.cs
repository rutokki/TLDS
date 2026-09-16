using System.Windows;
using System.Windows.Controls;
using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.Views;

public partial class DateRangeFilterView : UserControl
{
    public static readonly DependencyProperty FilterProperty =
        DependencyProperty.Register(nameof(Filter), typeof(DateRangeFilter), typeof(DateRangeFilterView), new PropertyMetadata(null));

    public DateRangeFilter? Filter
    {
        get => (DateRangeFilter?)GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    public DateRangeFilterView()
    {
        InitializeComponent();
    }
}
