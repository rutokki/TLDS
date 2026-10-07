using System.Collections.ObjectModel;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;

namespace TLDSDashBoard.ViewModels;

/// <summary>Filter state + results for the 열차 점유 리스트 (train occupancy list) page.</summary>
public sealed class TrainOccupancyViewModel : ViewModelBase
{
    private static readonly TrackCircuit AllTracks = new() { Id = "ALL", Name = "전체 궤도", StationId = "", Idx = -1, LevelType = "" };

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

    private TrackCircuit _selectedTrack = AllTracks;
    public TrackCircuit SelectedTrack { get => _selectedTrack; set => SetProperty(ref _selectedTrack, value); }

    public DateRangeFilter Range { get; } = new();

    public ObservableCollection<TrainOccupancyRecord> Results { get; } = new();

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public RelayCommand SearchCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand PrintCommand { get; }

    public TrainOccupancyViewModel()
    {
        RebuildTracks();
        SearchCommand = new RelayCommand(Search);
        ExportCsvCommand = new RelayCommand(ExportCsv);
        PrintCommand = new RelayCommand(() => StatusMessage = "인쇄를 준비 중입니다.");
        // No auto-search on load (OverView is the only page that renders data by default) — this page
        // starts empty until the user picks filters and clicks 조회.
    }

    private void ExportCsv()
    {
        var headers = new[] { "궤도명칭", "열차 진입 시간", "열차 통과 시간", "열차 점유 시간" };
        var rows = Results.Select(r => (IReadOnlyList<string>)new[]
        {
            r.TrackName, r.EntryTime, r.PassTime, r.OccupancyTime,
        });

        if (CsvExporter.Export("열차점유리스트.csv", headers, rows) is not null)
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
        // No real data source for train occupancy has been identified in the TLDS install yet (the
        // _file_log/_file_db tables found so far hold telemetry, thresholds and event/alarm logs only —
        // _file_log's OutValue01(Time,Len,Status BLOB) may be the occupancy/output status, but its format is
        // undocumented). Sample data is no longer shown, so this page stays empty until that source is wired.
        StatusMessage = "열차점유 실데이터 소스가 아직 연동되지 않았습니다 (TLDS DB에서 점유 기록 테이블 미확인).";
    }
}
