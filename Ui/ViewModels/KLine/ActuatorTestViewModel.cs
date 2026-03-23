using System;
using System.Threading.Tasks;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.KLine;

public partial class ActuatorTestViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _currentTest = string.Empty;

    public ActuatorTestViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private bool CanStart() => !IsBusy && _connectionService.State == ConnectionState.Connected;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        IsBusy = true;
        StatusText = "Running actuator test...";
        CurrentTest = string.Empty;
        try
        {
            await Task.Run(() =>
            {
                _connectionService.Tester!.ActuatorTest();
            });
            StatusText = "Actuator test completed.";
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
}
