using System.Collections.ObjectModel;
using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.ViewModels.Items;

/// <summary>
/// One TX or RX group's overlaid chart: up to 6 channels drawn on one shared canvas (each
/// independently normalized — see <see cref="ChannelSeriesVM"/>), a legend, the real (DB-sourced)
/// start/mid/end timestamps of the displayed window, and hover state that MetricGroupView's
/// mouse-move handler drives so the legend values and the header's <see cref="HoverTimeLabel"/>
/// track the mouse position over the chart.
/// </summary>
public sealed class CombinedChartVM : ViewModelBase
{
    public required string Header { get; init; }
    public required ObservableCollection<ChannelSeriesVM> Series { get; init; }

    /// <summary>Real sample timestamps for the displayed window, same indexing as each series' WindowValues — used to resolve the time under the mouse on hover.</summary>
    public required DateTime[] WindowTimestamps { get; init; }

    public required string TimeStartLabel { get; init; }
    public required string TimeMidLabel { get; init; }
    public required string TimeEndLabel { get; init; }

    private string _hoverTimeLabel = string.Empty;
    /// <summary>The exact timestamp under the mouse while hovering the chart; empty when not hovering.</summary>
    public string HoverTimeLabel { get => _hoverTimeLabel; set => SetProperty(ref _hoverTimeLabel, value); }
}
