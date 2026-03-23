using System;
using System.Threading.Tasks;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.KLine;

public partial class CodingViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetCodingCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReadIdentCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private int _softwareCoding;

    [ObservableProperty]
    private int _workshopCode;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public CodingViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private bool CanExecute() => !IsBusy && _connectionService.State == ConnectionState.Connected;

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ReadIdentAsync()
    {
        IsBusy = true;
        StatusText = "Reading identification...";
        try
        {
            await Task.Run(() =>
            {
                _connectionService.Tester!.ReadIdent();
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
    private async Task SetCodingAsync()
    {
        IsBusy = true;
        StatusText = $"Setting coding {SoftwareCoding}, WSC {WorkshopCode}...";
        try
        {
            var coding = SoftwareCoding;
            var wsc = WorkshopCode;
            await Task.Run(() =>
            {
                _connectionService.Tester!.SetSoftwareCoding(coding, wsc);
            });
            StatusText = "Coding set successfully.";
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
