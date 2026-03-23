using System;
using System.Threading.Tasks;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.KLine;

public partial class AdaptationViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;

    public IDialogService? DialogService { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReadCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private byte _channel = 0;

    [ObservableProperty]
    private ushort _channelValue = 0;

    [ObservableProperty]
    private ushort _login = 0;

    [ObservableProperty]
    private bool _useLogin;

    [ObservableProperty]
    private int _workshopCode = 0;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public AdaptationViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private bool CanExecute() => !IsBusy && _connectionService.State == ConnectionState.Connected;

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ReadAsync()
    {
        IsBusy = true;
        StatusText = $"Reading channel {Channel}...";
        try
        {
            var ch = Channel;
            var login = UseLogin ? (ushort?)Login : null;
            var wsc = WorkshopCode;
            await Task.Run(() =>
            {
                _connectionService.Tester!.AdaptationRead(ch, login, wsc);
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
    private async Task TestAsync()
    {
        IsBusy = true;
        StatusText = $"Testing channel {Channel} with value {ChannelValue}...";
        try
        {
            var ch = Channel;
            var val = ChannelValue;
            var login = UseLogin ? (ushort?)Login : null;
            var wsc = WorkshopCode;
            await Task.Run(() =>
            {
                _connectionService.Tester!.AdaptationTest(ch, val, login, wsc);
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
    private async Task SaveAsync()
    {
        if (DialogService != null &&
            !await DialogService.ConfirmAsync("Save Adaptation", $"Are you sure you want to save value {ChannelValue} to channel {Channel}?"))
            return;

        IsBusy = true;
        StatusText = $"Saving channel {Channel} value {ChannelValue}...";
        try
        {
            var ch = Channel;
            var val = ChannelValue;
            var login = UseLogin ? (ushort?)Login : null;
            var wsc = WorkshopCode;
            await Task.Run(() =>
            {
                _connectionService.Tester!.AdaptationSave(ch, val, login, wsc);
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
