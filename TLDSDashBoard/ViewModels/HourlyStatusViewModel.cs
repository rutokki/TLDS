using System.Collections.ObjectModel;
using System.Globalization;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.ViewModels;

/// <summary>
/// Filter state + results for the 시간별 상태 출력 (hourly status output) modal. One search (기간/역/장치
/// + conditions) now fills in all 7 category grids at once, instead of requiring a category to be picked
/// first to see its results.
/// </summary>
public sealed class HourlyStatusViewModel : ViewModelBase
{
    private static readonly (HourlyStatusCategory Category, string Label)[] CategoryDefs =
    {
        (HourlyStatusCategory.TrackFault, "궤도 장애"),
        (HourlyStatusCategory.SignalFailure, "신호기 고장"),
        (HourlyStatusCategory.SwitchMachineFailure, "선로전환기 고장"),
        (HourlyStatusCategory.BlockDeviceFailure, "폐색장치 고장"),
        (HourlyStatusCategory.LeuFailure, "LEU 고장"),
        (HourlyStatusCategory.SignalEquipmentEvent, "신호설비 이벤트"),
        (HourlyStatusCategory.SystemEvent, "시스템 이벤트"),
    };

    private readonly DataGenerator _generator = new();

    public ObservableCollection<HourlyStatusCategoryGroup> CategoryGroups { get; } = new(
        CategoryDefs.Select(c => new HourlyStatusCategoryGroup { Category = c.Category, Label = c.Label }));

    private HourlyStatusCategoryGroup _selectedGroup;
    /// <summary>Which category's grid is shown on the right. Purely a display switch — every category's
    /// data is already loaded by <see cref="Search"/>, so changing this does not re-query anything.</summary>
    public HourlyStatusCategoryGroup SelectedGroup { get => _selectedGroup; set => SetProperty(ref _selectedGroup, value); }

    public ObservableCollection<Station> Stations { get; } = new(ReferenceData.Stations);
    public IReadOnlyList<int> HourChoices { get; } = Enumerable.Range(0, 24).ToList();

    private Station _selectedStation = ReferenceData.Stations[0];
    public Station SelectedStation { get => _selectedStation; set => SetProperty(ref _selectedStation, value); }

    public DateRangeFilter Range { get; } = new();

    public bool ConditionInvalidOccupancy { get; set; } = true; // 부정 점유
    public bool ConditionVoltageWarning { get; set; } = true;   // 전압 주의
    public bool ConditionVoltageFault { get; set; } = true;     // 전압 장애
    public bool ConditionFuseFault { get; set; } = true;        // 퓨즈 고장
    public bool ConditionCommFault { get; set; } = true;        // 통신 고장

    private bool _excludeHourRange = true;
    public bool ExcludeHourRange { get => _excludeHourRange; set => SetProperty(ref _excludeHourRange, value); }

    private int _excludeFromHour = 0;
    public int ExcludeFromHour { get => _excludeFromHour; set => SetProperty(ref _excludeFromHour, value); }

    private int _excludeToHour = 4;
    public int ExcludeToHour { get => _excludeToHour; set => SetProperty(ref _excludeToHour, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private bool _isOpen;
    public bool IsOpen { get => _isOpen; set => SetProperty(ref _isOpen, value); }

    public RelayCommand SearchCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand PrintCommand { get; }
    public RelayCommand CloseCommand { get; }

    public HourlyStatusViewModel()
    {
        _selectedGroup = CategoryGroups[^1]; // 시스템 이벤트 (matches the reference screenshot's default selection)

        SearchCommand = new RelayCommand(Search);
        ExportCsvCommand = new RelayCommand(() => StatusMessage = "CSV로 저장했습니다.");
        PrintCommand = new RelayCommand(() => StatusMessage = "인쇄를 준비 중입니다.");
        CloseCommand = new RelayCommand(() => IsOpen = false);

        Search();
    }

    /// <summary>Opens the modal and re-runs the search so it reflects any filter changes since it was last closed.</summary>
    public void Open()
    {
        IsOpen = true;
        Search();
    }

    private void Search()
    {
        int total = 0;
        foreach (var group in CategoryGroups)
        {
            var rows = _generator.GenerateHourlyStatusEvents(group.Category, SelectedStation, Range.Start, Range.End);

            if (ExcludeHourRange)
            {
                rows = rows.Where(r =>
                {
                    var hour = DateTime.ParseExact(r.OccurredAt, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture).Hour;
                    return hour < ExcludeFromHour || hour >= ExcludeToHour;
                }).ToList();
            }

            group.Rows.Clear();
            foreach (var row in rows) group.Rows.Add(row);
            total += rows.Count;
        }
        StatusMessage = $"{total}건 조회됨";
    }
}
