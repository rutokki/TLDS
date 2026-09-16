using System.Collections.ObjectModel;
using TLDSDashBoard.Models;

namespace TLDSDashBoard.ViewModels.Items;

/// <summary>One category's result section in the 시간별 상태 출력 modal — every category is searched and shown at once, instead of picking one category at a time.</summary>
public sealed class HourlyStatusCategoryGroup
{
    public required HourlyStatusCategory Category { get; init; }
    public required string Label { get; init; }
    public ObservableCollection<HourlyStatusEventRecord> Rows { get; } = new();
}
