using Avalonia.Controls;
using BitFab.KW1281Test.Ui.ViewModels;

namespace BitFab.KW1281Test.Ui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
