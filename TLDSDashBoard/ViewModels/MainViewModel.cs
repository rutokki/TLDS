using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.ViewModels;

/// <summary>
/// 대시보드의 단일 뷰모델. 이 앱은 이미 데이터베이스에 있는 궤도회로 텔레메트리 이력(<see cref="IChannelDataRepository"/>
/// 참고)을 보여줄 뿐이며, 실시간 값을 스스로 생성하거나 받지 않는다. 6개 채널은 두 그룹(주파수/전압, ChannelCatalog
/// 참고)으로 나뉘고 각 그룹은 채널별 레인을 쌓아 그린다(채널마다 자신의 실제 값 축 — 단위 Hz/V가 하나의
/// 스케일을 공유하지 않기 때문); 레인의 라벨을 클릭하면 그 채널의 전체 이력을 보여주는 드릴인 모달이 열린다.
/// DB는 1초마다 샘플링하지만 앱은 60초마다만 다시 읽는다(기본 샘플링 속도와는 별개의 의도적인 갱신 주기).
/// OverView는 항상 [현재 − 10분, 현재] 창을 보여주며, 실제 _file_log에 그 창의 행이 없으면 차트를 비워 둔 채
/// 이유를 표시한다(<see cref="DataSourceLabel"/>/<see cref="ChannelDataEmptyMessage"/> 참고) — 생성된/샘플
/// 데이터를 대신 채우는 일은 없다.
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

    /// <summary>실제 _file_log에 현재 OverView 창의 행이 없으면 null.</summary>
    private ChannelDataSet? _channelData;
    private readonly List<MetricDef> _allChannels = new();
    private IReadOnlyList<ChannelThreshold>? _thresholds;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _refreshTimer;
    private DispatcherTimer? _toastTimer;

    // ---- 상태 ----
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

        // DB는 1초마다 샘플링하지만 60초마다만 읽는다 — 샘플링 속도와 무관한 의도적인 갱신 주기다.
        // 차트와 알람은 독립적으로 갱신되며(BuildAlarmData 참고) 둘 다 이 60초 주기에서 같이 실행되지만,
        // GraphSearch(장치 전환)는 차트만 다시 만들고 알람은 건드리지 않는다.
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _refreshTimer.Tick += (_, _) => { LoadChannelData(); BuildStaticData(); BuildAlarmData(); RecomputeDrill(); };
        _refreshTimer.Start();
    }

    /// <summary>선택한 장치의 [현재 − 10분, 현재]를 실제 _file_log DB(TLDS.exe가 쓰고 있는 시간 파일)에서
    /// 읽는다. 폴백 없음: 그 창에 행이 없으면 차트를 비우고 <see cref="ChannelDataEmptyMessage"/>가 이유를
    /// 알려준다.</summary>
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
            // 전체 사유는 배지의 툴팁에 넣는다 — 상단 바가 예전에는 이 예외 메시지 전체를 모든 페이지에 그대로
            // 찍었는데, 길고 시끄러운 데다 보고 있는 페이지와 관계도 없었다.
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
    /// <summary>OverView 차트가 비어 있는 이유(실데이터를 보여주는 동안은 null) — 페이지의 빈 상태 문구.</summary>
    public string? ChannelDataEmptyMessage { get => _channelDataEmptyMessage; private set => SetProperty(ref _channelDataEmptyMessage, value); }

    // ==================== 자식 페이지 뷰모델 ====================

    public TrainOccupancyViewModel TrainOccupancy { get; } // 열차 점유 상태 검색 모델
    public TrackFaultListViewModel TrackFaultList { get; } // 궤도 고장 상태 검색 모델
    public LevelRecordListViewModel LevelRecordList { get; } // 전압 리스트 검색 모델
    public HourlyStatusViewModel HourlyStatus { get; } // OverView 선택 모델
    public GraphDetailSearchViewModel GraphDetailSearch { get; } // 상세 그래프 검색 모델

    // ==================== 내비게이션 상태 ====================

    private string _activePageId = DefaultPageId;
    public string ActivePageId { get => _activePageId; private set => SetProperty(ref _activePageId, value); }

    private string _activePageTitle = NavDefs.First(n => n.Id == DefaultPageId).Label;
    /// <summary>상단 바에 표시되며, 현재 활성화된 사이드바 항목의 라벨.</summary>
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

    // ==================== 그래프 페이지 필터 ====================

    public ObservableCollection<TrackCircuit> AvailableTracks { get; }

    private TrackCircuit _selectedTrack;
    /// <summary>이 값을 바꾸면 새로 고른 장치로 OverView 차트가 즉시 다시 그려진다
    /// (별도 조회 버튼 없음 — 장치를 고르면 바로 미리 본다).</summary>
    public TrackCircuit SelectedTrack
    {
        get => _selectedTrack;
        set { if (SetProperty(ref _selectedTrack, value)) GraphSearch(); }
    }

    public DateRangeFilter GraphRange { get; } = new();

    public RelayCommand GraphSearchCommand { get; }

    // ==================== 바인딩되는 컬렉션 / 프로퍼티 ====================

    private CombinedChartVM? _frequencyChart;
    public CombinedChartVM? FrequencyChart { get => _frequencyChart; private set => SetProperty(ref _frequencyChart, value); }

    private CombinedChartVM? _voltageChart;
    public CombinedChartVM? VoltageChart { get => _voltageChart; private set => SetProperty(ref _voltageChart, value); }

    private ObservableCollection<AlarmEventModel> _recentAlarms = new();
    /// <summary>FrequencyChart/VoltageChart와 같은 10분 창의 알람 — Overview 페이지의 차트 아래에 표시된다.</summary>
    public ObservableCollection<AlarmEventModel> RecentAlarms { get => _recentAlarms; private set => SetProperty(ref _recentAlarms, value); }

    private string _dataSourceLabel = string.Empty;
    public string DataSourceLabel { get => _dataSourceLabel; private set => SetProperty(ref _dataSourceLabel, value); }

    private string _dataSourceDetail = string.Empty;
    /// <summary>더 자세한 설명(폴백했을 때는 실패 사유 포함) — 상단 바 배지의 툴팁.</summary>
    public string DataSourceDetail { get => _dataSourceDetail; private set => SetProperty(ref _dataSourceDetail, value); }

    private bool _isRealData;
    /// <summary>OverView가 실제 _file_log 텔레메트리를 보여주고 있으면 true, 샘플 데이터로 폴백했으면 false —
    /// 상단 바 배지 색과 사이드바 하단 상태를 결정한다.</summary>
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

    // ==================== 커맨드 ====================

    public RelayCommand ToggleExportCommand { get; }
    public RelayCommand ExportPngCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand CloseDrillCommand { get; }
    public RelayCommand NavClickCommand { get; }
    public RelayCommand OpenDrillCommand { get; }

    // ==================== 커맨드 핸들러 ====================

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
    /// <summary>상단 바의 "Export as CSV"(항상 보임)와 드릴인 모달의 CSV 버튼(같은 커맨드, DrillModalView 참고)이
    /// 공유한다 — 드릴 모달이 열려 있으면 한 채널의 전체 이력을, 아니면 6개 채널 전체의 현재 10분 창을
    /// 내보낸다.</summary>
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

            string fmt = m.Format;
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
                    string fmt = _allChannels[c].Format;
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

        // NavItemVM은 불변(init 전용)이다 — 항목을 바꾸지 말고 컬렉션을 다시 만든다.
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

    /// <summary>마우스 휠 줌: factor &lt;1이면 확대(보이는 샘플이 줄어듦), &gt;1이면 축소. 커서 아래의 샘플(anchorFraction,
    /// 현재 창에서 0..1)을 같은 화면 위치에 유지하므로, 항상 창의 중앙 쪽으로 줌하지 않고 지도를 포인터 쪽으로
    /// 줌하는 것과 같은 느낌이다.</summary>
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

    /// <summary>드래그로 팬: fractionDelta는 드래그 거리를 차트의 화면상 폭에 대한 비율로 나타낸 것이다
    /// (뷰가 현재 보이는 샘플 수를 알 필요가 없도록). 음수 = 왼쪽(과거)으로 이동.</summary>
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

    /// <summary>드릴 차트 위에서 마우스를 움직일 때의 연속 값 읽기 — fraction(0..1)은 차트의 화면상 폭에서
    /// 커서의 위치이며, 전체 채널 이력이 아니라 현재 보이는(줌된) 창 안의 샘플로 내부에서 변환된다.</summary>
    public void UpdateDrillHover(double fraction)
    {
        if (_drillMetricId is null || DrillData is null) return;
        var m = _allChannels.FirstOrDefault(x => x.Id == _drillMetricId);
        if (m is null) return;

        int visibleIndex = (int)Math.Round(fraction * (_drillWindowLength - 1));
        int actualIndex = Math.Clamp(_drillWindowStart + visibleIndex, 0, m.Data.Length - 1);
        string fmt = m.Format;
        DrillData.HoverValue = $"{m.Data[actualIndex].ToString(fmt, CultureInfo.InvariantCulture)} {m.Unit}";
        DrillData.HoverTimeLabel = _channelData!.Timestamps[actualIndex].ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public void ClearDrillHover()
    {
        if (DrillData is null) return;
        DrillData.HoverValue = null;
        DrillData.HoverTimeLabel = null;
    }

    // ==================== 파생 데이터 계산 ====================

    private void BuildStaticData()
    {
        _allChannels.Clear();

        // [현재 − 10분, 현재]에 실제 행이 없음: 페이지를 비워 둔다(ChannelDataEmptyMessage가 이유를 설명).
        // 실제 측정값이 아닌 것을 그리지 않는다.
        if (_channelData is not { } data)
        {
            FrequencyChart = null;
            VoltageChart = null;
            CloseDrill();
            SetActivePage(ActivePageId);
            return;
        }

        // ChannelCatalog 순서의 6개 채널(= Value0..Value5 = 임계값 인덱스) — id/이름/단위/색이 모두 카탈로그에서
        // 오므로 이 목록이 다른 페이지와 어긋날 수 없다.
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

        // 선택한 장치에 SetValue.db 행이 있으면 실제 기준/주의/경고 임계값을 쓰고, 없으면 채널별로 예전의 데이터
        // 추정값(아래)으로 폴백한다. 인덱스 0..5는 위 _allChannels의 ChannelCatalog 순서와 같다(_file_log의
        // Value0..Value5와 동일). 이 코드는 차트를 다시 만들 때마다(60초 갱신 타이머 포함) 실행되므로 try/catch로
        // 감쌌다 — 여기서 예상치 못한 실패(예: 잠긴 파일)가 페이지 전체를 죽이면 안 된다.
        IReadOnlyList<ChannelThreshold>? thresholds;
        try { thresholds = ThresholdRepository.LoadForDevice(SelectedTrack.Idx); }
        catch (Exception) { thresholds = null; }
        // RecomputeDrill(상세보기 모달)이 드릴한 채널 자체의 데이터 편차에서 자기만의 임시 추정값을 다시 구하는 대신
        // 이 레인들과 같은 점검/최소 선을 그릴 수 있도록 캐시한다 — 그 불일치 때문에 모달의 선이 여기 표시된 실제
        // 값과 달라 보였다.
        _thresholds = thresholds;

        CombinedChartVM BuildChart(string header, IEnumerable<(MetricDef Metric, int ChannelIndex)> channels)
        {
            var series = new ObservableCollection<ChannelSeriesVM>(channels.Select(c =>
            {
                var m = c.Metric;
                var window = TakeLast(m.Data, windowSize);
                string fmt = m.Format;
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
                    // 각 채널은 자신의 실제 축을 가진 자기 레인을 얻는다 — 단위(Hz/V)가 달라서 하나의 공유/겹친 스케일은
                    // 의미가 없다. 축은 위에서 점검/최소 선까지 포함하도록 확장되므로, 데이터가 이미 그만큼 넓게 퍼진
                    // 경우에만이 아니라 항상 선이 보인다.
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

        // 카탈로그 그룹마다 레인 블록 하나; ChannelIndex는 블록 안의 위치가 아니라 카탈로그/임계값 인덱스를
        // 그대로 유지한다(예전 TX/RX 분할은 Take(6)/Skip(6)을 썼다).
        IEnumerable<(MetricDef, int)> Group(string group) =>
            _allChannels.Select((m, i) => (m, i)).Where(x => ChannelCatalog.ChannelGroup(x.m.Id) == group);
        FrequencyChart = BuildChart(ChannelCatalog.FrequencyGroup, Group(ChannelCatalog.FrequencyGroup));
        VoltageChart = BuildChart(ChannelCatalog.VoltageGroup, Group(ChannelCatalog.VoltageGroup));

        SetActivePage(ActivePageId);
    }

    /// <summary>"최근 10분 알람" 목록을 다시 만든다. <see cref="BuildStaticData"/>와 분리해 둔다 —
    /// 알람은 시스템 전체 단위(어느 장치든 발생시킬 수 있음, AlarmEventModel.DeviceName 참고)이며 주파수/전압
    /// 차트가 현재 보여주는 장치에 한정되지 않으므로, 선택한 장치를 바꿔도(GraphSearch) 이것을 다시
    /// 계산하면 안 되고 60초 갱신 타이머만 계산한다.
    /// 출처: _file_db/Event-{yyyy-MM-dd}.db의 Alarm 테이블(창이 자정을 넘으면 양쪽 날). 실제 MAJOR/MINOR
    /// 코드표를 알기 전까지 심각도는 복구 상태로 정한다: 아직 발생 중(RESTORE_TIME 없음) → Critical,
    /// 이미 복구됨 → Info.</summary>
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
        // 최신순, 실시간 로그처럼.
        RecentAlarms = new ObservableCollection<AlarmEventModel>(rows.AsEnumerable().Reverse());
    }

    private string? _alarmEmptyMessage;
    /// <summary>알람 목록이 비었을 때(또는 DB를 읽지 못했을 때) 목록 대신 표시한다. 그 외에는 null.</summary>
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

        // 메인 페이지의 해당 채널 레인이 쓰는 것과 같은 실제 임계값(_allChannels의 인덱스가 Value0..5 /
        // thresholds[0..5]와 일치, BuildStaticData 참고) — 이 장치에 SetValue.db 행이 없을 때만 레인과 마찬가지로
        // 데이터에서 뽑은 임시값으로 폴백한다.
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
            Min = window.Min().ToString(m.Format, CultureInfo.InvariantCulture),
            Max = window.Max().ToString(m.Format, CultureInfo.InvariantCulture),
            Avg = window.Average().ToString("F" + (m.Decimals + 1), CultureInfo.InvariantCulture),
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
