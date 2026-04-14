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
    [NotifyCanExecuteChangedFor(nameof(ScanDidsCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Ready.";

    [ObservableProperty]
    private string _clusterInfo = string.Empty;

    [ObservableProperty]
    private string _skcResult = string.Empty;

    [ObservableProperty]
    private string _didScanResult = string.Empty;

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
            ScanDidsCommand.NotifyCanExecuteChanged();
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
                var ecuIdent = "";
                try
                {
                    var partNumData = uds.ReadDataByIdentifier(0xF187);
                    Logger.Log.WriteLine(
                        $"F187 raw ({partNumData.Length} bytes): {BitConverter.ToString(partNumData)}");
                    ecuIdent = partNumData.Length > 2
                        ? Encoding.ASCII.GetString(partNumData, 2, partNumData.Length - 2).TrimEnd('\0', ' ')
                        : Encoding.ASCII.GetString(partNumData).TrimEnd('\0', ' ');
                    Logger.Log.WriteLine($"Cluster part number: [{ecuIdent}]");
                }
                catch (Exception ex)
                {
                    Logger.Log.WriteLine($"F187 read failed: {ex.Message}");
                }

                // Validate cluster type if we got a full part number
                var partNumberGroups = Tester.FindAndParsePartNumber(ecuIdent);
                if (partNumberGroups.Length >= 4)
                {
                    if (!ecuIdent.Contains("VDO") && partNumberGroups[1] != "920")
                        throw new InvalidOperationException(
                            $"CAN GetSKC only supports VDO CAN (920) clusters. Detected: {string.Join(" ", partNumberGroups)}");
                    Logger.Log.WriteLine("VDO CAN cluster detected.");
                }
                else
                {
                    Logger.Log.WriteLine($"Part number not fully parsed [{ecuIdent}], proceeding with EEPROM read anyway...");
                }

                Logger.Log.WriteLine("Reading EEPROM via UDS ReadMemoryByAddress...");

                // Switch to extended diagnostic session for memory access
                try
                {
                    var sessionResp = uds.DiagnosticSessionControl(0x03); // extendedDiagnosticSession
                    Logger.Log.WriteLine($"Extended session OK ({BitConverter.ToString(sessionResp)})");
                }
                catch (Exception ex)
                {
                    Logger.Log.WriteLine($"Extended session failed: {ex.Message}");
                    // Try programming session as fallback
                    try
                    {
                        var sessionResp = uds.DiagnosticSessionControl(0x02); // programmingSession
                        Logger.Log.WriteLine($"Programming session OK ({BitConverter.ToString(sessionResp)})");
                    }
                    catch (Exception ex2)
                    {
                        Logger.Log.WriteLine($"Programming session also failed: {ex2.Message}");
                    }
                }

                // Attempt Security Access with VDO seed/key
                try
                {
                    Logger.Log.WriteLine("Attempting Security Access...");
                    var seedResp = uds.SecurityAccess(0x01, Array.Empty<byte>());
                    if (seedResp.Length > 1)
                    {
                        var seed = new byte[seedResp.Length - 1];
                        Array.Copy(seedResp, 1, seed, 0, seed.Length);
                        Logger.Log.WriteLine($"Seed: {BitConverter.ToString(seed)}");

                        var key = VdoKeyFinder.FindKey(seed, accessLevel: 1);
                        Logger.Log.WriteLine($"Key: {BitConverter.ToString(key)}");

                        var keyResp = uds.SecurityAccess(0x02, key);
                        Logger.Log.WriteLine($"Security Access granted! ({BitConverter.ToString(keyResp)})");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log.WriteLine($"Security Access failed: {ex.Message} - proceeding anyway...");
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

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ScanDidsAsync()
    {
        IsBusy = true;
        DidScanResult = string.Empty;
        StatusText = "Scanning cluster DIDs...";

        try
        {
            var canInterface = _connectionService.CanInterface!;

            var result = await Task.Run(() =>
            {
                using var transport = new ElmIsoTpTransport(canInterface, 0x714, 0x77E);
                if (!transport.Open())
                    throw new InvalidOperationException("Failed to open ISO-TP transport to cluster (0x714/0x77E)");

                using var uds = new UdsCanDialog(transport);
                var sb = new StringBuilder();
                var found = 0;

                // DID ranges to scan:
                // 0x0100-0x01FF: VW manufacturer-specific
                // 0x0200-0x02FF: VW manufacturer-specific
                // 0x0800-0x08FF: VW diagnostic data
                // 0xF100-0xF1FF: UDS identification
                // 0xF180-0xF19F: standard identification DIDs
                ushort[][] ranges =
                [
                    [0x0100, 0x01FF],
                    [0x0200, 0x02FF],
                    [0x0800, 0x08FF],
                    [0xF100, 0xF1FF],
                    [0xF000, 0xF0FF],
                ];

                foreach (var range in ranges)
                {
                    for (ushort did = range[0]; did <= range[1]; did++)
                    {
                        try
                        {
                            var resp = uds.ReadDataByIdentifier(did);
                            if (resp.Length > 2)
                            {
                                var dataBytes = new byte[resp.Length - 2];
                                Array.Copy(resp, 2, dataBytes, 0, dataBytes.Length);

                                var hex = BitConverter.ToString(dataBytes).Replace("-", " ");
                                var ascii = "";
                                foreach (var b in dataBytes)
                                    ascii += b is >= 0x20 and <= 0x7E ? (char)b : '.';

                                sb.AppendLine($"DID 0x{did:X4}: [{resp.Length - 2} bytes] {hex}");
                                sb.AppendLine($"           ASCII: {ascii}");
                                Logger.Log.WriteLine($"DID 0x{did:X4} ({resp.Length - 2}B): {hex} | {ascii}");
                                found++;
                            }
                        }
                        catch (NegativeUdsResponseException)
                        {
                            // DID not supported, skip
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("No response"))
                        {
                            // Timeout, skip
                        }
                    }
                }

                sb.Insert(0, $"=== Found {found} supported DIDs ===\n\n");
                return sb.ToString();
            });

            DidScanResult = result;
            StatusText = "DID scan complete.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"DID scan failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
