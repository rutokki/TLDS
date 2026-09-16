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
/// Data is (re)loaded from the DB every 60s to match the DB's 1-minute sampling interval; if the DB
/// can't be reached, generated sample data is shown instead so the UI still renders (see
/// <see cref="DataSourceLabel"/>).
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private static readonly (string Id, string Label) NavOverview = ("overview", "Overview");

    private static readonly (string Id, string Label)[] NavDefs =
    {
        ("overview", "Overview"), ("channels", "Channels"), ("alarms", "Alarms"),
        ("reports", "Reports"), ("settings", "Settings")
    };

    // 12-color free-assignment categorical palette (no TX/RX or unit-family grouping — picked only for mutual distinguishability on the dark background).
    private const string ColorTAC = "#4C9AFF";
    private const string ColorTV = "#FF9F40";
    private const string ColorTFP = "#36D399";
    private const string ColorTSP = "#FF6B6B";
    private const string ColorTHZ = "#B983FF";
    private const string ColorTA = "#FFD166";
    private const string ColorRFP = "#4EE1D6";
    private const string ColorRSP = "#FF7AC6";
    private const string ColorRHZ = "#A3E635";
    private const string ColorRA = "#8B93FF";
    private const string ColorRV1 = "#2DD4BF";
    private const string ColorRV2 = "#FB7185";

    private const string ColorSuccess = "#3FBE8A";
    private const string ColorCritical = "#D75B4A";
    private const string ColorTextSecondary = "#8A8F98";
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
    private DateTime _now = DateTime.Now;

    public MainViewModel()
    {
        LoadChannelData();
        // TODO: the alarm log is still generated sample data — swap for a real query once that table's schema is known too.
        Events = new ObservableCollection<AlarmEventModel>(new DataGenerator().GenerateEvents());

        ToggleExportCommand = new RelayCommand(ToggleExport);
        ExportPngCommand = new RelayCommand(() => ShowToast("Exported chart as PNG", 2200));
        ExportCsvCommand = new RelayCommand(() => ShowToast("Exported data as CSV", 2200));
        CloseDrillCommand = new RelayCommand(CloseDrill);
        NavClickCommand = new RelayCommand(p => OnNavClick((string)p!));
        OpenDrillCommand = new RelayCommand(p => OpenDrill((string)p!));

        BuildStaticData();
        RecomputeClockLabel();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => { _now = DateTime.Now; RecomputeClockLabel(); };
        _clockTimer.Start();

        // DB sampling interval is 1 minute — re-read it on the same cadence and rebuild the derived views.
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

    // ==================== bound collections / properties ====================

    public ObservableCollection<AlarmEventModel> Events { get; }

    private ObservableCollection<KpiItemVM> _kpis = new();
    public ObservableCollection<KpiItemVM> Kpis { get => _kpis; private set => SetProperty(ref _kpis, value); }

    private CombinedChartVM? _txChart;
    public CombinedChartVM? TxChart { get => _txChart; private set => SetProperty(ref _txChart, value); }

    private CombinedChartVM? _rxChart;
    public CombinedChartVM? RxChart { get => _rxChart; private set => SetProperty(ref _rxChart, value); }

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
        if (id == NavOverview.Id) return;
        var label = NavDefs.First(n => n.Id == id).Label;
        ShowToast($"{label} — coming soon", 2000);
    }

    private void OpenDrill(string id)
    {
        _drillMetricId = id;
        RecomputeDrill();
    }

    private void CloseDrill()
    {
        _drillMetricId = null;
        DrillData = null;
    }

    // ==================== derived-data computation ====================

    private void BuildStaticData()
    {
        // The 12 TX/RX channels, in declaration order — first 6 render as the TX group, last 6 as RX.
        _allChannels.Clear();
        _allChannels.AddRange(new[]
        {
            new MetricDef { Id = "TAC", Name = "송신 AC",     Unit = "V",  ColorHex = ColorTAC, Data = _channelData.TAC },
            new MetricDef { Id = "TV",  Name = "송신 출력전압", Unit = "V",  ColorHex = ColorTV,  Data = _channelData.TV },
            new MetricDef { Id = "TFP", Name = "송신 정펄스",   Unit = "ms", ColorHex = ColorTFP, Data = _channelData.TFP },
            new MetricDef { Id = "TSP", Name = "송신 부펄스",   Unit = "ms", ColorHex = ColorTSP, Data = _channelData.TSP },
            new MetricDef { Id = "THZ", Name = "송신 주파수",   Unit = "Hz", ColorHex = ColorTHZ, Data = _channelData.THZ },
            new MetricDef { Id = "TA",  Name = "송신 전류",     Unit = "A",  ColorHex = ColorTA,  Data = _channelData.TA },
            new MetricDef { Id = "RFP", Name = "수신 정펄스",   Unit = "ms", ColorHex = ColorRFP, Data = _channelData.RFP },
            new MetricDef { Id = "RSP", Name = "수신 부펄스",   Unit = "ms", ColorHex = ColorRSP, Data = _channelData.RSP },
            new MetricDef { Id = "RHZ", Name = "수신 주파수",   Unit = "Hz", ColorHex = ColorRHZ, Data = _channelData.RHZ },
            new MetricDef { Id = "RA",  Name = "수신 전류",     Unit = "A",  ColorHex = ColorRA,  Data = _channelData.RA },
            new MetricDef { Id = "RV1", Name = "수신 전압1",    Unit = "V",  ColorHex = ColorRV1, Data = _channelData.RV1 },
            new MetricDef { Id = "RV2", Name = "수신 전압2",    Unit = "V",  ColorHex = ColorRV2, Data = _channelData.RV2 },
        });

        // Display window: last 60 samples = last 60 minutes at the DB's 1-minute sampling interval.
        int windowSize = Math.Min(60, _channelData.Timestamps.Length);
        var tsWindow = TakeLast(_channelData.Timestamps, windowSize);

        CombinedChartVM BuildChart(string header, IEnumerable<MetricDef> channels)
        {
            var series = new ObservableCollection<ChannelSeriesVM>(channels.Select(m =>
            {
                var window = TakeLast(m.Data, windowSize);
                string fmt = m.Decimals == 2 ? "F2" : "F1";
                string latest = m.Data[^1].ToString(fmt, CultureInfo.InvariantCulture);
                double axisMin = window.Length > 0 ? window.Min() : 0;
                double axisMax = window.Length > 0 ? window.Max() : 0;
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
                    // Each channel gets its own stacked lane with its own real axis — units differ (V/A/Hz/ms)
                    // so one shared/overlaid scale wouldn't be meaningful.
                    Path = PathBuilder.PathFromArrayAutoRange(window, ChartMetrics.LaneChartW, ChartMetrics.LaneChartH, ChartMetrics.LaneChartPad, ChartMetrics.LaneChartPad),
                };
            }));

            string TimeLabel(int idx) => tsWindow.Length == 0 ? "" : tsWindow[idx].ToString("HH:mm", CultureInfo.InvariantCulture);

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

        int critCount = Events.Count(e => e.Severity == AlarmSeverity.Critical);

        // KPI row: 대표적으로 송신 출력전압(TV) / 수신 전압1(RV1) / 송신 주파수(THZ) / 활성 알람 4개를 선정 (필요시 다른 채널로 교체 가능)
        string KpiSpark(IReadOnlyList<double> full) =>
            PathBuilder.PathFromArrayAutoRange(TakeLast(full, 30), ChartMetrics.KpiSparkW, ChartMetrics.KpiSparkH, ChartMetrics.KpiSparkPad, ChartMetrics.KpiSparkPad);

        (string Label, string Value, string Unit, string Color, string SparkPath, string DeltaLabel, string DeltaColor, bool DeltaBold) DeltaKpi(string label, double[] data, string color)
        {
            double now = data[^1];
            double prev = data.Length > 20 ? data[^20] : data[0]; // DB may return fewer rows than expected (e.g. right after startup) — fall back to the oldest sample available
            double deltaPct = prev == 0 ? 0 : (now - prev) / prev * 100.0;
            string deltaLabel = (deltaPct >= 0 ? "▲ " : "▼ ") + Math.Abs(deltaPct).ToString("F1", CultureInfo.InvariantCulture) + "%";
            string deltaColor = deltaPct >= 0 ? ColorSuccess : ColorCritical;
            return (label, now.ToString("F1", CultureInfo.InvariantCulture), "", color, KpiSpark(data), deltaLabel, deltaColor, true);
        }

        var tv = DeltaKpi("송신 출력전압 (TV)", _channelData.TV, ColorTV);
        var rv1 = DeltaKpi("수신 전압1 (RV1)", _channelData.RV1, ColorRV1);

        Kpis = new ObservableCollection<KpiItemVM>
        {
            new() { Label = tv.Label, Value = tv.Value, Unit = "V", ColorHex = tv.Color, SparkPath = tv.SparkPath, DeltaLabel = tv.DeltaLabel, DeltaColorHex = tv.DeltaColor, DeltaBold = true },
            new() { Label = rv1.Label, Value = rv1.Value, Unit = "V", ColorHex = rv1.Color, SparkPath = rv1.SparkPath, DeltaLabel = rv1.DeltaLabel, DeltaColorHex = rv1.DeltaColor, DeltaBold = true },
            new()
            {
                Label = "송신 주파수 (THZ)", Value = _channelData.THZ[^1].ToString("F2", CultureInfo.InvariantCulture), Unit = "Hz",
                ColorHex = ColorTHZ, SparkPath = KpiSpark(_channelData.THZ),
                DeltaLabel = "stable", DeltaColorHex = ColorTextSecondary, DeltaBold = false
            },
            new()
            {
                Label = "Active Alarms", Value = critCount.ToString(CultureInfo.InvariantCulture), Unit = "critical",
                ColorHex = ColorCritical, SparkPath = KpiSpark(Enumerable.Range(1, Events.Count).Select(i => (double)i).ToArray()),
                DeltaLabel = $"{Events.Count} total", DeltaColorHex = ColorTextSecondary, DeltaBold = false
            },
        };

        NavItems = new ObservableCollection<NavItemVM>(NavDefs.Select(n => new NavItemVM
        {
            Id = n.Id,
            Label = n.Label,
            IsActive = n.Id == NavOverview.Id,
            DotColorHex = n.Id == NavOverview.Id ? ColorTV : ColorNavDotInactive,
            Badge = n.Id == "alarms" ? critCount.ToString(CultureInfo.InvariantCulture) : null
        }));
    }

    private void RecomputeDrill()
    {
        if (_drillMetricId is null) { DrillData = null; return; }
        var m = _allChannels.FirstOrDefault(x => x.Id == _drillMetricId);
        if (m is null) { DrillData = null; return; }

        var ts = _channelData.Timestamps;
        string rangeLabel = ts.Length > 0
            ? $"{ts[0]:yyyy-MM-dd HH:mm} ~ {ts[^1]:yyyy-MM-dd HH:mm}"
            : "";

        DrillData = new DrillDataVM
        {
            Name = m.Name,
            Unit = m.Unit,
            ColorHex = m.ColorHex,
            BigPath = PathBuilder.PathFromArrayAutoRange(m.Data, ChartMetrics.DrillChartW, ChartMetrics.DrillChartH, ChartMetrics.DrillChartPad, ChartMetrics.DrillChartPad),
            Min = m.Data.Min().ToString("F2", CultureInfo.InvariantCulture),
            Max = m.Data.Max().ToString("F2", CultureInfo.InvariantCulture),
            Avg = m.Data.Average().ToString("F2", CultureInfo.InvariantCulture),
            RangeLabel = rangeLabel,
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
