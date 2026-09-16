using System.Collections.ObjectModel;
using System.Globalization;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.ViewModels;

/// <summary>
/// 그래프 상세검색: all 12 TX/RX channels shown as 12 separate stacked lanes (same visual pattern as
/// MetricGroupView on the Overview page — one full-width row per channel, each with its own real
/// value axis, since units V/A/Hz/ms don't share a scale), NOT overlaid on one shared chart and NOT
/// as a grid of small cards — both were tried and rejected in favor of this stacked layout. All 12
/// lanes share one time window/zoom state (mouse-wheel zoom toward the cursor, left-drag pan, hover
/// crosshair — same interaction model as the drill-in modal) so zooming or hovering anywhere updates
/// every lane together. Defaults to the last 1 hour of 1-second-interval samples.
/// </summary>
public sealed class GraphDetailSearchViewModel : ViewModelBase
{
    private const int FullRangeSampleCount = 3600; // 1 hour at the 1-second sampling interval — the default 가로 시간 간격

    private readonly Dictionary<string, double[]> _channelData = new();
    private DateTime[] _fullTimestamps = Array.Empty<DateTime>();
    private int _windowStart;
    private int _windowLength;

    public ObservableCollection<TrackCircuit> AvailableTracks { get; } = new(ReferenceData.Tracks);
    public DateRangeFilter Range { get; } = new();

    private TrackCircuit _selectedTrack;
    public TrackCircuit SelectedTrack { get => _selectedTrack; set => SetProperty(ref _selectedTrack, value); }

    /// <summary>The 12 stacked lanes, one per TX/RX channel — built once per Search(), then mutated in place by Recompute() on zoom/pan/hover.</summary>
    public ObservableCollection<ChannelSeriesVM> Series { get; } = new();

    /// <summary>Real (start/mid/end) timestamps of the displayed window — same 3-label layout as MetricGroupView's time axis.</summary>
    private string _timeStartLabel = "";
    public string TimeStartLabel { get => _timeStartLabel; private set => SetProperty(ref _timeStartLabel, value); }
    private string _timeMidLabel = "";
    public string TimeMidLabel { get => _timeMidLabel; private set => SetProperty(ref _timeMidLabel, value); }
    private string _timeEndLabel = "";
    public string TimeEndLabel { get => _timeEndLabel; private set => SetProperty(ref _timeEndLabel, value); }

    private string _rangeLabel = "";
    public string RangeLabel { get => _rangeLabel; private set => SetProperty(ref _rangeLabel, value); }

    private string _windowLabel = "";
    public string WindowLabel { get => _windowLabel; private set => SetProperty(ref _windowLabel, value); }

    private string? _hoverTimeLabel;
    public string? HoverTimeLabel { get => _hoverTimeLabel; private set => SetProperty(ref _hoverTimeLabel, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public RelayCommand SearchCommand { get; }
    public RelayCommand ResetZoomCommand { get; }

    public GraphDetailSearchViewModel()
    {
        _selectedTrack = AvailableTracks[0];
        SearchCommand = new RelayCommand(Search);
        ResetZoomCommand = new RelayCommand(ResetZoom);
        Search();
    }

    private void Search()
    {
        // One generator instance per track (like GraphSearch in MainViewModel) so all 12 channels for the
        // same device share one continuous random stream, instead of each looking independently re-rolled.
        var generator = new DataGenerator(DataGenerator.SeedFrom(SelectedTrack.Id));
        _channelData.Clear();
        DateTime[]? timestamps = null;
        foreach (var id in DataGenerator.ChannelIds)
        {
            var (ts, values) = generator.GenerateSingleChannelSeries(id, Range.End, FullRangeSampleCount);
            timestamps ??= ts; // identical for every channel (same end + count) — only need it once
            _channelData[id] = values;
        }
        _fullTimestamps = timestamps ?? Array.Empty<DateTime>();
        _windowStart = 0;
        _windowLength = FullRangeSampleCount;

        Series.Clear();
        foreach (var id in DataGenerator.ChannelIds)
        {
            string fmt = DataGenerator.ChannelUnit(id) == "Hz" ? "F2" : "F1";
            Series.Add(new ChannelSeriesVM
            {
                Id = id,
                Name = DataGenerator.ChannelName(id),
                Unit = DataGenerator.ChannelUnit(id),
                ColorHex = DataGenerator.ChannelColor(id),
                WindowValues = Array.Empty<double>(),
                ValueFormat = fmt,
                LatestValue = "",
                Value = "",
                Path = "",
                AxisMaxLabel = "",
                AxisMinLabel = "",
                CheckedLinePath = "",
                MinimumLinePath = "",
                CheckedLabel = "",
                MinimumLabel = "",
            });
        }

        Recompute();
        StatusMessage = $"{SelectedTrack.Name} — 12개 채널 조회됨 (최근 1시간)";
    }

    public void ResetZoom()
    {
        if (_fullTimestamps.Length == 0) return;
        _windowStart = 0;
        _windowLength = _fullTimestamps.Length;
        Recompute();
    }

    /// <summary>Mouse-wheel zoom toward the cursor — same behavior as MainViewModel.ZoomDrill, see there for the reasoning.</summary>
    public void Zoom(double factor, double anchorFraction)
    {
        int fullLength = _fullTimestamps.Length;
        if (fullLength == 0) return;
        int minWindow = Math.Min(10, fullLength);
        int newLength = Math.Clamp((int)Math.Round(_windowLength * factor), minWindow, fullLength);

        int anchorIndex = _windowStart + (int)Math.Round(_windowLength * anchorFraction);
        int newStart = Math.Clamp(anchorIndex - (int)Math.Round(newLength * anchorFraction), 0, Math.Max(0, fullLength - newLength));

        _windowStart = newStart;
        _windowLength = newLength;
        Recompute();
    }

    /// <summary>Drag-to-pan, fractionDelta = drag distance as a fraction of the chart's on-screen width.</summary>
    public void Pan(double fractionDelta)
    {
        int fullLength = _fullTimestamps.Length;
        if (fullLength == 0) return;
        int deltaSamples = (int)Math.Round(fractionDelta * _windowLength);
        _windowStart = Math.Clamp(_windowStart - deltaSamples, 0, Math.Max(0, fullLength - _windowLength));
        Recompute();
    }

    /// <summary>Continuous value readout for EVERY lane at once — fraction (0..1) is the cursor's
    /// position across the shared chart area's on-screen width.</summary>
    public void UpdateHover(double fraction)
    {
        if (_windowLength == 0) return;
        int index = (int)Math.Round(fraction * (_windowLength - 1));
        index = Math.Clamp(index, 0, _windowLength - 1);

        int actualIndex = _windowStart + index;
        if (actualIndex >= 0 && actualIndex < _fullTimestamps.Length)
        {
            HoverTimeLabel = _fullTimestamps[actualIndex].ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        foreach (var s in Series) s.ShowValueAt(index);
    }

    public void ClearHover()
    {
        HoverTimeLabel = null;
        foreach (var s in Series) s.ResetToLatest();
    }

    private void Recompute()
    {
        int fullLength = _fullTimestamps.Length;
        if (fullLength == 0) return;

        _windowLength = Math.Clamp(_windowLength <= 0 ? fullLength : _windowLength, Math.Min(10, fullLength), fullLength);
        _windowStart = Math.Clamp(_windowStart, 0, Math.Max(0, fullLength - _windowLength));

        var tsWindow = new DateTime[_windowLength];
        Array.Copy(_fullTimestamps, _windowStart, tsWindow, 0, _windowLength);

        foreach (var s in Series)
        {
            var full = _channelData[s.Id];
            var window = new double[_windowLength];
            Array.Copy(full, _windowStart, window, 0, _windowLength);
            string fmt = s.ValueFormat;
            string latest = window.Length > 0 ? window[^1].ToString(fmt, CultureInfo.InvariantCulture) : "";

            // Each lane keeps its own real axis (not normalized/overlaid) — units differ (V/A/Hz/ms) so
            // one shared scale wouldn't be meaningful. Axis is expanded to fit the 점검/최소 lines too.
            var (checkedValue, minimumValue, axisMin, axisMax) = PathBuilder.ComputeReferenceLines(window);

            s.WindowValues = window;
            s.LatestValue = latest;
            s.Value = latest;
            s.AxisMaxLabel = axisMax.ToString(fmt, CultureInfo.InvariantCulture);
            s.AxisMinLabel = axisMin.ToString(fmt, CultureInfo.InvariantCulture);
            s.CheckedLabel = checkedValue.ToString(fmt, CultureInfo.InvariantCulture);
            s.MinimumLabel = minimumValue.ToString(fmt, CultureInfo.InvariantCulture);
            s.Path = PathBuilder.PathFromArray(window, axisMin, axisMax, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad);
            s.CheckedLinePath = PathBuilder.HorizontalLinePath(checkedValue, axisMin, axisMax, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad);
            s.MinimumLinePath = PathBuilder.HorizontalLinePath(minimumValue, axisMin, axisMax, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad);
        }

        string TimeLabel(int idx) => tsWindow.Length == 0 ? "" : tsWindow[idx].ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        TimeStartLabel = tsWindow.Length > 0 ? TimeLabel(0) : "";
        TimeMidLabel = tsWindow.Length > 0 ? TimeLabel(tsWindow.Length / 2) : "";
        TimeEndLabel = tsWindow.Length > 0 ? TimeLabel(tsWindow.Length - 1) : "";

        RangeLabel = tsWindow.Length > 0
            ? $"{tsWindow[0]:yyyy-MM-dd HH:mm:ss} ~ {tsWindow[^1]:yyyy-MM-dd HH:mm:ss}"
            : "";
        WindowLabel = $"{_windowLength}초 구간 표시 중 ({_windowLength}/{fullLength})";
    }
}
