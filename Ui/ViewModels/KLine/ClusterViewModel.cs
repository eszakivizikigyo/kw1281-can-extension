using System;
using System.Threading.Tasks;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.KLine;

public partial class ClusterViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;

    public IDialogService? DialogService { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetSkcCommand))]
    [NotifyCanExecuteChangedFor(nameof(GetClusterIdCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReadSoftwareVersionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleRB4ModeCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public ClusterViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private bool CanExecute() => !IsBusy && _connectionService.State == ConnectionState.Connected;

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task GetSkcAsync()
    {
        IsBusy = true;
        StatusText = "Reading SKC...";
        try
        {
            await Task.Run(() => _connectionService.Tester!.GetSkc());
            StatusText = "Done. Check log for SKC.";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task GetClusterIdAsync()
    {
        IsBusy = true;
        StatusText = "Reading Cluster ID...";
        try
        {
            await Task.Run(() => _connectionService.Tester!.GetClusterId());
            StatusText = "Done.";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ReadSoftwareVersionAsync()
    {
        IsBusy = true;
        StatusText = "Reading software version...";
        try
        {
            await Task.Run(() => _connectionService.Tester!.ReadSoftwareVersion());
            StatusText = "Done.";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ResetAsync()
    {
        if (DialogService != null &&
            !await DialogService.ConfirmAsync("Reset Cluster", "Are you sure you want to reset the cluster?"))
            return;

        IsBusy = true;
        StatusText = "Resetting cluster...";
        try
        {
            await Task.Run(() => _connectionService.Tester!.Reset());
            StatusText = "Reset sent.";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ToggleRB4ModeAsync()
    {
        IsBusy = true;
        StatusText = "Toggling RB4 mode...";
        try
        {
            await Task.Run(() => _connectionService.Tester!.ToggleRB4Mode());
            StatusText = "Done.";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
