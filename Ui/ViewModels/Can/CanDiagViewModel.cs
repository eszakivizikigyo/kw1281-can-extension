using System;
using System.Threading;
using System.Threading.Tasks;
using BitFab.KW1281Test.Interface;
using BitFab.KW1281Test.Kwp2000;
using BitFab.KW1281Test.Uds;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.Can;

public partial class CanDiagViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReadIdentCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReadVinCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReadPartNumberCommand))]
    [NotifyCanExecuteChangedFor(nameof(TesterPresentCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExtendedSessionCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private byte _controllerAddress = 0x01;

    [ObservableProperty]
    private string _protocol = "KWP2000";

    [ObservableProperty]
    private string _statusText = "Ready.";

    public CanDiagViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private bool CanConnect() => !IsBusy && _connectionService.State == ConnectionState.Connected
                                  && _connectionService.Mode == ConnectionMode.Can;
    private bool CanExecuteDiag() => !IsBusy && _connectionService.State == ConnectionState.Connected
                                      && _connectionService.Mode == ConnectionMode.Can;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsBusy = true;
        StatusText = "Opening TP 2.0 channel...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            var address = ControllerAddress;
            var proto = Protocol;

            await Task.Run(() =>
            {
                canInterface.InitializeRawCan(500);

                using var channel = new Tp20Channel(canInterface, address);
                if (!channel.Open())
                {
                    Logger.Log.WriteLine("Failed to open TP 2.0 channel");
                    return;
                }

                Logger.Log.WriteLine($"TP 2.0 channel open to 0x{address:X2}");

                if (proto == "KWP2000")
                {
                    using var kwp2000 = new Kwp2000CanDialog(channel);
                    var response = kwp2000.SendReceive(
                        DiagnosticService.readEcuIdentification,
                        [0x9B]);
                    Logger.Log.WriteLine($"ECU Identification: {Utils.DumpAscii(response.Body)}");
                }
                else
                {
                    using var uds = new UdsCanDialog(channel);
                    var partData = uds.ReadDataByIdentifier(0xF187);
                    if (partData.Length > 2)
                    {
                        Logger.Log.WriteLine($"Part Number: {Utils.DumpAscii(partData[2..])}");
                    }
                }
            });

            StatusText = "Done.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteDiag))]
    private async Task ReadIdentAsync()
    {
        IsBusy = true;
        StatusText = "Reading ECU identification...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            var address = ControllerAddress;

            await Task.Run(() =>
            {
                canInterface.InitializeRawCan(500);

                using var channel = new Tp20Channel(canInterface, address);
                if (!channel.Open())
                {
                    Logger.Log.WriteLine("Failed to open TP 2.0 channel");
                    return;
                }

                using var kwp2000 = new Kwp2000CanDialog(channel);
                var response = kwp2000.SendReceive(
                    DiagnosticService.readEcuIdentification,
                    [0x9B]);
                Logger.Log.WriteLine($"ECU Identification: {Utils.DumpAscii(response.Body)}");
            });

            StatusText = "Done.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"ReadIdent failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteDiag))]
    private async Task ReadVinAsync()
    {
        IsBusy = true;
        StatusText = "Reading VIN...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            var address = ControllerAddress;

            await Task.Run(() =>
            {
                canInterface.InitializeRawCan(500);

                using var channel = new Tp20Channel(canInterface, address);
                if (!channel.Open())
                {
                    Logger.Log.WriteLine("Failed to open TP 2.0 channel");
                    return;
                }

                using var uds = new UdsCanDialog(channel);
                var vinData = uds.ReadDataByIdentifier(0xF190);
                if (vinData.Length >= 2)
                {
                    var vin = System.Text.Encoding.ASCII.GetString(vinData, 2, vinData.Length - 2);
                    Logger.Log.WriteLine($"VIN: {vin}");
                }
            });

            StatusText = "Done.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"Read VIN failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteDiag))]
    private async Task ReadPartNumberAsync()
    {
        IsBusy = true;
        StatusText = "Reading Part Number...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            var address = ControllerAddress;

            await Task.Run(() =>
            {
                canInterface.InitializeRawCan(500);

                using var channel = new Tp20Channel(canInterface, address);
                if (!channel.Open())
                {
                    Logger.Log.WriteLine("Failed to open TP 2.0 channel");
                    return;
                }

                using var uds = new UdsCanDialog(channel);
                var partData = uds.ReadDataByIdentifier(0xF187);
                if (partData.Length > 2)
                {
                    Logger.Log.WriteLine($"Part Number: {Utils.DumpAscii(partData[2..])}");
                }
            });

            StatusText = "Done.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"Read Part Number failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteDiag))]
    private async Task TesterPresentAsync()
    {
        IsBusy = true;
        StatusText = "Sending TesterPresent...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            var address = ControllerAddress;
            var proto = Protocol;

            await Task.Run(() =>
            {
                canInterface.InitializeRawCan(500);

                using var channel = new Tp20Channel(canInterface, address);
                if (!channel.Open())
                {
                    Logger.Log.WriteLine("Failed to open TP 2.0 channel");
                    return;
                }

                if (proto == "KWP2000")
                {
                    using var kwp2000 = new Kwp2000CanDialog(channel);
                    kwp2000.SendReceive(DiagnosticService.testerPresent, []);
                    Logger.Log.WriteLine("TesterPresent positive response received!");
                }
                else
                {
                    using var uds = new UdsCanDialog(channel);
                    uds.TesterPresent();
                    Logger.Log.WriteLine("TesterPresent OK");
                }
            });

            StatusText = "Done.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"TesterPresent failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteDiag))]
    private async Task ExtendedSessionAsync()
    {
        IsBusy = true;
        StatusText = "Starting extended session...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            var address = ControllerAddress;

            await Task.Run(() =>
            {
                canInterface.InitializeRawCan(500);

                using var channel = new Tp20Channel(canInterface, address);
                if (!channel.Open())
                {
                    Logger.Log.WriteLine("Failed to open TP 2.0 channel");
                    return;
                }

                using var uds = new UdsCanDialog(channel);
                var response = uds.DiagnosticSessionControl(0x03);
                Logger.Log.WriteLine($"Extended session started ({response.Length} bytes response)");
            });

            StatusText = "Done.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"Extended session failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
