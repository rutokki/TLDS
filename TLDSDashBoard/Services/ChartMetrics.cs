namespace TLDSDashBoard.Services;

/// <summary>Fixed "viewBox" style coordinate spaces used throughout the dashboard's charts. XAML views use a Viewbox with Stretch="Fill" over a canvas of this exact size, so these numbers must match the XAML.</summary>
public static class ChartMetrics
{
    public const double KpiSparkW = 200, KpiSparkH = 40, KpiSparkPad = 4;
    public const double DrillChartW = 900, DrillChartH = 240, DrillChartPad = 16;

    /// <summary>One channel's row ("lane") inside a TX/RX group — each channel gets its own stacked chart with its own real value axis (not normalized/overlaid).</summary>
    public const double LaneChartW = 900, LaneChartH = 90, LaneChartPad = 10;
}
