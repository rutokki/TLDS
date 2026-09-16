using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.ViewModels.Items;

/// <summary>
/// One channel's chart state. Used two ways: mutably by the drill-in modal (zoom/pan/hover all update
/// it live while the modal stays open — see ChannelSeriesVM for the same reasoning), and as a plain
/// once-computed snapshot by 그래프 상세검색's 12-channel grid (no zoom/pan there, so Hover* just stay
/// unset). Either way the shape is the same, so one class covers both instead of two near-duplicates.
/// </summary>
public sealed class DrillDataVM : ViewModelBase
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Unit { get; init; }
    public required string ColorHex { get; init; }

    private string _bigPath = "";
    public string BigPath { get => _bigPath; set => SetProperty(ref _bigPath, value); }

    private string _min = "";
    public string Min { get => _min; set => SetProperty(ref _min, value); }

    private string _max = "";
    public string Max { get => _max; set => SetProperty(ref _max, value); }

    private string _avg = "";
    public string Avg { get => _avg; set => SetProperty(ref _avg, value); }

    /// <summary>점검(check)/최소(minimum) reference thresholds, same as the overview lane charts — see PathBuilder.HorizontalLinePath.</summary>
    private string _checkedLinePath = "";
    public string CheckedLinePath { get => _checkedLinePath; set => SetProperty(ref _checkedLinePath, value); }

    private string _minimumLinePath = "";
    public string MinimumLinePath { get => _minimumLinePath; set => SetProperty(ref _minimumLinePath, value); }

    /// <summary>Start~end timestamps of the currently visible (zoomed/panned) window.</summary>
    private string _rangeLabel = "";
    public string RangeLabel { get => _rangeLabel; set => SetProperty(ref _rangeLabel, value); }

    /// <summary>e.g. "42/140 샘플 표시 중" — zoom-level feedback (drill-in modal only).</summary>
    private string _windowLabel = "";
    public string WindowLabel { get => _windowLabel; set => SetProperty(ref _windowLabel, value); }

    /// <summary>Live value under the cursor; empty when the mouse isn't over the chart (drill-in modal only).</summary>
    private string? _hoverValue;
    public string? HoverValue { get => _hoverValue; set => SetProperty(ref _hoverValue, value); }

    private string? _hoverTimeLabel;
    public string? HoverTimeLabel { get => _hoverTimeLabel; set => SetProperty(ref _hoverTimeLabel, value); }
}
