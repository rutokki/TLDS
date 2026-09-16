using System.Windows.Controls;
using System.Windows.Input;
using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.Views;

/// <summary>Full-screen 시간별 상태 출력 modal. Clicking the dim overlay closes it; clicking the card itself does not (matches DrillModalView's click-outside-to-close behavior).</summary>
public partial class HourlyStatusModalView : UserControl
{
    public HourlyStatusModalView()
    {
        InitializeComponent();
    }

    private HourlyStatusViewModel? ViewModel => (DataContext as MainViewModel)?.HourlyStatus;

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.CloseCommand.Execute(null);
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void CloseButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.CloseCommand.Execute(null);
        e.Handled = true;
    }
}
