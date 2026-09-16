namespace TLDSDashBoard.ViewModels;

/// <summary>A 기간(date range) filter with hour/minute precision on both ends — used by every 조회 filter bar that needs a "시작 ~ 종료" range (열차점유/궤도장애/시간별상태출력/그래프).</summary>
public sealed class DateRangeFilter : ViewModelBase
{
    public IReadOnlyList<int> Hours { get; } = Enumerable.Range(0, 24).ToList();
    public IReadOnlyList<int> Minutes { get; } = Enumerable.Range(0, 60).ToList();

    private DateTime _startDate = DateTime.Today;
    public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }

    private int _startHour;
    public int StartHour { get => _startHour; set => SetProperty(ref _startHour, value); }

    private int _startMinute;
    public int StartMinute { get => _startMinute; set => SetProperty(ref _startMinute, value); }

    private DateTime _endDate = DateTime.Today;
    public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }

    private int _endHour = 23;
    public int EndHour { get => _endHour; set => SetProperty(ref _endHour, value); }

    private int _endMinute = 59;
    public int EndMinute { get => _endMinute; set => SetProperty(ref _endMinute, value); }

    public DateTime Start => StartDate.Date.AddHours(StartHour).AddMinutes(StartMinute);
    public DateTime End => EndDate.Date.AddHours(EndHour).AddMinutes(EndMinute).AddSeconds(59);
}
