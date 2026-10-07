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


    public ObservableCollection<HourlyStatusCategoryGroup> CategoryGroups { get; } = new(
        CategoryDefs.Select(c => new HourlyStatusCategoryGroup
        {
            Category = c.Category,
            Label = c.Label,
            ConditionOptions = BuildConditionOptions(c.Category),
        }));

    /// <summary>Placeholder condition checkboxes for one category — every category gets the same 5 for
    /// now since the real per-category conditions haven't been decided yet. Swap this out once they are;
    /// nothing else needs to change (the View already reads ConditionOptions per selected category, not
    /// a single fixed set).</summary>
    private static ObservableCollection<HourlyStatusConditionOption> BuildConditionOptions(HourlyStatusCategory category) => new(new[]
    {
        new HourlyStatusConditionOption { Label = "부정 점유" },
        new HourlyStatusConditionOption { Label = "전압 주의" },
        new HourlyStatusConditionOption { Label = "전압 장애" },
        new HourlyStatusConditionOption { Label = "퓨즈 고장" },
        new HourlyStatusConditionOption { Label = "통신 고장" },
    });

    private HourlyStatusCategoryGroup _selectedGroup;
    /// <summary>Which category's grid is shown on the right. Purely a display switch — every category's
    /// data is already loaded by <see cref="Search"/>, so changing this does not re-query anything.</summary>
    public HourlyStatusCategoryGroup SelectedGroup { get => _selectedGroup; set => SetProperty(ref _selectedGroup, value); }

    public ObservableCollection<Station> Stations { get; } = new(ReferenceData.Stations);
    public IReadOnlyList<int> HourChoices { get; } = Enumerable.Range(0, 24).ToList();

    private Station _selectedStation = ReferenceData.Stations[0];
    public Station SelectedStation { get => _selectedStation; set => SetProperty(ref _selectedStation, value); }

    public DateRangeFilter Range { get; } = new();

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
        ExportCsvCommand = new RelayCommand(ExportCsv);
        PrintCommand = new RelayCommand(() => StatusMessage = "인쇄를 준비 중입니다.");
        CloseCommand = new RelayCommand(() => IsOpen = false);

        // No auto-search on construction (OverView is the only page that renders data by default) — the
        // modal starts empty until 조회 is clicked; Open() still re-runs Search() on each reopen so it
        // reflects filter changes made while it was last closed (unchanged behavior).
    }

    /// <summary>Exports whichever category is currently shown (<see cref="SelectedGroup"/>) — every
    /// category is loaded at once by Search(), but only one is visible in the grid at a time, so that's
    /// what "CSV 저장" saves rather than all seven categories at once.</summary>
    private void ExportCsv()
    {
        var headers = new[] { "발생시각", "역 명칭", "장치이름", "내용" };
        var rows = SelectedGroup.Rows.Select(r => (IReadOnlyList<string>)new[]
        {
            r.OccurredAt, r.StationName, r.DeviceName, r.Content,
        });

        if (CsvExporter.Export($"시간별상태출력_{SelectedGroup.Label}.csv", headers, rows) is not null)
            StatusMessage = "CSV로 저장했습니다.";
    }

    /// <summary>Opens the modal and re-runs the search so it reflects any filter changes since it was last closed.</summary>
    public void Open()
    {
        IsOpen = true;
        Search();
    }

    private void Search()
    {
        Dictionary<HourlyStatusCategory, List<HourlyStatusEventRecord>> realByCategory;
        string? loadError = null;
        try
        {
            realByCategory = LoadRealEvents(Range.Start, Range.End);
        }
        catch (Exception ex)
        {
            realByCategory = Enum.GetValues<HourlyStatusCategory>().ToDictionary(c => c, _ => new List<HourlyStatusEventRecord>());
            loadError = ex.Message;
        }

        int total = 0;
        foreach (var group in CategoryGroups)
        {
            // Real Event log only — no sample-data fallback for empty categories/periods.
            var rows = realByCategory[group.Category];

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
        StatusMessage = loadError is not null ? $"이벤트 DB 조회 실패 ({loadError})"
            : total > 0 ? $"{total}건 조회됨"
            : "해당 기간의 이벤트 기록이 없습니다.";
    }

    /// <summary>Reads _file_db/Event-{yyyy-MM-dd}.db's Event table for every day in [start,end] and buckets
    /// each row into one of the 7 categories. The real DEVICE_TYPE→category code table hasn't been
    /// documented anywhere we've seen yet — only DEVICE_TYPE=4 has actually been observed, for
    /// system-level connect/disconnect events (TLDS/임펄스랙*/연동장치). Until the real mapping is known,
    /// this guesses from DEVICE_NAME content instead, which at least keys off text we can already read
    /// (see CategorizeByDeviceName) rather than an unverified numeric code — swap this out once the real
    /// DEVICE_TYPE table is confirmed.</summary>
    private static Dictionary<HourlyStatusCategory, List<HourlyStatusEventRecord>> LoadRealEvents(DateTime start, DateTime end)
    {
        var result = Enum.GetValues<HourlyStatusCategory>().ToDictionary(c => c, _ => new List<HourlyStatusEventRecord>());
        for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
        {
            foreach (var e in EventLogRepository.LoadEvents(day))
            {
                if (e.Time < start || e.Time > end) continue;
                var category = CategorizeByDeviceName(e.DeviceName);
                result[category].Add(new HourlyStatusEventRecord
                {
                    OccurredAt = e.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                    StationName = ReferenceData.Stations[0].Name,
                    DeviceName = e.DeviceName,
                    Content = e.Content,
                });
            }
        }
        return result;
    }

    private static HourlyStatusCategory CategorizeByDeviceName(string deviceName) => deviceName switch
    {
        var n when n.Contains("LEU") => HourlyStatusCategory.LeuFailure,
        var n when n.Contains("선로전환기") || n.Contains("전환기") => HourlyStatusCategory.SwitchMachineFailure,
        var n when n.Contains("신호기") => HourlyStatusCategory.SignalFailure,
        var n when n.Contains("폐색") => HourlyStatusCategory.BlockDeviceFailure,
        var n when n.Contains("궤도") => HourlyStatusCategory.TrackFault,
        var n when n.Contains("임펄스랙") || n.Contains("연동장치") || n == "TLDS" => HourlyStatusCategory.SystemEvent,
        _ => HourlyStatusCategory.SignalEquipmentEvent,
    };
}
