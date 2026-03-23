using Avalonia.Controls;
using BitFab.KW1281Test.Ui.Services;
using BitFab.KW1281Test.Ui.ViewModels;

namespace BitFab.KW1281Test.Ui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainWindowViewModel();
        vm.DialogService = new DialogService(this);
        vm.Log.StorageProviderFactory = () => StorageProvider;
        DataContext = vm;

        var settings = SettingsService.Load();
        if (settings.WindowWidth > 0)
            Width = settings.WindowWidth;
        if (settings.WindowHeight > 0)
            Height = settings.WindowHeight;

        Closing += (_, _) =>
        {
            vm.SaveSettings(Width, Height);
        };
    }
}
