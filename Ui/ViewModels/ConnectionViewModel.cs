using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels;

public partial class ConnectionViewModel : ViewModelBase
{
    public static ConnectionMode KLineMode => ConnectionMode.KLine;
    public static ConnectionMode CanMode => ConnectionMode.Can;

    private readonly IConnectionService _connectionService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string? _selectedPort;

    [ObservableProperty]
    private int _baudRate = 10400;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private ConnectionMode _mode = ConnectionMode.KLine;

    public bool IsKLineMode
    {
        get => Mode == ConnectionMode.KLine;
        set
        {
            if (value) Mode = ConnectionMode.KLine;
        }
    }

    public bool IsCanMode
    {
        get => Mode == ConnectionMode.Can;
        set
        {
            if (value) Mode = ConnectionMode.Can;
        }
    }

    partial void OnModeChanged(ConnectionMode value)
    {
        OnPropertyChanged(nameof(IsKLineMode));
        OnPropertyChanged(nameof(IsCanMode));
    }

    [ObservableProperty]
    private byte _controllerAddress = 0x17;

    [ObservableProperty]
    private string _statusText = "Disconnected";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool _isBusy;

    public ObservableCollection<string> AvailablePorts { get; } = [];

    public ConnectionViewModel(IConnectionService connectionService)
    {
        _connectionService = connectionService;
        _connectionService.StateChanged += OnConnectionStateChanged;
        RefreshPorts();
    }

    [RelayCommand]
    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (var port in SerialPort.GetPortNames())
        {
            AvailablePorts.Add(port);
        }
        if (AvailablePorts.Count > 0 && SelectedPort == null)
        {
            SelectedPort = AvailablePorts[0];
        }
    }

    private bool CanConnect() => !IsBusy && !IsConnected && !string.IsNullOrEmpty(SelectedPort);

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsBusy = true;
        _cts = new CancellationTokenSource();
        try
        {
            if (Mode == ConnectionMode.KLine)
            {
                await _connectionService.ConnectKLineAsync(
                    SelectedPort!, BaudRate, ControllerAddress, _cts.Token);
            }
            else
            {
                await _connectionService.ConnectCanAsync(
                    SelectedPort!, _cts.Token);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private bool CanDisconnect() => !IsBusy && IsConnected;

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            await _connectionService.DisconnectAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = _connectionService.State == ConnectionState.Connected;
            StatusText = _connectionService.State switch
            {
                ConnectionState.Disconnected => "Disconnected",
                ConnectionState.Connecting => "Connecting...",
                ConnectionState.Connected => _connectionService.StatusText ?? "Connected",
                ConnectionState.Disconnecting => "Disconnecting...",
                _ => "Unknown"
            };
        });
    }
}
