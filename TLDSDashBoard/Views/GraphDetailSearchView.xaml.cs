using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TLDSDashBoard.Services;
using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.Views;

/// <summary>
/// 그래프 상세검색 페이지 — DrillModalView와 같은 마우스 휠 줌 / 드래그 팬 / 호버 크로스헤어이지만, 이 페이지에
/// 인라인으로 표시되는 하나의 통합 채널 차트용이다. 상호작용 설계 메모는 DrillModalView를 참고;
/// 이것은 MainViewModel 대신 GraphDetailSearchViewModel에 대해 같은 로직을 쓴다.
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
            // 세로 확대 모드: 휠이 시간 창 대신 레인 크기를 조절하며, 커서를 기준으로 한다 — 아래 가로 줌이 커서의
            // X 비율을 기준으로 하는 것과 같은 발상을 Y축에 적용한 것이다: 포인터 아래에 있던 콘텐츠 지점은 크기를
            // 바꾼 뒤에도 그 아래에 그대로 있어야 하고, 위쪽에서 멀어지듯 커지기만 해서는 안 된다.
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

                // 이 핸들러 시점에는 레인 Border들이 아직 새 Height로 다시 측정되지 않았다(다음 레이아웃 패스에서
                // 일어난다) — 실제 스크롤은 레이아웃 이후로 미뤄서, ScrollToVerticalOffset이 최신 ScrollableHeight를
                // 기준으로 범위를 제한하게 한다.
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

    /// <summary>기본(확대하지 않은) 레인 높이를 다시 계산해서 모든 레인이 레인 ScrollViewer의 뷰포트를
    /// 정확히 채우게 한다 — 로드 시와 창 크기 변경 시 실행된다. LaneHeightScale(세로 확대 참고)이 이
    /// 기본값에 곱해지며, 배율 1.0에서는 모든 레인이 스크롤바 없이 들어간다.</summary>
    private void LanesScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 레인 수 = 채널 수(ChannelCatalog)이지 고정된 숫자가 아니다 — 첫 조회가 Series를 채우기 전이라도,
        // 카탈로그에 몇 개가 들어 있든 레인이 뷰포트를 채워야 한다.
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
        // 아직 조회 전(또는 마지막 조회에 데이터가 없음): 빈 카드 위에는 크로스헤어/툴팁이 없다 —
        // 예전에는 빈 영역 위에 크로스헤어와 빈 툴팁 상자가 그려졌다.
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

            // 세로 드래그는 같은 "잡고 끄는" 방식으로 레인 묶음을 스크롤한다 — 콘텐츠가 커서를 따라오므로, 아래로
            // 드래그하면 위쪽 레인이 보인다. 넘치는 것이 없으면(HasVerticalOverflow 참고) ScrollableHeight가 0이라
            // 아무 일도 일어나지 않는다.
            //
            // Y 변화량은 ChartArea가 아니라 반드시 LanesScroll(고정된 뷰포트 프레임)을 기준으로 재야 한다 —
            // ChartArea는 세로로 스크롤되는 콘텐츠 안에 있어서 스크롤하면 화면상 위치가 움직이고, 그러면 바로
            // 다음 MouseMove에서 GetPosition(ChartArea).Y가 실제 커서 이동에서 나온 것이 아닌 합성 점프로 오염되어
            // 또 다른 스크롤로 되먹임되는 식이 된다 — 이것이 보고된 심한 흔들림이었다. 이 파일의 이력에 있는
            // 툴팁 Popup 수정과 같은 종류의 원인이다: 드래그 변화량을, 그 변화량을 적용한 결과로 움직이는 요소를
            // 기준으로 재면 안 된다. LanesScroll 자체는 안쪽 콘텐츠가 스크롤되어도 움직이지 않으므로
            // GetPosition(LanesScroll)은 안정적이다.
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

    /// <summary>툴팁이 커서를 따라가도록 옮긴다(오른쪽 아래로 오프셋). Popup.HorizontalOffset/VerticalOffset은
    /// 별도의 최상위 창 위치를 직접 바꾸므로 측정/제한(clamp) 처리가 필요 없고, 위치 변경 자체가
    /// ChartArea의 히트 테스트를 흔들 위험도 없다(일반 위치 지정 Border가 이것이 대체한 튀거나 멈추는
    /// 현상을 일으킨 이유는 XAML의 Popup 주석 참고).</summary>
    private void PositionTooltip(Point pos)
    {
        const double offset = 16;
        HoverTooltip.HorizontalOffset = pos.X + offset;
        HoverTooltip.VerticalOffset = pos.Y + offset;
        HoverTooltip.IsOpen = true;
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
