using System;
using System.Threading;
using System.Threading.Tasks;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.KLine;

public partial class GroupReadViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReadCommand))]
    [NotifyCanExecuteChangedFor(nameof(BasicSettingCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private byte _groupNumber = 1;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _result = string.Empty;

    public GroupReadViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private bool CanExecute() => !IsBusy && _connectionService.State == ConnectionState.Connected;

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ReadAsync()
    {
        IsBusy = true;
        StatusText = $"Reading group {GroupNumber}...";
        Result = string.Empty;
        try
        {
            var group = GroupNumber;
            await Task.Run(() =>
            {
                _connectionService.Tester!.GroupRead(group);
            });
            StatusText = "Done.";
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

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task BasicSettingAsync()
    {
        IsBusy = true;
        StatusText = $"Basic Setting group {GroupNumber}...";
        Result = string.Empty;
        try
        {
            var group = GroupNumber;
            await Task.Run(() =>
            {
                _connectionService.Tester!.BasicSettingRead(group);
            });
            StatusText = "Done.";
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
