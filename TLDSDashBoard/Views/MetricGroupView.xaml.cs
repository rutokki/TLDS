using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.Views;

/// <summary>
/// TX 또는 RX 그룹 하나의 쌓인 레인 차트 블록(채널당 미니 차트 하나 + 시간축). MainWindow에서 두 번 쓰이며,
/// 각각 <see cref="Data"/>를 통해 서로 다른 <see cref="CombinedChartVM"/>을 가리킨다.
/// 호버 상호작용도 담당한다: <c>ChartArea</c>(한 레인이 아니라 레인 묶음 전체에 걸쳐 있음) 위 어디서든
/// 마우스 위치를 샘플 인덱스로 변환하고, 이것이 모든 레인에 걸친 크로스헤어 하나, 모든 레인에 표시되는 값
/// (각 계열의 가변 <see cref="ChannelSeriesVM.Value"/>를 통해), 호버 시각 칩
/// (<see cref="CombinedChartVM.HoverTimeLabel"/>)을 구동한다.
/// 예전의 고정 900단위 차트 캔버스와 달리 <c>ChartArea</c> 자체는 Viewbox로 감싸지 않고 실제 화면 크기로
/// 늘어나므로, <see cref="MouseEventArgs.GetPosition"/>의 마우스 좌표가 이미 실제 픽셀이어서 레인별
/// 차트(각각 자신의 Viewbox로 같은 화면 폭에 맞게 독립적으로 스케일됨)와 바로 정렬된다.
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
}
