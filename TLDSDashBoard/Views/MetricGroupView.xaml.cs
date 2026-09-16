using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.Views;

/// <summary>
/// One TX or RX group's stacked-lane chart block (one mini-chart per channel + time axis). Used
/// twice in MainWindow, each pointed at a different <see cref="CombinedChartVM"/> via <see cref="Data"/>.
/// Also owns the hover interaction: mouse position anywhere over <c>ChartArea</c> (which spans the
/// full stack of lanes, not just one) is converted to a sample index, which drives one crosshair
/// spanning every lane, every lane's displayed value (via each series' mutable
/// <see cref="ChannelSeriesVM.Value"/>), and the hovered-time chip (<see cref="CombinedChartVM.HoverTimeLabel"/>).
/// Unlike the earlier fixed-900-unit chart canvases, <c>ChartArea</c> itself is not wrapped in a
/// Viewbox — it is stretched to its real on-screen size, so mouse coordinates from
/// <see cref="MouseEventArgs.GetPosition"/> are already real pixels and line up directly with the
/// per-lane charts (each independently scaled by its own Viewbox to that same on-screen width).
/// </summary>
public partial class MetricGroupView : UserControl
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(CombinedChartVM), typeof(MetricGroupView), new PropertyMetadata(null));

    public CombinedChartVM? Data
    {
        get => (CombinedChartVM?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public MetricGroupView()
    {
        InitializeComponent();
        HoverTooltip.PlacementTarget = ChartArea;
    }

    private void ChartArea_MouseMove(object sender, MouseEventArgs e)
    {
        var data = Data;
        if (data is null || data.WindowTimestamps.Length == 0 || ChartArea.ActualWidth <= 0) return;

        int n = data.WindowTimestamps.Length;
        var pos = e.GetPosition(ChartArea);
        double frac = pos.X / ChartArea.ActualWidth;
        if (frac < 0) frac = 0;
        if (frac > 1) frac = 1;

        int index = n == 1 ? 0 : (int)Math.Round(frac * (n - 1));
        if (index < 0) index = 0;
        if (index > n - 1) index = n - 1;

        CrosshairLine.X1 = pos.X;
        CrosshairLine.X2 = pos.X;
        CrosshairLine.Y2 = ChartArea.ActualHeight;
        CrosshairLine.Visibility = Visibility.Visible;

        data.HoverTimeLabel = data.WindowTimestamps[index].ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        foreach (var series in data.Series)
        {
            series.ShowValueAt(index);
        }

        PositionTooltip(pos);
    }

    private void ChartArea_MouseLeave(object sender, MouseEventArgs e)
    {
        CrosshairLine.Visibility = Visibility.Collapsed;
        HoverTooltip.IsOpen = false;
        var data = Data;
        if (data is null) return;

        data.HoverTimeLabel = string.Empty;
        foreach (var series in data.Series)
        {
            series.ResetToLatest();
        }
    }

    /// <summary>Moves the tooltip to follow the cursor (offset down-right). Popup.HorizontalOffset/
    /// VerticalOffset reposition its separate top-level window directly — no Measure/clamp dance needed,
    /// and no risk of the reposition itself perturbing ChartArea's hit-testing (see the Popup comment
    /// in the XAML for why a plain repositioned Border caused the jumpy/stuck behavior this replaces).</summary>
    private void PositionTooltip(Point pos)
    {
        const double offset = 16;
        HoverTooltip.HorizontalOffset = pos.X + offset;
        HoverTooltip.VerticalOffset = pos.Y + offset;
        HoverTooltip.IsOpen = true;
    }
}
