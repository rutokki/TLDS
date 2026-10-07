using System.Windows.Controls;

namespace TLDSDashBoard.Views;

/// <summary>See LevelRecordListView for why the merged header lives in its own horizontal-only
/// ScrollViewer instead of wrapping the DataGrid — this keeps the DataGrid's own scrolling (and
/// therefore its UI virtualization) intact, syncing only the header's horizontal offset here.</summary>
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
