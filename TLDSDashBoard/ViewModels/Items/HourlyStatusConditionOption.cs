using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.ViewModels.Items;

/// <summary>One condition checkbox in the 시간별 상태 출력 modal's filter row. Each category
/// (<see cref="HourlyStatusCategoryGroup"/>) owns its own list of these — the checkbox set is meant to
/// differ per category (e.g. 궤도 장애 vs 시스템 이벤트 won't share the same conditions), not be one
/// fixed row shared by all seven, even though the actual per-category items are still TBD.</summary>
public sealed class HourlyStatusConditionOption : ViewModelBase
{
    public required string Label { get; init; }

    private bool _isChecked = true;
    public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value); }
}
