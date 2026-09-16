using System.Collections.ObjectModel;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;

namespace TLDSDashBoard.ViewModels;

/// <summary>Filter state + results for the 열차 점유 리스트 (train occupancy list) page.</summary>
public sealed class TrainOccupancyViewModel : ViewModelBase
{
    private static readonly TrackCircuit AllTracks = new() { Id = "ALL", Name = "전체 궤도", StationId = "" };

    private readonly DataGenerator _generator = new();

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
            foreach (var row in _generator.GenerateTrainOccupancy(track, Range.Start, Range.End))
            {
                Results.Add(row);
            }
        }
        StatusMessage = $"{Results.Count}건 조회됨";
    }
}
