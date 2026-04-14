using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Styling;
using BitFab.KW1281Test.Ui.Models;
using BitFab.KW1281Test.Ui.Services;
using BitFab.KW1281Test.Ui.ViewModels.Can;
using BitFab.KW1281Test.Ui.ViewModels.KLine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;
    private readonly UiLogAdapter _logAdapter;
    private readonly AppSettings _settings;
    private readonly Dictionary<string, ViewModelBase> _viewCache = new();

    public ConnectionViewModel Connection { get; }
    public LogViewModel Log { get; }

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    [ObservableProperty]
    private string _connectionStatusText = "Disconnected";

    [ObservableProperty]
    private bool _isDarkTheme;

    public IDialogService? DialogService { get; set; }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public MainWindowViewModel()
    {
        _settings = SettingsService.Load();
        _connectionService = new ConnectionService();
        _logAdapter = new UiLogAdapter();

        // Set the global logger to the UI adapter
        Logger.Log = _logAdapter;

        Connection = new ConnectionViewModel(_connectionService, _settings);
        Log = new LogViewModel(_logAdapter.Entries);

        NavigationItems = BuildNavigationTree();

        _isDarkTheme = _settings.ThemeVariant == "Dark";
        _connectionService.StateChanged += OnConnectionStateChanged;
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = value ? ThemeVariant.Dark : ThemeVariant.Light;
        }
        _settings.ThemeVariant = value ? "Dark" : "Light";
        SettingsService.Save(_settings);
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (value?.Tag == null)
            return;

        if (!_viewCache.TryGetValue(value.Tag, out var vm))
        {
            vm = CreateViewModel(value.Tag);
            if (vm != null)
                _viewCache[value.Tag] = vm;
        }

        CurrentView = vm;
    }

    private ViewModelBase? CreateViewModel(string tag) => tag switch
    {
        "KLine.FaultCodes"  => new FaultCodesViewModel(_connectionService) { DialogService = DialogService },
        "KLine.GroupRead"   => new GroupReadViewModel(_connectionService),
        "KLine.Adaptation"  => new AdaptationViewModel(_connectionService) { DialogService = DialogService },
        "KLine.ActuatorTest"=> new ActuatorTestViewModel(_connectionService),
        "KLine.Coding"      => new CodingViewModel(_connectionService) { DialogService = DialogService },
        "KLine.Eeprom"      => new EepromViewModel(_connectionService) { DialogService = DialogService },
        "KLine.Memory"      => new MemoryViewModel(_connectionService),
        "KLine.Cluster"     => new ClusterViewModel(_connectionService) { DialogService = DialogService },
        "Can.Monitor"       => new CanMonitorViewModel(_connectionService),
        "Can.AutoScan"      => new CanAutoScanViewModel(_connectionService),
        "Can.Diag"          => new CanDiagViewModel(_connectionService),
        "Can.MultiEcu"      => new CanMultiEcuViewModel(_connectionService),
        "Can.Skc"           => new CanSkcViewModel(_connectionService),
        "Can.Obd2"          => new Obd2ViewModel(_connectionService),
        _ => null,
    };

    public void SaveSettings(double width, double height)
    {
        _settings.WindowWidth = width;
        _settings.WindowHeight = height;
        Connection.SaveSettings(_settings);
        SettingsService.Save(_settings);
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ConnectionStatusText = _connectionService.State switch
            {
                ConnectionState.Disconnected => "Disconnected",
                ConnectionState.Connecting => "Connecting...",
                ConnectionState.Connected => _connectionService.StatusText ?? "Connected",
                ConnectionState.Disconnecting => "Disconnecting...",
                _ => "Unknown"
            };
        });
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
                    new NavigationItem { Title = "SKC / PIN", Tag = "Can.Skc" },
                    new NavigationItem { Title = "OBD2 Live Data", Tag = "Can.Obd2" },
                ]
            }
        ];
    }
}
