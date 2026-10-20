using System.Collections.ObjectModel;
using System.Globalization;
using TLDSDashBoard.Models;
using TLDSDashBoard.Services;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.ViewModels;

/// <summary>
/// 시간별 상태 출력 모달의 필터 상태 + 결과. 한 번의 조회(기간/역/장치 + 조건)가 이제 7개 카테고리 그리드를
/// 한꺼번에 채운다 — 결과를 보려고 카테고리를 먼저 고를 필요가 없다.
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

    /// <summary>카테고리별 조건 체크박스: (표시 라벨, 이벤트 CONTENTS 텍스트와 대조할 키워드).
    /// 시스템 이벤트는 실제 이벤트 로그에서 확인된 내용(시작/종료/접속됨/접속끊김)을 그대로 따르고, 나머지
    /// 여섯 개는 실제 Event 테이블에 아직 행이 없어서 도메인 지식으로 추정한 값이다 — 실제 문구를 알게 되면
    /// 여기의 라벨/키워드만 고치면 되고 다른 것은 바꿀 필요가 없다.</summary>
    private static readonly Dictionary<HourlyStatusCategory, (string Label, string[] Keywords)[]> ConditionDefs = new()
    {
        [HourlyStatusCategory.TrackFault] = new[]
        {
            ("부정 점유", new[] { "부정 점유" }),
            ("전압 주의", new[] { "전압 주의" }),
            ("전압 장애", new[] { "전압 장애" }),
            ("퓨즈 고장", new[] { "퓨즈 고장" }),
            ("통신 고장", new[] { "통신 고장" }),
        },
        [HourlyStatusCategory.SignalFailure] = new[]
        {
            ("고장", new[] { "고장" }),
            ("FLR", new[] { "FLR" }),

        },
        [HourlyStatusCategory.SwitchMachineFailure] = new[]
        {

            ("불일치", new[] { "불일치" }),

        },
        [HourlyStatusCategory.BlockDeviceFailure] = new[]
        {
            ("폐색 고장", new[] { "폐색 고장" }),
            ("송신 모듈 이상", new[] { "송신 모듈 이상" }),
            ("수신 모듈 고장", new[] { "수신 모듈 고장" }),
            ("주계/부계 고장", new[] { "주계/부계 고장" }),

        },
        [HourlyStatusCategory.LeuFailure] = new[]
        {
            ("LEU 고장", new[] { "LEU 고장" }),

        },
        [HourlyStatusCategory.SignalEquipmentEvent] = new[]
        {
            ("궤도", new[] { "궤도" }),
            ("신호기", new[] { "신호기" }),
            ("선로전환기", new[] { "선로전환기" }),

        },
        [HourlyStatusCategory.SystemEvent] = new[]
        {
            ("시작", new[] { "시작" }),
            ("종료", new[] { "종료" }),
            ("통신", new[] { "통신" }),
            ("1계/2계", new[] { "1계", "2계" }),
        },
    };

    private static ObservableCollection<HourlyStatusConditionOption> BuildConditionOptions(HourlyStatusCategory category)
    {
        return new(ConditionDefs[category].Select(d => new HourlyStatusConditionOption
        {
            Label = d.Label,
            Keywords = d.Keywords,
            DeviceChoices = category == HourlyStatusCategory.SignalEquipmentEvent ? DeviceChoicesFor(d.Label) : null,
        }));
    }

    /// <summary>신호설비 이벤트's 궤도/신호기/선로전환기 checkboxes each get a ComboBox of that kind of
    /// device's names (from index_{역}.xml via ReferenceData), "전체" first. Matched by checkbox label.</summary>
    private static IReadOnlyList<string>? DeviceChoicesFor(string label)
    {
        IEnumerable<string>? names = label switch
        {
            "궤도" => ReferenceData.Tracks.Select(t => t.Name).Distinct(),
            "신호기" => ReferenceData.Signals,
            "선로전환기" => ReferenceData.Switches,
            _ => null,
        };
        return names is null ? null : new[] { HourlyStatusConditionOption.AllDevices }.Concat(names).ToList();
    }

    /// <summary>이벤트의 DEVICE_NAME이 선택한 장치와 같으면 true: 완전 일치이거나, 이름이 마지막 공백 구분
    /// 단어인 경우(예: "신호기 3A") — 단순 Contains는 "3T"가 "23T"와 일치해 버린다.</summary>
    private static bool IsDevice(string deviceName, string picked)
    {
        return deviceName.Equals(picked, StringComparison.OrdinalIgnoreCase)
        || deviceName.EndsWith(" " + picked, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>모든 체크박스가 체크되어 있고 선택한 장치가 없으면(기본) 필터링하지 않으므로, 나열된 조건과 문구가
    /// 하나도 맞지 않는 이벤트도 그대로 보인다. 그 외에는 체크된 조건 중 하나 이상과 맞는 행으로 좁힌다:
    /// 그 체크박스에 장치를 골랐으면 장치명으로(이때는 장치가 판별 기준이고 이벤트 문구는 필요 없음),
    /// 아니면 이벤트 내용의 키워드로 맞춘다.</summary>
    private static List<HourlyStatusEventRecord> ApplyConditions(
        IReadOnlyList<HourlyStatusEventRecord> rows, IReadOnlyCollection<HourlyStatusConditionOption> options)
    {
        if (options.All(o => o.IsChecked && o.PickedDevice is null)) return rows.ToList();
        var active = options.Where(o => o.IsChecked).ToList();
        return rows.Where(r => active.Any(o => o.PickedDevice is { } device
            ? IsDevice(r.DeviceName, device)
            : o.Keywords.Any(k => r.Content.Contains(k, StringComparison.Ordinal)))).ToList();
    }

    private HourlyStatusCategoryGroup _selectedGroup;
    /// <summary>오른쪽에 표시할 카테고리 그리드. 순전히 표시 전환용이다 — 모든 카테고리의 데이터는 이미
    /// <see cref="Search"/>가 읽어 두었으므로, 이것을 바꿔도 다시 조회하지 않는다.</summary>
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

        // 생성 시 자동 조회하지 않는다(기본으로 데이터를 그리는 페이지는 OverView뿐) — 조회를 누를 때까지
        // 모달은 비어 있다. 다만 Open()은 다시 열 때마다 Search()를 다시 실행해서, 마지막으로 닫혀 있던 동안
        // 바뀐 필터 변경이 반영되게 한다(기존 동작 그대로).
    }

    /// <summary>현재 표시 중인 카테고리(<see cref="SelectedGroup"/>)를 내보낸다 — Search()가 모든 카테고리를
    /// 한꺼번에 읽지만 그리드에는 한 번에 하나만 보이므로, "CSV 저장"은 일곱 카테고리 전체가 아니라
    /// 그것을 저장한다.</summary>
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

    /// <summary>모달을 열고 조회를 다시 실행해서, 마지막으로 닫은 뒤 바뀐 필터가 반영되게 한다.</summary>
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
            // 실제 이벤트 로그만 쓴다 — 비어 있는 카테고리/기간에 대한 샘플 데이터 폴백은 없다.
            var rows = ApplyConditions(realByCategory[group.Category], group.ConditionOptions);

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

    /// <summary>[start,end]의 모든 날에 대해 _file_db/Event-{yyyy-MM-dd}.db의 Event 테이블을 읽고, 각 행을
    /// 7개 카테고리 중 하나로 분류한다. 실제 DEVICE_TYPE→카테고리 코드표는 아직 어디에도 문서화된 것을
    /// 보지 못했고, DEVICE_TYPE=4만 시스템 수준 접속/접속끊김 이벤트(TLDS/임펄스랙*/연동장치)에서 실제로
    /// 관찰되었다. 실제 매핑을 알기 전까지는 대신 DEVICE_NAME 내용으로 추측하는데, 확인되지 않은 숫자 코드보다
    /// 적어도 이미 읽을 수 있는 텍스트를 기준으로 하기 때문이다(CategorizeByDeviceName 참고) — 실제
    /// DEVICE_TYPE 표가 확인되면 이것을 교체한다.</summary>
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

    private static HourlyStatusCategory CategorizeByDeviceName(string deviceName)
    {
        return deviceName switch
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
}
