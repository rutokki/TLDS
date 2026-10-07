using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.ViewModels;

/// <summary>
/// The dashboard's single view model. This app displays track-circuit telemetry history that already lives
/// in a database (see <see cref="IChannelDataRepository"/>) — it does not generate or receive live
/// readings. The 6 channels are split into two groups (주파수/전압, see ChannelCatalog) and each group is rendered as a
/// stack of per-channel lanes (own real value axis per channel — their units, Hz/V, don't share
/// one scale); clicking a lane's label opens a drill-in modal with that one channel's full history.
/// The DB itself samples every 1 second, but the app only re-reads it every 60s (a deliberate refresh
/// cadence, separate from the underlying sample rate). OverView always shows the window
/// [now − 10분, now]; if the real _file_log has no rows for that window the charts are left empty with
/// the reason shown (see <see cref="DataSourceLabel"/>/<see cref="ChannelDataEmptyMessage"/>) — no
/// generated/sample data is ever substituted.
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

    private readonly IChannelDataRepository _repository = new RealTelemetryRepository();
    /// <summary>OverView display window — the last 10 minutes up to now (600 samples at 1/초).</summary>
    private static readonly TimeSpan OverviewWindow = TimeSpan.FromMinutes(10);

    /// <summary>Null when the real _file_log has no rows for the current OverView window.</summary>
    private ChannelDataSet? _channelData;
    private readonly List<MetricDef> _allChannels = new();
    private IReadOnlyList<ChannelThreshold>? _thresholds;
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
        ExportCsvCommand = new RelayCommand(ExportCsv);
        CloseDrillCommand = new RelayCommand(CloseDrill);
        NavClickCommand = new RelayCommand(p => OnNavClick((string)p!));
        OpenDrillCommand = new RelayCommand(p => OpenDrill((string)p!));
        GraphSearchCommand = new RelayCommand(GraphSearch);

        BuildStaticData();
        BuildAlarmData();
        RecomputeClockLabel();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => { _now = DateTime.Now; RecomputeClockLabel(); };
        _clockTimer.Start();

        // DB samples every 1 second, but we only poll it every 60s — a deliberate refresh cadence, not tied to the sample rate.
        // Charts and alarms are refreshed independently (see BuildAlarmData) — both happen to run on this
        // same 60초 cadence, but GraphSearch (device switch) only rebuilds charts, never alarms.
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _refreshTimer.Tick += (_, _) => { LoadChannelData(); BuildStaticData(); BuildAlarmData(); RecomputeDrill(); };
        _refreshTimer.Start();
    }

    /// <summary>Loads [now − 10분, now] for the currently selected device from the real _file_log DB
    /// (the hour file(s) TLDS.exe is writing). No fallback: when that window has no rows the charts are
    /// cleared and <see cref="ChannelDataEmptyMessage"/> says why.</summary>
    private void LoadChannelData()
    {
        var end = DateTime.Now;
        var start = end - OverviewWindow;
        string window = $"{start:HH:mm:ss} ~ {end:HH:mm:ss}";
        try
        {
            _channelData = _repository.LoadRange(SelectedTrack.Idx, start, end);
            var last = _channelData.Timestamps[^1];
            IsRealData = true;
            DataSourceLabel = $"실데이터 · {SelectedTrack.Name}";
            DataSourceDetail = $"DB 연결됨 — {_channelData.Timestamps.Length}건 ({window}), 마지막 수신 {last:HH:mm:ss}\n" +
                               $"경로: {TldsDataPaths.FileLogRoot}";
            ChannelDataEmptyMessage = null;
        }
        catch (Exception ex)
        {
            _channelData = null;
            IsRealData = false;
            DataSourceLabel = $"데이터 없음 · {SelectedTrack.Name}";
            // Full reason goes in the badge's tooltip — the topbar used to print this whole exception
            // message inline on every page, which was long, noisy and not about the page being viewed.
            DataSourceDetail = $"최근 10분({window}) 실데이터 없음\n{ex.Message}\n경로: {TldsDataPaths.FileLogRoot}";
            bool hourFileExists = System.IO.File.Exists(TldsDataPaths.HourLogFile(end));
            ChannelDataEmptyMessage =
                $"{SelectedTrack.Name} — 최근 10분({window}) 실측 데이터가 없습니다.\n" +
                (hourFileExists
                    ? "현재 시간대 로그 파일은 있지만 이 장치의 최근 기록이 없습니다."
                    : $"현재 시간대 로그 파일이 없습니다 — TLDS 프로그램이 실행 중인지 확인하세요.\n{TldsDataPaths.HourLogFile(end)}");
        }
    }

    private string? _channelDataEmptyMessage;
    /// <summary>Why the OverView charts are empty (null while real data is shown) — the page's empty-state text.</summary>
    public string? ChannelDataEmptyMessage { get => _channelDataEmptyMessage; private set => SetProperty(ref _channelDataEmptyMessage, value); }

    // ==================== child page view models ====================

    public TrainOccupancyViewModel TrainOccupancy { get; } // 열차 점유 상태 검색 모델
    public TrackFaultListViewModel TrackFaultList { get; } // 궤도 고장 상태 검색 모델
    public LevelRecordListViewModel LevelRecordList { get; } // 전압 리스트 검색 모델
    public HourlyStatusViewModel HourlyStatus { get; } // OverView 선택 모델
    public GraphDetailSearchViewModel GraphDetailSearch { get; } // 상세 그래프 검색 모델

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
    /// <summary>Changing this immediately re-renders the OverView charts for the newly picked device
    /// (no separate 조회 button — selecting a device previews it right away).</summary>
    public TrackCircuit SelectedTrack
    {
        get => _selectedTrack;
        set { if (SetProperty(ref _selectedTrack, value)) GraphSearch(); }
    }

    public DateRangeFilter GraphRange { get; } = new();

    public RelayCommand GraphSearchCommand { get; }

    // ==================== bound collections / properties ====================

    private CombinedChartVM? _frequencyChart;
    public CombinedChartVM? FrequencyChart { get => _frequencyChart; private set => SetProperty(ref _frequencyChart, value); }

    private CombinedChartVM? _voltageChart;
    public CombinedChartVM? VoltageChart { get => _voltageChart; private set => SetProperty(ref _voltageChart, value); }

    private ObservableCollection<AlarmEventModel> _recentAlarms = new();
    /// <summary>Alarms within the same 10-minute window as FrequencyChart/VoltageChart — shown below the charts on the Overview page.</summary>
    public ObservableCollection<AlarmEventModel> RecentAlarms { get => _recentAlarms; private set => SetProperty(ref _recentAlarms, value); }

    private string _dataSourceLabel = string.Empty;
    public string DataSourceLabel { get => _dataSourceLabel; private set => SetProperty(ref _dataSourceLabel, value); }

    private string _dataSourceDetail = string.Empty;
    /// <summary>Longer explanation (incl. the failure reason when falling back) — the topbar badge's tooltip.</summary>
    public string DataSourceDetail { get => _dataSourceDetail; private set => SetProperty(ref _dataSourceDetail, value); }

    private bool _isRealData;
    /// <summary>True when OverView is showing real _file_log telemetry, false when it fell back to sample
    /// data — drives the topbar badge color and the sidebar footer status.</summary>
    public bool IsRealData { get => _isRealData; private set => SetProperty(ref _isRealData, value); }

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

    private void ToggleExport()
    {
        ExportOpen = _exportOpen = !_exportOpen;
    }

    private void ShowToast(string message, int durationMs)
    {
        _toastTimer?.Stop();
        Toast = _toast = message;
        ExportOpen = _exportOpen = false;
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
        _toastTimer.Tick += (_, _) => { _toastTimer!.Stop(); Toast = _toast = null; };
        _toastTimer.Start();
    }
    /// <summary>Shared by the topbar's "Export as CSV" (always visible) and the drill-in modal's CSV
    /// button (same command, see DrillModalView) — exports the one channel's full history when the drill
    /// modal is open, otherwise the current 10-minute window across all 6 channels.</summary>
    private void ExportCsv()
    {
        if (_channelData is not { } data)
        {
            ShowToast("내보낼 실데이터가 없습니다.", 2200);
            return;
        }

        string? path;
        if (_drillMetricId is not null)
        {
            var m = _allChannels.FirstOrDefault(x => x.Id == _drillMetricId);
            if (m is null) return;

            string fmt = m.Decimals == 2 ? "F2" : "F1";
            var headers = new[] { "Timestamp", $"{m.Name}({m.Unit})" };
            var rows = data.Timestamps.Select((ts, i) => (IReadOnlyList<string>)new[]
            {
                ts.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                m.Data[i].ToString(fmt, CultureInfo.InvariantCulture),
            });
            path = CsvExporter.Export($"{m.Id}_전체이력.csv", headers, rows);
        }
        else
        {
            int windowSize = data.Timestamps.Length;
            var tsWindow = data.Timestamps;
            var channelWindows = _allChannels.Select(m => TakeLast(m.Data, windowSize)).ToArray();

            var headers = new List<string> { "Timestamp" };
            headers.AddRange(_allChannels.Select(m => $"{m.Name}({m.Unit})"));

            var rows = Enumerable.Range(0, tsWindow.Length).Select(i =>
            {
                var row = new List<string> { tsWindow[i].ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) };
                for (int c = 0; c < _allChannels.Count; c++)
                {
                    string fmt = _allChannels[c].Decimals == 2 ? "F2" : "F1";
                    row.Add(channelWindows[c][i].ToString(fmt, CultureInfo.InvariantCulture));
                }
                return (IReadOnlyList<string>)row;
            });
            path = CsvExporter.Export("OverView_채널.csv", headers, rows);
        }

        if (path is not null) ShowToast("CSV로 저장했습니다.", 2200);
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
        LoadChannelData(); // real _file_log data for the newly selected device, [now − 10분, now] — no fallback
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
        DrillData.HoverTimeLabel = _channelData!.Timestamps[actualIndex].ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
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
        _allChannels.Clear();

        // No real rows for [now − 10분, now]: leave the page empty (ChannelDataEmptyMessage explains why)
        // rather than drawing anything that isn't a real reading.
        if (_channelData is not { } data)
        {
            FrequencyChart = null;
            VoltageChart = null;
            CloseDrill();
            SetActivePage(ActivePageId);
            return;
        }

        // The 6 channels in ChannelCatalog order (= Value0..Value5 = threshold index) — id/name/unit/color
        // all come from the catalog so this list can't drift from the other pages.
        _allChannels.AddRange(ChannelCatalog.ChannelIds.Select(id => new MetricDef
        {
            Id = id,
            Name = ChannelCatalog.ChannelName(id),
            Unit = ChannelCatalog.ChannelUnit(id),
            ColorHex = ChannelCatalog.ChannelColor(id),
            Data = data.Get(id),
        }));

        // Display window = everything LoadChannelData read, i.e. exactly [now − 10분, now].
        int windowSize = data.Timestamps.Length;
        var tsWindow = data.Timestamps;

        // Real 기준/주의/경고 thresholds for the selected device, if SetValue.db has a row for it — falls
        // back to the old data-derived guess (below) per-channel when it doesn't. Index 0..5 matches the
        // ChannelCatalog order in _allChannels above (same as Value0..Value5 in _file_log). Wrapped
        // in try/catch since this runs on every chart rebuild (including the 60초 refresh timer) — an
        // unexpected failure here (e.g. a locked file) must not take the whole page down.
        IReadOnlyList<ChannelThreshold>? thresholds;
        try { thresholds = ThresholdRepository.LoadForDevice(SelectedTrack.Idx); }
        catch (Exception) { thresholds = null; }
        // Cached so RecomputeDrill (the 상세보기 modal) can draw the SAME 점검/최소 lines as these lanes
        // instead of re-deriving its own placeholder guess from the drilled channel's own data spread —
        // that mismatch was why the modal's lines looked different from the real values shown here.
        _thresholds = thresholds;

        CombinedChartVM BuildChart(string header, IEnumerable<(MetricDef Metric, int ChannelIndex)> channels)
        {
            var series = new ObservableCollection<ChannelSeriesVM>(channels.Select(c =>
            {
                var m = c.Metric;
                var window = TakeLast(m.Data, windowSize);
                string fmt = m.Decimals == 2 ? "F2" : "F1";
                string latest = m.Data[^1].ToString(fmt, CultureInfo.InvariantCulture);

                var threshold = thresholds is not null && c.ChannelIndex < thresholds.Count ? thresholds[c.ChannelIndex] : null;
                var (checkedValue, minimumValue, axisMin, axisMax) = threshold is not null
                    ? PathBuilder.ComputeReferenceLines(window, threshold.Caution, threshold.Warning)
                    : PathBuilder.ComputeReferenceLines(window);

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
                    // Each channel gets its own stacked lane with its own real axis — units differ (Hz/V)
                    // so one shared/overlaid scale wouldn't be meaningful. Axis is expanded (above) to fit
                    // the 점검/최소 lines too, so they're always visible instead of only when data happens
                    // to already span that far.
                    Path = PathBuilder.PathFromArray(window, axisMin, axisMax, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad),
                    CheckedLinePath = PathBuilder.HorizontalLinePath(checkedValue, axisMin, axisMax, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad),
                    MinimumLinePath = PathBuilder.HorizontalLinePath(minimumValue, axisMin, axisMax, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad),
                };
            }));

            string TimeLabel(int idx)
            {
                return tsWindow.Length == 0 ? "" : tsWindow[idx].ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            }

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

        // One lane block per catalog group; ChannelIndex stays the catalog/threshold index, not the
        // position inside the block (the old TX/RX split used Take(6)/Skip(6)).
        IEnumerable<(MetricDef, int)> Group(string group) =>
            _allChannels.Select((m, i) => (m, i)).Where(x => ChannelCatalog.ChannelGroup(x.m.Id) == group);
        FrequencyChart = BuildChart(ChannelCatalog.FrequencyGroup, Group(ChannelCatalog.FrequencyGroup));
        VoltageChart = BuildChart(ChannelCatalog.VoltageGroup, Group(ChannelCatalog.VoltageGroup));

        SetActivePage(ActivePageId);
    }

    /// <summary>Rebuilds the "최근 10분 알람" list. Kept separate from <see cref="BuildStaticData"/> —
    /// alarms are system-wide (any device can raise one, see AlarmEventModel.DeviceName), not scoped to
    /// whichever device the 주파수/전압 charts currently show, so switching the selected device (GraphSearch)
    /// must NOT recompute this; only the 60초 refresh timer does.
    /// Source: the Alarm table of _file_db/Event-{yyyy-MM-dd}.db (both days when the window crosses
    /// midnight). Severity is derived from restore state until the real MAJOR/MINOR code table is known:
    /// still active (no RESTORE_TIME) → Critical, already restored → Info.</summary>
    private void BuildAlarmData()
    {
        var alarmEnd = DateTime.Now;
        var alarmStart = alarmEnd - OverviewWindow;
        var rows = new List<AlarmEventModel>();
        try
        {
            for (var day = alarmStart.Date; day <= alarmEnd.Date; day = day.AddDays(1))
            {
                foreach (var a in EventLogRepository.LoadAlarms(day))
                {
                    if (a.GeneratedAt < alarmStart || a.GeneratedAt > alarmEnd) continue;
                    rows.Add(new AlarmEventModel
                    {
                        Time = a.GeneratedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                        DeviceName = a.DeviceName,
                        Message = a.RestoredAt is { } r ? $"{a.Content} (복구 {r:HH:mm:ss})" : a.Content,
                        Severity = a.RestoredAt is null ? AlarmSeverity.Critical : AlarmSeverity.Info,
                    });
                }
            }
            AlarmEmptyMessage = rows.Count == 0 ? "최근 10분간 발생한 알람이 없습니다." : null;
        }
        catch (Exception ex)
        {
            AlarmEmptyMessage = $"알람 DB를 읽지 못했습니다: {ex.Message}";
        }
        // Newest first, like a live log.
        RecentAlarms = new ObservableCollection<AlarmEventModel>(rows.AsEnumerable().Reverse());
    }

    private string? _alarmEmptyMessage;
    /// <summary>Shown in place of the alarm list when it's empty (or the DB couldn't be read); null otherwise.</summary>
    public string? AlarmEmptyMessage { get => _alarmEmptyMessage; private set => SetProperty(ref _alarmEmptyMessage, value); }

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
        Array.Copy(_channelData!.Timestamps, _drillWindowStart, tsWindow, 0, _drillWindowLength);

        string rangeLabel = tsWindow.Length > 0
            ? $"{tsWindow[0]:yyyy-MM-dd HH:mm:ss} ~ {tsWindow[^1]:yyyy-MM-dd HH:mm:ss}"
            : "";

        // Same real threshold the channel's lane on the main page uses (index into _allChannels matches
        // Value0..5 / thresholds[0..5], see BuildStaticData) — falls back to the data-derived placeholder
        // only when SetValue.db has no row for this device, same as the lanes do.
        int channelIndex = _allChannels.IndexOf(m);
        var threshold = channelIndex >= 0 ? _thresholds?[channelIndex] : null;
        var (checkedValue, minimumValue, axisMin, axisMax) = threshold is not null
            ? PathBuilder.ComputeReferenceLines(window, threshold.Caution, threshold.Warning)
            : PathBuilder.ComputeReferenceLines(window);

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
