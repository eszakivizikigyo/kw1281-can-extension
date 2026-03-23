using System;
using System.Threading.Tasks;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.KLine;

public partial class EepromViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;

    public IDialogService? DialogService { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReadCommand))]
    [NotifyCanExecuteChangedFor(nameof(WriteCommand))]
    [NotifyCanExecuteChangedFor(nameof(DumpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MapCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private uint _address;

    [ObservableProperty]
    private uint _length = 256;

    [ObservableProperty]
    private byte _writeValue;

    [ObservableProperty]
    private string _filename = "eeprom.bin";

    [ObservableProperty]
    private string _statusText = string.Empty;

    public EepromViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private bool CanExecute() => !IsBusy && _connectionService.State == ConnectionState.Connected;

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ReadAsync()
    {
        IsBusy = true;
        StatusText = $"Reading EEPROM at 0x{Address:X4}...";
        try
        {
            var addr = Address;
            await Task.Run(() =>
            {
                _connectionService.Tester!.ReadEeprom(addr);
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
    private async Task WriteAsync()
    {
        if (DialogService != null &&
            !await DialogService.ConfirmAsync("Write EEPROM", $"Are you sure you want to write 0x{WriteValue:X2} to EEPROM at 0x{Address:X4}?"))
            return;

        IsBusy = true;
        StatusText = $"Writing 0x{WriteValue:X2} to EEPROM at 0x{Address:X4}...";
        try
        {
            var addr = Address;
            var val = WriteValue;
            await Task.Run(() =>
            {
                _connectionService.Tester!.WriteEeprom(addr, val);
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
    private async Task DumpAsync()
    {
        IsBusy = true;
        StatusText = $"Dumping EEPROM 0x{Address:X4} ({Length} bytes)...";
        try
        {
            var addr = Address;
            var len = Length;
            var file = Filename;
            await Task.Run(() =>
            {
                _connectionService.Tester!.DumpEeprom(addr, len, file);
            });
            StatusText = $"Dump saved to {Filename}.";
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
    private async Task MapAsync()
    {
        IsBusy = true;
        StatusText = "Mapping EEPROM...";
        try
        {
            var file = Filename;
            await Task.Run(() =>
            {
                _connectionService.Tester!.MapEeprom(file);
            });
            StatusText = "Map complete.";
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
