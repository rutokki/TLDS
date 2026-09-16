namespace TLDSDashBoard.ViewModels.Items;

/// <summary>One day cell in <see cref="TLDSDashBoard.Views.SimpleDatePicker"/>'s month grid.</summary>
public sealed class CalendarDayCell
{
    public required DateTime Date { get; init; }
    public required string Label { get; init; }
    public required bool IsCurrentMonth { get; init; }
    public required bool IsSelected { get; init; }
    public required bool IsToday { get; init; }
}
