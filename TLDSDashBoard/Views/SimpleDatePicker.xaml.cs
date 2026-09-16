using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TLDSDashBoard.ViewModels.Items;

namespace TLDSDashBoard.Views;

/// <summary>
/// A self-contained date picker: hand-drawn month grid in a Popup, styled entirely with our own dark-theme
/// brushes. Exists because WPF's built-in Calendar control (used internally by DatePicker) resisted every
/// styling approach we tried — implicit Style, CalendarDayButtonStyle/CalendarButtonStyle, CalendarItem
/// Style — and kept rendering with its default light theme regardless. Building the grid ourselves removes
/// all ambiguity about which control actually owns which pixel.
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
