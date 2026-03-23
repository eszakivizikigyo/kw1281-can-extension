using System.Collections.ObjectModel;
using BitFab.KW1281Test.Ui.Models;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BitFab.KW1281Test.Ui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;
    private readonly UiLogAdapter _logAdapter;

    public ConnectionViewModel Connection { get; }
    public LogViewModel Log { get; }

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public MainWindowViewModel()
    {
        _connectionService = new ConnectionService();
        _logAdapter = new UiLogAdapter();

        // Set the global logger to the UI adapter
        Logger.Log = _logAdapter;

        Connection = new ConnectionViewModel(_connectionService);
        Log = new LogViewModel(_logAdapter.Entries);

        NavigationItems = BuildNavigationTree();
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (value?.Tag == null)
            return;

        // Navigation will be handled when we add more views
        // For now this is a placeholder
    }

    private static ObservableCollection<NavigationItem> BuildNavigationTree()
    {
        return
        [
            new NavigationItem
            {
                Title = "K-Line",
                Children =
                [
                    new NavigationItem { Title = "Fault Codes", Tag = "KLine.FaultCodes" },
                    new NavigationItem { Title = "Measuring Groups", Tag = "KLine.GroupRead" },
                    new NavigationItem { Title = "Adaptation", Tag = "KLine.Adaptation" },
                    new NavigationItem { Title = "Actuator Tests", Tag = "KLine.ActuatorTest" },
                    new NavigationItem { Title = "Coding", Tag = "KLine.Coding" },
                    new NavigationItem { Title = "EEPROM", Tag = "KLine.Eeprom" },
                    new NavigationItem { Title = "Memory", Tag = "KLine.Memory" },
                    new NavigationItem { Title = "Cluster", Tag = "KLine.Cluster" },
                ]
            },
            new NavigationItem
            {
                Title = "CAN Bus",
                Children =
                [
                    new NavigationItem { Title = "Monitor", Tag = "Can.Monitor" },
                    new NavigationItem { Title = "Auto Scan", Tag = "Can.AutoScan" },
                    new NavigationItem { Title = "Diagnostics", Tag = "Can.Diag" },
                    new NavigationItem { Title = "Multi-ECU", Tag = "Can.MultiEcu" },
                ]
            }
        ];
    }
}
