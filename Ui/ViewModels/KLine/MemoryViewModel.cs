using System;
using System.Threading.Tasks;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.KLine;

public enum MemoryType { RAM, ROM }

public partial class MemoryViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReadCommand))]
    [NotifyCanExecuteChangedFor(nameof(DumpCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private MemoryType _memoryType = MemoryType.RAM;

    [ObservableProperty]
    private uint _address;

    [ObservableProperty]
    private uint _length = 256;

    [ObservableProperty]
    private string _filename = "memory.bin";

    [ObservableProperty]
    private string _statusText = string.Empty;

    public bool IsRam
    {
        get => MemoryType == MemoryType.RAM;
        set { if (value) MemoryType = MemoryType.RAM; }
    }

    public bool IsRom
    {
        get => MemoryType == MemoryType.ROM;
        set { if (value) MemoryType = MemoryType.ROM; }
    }

    partial void OnMemoryTypeChanged(MemoryType value)
    {
        OnPropertyChanged(nameof(IsRam));
        OnPropertyChanged(nameof(IsRom));
    }

    public MemoryViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private bool CanExecute() => !IsBusy && _connectionService.State == ConnectionState.Connected;

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ReadAsync()
    {
        IsBusy = true;
        StatusText = $"Reading {MemoryType} at 0x{Address:X4}...";
        try
        {
            var addr = Address;
            var type = MemoryType;
            await Task.Run(() =>
            {
                if (type == MemoryType.RAM)
                    _connectionService.Tester!.ReadRam(addr);
                else
                    _connectionService.Tester!.ReadRom(addr);
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
        StatusText = $"Dumping {MemoryType} 0x{Address:X4} ({Length} bytes)...";
        try
        {
            var addr = Address;
            var len = Length;
            var file = Filename;
            var type = MemoryType;
            await Task.Run(() =>
            {
                if (type == MemoryType.RAM)
                    _connectionService.Tester!.DumpRam(addr, len, file);
                else
                    _connectionService.Tester!.DumpRom(addr, len, file);
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
}
