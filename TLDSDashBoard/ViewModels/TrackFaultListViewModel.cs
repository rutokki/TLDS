using System.Collections.ObjectModel;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;

namespace TLDSDashBoard.ViewModels;

/// <summary>Filter state + results for the 궤도 장애 및 경보 리스트 (track fault &amp; alarm list) page.</summary>
public sealed class TrackFaultListViewModel : ViewModelBase
{
    private static readonly TrackCircuit AllTracks = new() { Id = "ALL", Name = "전체 궤도", StationId = "" };

    private readonly DataGenerator _generator = new();

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
        ExportCsvCommand = new RelayCommand(() => StatusMessage = "CSV로 저장했습니다.");
        PrintCommand = new RelayCommand(() => StatusMessage = "인쇄를 준비 중입니다.");
        Search();
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

        foreach (var track in tracks)
        {
            foreach (var row in _generator.GenerateTrackFaults(track, Range.Start, Range.End))
            {
                Results.Add(row);
            }
        }
        StatusMessage = $"{Results.Count}건 조회됨";
    }
}
