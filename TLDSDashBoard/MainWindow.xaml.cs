using System.Windows;
using TLDSDashBoard.ViewModels;

namespace TLDSDashBoard;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
