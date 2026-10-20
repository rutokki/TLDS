using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.Views;

/// <summary>
/// 직접 그린 월 달력 격자를 Popup 안에 둔, 독립적으로 동작하는 날짜 선택기로, 우리 어두운 테마 브러시로만 꾸민다.
/// WPF 기본 Calendar 컨트롤(DatePicker가 내부적으로 씀)이 우리가 시도한 모든 스타일링 방법 — 암시적
/// Style, CalendarDayButtonStyle/CalendarButtonStyle, CalendarItem Style — 에 저항하며 무엇을 해도 기본
/// 밝은 테마로 그려졌기 때문에 만들었다. 격자를 직접 만들면 어느 컨트롤이 어느 픽셀을 소유하는지에 대한
/// 모호함이 모두 사라진다.
/// </summary>
public partial class SimpleDatePicker : UserControl
{
    public static readonly DependencyProperty SelectedDateProperty =
        DependencyProperty.Register(nameof(SelectedDate), typeof(DateTime), typeof(SimpleDatePicker),
            new FrameworkPropertyMetadata(DateTime.Today, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateChanged));

    public DateTime SelectedDate
    {
        get => (DateTime)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public ObservableCollection<CalendarDayCell> Days { get; } = new();

    private DateTime _displayedMonth;

    public SimpleDatePicker()
    {
        InitializeComponent();
        _displayedMonth = new DateTime(SelectedDate.Year, SelectedDate.Month, 1);
        Loaded += (_, _) => Refresh();
    }

    private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (SimpleDatePicker)d;
        var date = (DateTime)e.NewValue;
        picker._displayedMonth = new DateTime(date.Year, date.Month, 1);
        picker.Refresh();
    }

    private void Refresh()
    {
        HeaderTextBlock.Text = _displayedMonth.ToString("yyyy년 MM월", CultureInfo.InvariantCulture);

        Days.Clear();
        int leadingBlankDays = (int)_displayedMonth.DayOfWeek;
        var gridStart = _displayedMonth.AddDays(-leadingBlankDays);
        var today = DateTime.Today;

        for (int i = 0; i < 42; i++)
        {
            var date = gridStart.AddDays(i);
            Days.Add(new CalendarDayCell
            {
                Date = date,
                Label = date.Day.ToString(CultureInfo.InvariantCulture),
                IsCurrentMonth = date.Month == _displayedMonth.Month,
                IsSelected = date.Date == SelectedDate.Date,
                IsToday = date.Date == today,
            });
        }
    }

    private void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        _displayedMonth = _displayedMonth.AddMonths(-1);
        Refresh();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _displayedMonth = _displayedMonth.AddMonths(1);
        Refresh();
    }

    private void Day_Click(object sender, RoutedEventArgs e)
    {
        SelectedDate = (DateTime)((Button)sender).Tag;
        ToggleBtn.IsChecked = false;
    }
}
