using System.Windows.Controls;
using System.Windows.Input;

namespace TLDSDashBoard.Views;

/// <summary>병합 헤더와 DataGrid의 가로 스크롤 동기화 방식은 TrackFaultListView/LevelRecordListView와 같다.
/// 추가로 % 입력칸에서 Enter를 누르면 포커스를 옮기지 않고도 값을 바로 확정한다.</summary>
public partial class TrackVoltageListView : UserControl
{
    public TrackVoltageListView()
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

    private void PctBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox box) return;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        e.Handled = true;
    }
}
