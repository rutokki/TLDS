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
        // 시작 시 자동 조회하지 않는다(기본으로 데이터를 그리는 페이지는 OverView뿐) — 이 페이지는 사용자가
        // 필터를 고르고 조회를 누를 때까지 비어 있다.
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
        // TLDS 설치에서 열차 점유의 실제 데이터 출처가 아직 확인되지 않았다(지금까지 찾은 _file_log/_file_db
        // 테이블은 텔레메트리, 임계값, 이벤트/알람 로그뿐이다 — _file_log의 OutValue01(Time,Len,Status BLOB)이
        // 점유/출력 상태일 수 있지만 형식이 문서화되어 있지 않다). 샘플 데이터는 더 이상 보여주지 않으므로,
        // 그 출처가 연결될 때까지 이 페이지는 비어 있다.
        StatusMessage = "열차점유 실데이터 소스가 아직 연동되지 않았습니다 (TLDS DB에서 점유 기록 테이블 미확인).";
    }
}
