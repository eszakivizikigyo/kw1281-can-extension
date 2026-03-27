using System;
using System.Threading.Tasks;
using BitFab.KW1281Test.Cluster;
using BitFab.KW1281Test.Interface;
using BitFab.KW1281Test.Kwp2000;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.Can;

public partial class CanSkcViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetSkcCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReadClusterInfoCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Ready.";

    [ObservableProperty]
    private string _clusterInfo = string.Empty;

    [ObservableProperty]
    private string _skcResult = string.Empty;

    public CanSkcViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private bool CanExecute() => !IsBusy && _connectionService.State == ConnectionState.Connected
                                  && _connectionService.Mode == ConnectionMode.Can;

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ReadClusterInfoAsync()
    {
        IsBusy = true;
        StatusText = "Reading cluster identification...";
        ClusterInfo = string.Empty;

        try
        {
            var canInterface = _connectionService.CanInterface!;

            var ident = await Task.Run(() =>
            {
                canInterface.InitializeRawCan(500);

                using var channel = new Tp20Channel(canInterface, 0x17);
                if (!channel.Open())
                    throw new InvalidOperationException("Failed to open TP 2.0 channel to cluster (0x17)");

                using var kwp2000 = new Kwp2000CanDialog(channel);
                var response = kwp2000.SendReceive(
                    DiagnosticService.readEcuIdentification,
                    new byte[] { 0x9B });
                return Utils.DumpAscii(response.Body);
            });

            ClusterInfo = ident;
            Logger.Log.WriteLine($"Cluster: {ident}");
            StatusText = "Done.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"ReadClusterInfo failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task GetSkcAsync()
    {
        IsBusy = true;
        StatusText = "Reading SKC over CAN...";
        SkcResult = string.Empty;

        try
        {
            var canInterface = _connectionService.CanInterface!;

            var result = await Task.Run(() =>
            {
                canInterface.InitializeRawCan(500);

                using var channel = new Tp20Channel(canInterface, 0x17);
                if (!channel.Open())
                    throw new InvalidOperationException("Failed to open TP 2.0 channel to cluster (0x17)");

                using var kwp2000 = new Kwp2000CanDialog(channel);

                // Read ECU identification
                var identResponse = kwp2000.SendReceive(
                    DiagnosticService.readEcuIdentification,
                    new byte[] { 0x9B });
                var ecuIdent = Utils.DumpAscii(identResponse.Body);
                Logger.Log.WriteLine($"Cluster: {ecuIdent}");

                var partNumberGroups = Tester.FindAndParsePartNumber(ecuIdent);
                if (partNumberGroups.Length < 4)
                    throw new InvalidOperationException($"Unable to parse part number from: {ecuIdent}");

                if (!ecuIdent.Contains("VDO") || partNumberGroups[1] != "920")
                    throw new InvalidOperationException(
                        $"CAN GetSKC only supports VDO CAN (920) clusters. Detected: {string.Join(" ", partNumberGroups)}");

                Logger.Log.WriteLine("VDO CAN cluster detected. Reading EEPROM...");

                // Start diagnostic session for memory access
                try
                {
                    kwp2000.StartDiagnosticSession(0x84, 0x14);
                }
                catch (Exception ex)
                {
                    Logger.Log.WriteLine($"Warning: Diagnostic session start failed: {ex.Message}");
                }

                // Read EEPROM region containing SKC (0x90 to 0x10C = 0x7C bytes)
                const ushort startAddress = 0x90;
                const byte length = 0x7C;
                const byte chunkSize = 32;

                var buffer = new byte[length];
                int offset = 0;
                while (offset < length)
                {
                    var readLen = (byte)Math.Min(chunkSize, length - offset);
                    var data = kwp2000.ReadMemoryByAddress((uint)(startAddress + offset), readLen);
                    Array.Copy(data, 0, buffer, offset, data.Length);
                    offset += data.Length;
                }

                var skc = VdoCluster.GetSkc(buffer, startAddress);
                if (skc.HasValue)
                {
                    Logger.Log.WriteLine($"SKC: {skc:D5}");
                    return $"SKC: {skc:D5}";
                }

                Logger.Log.WriteLine("Unable to determine SKC from EEPROM data.");
                return "Unable to determine SKC from EEPROM data.";
            });

            SkcResult = result;
            StatusText = "Done.";
        }
        catch (NegativeResponseException ex)
        {
            StatusText = $"Memory read rejected: {ex.Message}";
            SkcResult = "Security access may be required.";
            Logger.Log.WriteLine($"Memory read rejected: {ex.Message}");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"GetSkc failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
