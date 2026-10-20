using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.ViewModels.Items;

/// <summary>시간별 상태 출력 모달 필터 줄의 조건 체크박스 하나. 카테고리(<see cref="HourlyStatusCategoryGroup"/>)마다
/// 이 목록을 따로 가진다 — 체크박스 구성은 카테고리마다 다르도록(예: 궤도 장애와 시스템 이벤트는 같은 조건을
/// 공유하지 않음) 설계되었으며, 일곱 개가 공유하는 고정된 한 줄이 아니다. 실제 항목은
/// HourlyStatusViewModel.ConditionDefs에 있다.</summary>
public sealed class HourlyStatusConditionOption : ViewModelBase
{
    public required string Label { get; init; }

    /// <summary>이 행이 이 조건에 속하는지 판단하려고 이벤트 CONTENTS 텍스트와 대조하는 부분 문자열
    /// (HourlyStatusViewModel.ApplyConditions 참고).</summary>
    public required IReadOnlyList<string> Keywords { get; init; }

    /// <summary>이 체크박스 옆의 ComboBox에 제공하는 선택적 장치 이름(궤도/신호기/선로전환기 명칭). 첫 항목은
    /// <see cref="AllDevices"/>. 장치 선택기가 없는 조건에서는 null.</summary>
    public IReadOnlyList<string>? DeviceChoices { get; init; }

    public bool HasDeviceChoices => DeviceChoices is { Count: > 0 };

    public const string AllDevices = "전체";

    private string _selectedDevice = AllDevices;
    public string SelectedDevice { get => _selectedDevice; set => SetProperty(ref _selectedDevice, value); }

    /// <summary>The specific device picked in the ComboBox, or null when none/"전체" is selected.</summary>
    public string? PickedDevice => HasDeviceChoices && SelectedDevice != AllDevices ? SelectedDevice : null;

    private bool _isChecked = true;
    public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value); }
}
