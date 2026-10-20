using System.Collections.ObjectModel;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;

namespace TLDSDashBoard.ViewModels;

/// <summary>Filter state + results for the 레벨 기록 리스트 (level record list) page.</summary>
public sealed class LevelRecordListViewModel : ViewModelBase
{
    private static readonly TrackCircuit AllTracks = new() { Id = "ALL", Name = "전체 궤도", StationId = "", Idx = -1, LevelType = "" };


    /// <summary>Real telemetry is per-second (see Search()), so an unbounded 기간 could mean hundreds of
    /// thousands of rows for one 조회 — this caps how far apart Start/End are allowed to be.</summary>
    public static readonly TimeSpan MaxSpan = TimeSpan.FromHours(2);

    public ObservableCollection<Station> Stations { get; } = new(ReferenceData.Stations);
    public ObservableCollection<TrackCircuit> Tracks { get; } = new();

    private Station _selectedStation = ReferenceData.Stations[0];
    public Station SelectedStation
    {
        get => _selectedStation;
        set
        {
            if (!SetProperty(ref _selectedStation, value)) return;
            RebuildTracks();
        }
    }

    private TrackCircuit _selectedTrack = null!; // 아래 생성자에서 RebuildTracks()가 지정함
    public TrackCircuit SelectedTrack { get => _selectedTrack; set => SetProperty(ref _selectedTrack, value); }

    /// <summary>사용자가 설정하는 시작~종료(양쪽 모두 날짜+시+분), 그래프 상세검색/궤도장애와 같은 컨트롤 —
    /// 이제 행이 초 단위이고 사용자가 한 시간 단위 묶음이 아니라 정확한 창을 지정해야 하므로, 예전의
    /// 24×1시간 고정 블록 콤보를 대체한다. Search()가 기간을 <see cref="MaxSpan"/>으로 제한한다.</summary>
    public DateRangeFilter Range { get; } = new();

    // 고정 컬렉션에 Add하는 방식이 아니라 설정 가능한 프로퍼티로 두어서, Search()가 행마다 CollectionChanged를
    // 일으키는 대신 PropertyChanged 한 번으로 새 ObservableCollection 통째로 바꿀 수 있게 한다 —
    // "전체 궤도" × 1시간 초단위 조회는 수만 행이 될 수 있고, 바인딩된 DataGrid에 반복문으로 Add하면
    // 그 규모에서 UI 스레드가 눈에 띄게 멈춘다(그래프 페이지 지연과 같은 종류의 문제).
    private ObservableCollection<LevelRecord> _results = new();
    public ObservableCollection<LevelRecord> Results { get => _results; private set => SetProperty(ref _results, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public RelayCommand SearchCommand { get; }
    public RelayCommand ExportCsvCommand { get; }

    public LevelRecordListViewModel()
    {
        RebuildTracks();
        SearchCommand = new RelayCommand(Search);
        ExportCsvCommand = new RelayCommand(ExportCsv);
        // 시작 시 자동 조회하지 않는다(기본으로 데이터를 그리는 페이지는 OverView뿐) — 이 페이지는 사용자가
        // 기간/궤도를 고르고 조회를 누를 때까지 비어 있다.
    }

    private static readonly string[] ChannelLabels =
        { "궤도 주파수", "차상 주파수", "지상 주파수", "송신 전압", "수신 A 전압", "수신 B 전압" };

    private void ExportCsv()
    {
        var headers = new List<string> { "역명칭", "궤도명칭", "기록일자", "시간대", "시간" };
        foreach (var ch in ChannelLabels)
            foreach (var suffix in CsvExporter.MeasurementSuffixes)
                headers.Add($"{ch}-{suffix}");

        var rows = Results.Select(r => (IReadOnlyList<string>)new List<string>
        {
            r.StationName, r.TrackName, r.RecordDate, r.HourRangeLabel, r.TimeLabel,
        }
        .Concat(CsvExporter.FlattenChannel(r.TRK_FREQ)).Concat(CsvExporter.FlattenChannel(r.CAB_FREQ))
        .Concat(CsvExporter.FlattenChannel(r.CODE_FREQ)).Concat(CsvExporter.FlattenChannel(r.TX_VOLTAGE))
        .Concat(CsvExporter.FlattenChannel(r.RX_A_VOLTAGE)).Concat(CsvExporter.FlattenChannel(r.RX_B_VOLTAGE))
        .ToList());

        if (CsvExporter.Export("레벨기록리스트.csv", headers, rows) is not null)
            StatusMessage = "CSV로 저장했습니다.";
    }

    private void RebuildTracks()
    {
        Tracks.Clear();
        Tracks.Add(AllTracks);
        foreach (var t in ReferenceData.TracksForStation(_selectedStation.Id)) Tracks.Add(t);
        // "전체 궤도"가 아니라 특정 궤도 하나를 기본으로 한다 — 이제 모든 행이 실제 텔레메트리 1초라서
        // (Search() 참고), 전체 궤도를 기본으로 하면 페이지를 처음 그리기만 해도 수만 행을 만들어야 한다.
        // "전체 궤도"는 필요할 때 콤보에서 고를 수 있다.
        SelectedTrack = Tracks.Count > 1 ? Tracks[1] : AllTracks;
    }

    private static readonly IChannelDataRepository Repository = new RealTelemetryRepository();

    private void Search()
    {
        var tracks = SelectedTrack.Id == "ALL"
            ? ReferenceData.TracksForStation(SelectedStation.Id)
            : new[] { SelectedTrack };

        var start = Range.Start;
        var end = Range.End;
        bool clamped = false;
        bool foundLog = true;
        if (end - start > MaxSpan)
        {
            // 2시간 창이 예전에는 항상 기간의 시작에서 시작해서, 하루 전체 조회(00:00~23:59)가 00:00~02:00만 보고
            // 그날 로그가 더 늦은 시간뿐이면(예: 16시 파일 하나) 빈 결과가 나왔다. 대신 기간 안에서 실제로 _file_log 시간
            // 파일이 있는 첫 시각에서 창을 시작하고, 없으면 기간의 시작으로 폴백한다.
            var firstLogged = FirstLoggedHour(start, end);
            foundLog = firstLogged is not null;
            if (firstLogged is { } h && h > start) start = h;
            end = start + MaxSpan < end ? start + MaxSpan : end;
            clamped = true;
        }

        List<LevelRecord> realRows;
        try
        {
            realRows = LoadRealRecords(tracks, SelectedStation, start, end);
        }
        catch (Exception ex)
        {
            Results = new ObservableCollection<LevelRecord>();
            StatusMessage = $"실데이터 조회 실패 ({ex.Message})";
            return;
        }

        string clampNote = !clamped ? ""
            : foundLog ? $" (최대 {MaxSpan.TotalHours:0}시간까지만 조회 — 로그가 있는 첫 구간 {start:MM-dd HH:mm}~{end:HH:mm})"
            : " (기간 내 로그 파일 없음)";

        // No sample-data fallback: an empty result means _file_log has no rows for this track/기간.
        Results = new ObservableCollection<LevelRecord>(realRows);
        StatusMessage = Results.Count > 0
            ? $"{Results.Count}건 조회됨 (초 단위){clampNote}"
            : $"해당 기간의 실측 데이터가 없습니다{clampNote}";
    }

    /// <summary>[start,end] 안에서 _file_log 시간 파일이 존재하는 첫 시각(그 시각에 파일이 있으면 start 자체이므로
    /// 시간 중간의 시작이 유지됨), 없으면 null. 파일 존재 여부만 확인하므로 여러 날 기간도 시간 단위로
    /// 훑을 만큼 가볍다.</summary>
    private static DateTime? FirstLoggedHour(DateTime start, DateTime end)
    {
        for (var hour = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0); hour <= end; hour = hour.AddHours(1))
        {
            if (System.IO.File.Exists(TldsDataPaths.HourLogFile(hour)))
                return hour < start ? start : hour;
        }
        return null;
    }

    /// <summary>[start,end] 안에서 <paramref name="tracks"/>의 모든 궤도에 대해 실제 텔레메트리 샘플(1개/초)마다
    /// LevelRecord 하나를 만든다 — 고정된 표준/점검/최소는 SetValue.db(로그가 아니라 장치당 한 행)에서 오고,
    /// 측정은 같은 초의 해당 실제 샘플에서 오므로, 모든 행의 네 값이 실제 한 시점에 맞춰진다. 임계값 행이 없거나
    /// 이 기간에 _file_log가 다루지 않는 궤도는 조용히 건너뛴다(오류 아님) — 행이 0개일 뿐이며,
    /// TrackFaultListViewModel의 궤도별 실데이터 루프와 같다.</summary>
    private static List<LevelRecord> LoadRealRecords(IEnumerable<TrackCircuit> tracks, Station station, DateTime start, DateTime end)
    {
        var results = new List<LevelRecord>();
        foreach (var track in tracks)
        {
            var thresholds = ThresholdRepository.LoadForDevice(track.Idx);
            if (thresholds is null) continue;

            ChannelDataSet set;
            try { set = Repository.LoadRange(track.Idx, start, end); }
            catch (Exception) { continue; }

            for (int i = 0; i < set.Timestamps.Length; i++)
            {
                var ts = set.Timestamps[i];
                results.Add(new LevelRecord
                {
                    StationName = station.Name,
                    TrackName = track.Name,
                    RecordDate = ts.ToString("yyyy-MM-dd"),
                    HourRangeLabel = $"{ts.Hour:00} ~ {ts.Hour + 1:00}시",
                    TimeLabel = ts.ToString("HH:mm:ss"),
                    TRK_FREQ = ThresholdRepository.ToMeasurement(thresholds[0], set.TRK_FREQ[i]),
                    CAB_FREQ = ThresholdRepository.ToMeasurement(thresholds[1], set.CAB_FREQ[i]),
                    CODE_FREQ = ThresholdRepository.ToMeasurement(thresholds[2], set.CODE_FREQ[i]),
                    TX_VOLTAGE = ThresholdRepository.ToMeasurement(thresholds[3], set.TX_VOLTAGE[i]),
                    RX_A_VOLTAGE = ThresholdRepository.ToMeasurement(thresholds[4], set.RX_A_VOLTAGE[i]),
                    RX_B_VOLTAGE = ThresholdRepository.ToMeasurement(thresholds[5], set.RX_B_VOLTAGE[i]),

                });
            }
        }
        return results;
    }
}
