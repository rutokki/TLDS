using System.Globalization;
using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.ViewModels.Items;

/// <summary>
/// One channel's row ("lane") inside a <see cref="CombinedChartVM"/> — its own stacked mini-chart
/// with its own real value axis (<see cref="AxisMaxLabel"/>/<see cref="AxisMinLabel"/>, the plotted
/// window's own min/max — channels are stacked instead of overlaid specifically because their units,
/// V/A/Hz/ms, don't share one scale). <see cref="Value"/> starts equal to <see cref="LatestValue"/>
/// but is mutated live while the mouse hovers the shared chart area (see MetricGroupView's mouse
/// handlers), so the label shows the value under the cursor instead of the most recent sample;
/// <see cref="WindowValues"/> is what makes that lookup possible. Clicking the label opens the
/// drill-in modal for this channel.
///
/// The chart-shape properties (Path/axis labels/reference lines/WindowValues) are mutable (not
/// init-only) so a page that keeps one ChannelSeriesVM per channel alive across zoom/pan — see
/// GraphDetailSearchViewModel — can recompute them in place instead of rebuilding the whole
/// collection every frame, the same way Value is already mutated in place for hover.
/// </summary>
public sealed class ChannelSeriesVM : ViewModelBase
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string ColorHex { get; init; }

    private string _path = "";
    public required string Path { get => _path; set => SetProperty(ref _path, value); }

    /// <summary>Value-axis labels for this lane's own chart (top/max and bottom/min of the plotted window,
    /// expanded to include the 점검/최소 reference lines below) — real numbers, not normalized.</summary>
    private string _axisMaxLabel = "";
    public required string AxisMaxLabel { get => _axisMaxLabel; set => SetProperty(ref _axisMaxLabel, value); }
    private string _axisMinLabel = "";
    public required string AxisMinLabel { get => _axisMinLabel; set => SetProperty(ref _axisMinLabel, value); }

    /// <summary>점검(check)/최소(minimum) reference thresholds, drawn as flat dashed lines alongside the
    /// real data on the SAME axis scale — see PathBuilder.HorizontalLinePath.</summary>
    private string _checkedLinePath = "";
    public required string CheckedLinePath { get => _checkedLinePath; set => SetProperty(ref _checkedLinePath, value); }
    private string _minimumLinePath = "";
    public required string MinimumLinePath { get => _minimumLinePath; set => SetProperty(ref _minimumLinePath, value); }
    private string _checkedLabel = "";
    public required string CheckedLabel { get => _checkedLabel; set => SetProperty(ref _checkedLabel, value); }
    private string _minimumLabel = "";
    public required string MinimumLabel { get => _minimumLabel; set => SetProperty(ref _minimumLabel, value); }

    /// <summary>The channel's real values for the displayed window, same indexing as <see cref="CombinedChartVM.WindowTimestamps"/> — used to look up the value under the mouse on hover.</summary>
    private double[] _windowValues = Array.Empty<double>();
    public required double[] WindowValues { get => _windowValues; set => SetProperty(ref _windowValues, value); }

    /// <summary>Number format ("F1"/"F2") applied when rendering a value from <see cref="WindowValues"/>.</summary>
    private string _valueFormat = "F1";
    public required string ValueFormat { get => _valueFormat; set => SetProperty(ref _valueFormat, value); }

    /// <summary>The most recent sample's formatted value — what <see cref="Value"/> resets to when the mouse leaves the chart.</summary>
    private string _latestValue = "";
    public required string LatestValue { get => _latestValue; set => SetProperty(ref _latestValue, value); }

    private string _value = string.Empty;
    /// <summary>Currently displayed value: the latest sample normally, or the sample under the mouse while hovering.</summary>
    public string Value { get => _value; set => SetProperty(ref _value, value); }

    public void ShowValueAt(int index)
    {
        Value = index >= 0 && index < WindowValues.Length
            ? WindowValues[index].ToString(ValueFormat, CultureInfo.InvariantCulture)
            : LatestValue;
    }

    public void ResetToLatest() => Value = LatestValue;
}
