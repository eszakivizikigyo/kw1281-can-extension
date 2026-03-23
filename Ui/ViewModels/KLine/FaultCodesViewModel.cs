using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using BitFab.KW1281Test.Blocks;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.KLine;

public partial class FaultCodesViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;

    public IDialogService? DialogService { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReadCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public ObservableCollection<FaultCodeItem> FaultCodes { get; } = [];

    public FaultCodesViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private bool CanExecute() => !IsBusy && _connectionService.State == ConnectionState.Connected;

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ReadAsync()
    {
        IsBusy = true;
        FaultCodes.Clear();
        StatusText = "Reading fault codes...";
        try
        {
            await Task.Run(() =>
            {
                _connectionService.Tester!.ReadFaultCodes();
            });
            StatusText = FaultCodes.Count == 0
                ? "No fault codes found."
                : $"{FaultCodes.Count} fault code(s) found.";
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
    private async Task ClearAsync()
    {
        if (DialogService != null &&
            !await DialogService.ConfirmAsync("Clear Fault Codes", "Are you sure you want to clear all fault codes?"))
            return;

        IsBusy = true;
        FaultCodes.Clear();
        StatusText = "Clearing fault codes...";
        try
        {
            await Task.Run(() =>
            {
                _connectionService.Tester!.ClearFaultCodes();
            });
            StatusText = "Fault codes cleared.";
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

public record FaultCodeItem(int Dtc, string StatusText)
{
    public string DtcFormatted => $"{Dtc:D5}";
}
