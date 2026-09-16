using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Threading;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.ViewModels;

/// <summary>
/// The dashboard's single view model. This app displays TX/RX telemetry history that already lives
/// in a database (see <see cref="IChannelDataRepository"/>) — it does not generate or receive live
/// readings. The 12 channels are split into two groups (TX/RX) and each group is rendered as a
/// stack of per-channel lanes (own real value axis per channel — their units, V/A/Hz/ms, don't share
/// one scale); clicking a lane's label opens a drill-in modal with that one channel's full history.
/// The DB itself samples every 1 second, but the app only re-reads it every 60s (a deliberate refresh
/// cadence, separate from the underlying sample rate); if the DB can't be reached, generated sample
/// data is shown instead so the UI still renders (see <see cref="DataSourceLabel"/>).
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private const string DefaultPageId = "graph";

    private static readonly (string Id, string Label)[] NavDefs =
    {
        ("graph", "OverView"), ("graphsearch", "그래프 상세검색"), ("occupancy", "열차점유리스트"),
        ("trackfault", "궤도 장애/경보"), ("levelrecord", "레벨 기록 리스트"), ("hourlystatus", "시간별 상태 출력")
    };

    private const string ColorNavDotActive = "#FF9F40";
    private const string ColorNavDotInactive = "#43474F";

    private readonly IChannelDataRepository _repository = new SqliteChannelDataRepository();
    private ChannelDataSet _channelData = null!;
    private readonly List<MetricDef> _allChannels = new();
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _refreshTimer;
    private DispatcherTimer? _toastTimer;

    // ---- state ----
    private bool _exportOpen;
    private string? _toast;
    private string? _drillMetricId;
    private int _drillWindowStart;
    private int _drillWindowLength;
    private DateTime _now = DateTime.Now;

    public MainViewModel()
    {
        TrainOccupancy = new TrainOccupancyViewModel();
        TrackFaultList = new TrackFaultListViewModel();
        LevelRecordList = new LevelRecordListViewModel();
        HourlyStatus = new HourlyStatusViewModel();
        GraphDetailSearch = new GraphDetailSearchViewModel();

        AvailableTracks = new ObservableCollection<TrackCircuit>(ReferenceData.Tracks);
        _selectedTrack = AvailableTracks[0];

        LoadChannelData();

        ToggleExportCommand = new RelayCommand(ToggleExport);
        ExportPngCommand = new RelayCommand(() => ShowToast("Exported chart as PNG", 2200));
        ExportCsvCommand = new RelayCommand(() => ShowToast("Exported data as CSV", 2200));
        CloseDrillCommand = new RelayCommand(CloseDrill);
        NavClickCommand = new RelayCommand(p => OnNavClick((string)p!));
        OpenDrillCommand = new RelayCommand(p => OpenDrill((string)p!));
        GraphSearchCommand = new RelayCommand(GraphSearch);

        BuildStaticData();
        RecomputeClockLabel();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => { _now = DateTime.Now; RecomputeClockLabel(); };
        _clockTimer.Start();

        // DB samples every 1 second, but we only poll it every 60s — a deliberate refresh cadence, not tied to the sample rate.
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _refreshTimer.Tick += (_, _) => { LoadChannelData(); BuildStaticData(); RecomputeDrill(); };
        _refreshTimer.Start();
    }

    /// <summary>Loads the latest history from the DB; falls back to generated sample data (and flags that via <see cref="DataSourceLabel"/>) if the DB isn't reachable yet.</summary>
    private void LoadChannelData()
    {
        try
        {
            _channelData = _repository.LoadRecent(DataGenerator.SampleCount);
            DataSourceLabel = "DB 연동됨";
        }
        catch (Exception ex)
        {
            _channelData = new DataGenerator().GenerateChannels();
            DataSourceLabel = $"DB 연결 실패 · 샘플 데이터 표시 중 ({ex.Message})";
        }
    }

    // ==================== child page view models ====================

    public TrainOccupancyViewModel TrainOccupancy { get; }
    public TrackFaultListViewModel TrackFaultList { get; }
    public LevelRecordListViewModel LevelRecordList { get; }
    public HourlyStatusViewModel HourlyStatus { get; }
    public GraphDetailSearchViewModel GraphDetailSearch { get; }

    // ==================== navigation state ====================

    private string _activePageId = DefaultPageId;
    public string ActivePageId { get => _activePageId; private set => SetProperty(ref _activePageId, value); }

    private string _activePageTitle = NavDefs.First(n => n.Id == DefaultPageId).Label;
    /// <summary>Shown in the topbar — the label of whichever sidebar item is currently active.</summary>
    public string ActivePageTitle { get => _activePageTitle; private set => SetProperty(ref _activePageTitle, value); }

    private bool _isOccupancyActive;
    public bool IsOccupancyActive { get => _isOccupancyActive; private set => SetProperty(ref _isOccupancyActive, value); }

    private bool _isTrackFaultActive;
    public bool IsTrackFaultActive { get => _isTrackFaultActive; private set => SetProperty(ref _isTrackFaultActive, value); }

    private bool _isLevelRecordActive;
    public bool IsLevelRecordActive { get => _isLevelRecordActive; private set => SetProperty(ref _isLevelRecordActive, value); }

    private bool _isGraphActive;
    public bool IsGraphActive { get => _isGraphActive; private set => SetProperty(ref _isGraphActive, value); }

    private bool _isGraphSearchActive;
    public bool IsGraphSearchActive { get => _isGraphSearchActive; private set => SetProperty(ref _isGraphSearchActive, value); }

    // ==================== graph page filters ====================

    public ObservableCollection<TrackCircuit> AvailableTracks { get; }

    private TrackCircuit _selectedTrack;
    public TrackCircuit SelectedTrack { get => _selectedTrack; set => SetProperty(ref _selectedTrack, value); }

    public DateRangeFilter GraphRange { get; } = new();

    public RelayCommand GraphSearchCommand { get; }

    // ==================== bound collections / properties ====================

    private CombinedChartVM? _txChart;
    public CombinedChartVM? TxChart { get => _txChart; private set => SetProperty(ref _txChart, value); }

    private CombinedChartVM? _rxChart;
    public CombinedChartVM? RxChart { get => _rxChart; private set => SetProperty(ref _rxChart, value); }

    private ObservableCollection<AlarmEventModel> _recentAlarms = new();
    /// <summary>Alarms within the same 10-minute window as TxChart/RxChart — shown below the charts on the Overview page.</summary>
    public ObservableCollection<AlarmEventModel> RecentAlarms { get => _recentAlarms; private set => SetProperty(ref _recentAlarms, value); }

    private string _dataSourceLabel = string.Empty;
    public string DataSourceLabel { get => _dataSourceLabel; private set => SetProperty(ref _dataSourceLabel, value); }

    private ObservableCollection<NavItemVM> _navItems = new();
    public ObservableCollection<NavItemVM> NavItems { get => _navItems; private set => SetProperty(ref _navItems, value); }

    private bool _exportOpenProp;
    public bool ExportOpen { get => _exportOpenProp; private set => SetProperty(ref _exportOpenProp, value); }

    private string? _toastProp;
    public string? Toast { get => _toastProp; private set => SetProperty(ref _toastProp, value); }

    private DrillDataVM? _drillData;
    public DrillDataVM? DrillData { get => _drillData; private set => SetProperty(ref _drillData, value); }

    private string _clockLabel = string.Empty;
    public string ClockLabel { get => _clockLabel; private set => SetProperty(ref _clockLabel, value); }

    // ==================== commands ====================

    public RelayCommand ToggleExportCommand { get; }
    public RelayCommand ExportPngCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand CloseDrillCommand { get; }
    public RelayCommand NavClickCommand { get; }
    public RelayCommand OpenDrillCommand { get; }

    // ==================== command handlers ====================

    private void ToggleExport() => ExportOpen = _exportOpen = !_exportOpen;

    private void ShowToast(string message, int durationMs)
    {
        _toastTimer?.Stop();
        Toast = _toast = message;
        ExportOpen = _exportOpen = false;
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
        _toastTimer.Tick += (_, _) => { _toastTimer!.Stop(); Toast = _toast = null; };
        _toastTimer.Start();
    }

    private void OnNavClick(string id)
    {
        if (id == "hourlystatus")
        {
            HourlyStatus.Open();
            return;
        }
        SetActivePage(id);
    }

    private void SetActivePage(string id)
    {
        ActivePageId = id;
        ActivePageTitle = NavDefs.First(n => n.Id == id).Label;
        IsOccupancyActive = id == "occupancy";
        IsTrackFaultActive = id == "trackfault";
        IsLevelRecordActive = id == "levelrecord";
        IsGraphActive = id == "graph";
        IsGraphSearchActive = id == "graphsearch";

        // NavItemVM is immutable (init-only) — rebuild the collection rather than mutating items.
        NavItems = new ObservableCollection<NavItemVM>(NavDefs.Select(n => new NavItemVM
        {
            Id = n.Id,
            Label = n.Label,
            IsActive = n.Id == id,
            DotColorHex = n.Id == id ? ColorNavDotActive : ColorNavDotInactive,
        }));
    }

    private void GraphSearch()
    {
        _channelData = new DataGenerator(DataGenerator.SeedFrom(SelectedTrack.Id)).GenerateChannels();
        DataSourceLabel = $"샘플 데이터 표시 중 (장치: {SelectedTrack.Name})";
        BuildStaticData();
    }

    private void OpenDrill(string id)
    {
        _drillMetricId = id;
        var m = _allChannels.FirstOrDefault(x => x.Id == id);
        _drillWindowStart = 0;
        _drillWindowLength = m?.Data.Length ?? 0;
        RecomputeDrill();
    }

    private void CloseDrill()
    {
        _drillMetricId = null;
        DrillData = null;
    }

    /// <summary>Mouse-wheel zoom: factor &lt;1 zooms in (fewer visible samples), &gt;1 zooms out. Keeps the
    /// sample under the cursor (anchorFraction, 0..1 across the current window) at the same screen position,
    /// same feel as zooming a map toward the pointer instead of always toward the window's center.</summary>
    public void ZoomDrill(double factor, double anchorFraction)
    {
        if (_drillMetricId is null) return;
        var m = _allChannels.FirstOrDefault(x => x.Id == _drillMetricId);
        if (m is null) return;

        int fullLength = m.Data.Length;
        int minWindow = Math.Min(10, fullLength);
        int newLength = Math.Clamp((int)Math.Round(_drillWindowLength * factor), minWindow, fullLength);

        int anchorIndex = _drillWindowStart + (int)Math.Round(_drillWindowLength * anchorFraction);
        int newStart = Math.Clamp(anchorIndex - (int)Math.Round(newLength * anchorFraction), 0, Math.Max(0, fullLength - newLength));

        _drillWindowStart = newStart;
        _drillWindowLength = newLength;
        RecomputeDrill();
    }

    /// <summary>Drag-to-pan: fractionDelta is the drag distance as a fraction of the chart's on-screen width
    /// (so the view doesn't need to know how many samples are currently visible). Negative = pan left/back
    /// in time.</summary>
    public void PanDrill(double fractionDelta)
    {
        if (_drillMetricId is null) return;
        var m = _allChannels.FirstOrDefault(x => x.Id == _drillMetricId);
        if (m is null) return;

        int fullLength = m.Data.Length;
        int deltaSamples = (int)Math.Round(fractionDelta * _drillWindowLength);
        _drillWindowStart = Math.Clamp(_drillWindowStart - deltaSamples, 0, Math.Max(0, fullLength - _drillWindowLength));
        RecomputeDrill();
    }

    public void ResetDrillZoom()
    {
        if (_drillMetricId is null) return;
        var m = _allChannels.FirstOrDefault(x => x.Id == _drillMetricId);
        if (m is null) return;

        _drillWindowStart = 0;
        _drillWindowLength = m.Data.Length;
        RecomputeDrill();
    }

    /// <summary>Continuous value readout as the mouse moves over the drill chart — fraction (0..1) is the
    /// cursor's position across the chart's on-screen width, mapped internally to a sample within the
    /// CURRENTLY VISIBLE (zoomed) window, not the full channel history.</summary>
    public void UpdateDrillHover(double fraction)
    {
        if (_drillMetricId is null || DrillData is null) return;
        var m = _allChannels.FirstOrDefault(x => x.Id == _drillMetricId);
        if (m is null) return;

        int visibleIndex = (int)Math.Round(fraction * (_drillWindowLength - 1));
        int actualIndex = Math.Clamp(_drillWindowStart + visibleIndex, 0, m.Data.Length - 1);
        string fmt = m.Decimals == 2 ? "F2" : "F1";
        DrillData.HoverValue = $"{m.Data[actualIndex].ToString(fmt, CultureInfo.InvariantCulture)} {m.Unit}";
        DrillData.HoverTimeLabel = _channelData.Timestamps[actualIndex].ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public void ClearDrillHover()
    {
        if (DrillData is null) return;
        DrillData.HoverValue = null;
        DrillData.HoverTimeLabel = null;
    }

    // ==================== derived-data computation ====================

    private void BuildStaticData()
    {
        // The 12 TX/RX channels, in declaration order — first 6 render as the TX group, last 6 as RX.
        _allChannels.Clear();
        _allChannels.AddRange(new[]
        {
            new MetricDef { Id = "TAC", Name = "송신 AC",     Unit = "V",  ColorHex = DataGenerator.ChannelColor("TAC"), Data = _channelData.TAC },
            new MetricDef { Id = "TV",  Name = "송신 출력전압", Unit = "V",  ColorHex = DataGenerator.ChannelColor("TV"),  Data = _channelData.TV },
            new MetricDef { Id = "TFP", Name = "송신 정펄스",   Unit = "ms", ColorHex = DataGenerator.ChannelColor("TFP"), Data = _channelData.TFP },
            new MetricDef { Id = "TSP", Name = "송신 부펄스",   Unit = "ms", ColorHex = DataGenerator.ChannelColor("TSP"), Data = _channelData.TSP },
            new MetricDef { Id = "THZ", Name = "송신 주파수",   Unit = "Hz", ColorHex = DataGenerator.ChannelColor("THZ"), Data = _channelData.THZ },
            new MetricDef { Id = "TA",  Name = "송신 전류",     Unit = "A",  ColorHex = DataGenerator.ChannelColor("TA"),  Data = _channelData.TA },
            new MetricDef { Id = "RFP", Name = "수신 정펄스",   Unit = "ms", ColorHex = DataGenerator.ChannelColor("RFP"), Data = _channelData.RFP },
            new MetricDef { Id = "RSP", Name = "수신 부펄스",   Unit = "ms", ColorHex = DataGenerator.ChannelColor("RSP"), Data = _channelData.RSP },
            new MetricDef { Id = "RHZ", Name = "수신 주파수",   Unit = "Hz", ColorHex = DataGenerator.ChannelColor("RHZ"), Data = _channelData.RHZ },
            new MetricDef { Id = "RA",  Name = "수신 전류",     Unit = "A",  ColorHex = DataGenerator.ChannelColor("RA"),  Data = _channelData.RA },
            new MetricDef { Id = "RV1", Name = "수신 전압1",    Unit = "V",  ColorHex = DataGenerator.ChannelColor("RV1"), Data = _channelData.RV1 },
            new MetricDef { Id = "RV2", Name = "수신 전압2",    Unit = "V",  ColorHex = DataGenerator.ChannelColor("RV2"), Data = _channelData.RV2 },
        });

        // Display window: last 600 samples = last 10 minutes at the DB's 1-second sampling interval
        // (1 minute felt too short to read a trend on).
        int windowSize = Math.Min(600, _channelData.Timestamps.Length);
        var tsWindow = TakeLast(_channelData.Timestamps, windowSize);

        CombinedChartVM BuildChart(string header, IEnumerable<MetricDef> channels)
        {
            var series = new ObservableCollection<ChannelSeriesVM>(channels.Select(m =>
            {
                var window = TakeLast(m.Data, windowSize);
                string fmt = m.Decimals == 2 ? "F2" : "F1";
                string latest = m.Data[^1].ToString(fmt, CultureInfo.InvariantCulture);

                var (checkedValue, minimumValue, axisMin, axisMax) = PathBuilder.ComputeReferenceLines(window);

                return new ChannelSeriesVM
                {
                    Id = m.Id,
                    Name = m.Name,
                    Unit = m.Unit,
                    ColorHex = m.ColorHex,
                    WindowValues = window,
                    ValueFormat = fmt,
                    LatestValue = latest,
                    Value = latest,
                    AxisMaxLabel = axisMax.ToString(fmt, CultureInfo.InvariantCulture),
                    AxisMinLabel = axisMin.ToString(fmt, CultureInfo.InvariantCulture),
                    CheckedLabel = checkedValue.ToString(fmt, CultureInfo.InvariantCulture),
                    MinimumLabel = minimumValue.ToString(fmt, CultureInfo.InvariantCulture),
                    // Each channel gets its own stacked lane with its own real axis — units differ (V/A/Hz/ms)
                    // so one shared/overlaid scale wouldn't be meaningful. Axis is expanded (above) to fit
                    // the 점검/최소 lines too, so they're always visible instead of only when data happens
                    // to already span that far.
                    Path = PathBuilder.PathFromArray(window, axisMin, axisMax, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad),
                    CheckedLinePath = PathBuilder.HorizontalLinePath(checkedValue, axisMin, axisMax, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad),
                    MinimumLinePath = PathBuilder.HorizontalLinePath(minimumValue, axisMin, axisMax, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad),
                };
            }));

            string TimeLabel(int idx) => tsWindow.Length == 0 ? "" : tsWindow[idx].ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            return new CombinedChartVM
            {
                Header = header,
                Series = series,
                WindowTimestamps = tsWindow,
                TimeStartLabel = tsWindow.Length > 0 ? TimeLabel(0) : "",
                TimeMidLabel = tsWindow.Length > 0 ? TimeLabel(tsWindow.Length / 2) : "",
                TimeEndLabel = tsWindow.Length > 0 ? TimeLabel(tsWindow.Length - 1) + " (최신)" : "",
            };
        }

        TxChart = BuildChart("송신 (TX)", _allChannels.Take(6));
        RxChart = BuildChart("수신 (RX)", _allChannels.Skip(6));

        // Same 10-minute window as the charts above — re-seeded per window-start so the alarm set
        // changes as the window slides forward instead of freezing at whatever it rolled on startup.
        var alarmStart = tsWindow.Length > 0 ? tsWindow[0] : DateTime.Now.AddMinutes(-10);
        var alarmEnd = tsWindow.Length > 0 ? tsWindow[^1] : DateTime.Now;
        RecentAlarms = new ObservableCollection<AlarmEventModel>(
            new DataGenerator(DataGenerator.SeedFrom(alarmStart.ToString("O"))).GenerateRecentAlarms(alarmStart, alarmEnd));

        SetActivePage(ActivePageId);
    }

    private void RecomputeDrill()
    {
        if (_drillMetricId is null) { DrillData = null; return; }
        var m = _allChannels.FirstOrDefault(x => x.Id == _drillMetricId);
        if (m is null) { DrillData = null; return; }

        int fullLength = m.Data.Length;
        _drillWindowLength = Math.Clamp(_drillWindowLength <= 0 ? fullLength : _drillWindowLength, Math.Min(10, fullLength), fullLength);
        _drillWindowStart = Math.Clamp(_drillWindowStart, 0, Math.Max(0, fullLength - _drillWindowLength));

        var window = new double[_drillWindowLength];
        Array.Copy(m.Data, _drillWindowStart, window, 0, _drillWindowLength);

        var tsWindow = new DateTime[_drillWindowLength];
        Array.Copy(_channelData.Timestamps, _drillWindowStart, tsWindow, 0, _drillWindowLength);

        string rangeLabel = tsWindow.Length > 0
            ? $"{tsWindow[0]:yyyy-MM-dd HH:mm:ss} ~ {tsWindow[^1]:yyyy-MM-dd HH:mm:ss}"
            : "";

        var (checkedValue, minimumValue, axisMin, axisMax) = PathBuilder.ComputeReferenceLines(window);

        DrillData = new DrillDataVM
        {
            Id = m.Id,
            Name = m.Name,
            Unit = m.Unit,
            ColorHex = m.ColorHex,
            BigPath = PathBuilder.PathFromArray(window, axisMin, axisMax, ChartMetrics.DrillChartW, ChartMetrics.DrillChartH, ChartMetrics.DrillChartPad, ChartMetrics.DrillChartPad),
            CheckedLinePath = PathBuilder.HorizontalLinePath(checkedValue, axisMin, axisMax, ChartMetrics.DrillChartW, ChartMetrics.DrillChartH, ChartMetrics.DrillChartPad, ChartMetrics.DrillChartPad),
            MinimumLinePath = PathBuilder.HorizontalLinePath(minimumValue, axisMin, axisMax, ChartMetrics.DrillChartW, ChartMetrics.DrillChartH, ChartMetrics.DrillChartPad, ChartMetrics.DrillChartPad),
            Min = window.Min().ToString("F2", CultureInfo.InvariantCulture),
            Max = window.Max().ToString("F2", CultureInfo.InvariantCulture),
            Avg = window.Average().ToString("F2", CultureInfo.InvariantCulture),
            RangeLabel = rangeLabel,
            WindowLabel = $"{_drillWindowLength}/{fullLength} 샘플 표시 중",
        };
    }

    private void RecomputeClockLabel()
    {
        ClockLabel = _now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "  " + _now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }


    private static T[] TakeLast<T>(IReadOnlyList<T> arr, int count)
    {
        int n = Math.Min(count, arr.Count);
        var result = new T[n];
        for (int i = 0; i < n; i++) result[i] = arr[arr.Count - n + i];
        return result;
    }
}
