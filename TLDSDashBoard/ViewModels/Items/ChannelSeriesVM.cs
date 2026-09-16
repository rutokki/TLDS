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
/// </summary>
public sealed class ChannelSeriesVM : ViewModelBase
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string ColorHex { get; init; }
    public required string Path { get; init; }

    /// <summary>Value-axis labels for this lane's own chart (top/max and bottom/min of the plotted window) — real numbers, not normalized.</summary>
    public required string AxisMaxLabel { get; init; }
    public required string AxisMinLabel { get; init; }

    /// <summary>The channel's real values for the displayed window, same indexing as <see cref="CombinedChartVM.WindowTimestamps"/> — used to look up the value under the mouse on hover.</summary>
    public required double[] WindowValues { get; init; }

    /// <summary>Number format ("F1"/"F2") applied when rendering a value from <see cref="WindowValues"/>.</summary>
    public required string ValueFormat { get; init; }

    /// <summary>The most recent sample's formatted value — what <see cref="Value"/> resets to when the mouse leaves the chart.</summary>
    public required string LatestValue { get; init; }

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
