using System.Collections.ObjectModel;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;

namespace TLDSDashBoard.ViewModels;

/// <summary>Filter state + results for the 레벨 기록 리스트 (level record list) page.</summary>
public sealed class LevelRecordListViewModel : ViewModelBase
{
    private readonly DataGenerator _generator = new();

    public ObservableCollection<Station> Stations { get; } = new(ReferenceData.Stations);
    public ObservableCollection<TrackCircuit> Tracks { get; } = new();
    public ObservableCollection<string> HourRanges { get; } = new(
        Enumerable.Range(0, 24).Select(h => $"{h:00} ~ {h + 1:00}시"));

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

    private TrackCircuit _selectedTrack;
    public TrackCircuit SelectedTrack { get => _selectedTrack; set => SetProperty(ref _selectedTrack, value); }

    private DateTime _recordDate = DateTime.Today;
    public DateTime RecordDate { get => _recordDate; set => SetProperty(ref _recordDate, value); }

    private string _selectedHourRange;
    public string SelectedHourRange { get => _selectedHourRange; set => SetProperty(ref _selectedHourRange, value); }

    public ObservableCollection<LevelRecord> Results { get; } = new();

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public RelayCommand SearchCommand { get; }
    public RelayCommand ExportCsvCommand { get; }

    public LevelRecordListViewModel()
    {
        _selectedTrack = ReferenceData.TracksForStation(_selectedStation.Id).First();
        _selectedHourRange = HourRanges[11];
        RebuildTracks();
        SearchCommand = new RelayCommand(Search);
        ExportCsvCommand = new RelayCommand(() => StatusMessage = "CSV로 저장했습니다.");
        Search();
    }

    private void RebuildTracks()
    {
        Tracks.Clear();
        foreach (var t in ReferenceData.TracksForStation(_selectedStation.Id)) Tracks.Add(t);
        SelectedTrack = Tracks[0];
    }

    private void Search()
    {
        Results.Clear();
        foreach (var row in _generator.GenerateLevelRecords(SelectedTrack, SelectedStation, RecordDate, SelectedHourRange))
        {
            Results.Add(row);
        }
        StatusMessage = $"{Results.Count}건 조회됨";
    }
}
