using System.Windows.Controls;

namespace TLDSDashBoard.Views;

/// <summary>
/// 24개 측정 컬럼 병합 헤더는 자신만의 가로 전용 ScrollViewer(HeaderScroll) 안에 있어서, 아래 DataGrid가
/// 바깥 ScrollViewer 안에서 전체 높이로 늘어나는 대신 자신의 세로+가로 스크롤 — 그리고 UI 가상화 —
/// 을 그대로 유지할 수 있다(행이 몇 개에서 수천 개로 늘었을 때 그 구조가 가상화를 죽인 이유는 Grid
/// 위의 XAML 주석 참고). 이 핸들러가 둘을 맞추는 유일한 장치다: DataGrid가 가로로 스크롤될 때마다
/// 헤더도 같이 스크롤한다.
/// </summary>
public partial class LevelRecordListView : UserControl
{
    public LevelRecordListView()
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
