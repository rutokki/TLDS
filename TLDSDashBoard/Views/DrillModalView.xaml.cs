using System.Windows.Controls;
using System.Windows.Input;
using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard.Views;

/// <summary>Full-screen drill-in modal. Clicking the dim overlay closes it; clicking the card itself does not (event is marked handled before it bubbles to the overlay), matching the original prototype's stopPropagation click-outside-to-close behavior.</summary>
public partial class DrillModalView : UserControl
{
    public DrillModalView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.CloseDrillCommand.Execute(null);
    }

    private void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void CloseButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.CloseDrillCommand.Execute(null);
        e.Handled = true;
    }
}
