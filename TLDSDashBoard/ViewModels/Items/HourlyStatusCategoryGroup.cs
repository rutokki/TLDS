using System.Collections.ObjectModel;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.ViewModels.Items;

/// <summary>One category's result section in the 시간별 상태 출력 modal — every category is searched and shown at once, instead of picking one category at a time.</summary>
public sealed class HourlyStatusCategoryGroup
{
    public required HourlyStatusCategory Category { get; init; }
    public required string Label { get; init; }
    public ObservableCollection<HourlyStatusEventRecord> Rows { get; } = new();

    /// <summary>이 카테고리 고유의 조건 체크박스 — 이 그룹이 <see cref="HourlyStatusViewModel.SelectedGroup"/>일 때
    /// 다른 카테고리 것 대신 표시된다. 카테고리별 항목과 키워드는 HourlyStatusViewModel.ConditionDefs에
    /// 정의되어 있다.</summary>
    public required ObservableCollection<HourlyStatusConditionOption> ConditionOptions { get; init; }
}
