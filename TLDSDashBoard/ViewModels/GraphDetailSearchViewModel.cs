using System.Collections.ObjectModel;
using System.Globalization;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.ViewModels;

/// <summary>
/// 그래프 상세검색: 모든 채널을 각각 별도의 쌓인 레인으로 보여준다(Overview 페이지의 MetricGroupView와 같은
/// 시각 패턴 — 채널당 전체 폭 한 행, 각자 실제 값 축을 가짐. 단위(V/A/Hz/ms)가 스케일을 공유하지 않기
/// 때문이다). 하나의 공유 차트에 겹쳐 그리거나 작은 카드 격자로 하지 않는다 — 둘 다 시도했다가 반려되어
/// 이 쌓인 레이아웃으로 정해졌다. 모든 레인은 하나의 시간 창/줌 상태를 공유하므로(마우스 휠로 커서 쪽 줌,
/// 왼쪽 드래그 팬, 호버 크로스헤어 — 드릴인 모달과 같은 상호작용 모델) 어디서 줌하거나 호버해도 모든
/// 레인이 함께 갱신된다. 기본값은 최근 1시간의 1초 간격 샘플이다.
/// </summary>
public sealed class GraphDetailSearchViewModel : ViewModelBase
{
    // 드래그/줌 중 마우스를 움직일 때마다(Recompute가 Pan 호출마다 실행됨) 모든 레인의 약 3600포인트
    // WPF Path 문자열을 다시 만드는 것이 보고된 지연의 원인이었다 — 차트 영역은 폭이 몇백 픽셀뿐이라
    // 이보다 많은 점은 어차피 시각적으로 구분되지 않는다.
    private const int MaxRenderPoints = 400;

    private readonly Dictionary<string, double[]> _channelData = new();
    private DateTime[] _fullTimestamps = Array.Empty<DateTime>();
    private int _windowStart;
    private int _windowLength;
    /// <summary>선택한 장치의 채널별 실제 점검(Caution)/최소(Warning) 임계값. Search()마다 한 번 로드한다 —
    /// 인덱스는 ChannelCatalog.ChannelIds 순서(TRK_FREQ..RX_B_VOLTAGE)와 같으며, MainViewModel.BuildStaticData와
    /// 같은 규칙이다. SetValue.db에 이 장치의 행이 없으면 null이고, 그 경우 Recompute()는 대신 데이터에서
    /// 뽑은 임시값으로 폴백한다(PathBuilder.ComputeReferenceLines 참고).</summary>
    private IReadOnlyList<ChannelThreshold>? _thresholds;

    public ObservableCollection<TrackCircuit> AvailableTracks { get; } = new(ReferenceData.Tracks);
    public DateRangeFilter Range { get; } = new();

    private TrackCircuit _selectedTrack;
    public TrackCircuit SelectedTrack { get => _selectedTrack; set => SetProperty(ref _selectedTrack, value); }

    /// <summary>쌓인 레인들, 채널당 하나 — Search()마다 한 번 만들고, 이후 줌/팬/호버 때 Recompute()가 제자리에서 갱신한다.</summary>
    public ObservableCollection<ChannelSeriesVM> Series { get; } = new();

    /// <summary>표시 중인 창의 실제 (시작/중간/끝) 시각 — MetricGroupView의 시간축과 같은 3개 라벨 배치.</summary>
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

    /// <summary>확대하지 않은 레인 하나의 높이(픽셀) — 뷰가 ScrollViewer의 실제 뷰포트 높이로 설정한다
    /// (뷰포트 높이 ÷ 채널 수). 그래서 LaneHeightScale == 1일 때 모든 레인이 스크롤 없이 한 화면에 들어간다.
    /// GraphDetailSearchView.LanesScroll_SizeChanged 참고.</summary>
    private double _baseLaneHeight = 54;
    private double _laneHeightScale = 1.0;

    /// <summary>라벨 컬럼과 차트 컬럼이 함께 바인딩하는 실제 레인 한 줄의 픽셀 높이라서 행이 정렬된 채로 유지된다.
    /// 기본 높이나 줌 배율이 바뀔 때마다 다시 계산된다.</summary>
    public double LaneHeight => _baseLaneHeight * _laneHeightScale;

    public void SetBaseLaneHeight(double pixels)
    {
        // 레인마다 개별적으로 픽셀 반올림된 Border 높이를 모두 합쳤을 때 뷰포트를 1픽셀이라도 넘지 않도록
        // 약간 뺀다 — 그 초과가 확대하지 않은 한 화면 크기에서도 "Auto" 세로 스크롤바를 작동시켜서, Viewbox로
        // 늘어난 차트의 폭을 몇 픽셀 빼앗아 크기가 눈에 띄게 바뀌게 했다(아래 HasVerticalOverflow도 참고).
        pixels = Math.Max(0, pixels - 0.5);
        if (pixels <= 0 || double.IsNaN(pixels) || Math.Abs(pixels - _baseLaneHeight) < 0.5) return;
        _baseLaneHeight = pixels;
        OnPropertyChanged(nameof(LaneHeight));
    }

    /// <summary>LaneHeightScale에 <paramref name="factor"/>를 곱하고 [1, 6] 범위로 제한한다 — 1이 "한 화면에
    /// 맞는" 크기이고, 더 큰 값은 일부러 ScrollViewer 뷰포트를 넘치게 해서 사용자가 확대된 레인을 스크롤해서
    /// 볼 수 있게 한다.</summary>
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

    /// <summary>세로 확대로 레인이 실제로 한 화면 기준보다 커졌을 때 true. 레인 ScrollViewer의 세로 스크롤바
    /// 표시 여부가 이 값에 바인딩된다(마우스 휠이 어떤 모드인지일 뿐인 <see cref="IsVerticalZoomMode"/>가 아니라)
    /// — 그래서 스크롤바와 그것이 차트에서 빼앗는 폭이, 정말로 스크롤할 것이 있을 때만 나타나고
    /// 기본 크기에서 반올림 오차로 나타나는 일이 없다.</summary>
    public bool HasVerticalOverflow => _laneHeightScale > 1.0;

    private bool _isVerticalZoomMode;
    /// <summary>켜면 차트 영역의 마우스 휠이 평소의 가로 시간 줌 대신 레인 크기를 조절한다(AdjustLaneZoom)
    /// — GraphDetailSearchView.ChartArea_MouseWheel 참고.</summary>
    public bool IsVerticalZoomMode { get => _isVerticalZoomMode; set => SetProperty(ref _isVerticalZoomMode, value); }

    private bool _showMeasuredLine = true;
    /// <summary>모든 레인에서 색이 있는 측정 데이터 곡선(Path)을 켜고 끈다.</summary>
    public bool ShowMeasuredLine { get => _showMeasuredLine; set => SetProperty(ref _showMeasuredLine, value); }

    private bool _showReferenceLines = true;
    /// <summary>모든 레인에서 점선 점검/최소 임계선(CheckedLinePath + MinimumLinePath)을 함께 켜고 끈다 —
    /// 측정값을 사이에 두고 항상 한 쌍이므로 스위치 하나로 둘 다 제어한다.</summary>
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
        // 시작 시 자동 조회하지 않는다(기본으로 데이터를 그리는 페이지는 OverView뿐) — 이 페이지는 사용자가
        // 기간/장치를 고르고 조회를 누를 때까지 비어 있다.
    }

    private static readonly IChannelDataRepository Repository = new RealTelemetryRepository();

    /// <summary>선택한 기간을 정확히 조회한다 — 목(mock) 폴백 없음. 이 페이지는 실제 현장 텔레메트리를 살펴보는
    /// 용도이므로, <see cref="Repository"/>에 요청한 장치/기간의 데이터가 없으면 화면은 (그 사실을 알리는 상태
    /// 메시지와 함께) 아무것도 보여주지 않는다. 실제 측정값으로 오인될 수 있는 생성된 가짜 데이터로
    /// 조용히 대체하지 않는다.</summary>
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

        // 선택한 장치의 실제 점검/최소 임계값 — MainViewModel의 레인이 쓰는 것과 같은 출처라서, 이 페이지의
        // 기준선이 OverView와 일치한다. 데이터에서 뽑은 임시값을 그리면 실제 Caution/Warning 값이 이 창의
        // 작은 변동 범위에서 멀 때 "이상해" 보일 수 있다(예: 데이터 바로 위에 겹친 평평한 선).
        try { _thresholds = hasData ? ThresholdRepository.LoadForDevice(SelectedTrack.Idx) : null; }
        catch (Exception) { _thresholds = null; }

        _windowStart = 0;
        _windowLength = _fullTimestamps.Length;

        Series.Clear();
        if (hasData)
        {
            foreach (var id in ChannelCatalog.ChannelIds)
            {
                string fmt = ChannelCatalog.ChannelFormat(id);
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
            // Recompute()는 창이 비어 있으면 즉시 빠져나가므로 이전 조회의 시간/범위 라벨이 화면에 남는다 —
            // 데이터 없음 결과가 오래된 값이 아니라 빈 화면으로 읽히도록 명시적으로 지운다.
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

    /// <summary>커서 쪽으로 마우스 휠 줌 — MainViewModel.ZoomDrill과 같은 동작이며, 이유는 그쪽을 참고.</summary>
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

    /// <summary>드래그로 팬, fractionDelta = 드래그 거리를 차트의 화면상 폭에 대한 비율로 나타낸 값.</summary>
    public void Pan(double fractionDelta)
    {
        int fullLength = _fullTimestamps.Length;
        if (fullLength == 0) return;
        int deltaSamples = (int)Math.Round(fractionDelta * _windowLength);
        _windowStart = Math.Clamp(_windowStart - deltaSamples, 0, Math.Max(0, fullLength - _windowLength));
        Recompute();
    }

    /// <summary>모든 레인의 값을 한 번에 연속으로 읽어낸다 — fraction(0..1)은 공유 차트 영역의 화면상 폭에서
    /// 커서의 위치다.</summary>
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

            // 한 번만 다운샘플링한다(구간별 최소/최대 방식은 전역 최소/최대를 유지하므로, 아래 작은 배열에 대해
            // ComputeReferenceLines와 PathFromArray를 모두 실행할 수 있다). 줌/팬 틱마다 전체 창 해상도로
            // 처리하지 않기 위함이다 — 위의 MaxRenderPoints 참고.
            var renderWindow = window.Length > MaxRenderPoints ? PathBuilder.Downsample(window, MaxRenderPoints) : window;

            // 각 레인은 자신의 실제 축을 유지한다(정규화/겹쳐 그리기 아님) — 단위(V/A/Hz/ms)가 달라 하나의 공유
            // 스케일은 의미가 없기 때문이다. 축은 점검/최소 선까지 포함하도록 확장된다. 실제 임계값(Search() 참고)이
            // 우선하며, SetValue.db에 이 장치의 행이 없을 때만 데이터에서 뽑은 임시값으로 폴백한다.
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
