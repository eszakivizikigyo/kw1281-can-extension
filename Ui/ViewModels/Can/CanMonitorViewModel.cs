using System;
using System.Threading;
using System.Threading.Tasks;
using BitFab.KW1281Test.Interface;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.Can;

public partial class CanMonitorViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isMonitoring;

    [ObservableProperty]
    private int _messageCount;

    [ObservableProperty]
    private string _statusText = "Ready.";

    public CanMonitorViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private bool CanStart() => !IsMonitoring && _connectionService.State == ConnectionState.Connected
                                && _connectionService.Mode == ConnectionMode.Can;
    private bool CanStop() => IsMonitoring;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        IsMonitoring = true;
        MessageCount = 0;
        StatusText = "Monitoring CAN bus...";
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            var canInterface = _connectionService.CanInterface!;
            await Task.Run(() =>
            {
                canInterface.SetCanSpeed(500);
                canInterface.SetMonitorMode(true);

                Logger.Log.WriteLine("Monitoring CAN bus traffic (500 kbps)...");
                while (!ct.IsCancellationRequested)
                {
                    var msg = canInterface.ReceiveCanMessage(500);
                    if (msg != null)
                    {
                        MessageCount++;
                        Logger.Log.WriteLine($"[{MessageCount,6}] {msg}");
                    }
                }
            }, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsMonitoring = false;
            StatusText = $"Stopped. Total messages: {MessageCount}";
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _cts?.Cancel();
    }
}
