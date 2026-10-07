using System.Windows.Controls;

namespace TLDSDashBoard.Views;

/// <summary>
/// The 24-measurement-column merged header lives in its own horizontal-only ScrollViewer (HeaderScroll)
/// so the DataGrid below can keep its OWN vertical+horizontal scrolling — and therefore its UI
/// virtualization — instead of being stretched to full height inside an outer ScrollViewer (see the XAML
/// comment above the Grid for why that killed virtualization once rows went from a handful to thousands).
/// This handler is the only thing keeping the two in sync: whenever the DataGrid scrolls horizontally,
/// the header is scrolled to match.
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
