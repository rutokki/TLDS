namespace TLDSDashBoard.Services;

/// <summary>대시보드 차트 전반에서 쓰는 고정 "viewBox" 방식 좌표 공간. XAML 뷰는 이 크기와 정확히 같은 캔버스 위에 Stretch="Fill"인 Viewbox를 쓰므로, 이 숫자들은 XAML과 일치해야 한다.</summary>
public static class ChartMetrics
{
    public const double DrillChartW = 900, DrillChartH = 240, DrillChartPad = 16;

    /// <summary>One channel's row ("lane") inside a 주파수/전압 group — each channel gets its own stacked chart with its own real value axis (not normalized/overlaid).</summary>
    public const double LaneChartW = 900, LaneChartH = 90, LaneChartPad = 10;

    /// <summary>그래프 상세검색의 통합 차트 하나 — 모든 채널을 겹쳐 그리고, 각 채널을 이 같은 공간을
    /// 채우도록 자동 스케일링한다(PathFromArrayAutoRange가 채널별로 정규화) — 그래서 단위가 달라도
    /// 상대적인 추세를 함께 읽는 데 지장이 없다.</summary>
    public const double OverlayChartW = 1400, OverlayChartH = 460, OverlayChartPad = 16;
}
