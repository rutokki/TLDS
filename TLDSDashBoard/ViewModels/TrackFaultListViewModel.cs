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
        // No auto-search on load (OverView is the only page that renders data by default) — this page
        // starts empty until the user picks filters and clicks 조회.
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
        // No sample-data fallback: an empty result means the real Alarm log has nothing for this filter.
        StatusMessage = Results.Count > 0
            ? $"{Results.Count}건 조회됨"
            : "해당 기간/조건의 장애·경보 기록이 없습니다.";
    }

    private static readonly IChannelDataRepository Telemetry = new RealTelemetryRepository();

    /// <summary>Reads _file_db/Event-{yyyy-MM-dd}.db's Alarm table for every day in [start,end], keeping
    /// only rows whose DEVICE_NAME matches one of the tracks being queried. The Alarm table has no
    /// IMP/AF/channel-snapshot columns, so TrackType comes from the matched TrackCircuit, 표준/점검/최소
    /// from ThresholdRepository (SetValue.db), and 측정 from the real _file_log sample at the alarm's
    /// generate time (first sample within 60초 after it). When no such sample exists 측정 is left blank —
    /// never estimated.</summary>
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
                if (thresholds is null) continue; // no channel data to show for this device

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
