using System;
using System.Text;
using System.Threading.Tasks;
using BitFab.KW1281Test.Cluster;
using BitFab.KW1281Test.Interface;
using BitFab.KW1281Test.Uds;
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
        _connectionService.StateChanged += OnConnectionStateChanged;
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            GetSkcCommand.NotifyCanExecuteChanged();
            ReadClusterInfoCommand.NotifyCanExecuteChanged();
        });
    }

    private bool CanExecute() => !IsBusy && _connectionService.State == ConnectionState.Connected
                                  && _connectionService.Mode == ConnectionMode.Can;

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ReadClusterInfoAsync()
    {
        IsBusy = true;
        StatusText = "Reading cluster identification via UDS...";
        ClusterInfo = string.Empty;

        try
        {
            var canInterface = _connectionService.CanInterface!;

            var ident = await Task.Run(() =>
            {
                // Cluster (0x17) → TX=0x714, RX=0x77E (verified live on T5GP)
                using var transport = new ElmIsoTpTransport(canInterface, 0x714, 0x77E);
                if (!transport.Open())
                    throw new InvalidOperationException("Failed to open ISO-TP transport to cluster (0x714/0x77E)");

                using var uds = new UdsCanDialog(transport);

                // Read system name (F197) and part number (F187)
                var sb = new StringBuilder();

                var sysName = uds.ReadDataByIdentifier(0xF197);
                if (sysName.Length > 2)
                    sb.Append(Encoding.ASCII.GetString(sysName, 2, sysName.Length - 2).TrimEnd('\0'));

                var partNum = uds.ReadDataByIdentifier(0xF187);
                if (partNum.Length > 2)
                    sb.Append(" PN: ").Append(Encoding.ASCII.GetString(partNum, 2, partNum.Length - 2).TrimEnd('\0'));

                var swVer = uds.ReadDataByIdentifier(0xF189);
                if (swVer.Length > 2)
                    sb.Append(" SW: ").Append(Encoding.ASCII.GetString(swVer, 2, swVer.Length - 2).TrimEnd('\0'));

                return sb.ToString();
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
        StatusText = "Reading SKC via UDS/ISO-TP...";
        SkcResult = string.Empty;

        try
        {
            var canInterface = _connectionService.CanInterface!;

            var result = await Task.Run(() =>
            {
                // Cluster (0x17) → TX=0x714, RX=0x77E (verified live on T5GP)
                using var transport = new ElmIsoTpTransport(canInterface, 0x714, 0x77E);
                if (!transport.Open())
                    throw new InvalidOperationException("Failed to open ISO-TP transport to cluster (0x714/0x77E)");

                using var uds = new UdsCanDialog(transport);

                // Read ECU identification via ReadDataByIdentifier
                var partNumData = uds.ReadDataByIdentifier(0xF187);
                var ecuIdent = partNumData.Length > 2
                    ? Encoding.ASCII.GetString(partNumData, 2, partNumData.Length - 2).TrimEnd('\0')
                    : "";
                Logger.Log.WriteLine($"Cluster part number: {ecuIdent}");

                var partNumberGroups = Tester.FindAndParsePartNumber(ecuIdent);
                if (partNumberGroups.Length < 4)
                    throw new InvalidOperationException($"Unable to parse part number from: {ecuIdent}");

                if (!ecuIdent.Contains("VDO") && partNumberGroups[1] != "920")
                    throw new InvalidOperationException(
                        $"CAN GetSKC only supports VDO CAN (920) clusters. Detected: {string.Join(" ", partNumberGroups)}");

                Logger.Log.WriteLine("VDO CAN cluster detected. Reading EEPROM via UDS ReadMemoryByAddress...");

                // Switch to extended diagnostic session for memory access
                try
                {
                    uds.DiagnosticSessionControl(0x03); // extendedDiagnosticSession
                }
                catch (Exception ex)
                {
                    Logger.Log.WriteLine($"Warning: Extended session start failed: {ex.Message}");
                }

                // Read EEPROM region containing SKC (0x90 to 0x10C = 0x7C bytes)
                const uint startAddress = 0x90;
                const uint length = 0x7C;
                const uint chunkSize = 32;

                var buffer = new byte[length];
                uint offset = 0;
                while (offset < length)
                {
                    var readLen = Math.Min(chunkSize, length - offset);
                    var data = uds.ReadMemoryByAddress(startAddress + offset, readLen);
                    Array.Copy(data, 0, buffer, (int)offset, data.Length);
                    offset += (uint)data.Length;
                }

                var skc = VdoCluster.GetSkc(buffer, (ushort)startAddress);
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
        catch (NegativeUdsResponseException ex)
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
