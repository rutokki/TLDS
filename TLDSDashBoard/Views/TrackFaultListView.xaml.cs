using System.Windows.Controls;

namespace TLDSDashBoard.Views;

/// <summary>병합 헤더가 DataGrid를 감싸는 대신 자신만의 가로 전용 ScrollViewer 안에 있는 이유는
/// LevelRecordListView를 참고 — 이렇게 하면 DataGrid 자체의 스크롤(그리고 UI 가상화)이 유지되고, 여기서는
/// 헤더의 가로 오프셋만 동기화한다.</summary>
public partial class TrackFaultListView : UserControl
{
    public TrackFaultListView()
    {
        InitializeComponent();
    }

    private void MainGrid_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.HorizontalChange != 0)
        {
            HeaderScroll.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
    }
}
