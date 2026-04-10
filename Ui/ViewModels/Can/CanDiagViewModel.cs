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
        StatusText = "Connecting...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            var address = ControllerAddress;
            var proto = Protocol;

            await Task.Run(() =>
            {
                if (!canInterface.InitializeRawCan(500))
                    throw new InvalidOperationException("This adapter does not support raw CAN mode.");

                using var transport = OpenTransport(canInterface, address);
                if (transport == null)
                {
                    Logger.Log.WriteLine("No module responded (tried TP 2.0 and UDS/ISO-TP)");
                    return;
                }

                if (proto == "KWP2000" && transport is Tp20Channel ch)
                {
                    using var kwp2000 = new Kwp2000CanDialog(ch);
                    var response = kwp2000.SendReceive(
                        DiagnosticService.readEcuIdentification,
                        [0x9B]);
                    Logger.Log.WriteLine($"ECU Identification: {Utils.DumpAscii(response.Body)}");
                }
                else
                {
                    using var uds = new UdsCanDialog(transport);
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
                if (!canInterface.InitializeRawCan(500))
                    throw new InvalidOperationException("This adapter does not support raw CAN mode.");

                using var transport = OpenTransport(canInterface, address);
                if (transport == null)
                {
                    Logger.Log.WriteLine("No module responded");
                    return;
                }

                if (transport is Tp20Channel ch)
                {
                    using var kwp2000 = new Kwp2000CanDialog(ch);
                    var response = kwp2000.SendReceive(
                        DiagnosticService.readEcuIdentification,
                        [0x9B]);
                    Logger.Log.WriteLine($"ECU Identification: {Utils.DumpAscii(response.Body)}");
                }
                else
                {
                    using var uds = new UdsCanDialog(transport);
                    var partData = uds.ReadDataByIdentifier(0xF187);
                    if (partData.Length > 2)
                        Logger.Log.WriteLine($"Part Number: {Utils.DumpAscii(partData[2..])}");
                }
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
                if (!canInterface.InitializeRawCan(500))
                    throw new InvalidOperationException("This adapter does not support raw CAN mode.");

                using var transport = OpenTransport(canInterface, address);
                if (transport == null)
                {
                    Logger.Log.WriteLine("No module responded");
                    return;
                }

                using var uds = new UdsCanDialog(transport);
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
                if (!canInterface.InitializeRawCan(500))
                    throw new InvalidOperationException("This adapter does not support raw CAN mode.");

                using var transport = OpenTransport(canInterface, address);
                if (transport == null)
                {
                    Logger.Log.WriteLine("No module responded");
                    return;
                }

                using var uds = new UdsCanDialog(transport);
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
                if (!canInterface.InitializeRawCan(500))
                    throw new InvalidOperationException("This adapter does not support raw CAN mode.");

                using var transport = OpenTransport(canInterface, address);
                if (transport == null)
                {
                    Logger.Log.WriteLine("No module responded");
                    return;
                }

                if (proto == "KWP2000" && transport is Tp20Channel ch)
                {
                    using var kwp2000 = new Kwp2000CanDialog(ch);
                    kwp2000.SendReceive(DiagnosticService.testerPresent, []);
                    Logger.Log.WriteLine("TesterPresent positive response received!");
                }
                else
                {
                    using var uds = new UdsCanDialog(transport);
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
                if (!canInterface.InitializeRawCan(500))
                    throw new InvalidOperationException("This adapter does not support raw CAN mode.");

                using var transport = OpenTransport(canInterface, address);
                if (transport == null)
                {
                    Logger.Log.WriteLine("No module responded");
                    return;
                }

                using var uds = new UdsCanDialog(transport);
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

    /// <summary>
    /// Try to open a transport to the given VAG module address.
    /// First tries VW TP 2.0, then falls back to UDS/ISO-TP on standard CAN IDs.
    /// </summary>
    private static ICanTransport? OpenTransport(CanInterface canInterface, byte address)
    {
        // Try VW TP 2.0 first
        var channel = new Tp20Channel(canInterface, address);
        if (channel.Open())
        {
            Logger.Log.WriteLine($"TP 2.0 channel open to 0x{address:X2}");
            return channel;
        }
        channel.Dispose();

        // Map VAG address to standard UDS CAN ID pair
        var (txId, rxId) = MapToUdsCanIds(address);
        if (txId == 0) return null;

        Logger.Log.WriteLine($"TP 2.0 failed — trying UDS/ISO-TP on 0x{txId:X3}/0x{rxId:X3}...");

        var transport = new ElmIsoTpTransport(canInterface, txId, rxId);
        if (transport.Open())
        {
            Logger.Log.WriteLine($"ISO-TP transport open: TX=0x{txId:X3} RX=0x{rxId:X3}");
            return transport;
        }
        transport.Dispose();
        return null;
    }

    /// <summary>
    /// Map VAG controller address to standard OBD/UDS CAN ID pair.
    /// </summary>
    private static (uint txId, uint rxId) MapToUdsCanIds(byte vagAddress) => vagAddress switch
    {
        0x01 => (0x7E0, 0x7E8), // Engine
        0x02 => (0x7E1, 0x7E9), // Transmission
        0x03 => (0x7E2, 0x7EA), // ABS/ESP
        0x15 => (0x7E3, 0x7EB), // Airbag
        0x08 => (0x7E4, 0x7EC), // Climate/AC
        0x44 => (0x7E5, 0x7ED), // Steering
        0x19 => (0x7E6, 0x7EE), // Gateway
        0x17 => (0x7E7, 0x7EF), // Instrument Cluster
        _ => (0, 0)
    };
}
