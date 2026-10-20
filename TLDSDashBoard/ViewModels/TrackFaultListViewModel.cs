using System.Collections.ObjectModel;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;

namespace TLDSDashBoard.ViewModels;

/// <summary>Filter state + results for the 궤도 장애 및 경보 리스트 (track fault &amp; alarm list) page.</summary>
public sealed class TrackFaultListViewModel : ViewModelBase
{
    private static readonly TrackCircuit AllTracks = new() { Id = "ALL", Name = "전체 궤도", StationId = "", Idx = -1, LevelType = "" };

    public ObservableCollection<Station> Stations { get; } = new(ReferenceData.Stations);
    public ObservableCollection<TrackCircuit> Tracks { get; } = new();
    public ObservableCollection<string> StatusOptions { get; } = new(new[] { "전체상태", "발생중", "복구됨" });

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

    private TrackCircuit _selectedTrack = AllTracks;
    public TrackCircuit SelectedTrack { get => _selectedTrack; set => SetProperty(ref _selectedTrack, value); }

    private string _selectedStatus = "전체상태";
    public string SelectedStatus { get => _selectedStatus; set => SetProperty(ref _selectedStatus, value); }

    public DateRangeFilter Range { get; } = new();

    public ObservableCollection<TrackFaultRecord> Results { get; } = new();

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public RelayCommand SearchCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand PrintCommand { get; }

    public TrackFaultListViewModel()
    {
        RebuildTracks();
        SearchCommand = new RelayCommand(Search);
        ExportCsvCommand = new RelayCommand(ExportCsv);
        PrintCommand = new RelayCommand(() => StatusMessage = "인쇄를 준비 중입니다.");
        // 시작 시 자동 조회하지 않는다(기본으로 데이터를 그리는 페이지는 OverView뿐) — 이 페이지는 사용자가
        // 필터를 고르고 조회를 누를 때까지 비어 있다.
    }

    private static readonly string[] ChannelLabels =
        { "궤도 주파수", "차상 주파수", "지상 주파수", "송신 전압", "수신 A 전압", "수신 B 전압"};
    private void ExportCsv()
    {
        var headers = new List<string> { "궤도명칭", "일자", "발생시각", "복구시각", "장애종류", "IMP", "AF", "궤도타입" };
        foreach (var ch in ChannelLabels)
            foreach (var suffix in CsvExporter.MeasurementSuffixes)
                headers.Add($"{suffix}-{ch}");

        var rows = Results.Select(r => (IReadOnlyList<string>)new List<string>
        {
            r.TrackName, r.Date, r.OccurredAt, r.RecoveredAt, r.FaultType, r.Imp, r.Af, r.TrackType,
        }
        .Concat(CsvExporter.FlattenChannel(r.TRK_FREQ)).Concat(CsvExporter.FlattenChannel(r.CAB_FREQ))
        .Concat(CsvExporter.FlattenChannel(r.CODE_FREQ)).Concat(CsvExporter.FlattenChannel(r.TX_VOLTAGE))
        .Concat(CsvExporter.FlattenChannel(r.RX_A_VOLTAGE)).Concat(CsvExporter.FlattenChannel(r.RX_B_VOLTAGE))
        .ToList());

        if (CsvExporter.Export("궤도장애경보리스트.csv", headers, rows) is not null)
            StatusMessage = "CSV로 저장했습니다.";
    }

    private void RebuildTracks()
    {
        Tracks.Clear();
        Tracks.Add(AllTracks);
        foreach (var t in ReferenceData.TracksForStation(_selectedStation.Id)) Tracks.Add(t);
        SelectedTrack = AllTracks;
    }

    private void Search()
    {
        Results.Clear();
        var tracks = SelectedTrack.Id == "ALL"
            ? ReferenceData.TracksForStation(SelectedStation.Id)
            : new[] { SelectedTrack };
        var tracksByName = tracks.ToDictionary(t => t.Name);

        List<TrackFaultRecord> rows;
        try
        {
            rows = LoadRealAlarms(tracksByName, Range.Start, Range.End);
        }
        catch (Exception ex)
        {
            StatusMessage = $"알람 DB 조회 실패 ({ex.Message})";
            return;
        }

        // 상태 filter — "발생중" = not restored yet, "복구됨" = has a RESTORE_TIME.
        rows = SelectedStatus switch
        {
            "발생중" => rows.Where(r => string.IsNullOrEmpty(r.RecoveredAt)).ToList(),
            "복구됨" => rows.Where(r => !string.IsNullOrEmpty(r.RecoveredAt)).ToList(),
            _ => rows,
        };

        foreach (var row in rows) Results.Add(row);
        // 샘플 데이터 폴백 없음: 결과가 비어 있으면 실제 Alarm 로그에 이 필터에 해당하는 것이 없다는 뜻이다.
        StatusMessage = Results.Count > 0
            ? $"{Results.Count}건 조회됨"
            : "해당 기간/조건의 장애·경보 기록이 없습니다.";
    }

    private static readonly IChannelDataRepository Telemetry = new RealTelemetryRepository();

    /// <summary>[start,end]의 모든 날에 대해 _file_db/Event-{yyyy-MM-dd}.db의 Alarm 테이블을 읽되, DEVICE_NAME이
    /// 조회 대상 궤도 중 하나와 일치하는 행만 남긴다. Alarm 테이블에는 IMP/AF/채널 스냅샷 컬럼이 없으므로
    /// TrackType은 일치한 TrackCircuit에서, 표준/점검/최소는 ThresholdRepository(SetValue.db)에서, 측정은 알람
    /// 발생 시각의 실제 _file_log 샘플(그 뒤 60초 이내의 첫 샘플)에서 가져온다. 그런 샘플이 없으면 측정은
    /// 빈칸으로 두고 — 절대 추정하지 않는다.</summary>
    private static List<TrackFaultRecord> LoadRealAlarms(IReadOnlyDictionary<string, TrackCircuit> tracksByName, DateTime start, DateTime end)
    {
        var results = new List<TrackFaultRecord>();
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            foreach (var alarm in EventLogRepository.LoadAlarms(day))
            {
                if (alarm.GeneratedAt < start || alarm.GeneratedAt > end) continue;
                if (!tracksByName.TryGetValue(alarm.DeviceName, out var track)) continue;

                var thresholds = ThresholdRepository.LoadForDevice(track.Idx);
                if (thresholds is null) continue; // 이 장치에 표시할 채널 데이터가 없음

                ChannelDataSet? sample = null;
                try { sample = Telemetry.LoadRange(track.Idx, alarm.GeneratedAt, alarm.GeneratedAt.AddSeconds(60)); }
                catch (Exception) { /* no telemetry at that moment — 측정 stays blank */ }
                double? At(Func<ChannelDataSet, double[]> channel)
                {
                    return sample is null ? null : channel(sample)[0];
                }

                results.Add(new TrackFaultRecord
                {
                    TrackName = track.Name,
                    Date = alarm.GeneratedAt.ToString("yyyy-MM-dd"),
                    OccurredAt = alarm.GeneratedAt.ToString("HH:mm:ss"),
                    RecoveredAt = alarm.RestoredAt?.ToString("HH:mm:ss") ?? "",
                    FaultType = alarm.Content,
                    Imp = "",
                    Af = "",
                    TrackType = track.LevelType,
                    TRK_FREQ = ThresholdRepository.ToMeasurement(thresholds[0], At(s => s.TRK_FREQ)),
                    CAB_FREQ = ThresholdRepository.ToMeasurement(thresholds[1], At(s => s.CAB_FREQ)),
                    CODE_FREQ = ThresholdRepository.ToMeasurement(thresholds[2], At(s => s.CODE_FREQ)),
                    TX_VOLTAGE = ThresholdRepository.ToMeasurement(thresholds[3], At(s => s.TX_VOLTAGE)),
                    RX_A_VOLTAGE = ThresholdRepository.ToMeasurement(thresholds[4], At(s => s.RX_A_VOLTAGE)),
                    RX_B_VOLTAGE = ThresholdRepository.ToMeasurement(thresholds[5], At(s => s.RX_B_VOLTAGE)),

                });
            }
        }
        return results.OrderBy(r => r.Date).ThenBy(r => r.OccurredAt).ToList();
    }
}
