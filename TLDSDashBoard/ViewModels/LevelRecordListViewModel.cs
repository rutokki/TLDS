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

    private TrackCircuit _selectedTrack = null!; // assigned by RebuildTracks() in the constructor below
    public TrackCircuit SelectedTrack { get => _selectedTrack; set => SetProperty(ref _selectedTrack, value); }

    /// <summary>User-settable 시작~종료 (date+hour+minute on both ends), same control as 그래프
    /// 상세검색/궤도장애 — replaces the old fixed 24×1시간 block combo now that rows are per-second and the
    /// user needs to target an exact window, not just an hour bucket. Search() clamps the span to
    /// <see cref="MaxSpan"/>.</summary>
    public DateRangeFilter Range { get; } = new();

    // A settable property (not a fixed collection mutated via Add) so Search() can swap in a whole new
    // ObservableCollection in one PropertyChanged notification instead of one CollectionChanged per row —
    // "전체 궤도" × 1시간 초단위 조회 can mean tens of thousands of rows, and Add-in-a-loop against a bound
    // DataGrid at that volume visibly freezes the UI thread (same class of issue as the graph-page lag).
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
        // No auto-search on load (OverView is the only page that renders data by default) — this page
        // starts empty until the user picks a 기간/궤도 and clicks 조회.
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
        // Default to one specific track, not "전체 궤도" — now that every row is one real second of
        // telemetry (see Search()), an all-tracks default would mean building tens of thousands of rows
        // just to render the page for the first time. "전체 궤도" stays available in the combo when wanted.
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
            // The 2시간 window used to always begin at the range's start — so a whole-day 조회 (00:00~23:59)
            // only ever looked at 00:00~02:00 and came back empty whenever the day's logs were later (e.g.
            // only a 16시 file). Start the window at the first hour inside the range that actually has a
            // _file_log hour file instead; fall back to the range start when none exists.
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

    /// <summary>First hour in [start,end] whose _file_log hour file exists (start itself if that hour has
    /// one, so a mid-hour start is kept), or null if none does. File-existence only — cheap enough to walk
    /// a multi-day range hour by hour.</summary>
    private static DateTime? FirstLoggedHour(DateTime start, DateTime end)
    {
        for (var hour = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0); hour <= end; hour = hour.AddHours(1))
        {
            if (System.IO.File.Exists(TldsDataPaths.HourLogFile(hour)))
                return hour < start ? start : hour;
        }
        return null;
    }

    /// <summary>Builds one LevelRecord per real telemetry sample (1개/초) for every track in
    /// <paramref name="tracks"/> within [start,end] — the fixed 표준/점검/최소 come from SetValue.db (one
    /// row per device, not a log) while 측정 comes from the matching real sample in that same second, so
    /// every row's four values line up on one real point in time. A track with no threshold row, or no
    /// _file_log coverage for this period, is silently skipped (not an error) — it just contributes zero
    /// rows, same as TrackFaultListViewModel's per-track real-data loop.</summary>
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
