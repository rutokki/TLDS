namespace TLDSDashBoard.ViewModels.Items;

/// <summary><see cref="TLDSDashBoard.Views.SimpleDatePicker"/>의 월 달력 격자에서 하루 칸 하나.</summary>
public sealed class CalendarDayCell
{
    public required DateTime Date { get; init; }
    public required string Label { get; init; }
    public required bool IsCurrentMonth { get; init; }
    public required bool IsSelected { get; init; }
    public required bool IsToday { get; init; }
}
