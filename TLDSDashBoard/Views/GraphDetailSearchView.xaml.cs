using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.Views;

/// <summary>
/// 그래프 상세검색 page — same mouse-wheel zoom / drag pan / hover crosshair as DrillModalView, but for the
/// single combined 12-channel chart shown inline on this page. See DrillModalView for the interaction
/// design notes; this is the same logic against GraphDetailSearchViewModel instead of MainViewModel.
/// </summary>
public partial class GraphDetailSearchView : UserControl
{
    private bool _isDragging;
    private double _lastDragX;

    public GraphDetailSearchView()
    {
        InitializeComponent();
        HoverTooltip.PlacementTarget = ChartArea;
    }

    private GraphDetailSearchViewModel? ViewModel => (DataContext as MainViewModel)?.GraphDetailSearch;

    private void ChartArea_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ChartArea.ActualWidth <= 0) return;
        double fraction = Clamp01(e.GetPosition(ChartArea).X / ChartArea.ActualWidth);
        double factor = e.Delta > 0 ? 0.8 : 1.25;
        ViewModel?.Zoom(factor, fraction);
        e.Handled = true;
    }

    private void ChartArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastDragX = e.GetPosition(ChartArea).X;
        ChartArea.CaptureMouse();
        e.Handled = true;
    }

    private void ChartArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ChartArea.ReleaseMouseCapture();
    }

    private void ChartArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (ChartArea.ActualWidth <= 0) return;
        var pos = e.GetPosition(ChartArea);
        double fraction = Clamp01(pos.X / ChartArea.ActualWidth);

        CrosshairLine.X1 = pos.X;
        CrosshairLine.X2 = pos.X;
        CrosshairLine.Y2 = ChartArea.ActualHeight;
        CrosshairLine.Visibility = Visibility.Visible;
        ViewModel?.UpdateHover(fraction);
        PositionTooltip(pos);

        if (_isDragging)
        {
            double fractionDelta = (pos.X - _lastDragX) / ChartArea.ActualWidth;
            ViewModel?.Pan(fractionDelta);
            _lastDragX = pos.X;
        }
    }

    private void ChartArea_MouseLeave(object sender, MouseEventArgs e)
    {
        CrosshairLine.Visibility = Visibility.Collapsed;
        HoverTooltip.IsOpen = false;
        ViewModel?.ClearHover();
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

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
