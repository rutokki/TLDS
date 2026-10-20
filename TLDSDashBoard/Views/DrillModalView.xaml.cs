using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.Views;

/// <summary>
/// 전체 화면 드릴인 모달. 어두운 오버레이를 클릭하면 닫히고, 카드 자체를 클릭해도 닫히지 않는다
/// (이벤트가 오버레이로 버블링되기 전에 처리됨으로 표시됨). 원래 프로토타입의 stopPropagation
/// 바깥 클릭 시 닫기 동작과 같다.
///
/// 차트 영역은 마우스 휠 줌(커서 쪽으로), 왼쪽 드래그 팬, 연속 호버 읽기(크로스헤어 + 값/시각 칩)도
/// 지원한다 — 모두 MainViewModel.ZoomDrill/PanDrill/UpdateDrillHover를 통해 구동되며, 이 메서드들이
/// 실제 줌 창 상태를 소유한다(이 뷰는 자신의 화면상 폭에 대한 비율만 보고하고 샘플 수는 절대 다루지
/// 않는다. 지금 몇 개의 샘플이 보이는지 모르기 때문이다).
/// </summary>
public partial class DrillModalView : UserControl
{
    private bool _isDragging;
    private double _lastDragX;

    public DrillModalView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.CloseDrillCommand.Execute(null);
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void CloseButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.CloseDrillCommand.Execute(null);
        e.Handled = true;
    }

    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ResetDrillZoom();
    }

    private void ChartArea_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ChartArea.ActualWidth <= 0) return;
        double fraction = Clamp01(e.GetPosition(ChartArea).X / ChartArea.ActualWidth);
        double factor = e.Delta > 0 ? 0.8 : 1.25; // 휠 위 = 확대(창이 작아짐), 휠 아래 = 축소
        ViewModel?.ZoomDrill(factor, fraction);
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
        ViewModel?.UpdateDrillHover(fraction);

        if (_isDragging)
        {
            double fractionDelta = (pos.X - _lastDragX) / ChartArea.ActualWidth;
            ViewModel?.PanDrill(fractionDelta);
            _lastDragX = pos.X;
        }
    }

    private void ChartArea_MouseLeave(object sender, MouseEventArgs e)
    {
        CrosshairLine.Visibility = Visibility.Collapsed;
        ViewModel?.ClearDrillHover();
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}
