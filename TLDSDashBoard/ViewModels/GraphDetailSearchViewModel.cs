using System.Collections.ObjectModel;
using System.Globalization;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.ViewModels;

/// <summary>
/// 그래프 상세검색: all 6 channels shown as separate stacked lanes (same visual pattern as
/// MetricGroupView on the Overview page — one full-width row per channel, each with its own real
/// value axis, since units V/A/Hz/ms don't share a scale), NOT overlaid on one shared chart and NOT
/// as a grid of small cards — both were tried and rejected in favor of this stacked layout. All 12
/// lanes share one time window/zoom state (mouse-wheel zoom toward the cursor, left-drag pan, hover
/// crosshair — same interaction model as the drill-in modal) so zooming or hovering anywhere updates
/// every lane together. Defaults to the last 1 hour of 1-second-interval samples.
/// </summary>
public sealed class GraphDetailSearchViewModel : ViewModelBase
{
    // Rebuilding a ~3600-point WPF Path string for all 12 lanes on every single mouse-move tick during
    // drag/zoom (Recompute runs on every Pan call) was the source of the reported lag — the chart area is
    // only a few hundred pixels wide, so points beyond this are visually indistinguishable anyway.
    private const int MaxRenderPoints = 400;

    private readonly Dictionary<string, double[]> _channelData = new();
    private DateTime[] _fullTimestamps = Array.Empty<DateTime>();
    private int _windowStart;
    private int _windowLength;
    /// <summary>Real per-channel 점검(Caution)/최소(Warning) thresholds for the selected device, loaded
    /// once per Search() — index matches ChannelCatalog.ChannelIds order (TRK_FREQ..RX_B_VOLTAGE), same convention as
    /// MainViewModel.BuildStaticData. Null when SetValue.db has no row for this device, in which case
    /// Recompute() falls back to a data-derived placeholder instead (see PathBuilder.ComputeReferenceLines).</summary>
    private IReadOnlyList<ChannelThreshold>? _thresholds;

    public ObservableCollection<TrackCircuit> AvailableTracks { get; } = new(ReferenceData.Tracks);
    public DateRangeFilter Range { get; } = new();

    private TrackCircuit _selectedTrack;
    public TrackCircuit SelectedTrack { get => _selectedTrack; set => SetProperty(ref _selectedTrack, value); }

    /// <summary>The stacked lanes, one per channel — built once per Search(), then mutated in place by Recompute() on zoom/pan/hover.</summary>
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

    // ---- lane sizing (fit-one-screen by default, 세로 확대 grows it) ----

    /// <summary>Unzoomed per-lane height in pixels — set by the View from its ScrollViewer's actual
    /// viewport height (viewport / 12), so all 12 lanes fit on screen with no scrolling at LaneHeightScale
    /// == 1. See GraphDetailSearchView.LanesScroll_SizeChanged.</summary>
    private double _baseLaneHeight = 54;
    private double _laneHeightScale = 1.0;

    /// <summary>The actual per-lane pixel height bound by both the label column and the chart column so
    /// their rows stay aligned. Recomputed whenever the base height or the zoom scale changes.</summary>
    public double LaneHeight => _baseLaneHeight * _laneHeightScale;

    public void SetBaseLaneHeight(double pixels)
    {
        // Subtract a hair so that summing 12 independently pixel-rounded Border heights back up can never
        // overshoot the viewport by a pixel — that overshoot was tripping the "Auto" vertical scrollbar
        // even at the unzoomed fit-one-screen size, which stole a few px of width from the Viewbox-
        // stretched charts and visibly resized them (see also HasVerticalOverflow below).
        pixels = Math.Max(0, pixels - 0.5);
        if (pixels <= 0 || double.IsNaN(pixels) || Math.Abs(pixels - _baseLaneHeight) < 0.5) return;
        _baseLaneHeight = pixels;
        OnPropertyChanged(nameof(LaneHeight));
    }

    /// <summary>Multiplies LaneHeightScale by <paramref name="factor"/>, clamped to [1, 6] — 1 is the
    /// "fits one screen" size, higher values intentionally overflow the ScrollViewer's viewport so the
    /// user can scroll through enlarged lanes.</summary>
    public void AdjustLaneZoom(double factor)
    {
        double next = Math.Clamp(_laneHeightScale * factor, 1.0, 6.0);
        if (Math.Abs(next - _laneHeightScale) < 0.001) return;
        _laneHeightScale = next;
        OnPropertyChanged(nameof(LaneHeight));
        OnPropertyChanged(nameof(HasVerticalOverflow));
    }

    public void ResetLaneZoom()
    {
        if (_laneHeightScale == 1.0) return;
        _laneHeightScale = 1.0;
        OnPropertyChanged(nameof(LaneHeight));
        OnPropertyChanged(nameof(HasVerticalOverflow));
    }

    /// <summary>True once 세로 확대 has actually made the lanes taller than the fit-one-screen baseline.
    /// The lanes ScrollViewer's vertical scrollbar visibility is bound to this (not to
    /// <see cref="IsVerticalZoomMode"/>, which is just which mode the mouse wheel is in) so the scrollbar
    /// — and the width it steals from the charts — only ever appears once there's really something to
    /// scroll, never as a rounding artifact at the default size.</summary>
    public bool HasVerticalOverflow => _laneHeightScale > 1.0;

    private bool _isVerticalZoomMode;
    /// <summary>When on, the chart area's mouse wheel resizes lanes (AdjustLaneZoom) instead of doing the
    /// usual horizontal time zoom — see GraphDetailSearchView.ChartArea_MouseWheel.</summary>
    public bool IsVerticalZoomMode { get => _isVerticalZoomMode; set => SetProperty(ref _isVerticalZoomMode, value); }

    private bool _showMeasuredLine = true;
    /// <summary>Toggles the colored measured-data curve (Path) on every lane.</summary>
    public bool ShowMeasuredLine { get => _showMeasuredLine; set => SetProperty(ref _showMeasuredLine, value); }

    private bool _showReferenceLines = true;
    /// <summary>Toggles the dashed 점검/최소 threshold lines (CheckedLinePath + MinimumLinePath) together
    /// on every lane — they're always a pair around the measured value, so one switch controls both.</summary>
    public bool ShowReferenceLines { get => _showReferenceLines; set => SetProperty(ref _showReferenceLines, value); }

    public RelayCommand SearchCommand { get; }
    public RelayCommand ResetZoomCommand { get; }

    public GraphDetailSearchViewModel()
    {
        _selectedTrack = AvailableTracks[0];
        // Default 조회 range is the last 1 hour ending now (matches the "기본 1시간" label on this page),
        // not a whole-day range like the other 조회 pages default to.
        var end = DateTime.Now;
        var start = end.AddHours(-1);
        Range.StartDate = start.Date; Range.StartHour = start.Hour; Range.StartMinute = start.Minute;
        Range.EndDate = end.Date; Range.EndHour = end.Hour; Range.EndMinute = end.Minute;
        SearchCommand = new RelayCommand(Search);
        ResetZoomCommand = new RelayCommand(ResetZoom);
        // No auto-search on load (OverView is the only page that renders data by default) — this page
        // starts empty until the user picks a 기간/장치 and clicks 조회.
    }

    private static readonly IChannelDataRepository Repository = new RealTelemetryRepository();

    /// <summary>Queries exactly the selected 기간 — no mock fallback. This page exists to inspect real
    /// field telemetry, so if <see cref="Repository"/> has no coverage for the requested device/period the
    /// screen shows nothing (with a status message saying so) rather than silently substituting generated
    /// placeholder data that could be mistaken for a real reading.</summary>
    private void Search()
    {
        _channelData.Clear();
        bool hasData;
        string statusMessage;
        try
        {
            var set = Repository.LoadRange(SelectedTrack.Idx, Range.Start, Range.End);
            _fullTimestamps = set.Timestamps;
            _channelData["TRK_FREQ"] = set.TRK_FREQ; _channelData["CAB_FREQ"] = set.CAB_FREQ; _channelData["CODE_FREQ"] = set.CODE_FREQ;
            _channelData["TX_VOLTAGE"] = set.TX_VOLTAGE; _channelData["RX_A_VOLTAGE"] = set.RX_A_VOLTAGE; _channelData["RX_B_VOLTAGE"] = set.RX_B_VOLTAGE;

            hasData = true;
            statusMessage = $"{SelectedTrack.Name} — 실데이터 {set.Timestamps.Length}건 조회됨 ({Range.Start:yyyy-MM-dd HH:mm}~{Range.End:yyyy-MM-dd HH:mm})";
        }
        catch (Exception ex)
        {
            _fullTimestamps = Array.Empty<DateTime>();
            hasData = false;
            statusMessage = $"{SelectedTrack.Name} — 해당 기간의 실데이터가 없습니다 ({ex.Message})";
        }

        // Real 점검/최소 thresholds for the selected device — same source MainViewModel's lanes use, so
        // this page's reference lines agree with OverView's instead of drawing a data-derived placeholder
        // that can look "off" (e.g. a flat line right on top of the data) whenever the real Caution/Warning
        // values are far from this window's own tiny fluctuation range.
        try { _thresholds = hasData ? ThresholdRepository.LoadForDevice(SelectedTrack.Idx) : null; }
        catch (Exception) { _thresholds = null; }

        _windowStart = 0;
        _windowLength = _fullTimestamps.Length;

        Series.Clear();
        if (hasData)
        {
            foreach (var id in ChannelCatalog.ChannelIds)
            {
                string fmt = ChannelCatalog.ChannelUnit(id) == "Hz" ? "F2" : "F1";
                Series.Add(new ChannelSeriesVM
                {
                    Id = id,
                    Name = ChannelCatalog.ChannelName(id),
                    Unit = ChannelCatalog.ChannelUnit(id),
                    ColorHex = ChannelCatalog.ChannelColor(id),
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
        }

        Recompute();
        if (!hasData)
        {
            // Recompute() bails out immediately on an empty window, leaving the previous search's time/range
            // labels on screen — clear them explicitly so a no-data result reads as empty, not stale.
            TimeStartLabel = TimeMidLabel = TimeEndLabel = RangeLabel = WindowLabel = "";
            HoverTimeLabel = null;
        }
        StatusMessage = statusMessage;
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

        for (int ci = 0; ci < Series.Count; ci++)
        {
            var s = Series[ci];
            var full = _channelData[s.Id];
            var window = new double[_windowLength];
            Array.Copy(full, _windowStart, window, 0, _windowLength);
            string fmt = s.ValueFormat;
            string latest = window.Length > 0 ? window[^1].ToString(fmt, CultureInfo.InvariantCulture) : "";

            // Downsample once (min/max-per-bucket keeps the true global min/max, so ComputeReferenceLines
            // and PathFromArray can both run against the smaller array below) instead of at full window
            // resolution on every zoom/pan tick — see MaxRenderPoints above.
            var renderWindow = window.Length > MaxRenderPoints ? PathBuilder.Downsample(window, MaxRenderPoints) : window;

            // Each lane keeps its own real axis (not normalized/overlaid) — units differ (V/A/Hz/ms) so
            // one shared scale wouldn't be meaningful. Axis is expanded to fit the 점검/최소 lines too.
            // Real thresholds (see Search()) take priority; only fall back to the data-derived placeholder
            // when SetValue.db has no row for this device.
            var threshold = _thresholds is not null && ci < _thresholds.Count ? _thresholds[ci] : null;
            var (checkedValue, minimumValue, axisMin, axisMax) = threshold is not null
                ? PathBuilder.ComputeReferenceLines(renderWindow, threshold.Caution, threshold.Warning)
                : PathBuilder.ComputeReferenceLines(renderWindow);

            s.WindowValues = window;
            s.LatestValue = latest;
            s.Value = latest;
            s.AxisMaxLabel = axisMax.ToString(fmt, CultureInfo.InvariantCulture);
            s.AxisMinLabel = axisMin.ToString(fmt, CultureInfo.InvariantCulture);
            s.CheckedLabel = checkedValue.ToString(fmt, CultureInfo.InvariantCulture);
            s.MinimumLabel = minimumValue.ToString(fmt, CultureInfo.InvariantCulture);
            s.Path = PathBuilder.PathFromArray(renderWindow, axisMin, axisMax, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad);
            s.CheckedLinePath = PathBuilder.HorizontalLinePath(checkedValue, axisMin, axisMax, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad);
            s.MinimumLinePath = PathBuilder.HorizontalLinePath(minimumValue, axisMin, axisMax, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad);
        }

        string TimeLabel(int idx)
        {
            return tsWindow.Length == 0 ? "" : tsWindow[idx].ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }

        TimeStartLabel = tsWindow.Length > 0 ? TimeLabel(0) : "";
        TimeMidLabel = tsWindow.Length > 0 ? TimeLabel(tsWindow.Length / 2) : "";
        TimeEndLabel = tsWindow.Length > 0 ? TimeLabel(tsWindow.Length - 1) : "";

        RangeLabel = tsWindow.Length > 0
            ? $"{tsWindow[0]:yyyy-MM-dd HH:mm:ss} ~ {tsWindow[^1]:yyyy-MM-dd HH:mm:ss}"
            : "";
        WindowLabel = $"{_windowLength}초 구간 표시 중 ({_windowLength}/{fullLength})";
    }
}
