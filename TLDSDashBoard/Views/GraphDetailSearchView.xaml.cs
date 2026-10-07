using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TLDSDashBoard.Services;
using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.Views;

/// <summary>
/// 그래프 상세검색 page — same mouse-wheel zoom / drag pan / hover crosshair as DrillModalView, but for the
/// single combined channel chart shown inline on this page. See DrillModalView for the interaction
/// design notes; this is the same logic against GraphDetailSearchViewModel instead of MainViewModel.
/// </summary>
public partial class GraphDetailSearchView : UserControl
{
    private bool _isDragging;
    private double _lastDragX;
    private double _lastDragY;

    public GraphDetailSearchView()
    {
        InitializeComponent();
        HoverTooltip.PlacementTarget = ChartArea;
    }

    private GraphDetailSearchViewModel? ViewModel => (DataContext as MainViewModel)?.GraphDetailSearch;

    private void ChartArea_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var vm = ViewModel;
        if (vm is null) return;

        if (vm.IsVerticalZoomMode)
        {
            // 세로 확대 모드: wheel resizes lanes instead of the time window, anchored on the cursor —
            // same idea as the horizontal zoom anchoring on the cursor's X fraction below, just on the Y
            // axis: whatever content point is under the pointer should stay under it after the resize,
            // not just grow away from the top.
            int laneCount = vm.Series.Count;
            if (laneCount > 0)
            {
                double mouseY = e.GetPosition(LanesScroll).Y;
                double contentY = LanesScroll.VerticalOffset + mouseY;
                double oldExtent = vm.LaneHeight * laneCount;

                vm.AdjustLaneZoom(e.Delta > 0 ? 1.15 : 1 / 1.15);

                double newExtent = vm.LaneHeight * laneCount;
                double scaleRatio = oldExtent > 0 ? newExtent / oldExtent : 1;
                double newOffset = Math.Max(0, contentY * scaleRatio - mouseY);

                // The lane Borders haven't re-measured to their new Height yet at this point in the
                // handler (that happens on the next layout pass) — defer the actual scroll until after
                // layout so ScrollToVerticalOffset clamps against the up-to-date ScrollableHeight.
                Dispatcher.BeginInvoke(new Action(() => LanesScroll.ScrollToVerticalOffset(newOffset)),
                    System.Windows.Threading.DispatcherPriority.Loaded);
            }
            e.Handled = true;
            return;
        }

        if (ChartArea.ActualWidth <= 0) return;
        double fraction = Clamp01(e.GetPosition(ChartArea).X / ChartArea.ActualWidth);
        double factor = e.Delta > 0 ? 0.8 : 1.25;
        vm.Zoom(factor, fraction);
        e.Handled = true;
    }

    /// <summary>Recomputes the base (unzoomed) lane height so all lanes exactly fill the lanes
    /// ScrollViewer's viewport — fires on load and on window resize. LaneHeightScale (see 세로 확대) then
    /// multiplies this base value; at scale 1.0 all lanes fit with no scrollbar.</summary>
    private void LanesScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Lane count = channel count (ChannelCatalog), not a fixed number — the lanes must fill the
        // viewport whatever the catalog holds, even before the first 조회 has populated Series.
        ViewModel?.SetBaseLaneHeight(LanesScroll.ActualHeight / ChannelCatalog.ChannelCount);
    }

    private void ResetLaneZoomButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ResetLaneZoom();
    }

    private void ChartArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastDragX = e.GetPosition(ChartArea).X;
        _lastDragY = e.GetPosition(LanesScroll).Y;
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
        // Nothing queried yet (or the last 조회 found no data): no crosshair/tooltip over the empty card —
        // it used to draw a crosshair and an empty tooltip box over the blank area.
        if (ChartArea.ActualWidth <= 0) return;
        if (ViewModel is not { Series.Count: > 0 })
        {
            CrosshairLine.Visibility = Visibility.Collapsed;
            HoverTooltip.IsOpen = false;
            return;
        }
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

            // Vertical drag scrolls the lane stack the same "grab and drag" way — content follows the
            // cursor, so dragging down reveals lanes above. A no-op when nothing overflows (see
            // HasVerticalOverflow) since ScrollableHeight is then 0.
            //
            // The Y delta MUST be measured against LanesScroll (the fixed viewport frame), not ChartArea —
            // ChartArea lives inside the vertically-scrolled content, so scrolling it moves it on screen,
            // which corrupts GetPosition(ChartArea).Y on the very next MouseMove with a synthetic jump that
            // didn't come from real cursor movement, feeding back into another scroll and so on — this was
            // the reported violent shaking. Same root-cause class as the tooltip Popup fix elsewhere in
            // this file's history: never measure a drag delta against an element that moves as a RESULT of
            // applying that same delta. LanesScroll itself doesn't move when its own content scrolls, so
            // GetPosition(LanesScroll) stays stable.
            double posInScrollY = e.GetPosition(LanesScroll).Y;
            double deltaY = posInScrollY - _lastDragY;
            if (deltaY != 0)
            {
                LanesScroll.ScrollToVerticalOffset(LanesScroll.VerticalOffset - deltaY);
            }
            _lastDragY = posInScrollY;
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
