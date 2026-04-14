using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BitFab.KW1281Test.Cluster;
using BitFab.KW1281Test.Interface;
using BitFab.KW1281Test.Uds;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.Can;

public partial class ClusterSwapViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;

    // Cluster CAN IDs (verified live on T5GP)
    private const uint ClusterTxId = 0x714;
    private const uint ClusterRxId = 0x77E;

    // EEPROM region
    private const uint EepromStart = 0x0000;
    private const uint EepromSize = 0x0800; // 2 KB
    private const uint ChunkSize = 32;

    // Immo region for SKC extraction
    private const uint ImmoRegionStart = 0x0090;
    private const uint ImmoRegionSize = 0x007C;

    public IDialogService? DialogService { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DumpEepromCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReadClusterInfoCommand))]
    [NotifyCanExecuteChangedFor(nameof(WriteEepromCommand))]
    [NotifyCanExecuteChangedFor(nameof(WriteImmoRegionCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Ready.";

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _clusterInfo = string.Empty;

    [ObservableProperty]
    private string _skcInfo = string.Empty;

    [ObservableProperty]
    private string _immoId = string.Empty;

    [ObservableProperty]
    private string _dumpHex = string.Empty;

    [ObservableProperty]
    private string _savePath = string.Empty;

    [ObservableProperty]
    private string _loadPath = string.Empty;

    // Holds the last read EEPROM dump
    private byte[]? _currentDump;

    // Holds a loaded dump from file (for write/clone)
    private byte[]? _loadedDump;

    public ClusterSwapViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
        _connectionService.StateChanged += OnConnectionStateChanged;
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            DumpEepromCommand.NotifyCanExecuteChanged();
            ReadClusterInfoCommand.NotifyCanExecuteChanged();
            WriteEepromCommand.NotifyCanExecuteChanged();
            WriteImmoRegionCommand.NotifyCanExecuteChanged();
        });
    }

    private bool CanExecute() => !IsBusy && _connectionService.State == ConnectionState.Connected
                                  && _connectionService.Mode == ConnectionMode.Can;

    private bool CanWrite() => CanExecute() && _loadedDump != null;

    /// <summary>
    /// Read cluster identification and SKC via UDS.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ReadClusterInfoAsync()
    {
        IsBusy = true;
        StatusText = "Reading cluster identification...";
        ClusterInfo = string.Empty;
        SkcInfo = string.Empty;
        ImmoId = string.Empty;

        try
        {
            var canInterface = _connectionService.CanInterface!;

            var result = await Task.Run(() =>
            {
                using var transport = new ElmIsoTpTransport(canInterface, ClusterTxId, ClusterRxId);
                if (!transport.Open())
                    throw new InvalidOperationException("Failed to open ISO-TP transport to cluster");

                using var uds = new UdsCanDialog(transport);

                // Read identification DIDs
                var sb = new StringBuilder();

                try
                {
                    var sysName = uds.ReadDataByIdentifier(0xF197);
                    if (sysName.Length > 2)
                        sb.AppendLine("System: " + Encoding.ASCII.GetString(sysName, 2, sysName.Length - 2).TrimEnd('\0'));
                }
                catch { }

                try
                {
                    var partNum = uds.ReadDataByIdentifier(0xF187);
                    if (partNum.Length > 2)
                        sb.AppendLine("Part No: " + Encoding.ASCII.GetString(partNum, 2, partNum.Length - 2).TrimEnd('\0'));
                }
                catch { }

                try
                {
                    var swVer = uds.ReadDataByIdentifier(0xF189);
                    if (swVer.Length > 2)
                        sb.AppendLine("SW Ver: " + Encoding.ASCII.GetString(swVer, 2, swVer.Length - 2).TrimEnd('\0'));
                }
                catch { }

                // Read immo region to extract SKC
                string skcText = "";
                string immoIdText = "";
                try
                {
                    var immoData = ReadRegion(uds, ImmoRegionStart, ImmoRegionSize);
                    var skc = VdoCluster.GetSkc(immoData, (int)ImmoRegionStart);
                    skcText = skc.HasValue ? $"SKC: {skc:D5}" : "SKC: not found";

                    // Find immo ID
                    var text = Encoding.ASCII.GetString(immoData);
                    var match = Regex.Match(text, @"[A-Z]{2}Z\dZ0[A-Z]\d{7}");
                    immoIdText = match.Success ? match.Value : "not found";
                }
                catch (Exception ex)
                {
                    skcText = $"SKC read failed: {ex.Message}";
                }

                return (sb.ToString().TrimEnd(), skcText, immoIdText);
            });

            ClusterInfo = result.Item1;
            SkcInfo = result.Item2;
            ImmoId = result.Item3;
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

    /// <summary>
    /// Dump the full 2KB EEPROM from the cluster and save to file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task DumpEepromAsync()
    {
        IsBusy = true;
        Progress = 0;
        StatusText = "Dumping cluster EEPROM (2 KB)...";
        DumpHex = string.Empty;

        try
        {
            var canInterface = _connectionService.CanInterface!;

            var dump = await Task.Run(() =>
            {
                using var transport = new ElmIsoTpTransport(canInterface, ClusterTxId, ClusterRxId);
                if (!transport.Open())
                    throw new InvalidOperationException("Failed to open ISO-TP transport to cluster");

                using var uds = new UdsCanDialog(transport);

                // Try extended session for memory access
                try { uds.DiagnosticSessionControl(0x03); }
                catch (Exception ex) { Logger.Log.WriteLine($"Extended session: {ex.Message}"); }

                return ReadRegion(uds, EepromStart, EepromSize, progress =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Progress = progress);
                });
            });

            _currentDump = dump;

            // Generate hex view of immo region
            DumpHex = FormatHexDump(dump, EepromStart, ImmoRegionStart, ImmoRegionSize);

            // Extract SKC info
            var immoSlice = new byte[ImmoRegionSize];
            Array.Copy(dump, ImmoRegionStart - EepromStart, immoSlice, 0, ImmoRegionSize);
            var skc = VdoCluster.GetSkc(immoSlice, (int)ImmoRegionStart);
            SkcInfo = skc.HasValue ? $"SKC: {skc:D5}" : "SKC: not found in dump";

            // Find immo ID
            var text = Encoding.ASCII.GetString(immoSlice);
            var match = Regex.Match(text, @"[A-Z]{2}Z\dZ0[A-Z]\d{7}");
            ImmoId = match.Success ? match.Value : "not found";

            // Save to file
            var filename = $"cluster_eeprom_{DateTime.Now:yyyyMMdd_HHmmss}.bin";
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), filename);
            await File.WriteAllBytesAsync(path, dump);
            SavePath = path;

            StatusText = $"Done. Saved {dump.Length} bytes to {filename}";
            Logger.Log.WriteLine($"EEPROM dump saved: {path}");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"DumpEeprom failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Load a previously saved EEPROM dump from file.
    /// </summary>
    [RelayCommand]
    private async Task LoadDumpAsync()
    {
        StatusText = "Select dump file...";
        try
        {
            // Use file path from LoadPath field (user types or pastes)
            if (string.IsNullOrWhiteSpace(LoadPath))
            {
                StatusText = "Enter a .bin file path in the Load Path field.";
                return;
            }

            if (!File.Exists(LoadPath))
            {
                StatusText = $"File not found: {LoadPath}";
                return;
            }

            _loadedDump = await File.ReadAllBytesAsync(LoadPath);

            if (_loadedDump.Length != EepromSize)
            {
                StatusText = $"Warning: File is {_loadedDump.Length} bytes (expected {EepromSize}).";
                if (_loadedDump.Length < ImmoRegionStart + ImmoRegionSize)
                {
                    StatusText += " Too small for immo region.";
                    _loadedDump = null;
                    WriteEepromCommand.NotifyCanExecuteChanged();
                    WriteImmoRegionCommand.NotifyCanExecuteChanged();
                    return;
                }
            }

            // Show immo info from loaded dump
            var immoSlice = new byte[ImmoRegionSize];
            var offset = (int)(ImmoRegionStart - EepromStart);
            if (offset + ImmoRegionSize <= _loadedDump.Length)
            {
                Array.Copy(_loadedDump, offset, immoSlice, 0, ImmoRegionSize);
                var skc = VdoCluster.GetSkc(immoSlice, (int)ImmoRegionStart);
                var text = Encoding.ASCII.GetString(immoSlice);
                var match = Regex.Match(text, @"[A-Z]{2}Z\dZ0[A-Z]\d{7}");

                DumpHex = $"--- Loaded file: {Path.GetFileName(LoadPath)} ---\n" +
                          $"SKC: {(skc.HasValue ? $"{skc:D5}" : "not found")}\n" +
                          $"Immo ID: {(match.Success ? match.Value : "not found")}\n\n" +
                          FormatHexDump(_loadedDump, EepromStart, ImmoRegionStart, ImmoRegionSize);
            }

            WriteEepromCommand.NotifyCanExecuteChanged();
            WriteImmoRegionCommand.NotifyCanExecuteChanged();
            StatusText = $"Loaded {_loadedDump.Length} bytes from {Path.GetFileName(LoadPath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Write full EEPROM from loaded file to cluster.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task WriteEepromAsync()
    {
        if (_loadedDump == null) return;

        if (DialogService != null &&
            !await DialogService.ConfirmAsync("Write Full EEPROM",
                $"This will overwrite the ENTIRE cluster EEPROM ({_loadedDump.Length} bytes).\n\n" +
                "This operation cannot be undone!\n\nAre you sure?"))
            return;

        IsBusy = true;
        Progress = 0;
        StatusText = "Writing full EEPROM to cluster...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            var dataToWrite = _loadedDump;

            await Task.Run(() =>
            {
                using var transport = new ElmIsoTpTransport(canInterface, ClusterTxId, ClusterRxId);
                if (!transport.Open())
                    throw new InvalidOperationException("Failed to open ISO-TP transport to cluster");

                using var uds = new UdsCanDialog(transport);

                // Extended session
                uds.DiagnosticSessionControl(0x03);

                // Security access may be needed
                PerformSecurityAccess(uds);

                WriteRegion(uds, EepromStart, dataToWrite, progress =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Progress = progress);
                });
            });

            StatusText = $"Done. Wrote {_loadedDump.Length} bytes to cluster EEPROM.";
            Logger.Log.WriteLine("Full EEPROM write completed.");
        }
        catch (NegativeUdsResponseException ex)
        {
            StatusText = $"Write rejected: {ex.Message}";
            Logger.Log.WriteLine($"EEPROM write rejected: {ex.Message}");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"WriteEeprom failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Write only the immo region (0x90-0x10C) from loaded dump to cluster.
    /// This copies SKC + immo ID + key data from old cluster to new.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task WriteImmoRegionAsync()
    {
        if (_loadedDump == null) return;

        if (DialogService != null &&
            !await DialogService.ConfirmAsync("Write Immo Region",
                $"This will overwrite the cluster immo region (0x{ImmoRegionStart:X3}-0x{ImmoRegionStart + ImmoRegionSize:X3}).\n" +
                "This includes SKC, Immo ID, and key data.\n\nAre you sure?"))
            return;

        IsBusy = true;
        Progress = 0;
        StatusText = "Writing immo region to cluster...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            var immoData = new byte[ImmoRegionSize];
            Array.Copy(_loadedDump, ImmoRegionStart - EepromStart, immoData, 0, ImmoRegionSize);

            await Task.Run(() =>
            {
                using var transport = new ElmIsoTpTransport(canInterface, ClusterTxId, ClusterRxId);
                if (!transport.Open())
                    throw new InvalidOperationException("Failed to open ISO-TP transport to cluster");

                using var uds = new UdsCanDialog(transport);

                // Extended session
                uds.DiagnosticSessionControl(0x03);

                // Security access
                PerformSecurityAccess(uds);

                WriteRegion(uds, ImmoRegionStart, immoData, progress =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Progress = progress);
                });
            });

            StatusText = $"Done. Wrote {ImmoRegionSize} bytes (immo region) to cluster.";
            Logger.Log.WriteLine("Immo region write completed.");
        }
        catch (NegativeUdsResponseException ex)
        {
            StatusText = $"Write rejected: {ex.Message}";
            Logger.Log.WriteLine($"Immo write rejected: {ex.Message}");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"WriteImmoRegion failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // --- Helper methods ---

    private static byte[] ReadRegion(UdsCanDialog uds, uint start, uint length,
        Action<int>? progressCallback = null)
    {
        var buffer = new byte[length];
        uint offset = 0;

        while (offset < length)
        {
            var readLen = Math.Min(ChunkSize, length - offset);
            var data = uds.ReadMemoryByAddress(start + offset, readLen);
            Array.Copy(data, 0, buffer, (int)offset, data.Length);
            offset += (uint)data.Length;

            progressCallback?.Invoke((int)(offset * 100 / length));
        }

        return buffer;
    }

    private static void WriteRegion(UdsCanDialog uds, uint start, byte[] data,
        Action<int>? progressCallback = null)
    {
        uint offset = 0;
        uint length = (uint)data.Length;
        const uint writeChunk = 16; // Smaller chunks for write safety

        while (offset < length)
        {
            var writeLen = (int)Math.Min(writeChunk, length - offset);
            var chunk = new byte[writeLen];
            Array.Copy(data, (int)offset, chunk, 0, writeLen);

            uds.WriteMemoryByAddress(start + offset, chunk);
            offset += (uint)writeLen;

            progressCallback?.Invoke((int)(offset * 100 / length));
        }
    }

    /// <summary>
    /// Attempt UDS Security Access using VDO seed/key algorithm.
    /// </summary>
    private static void PerformSecurityAccess(UdsCanDialog uds)
    {
        try
        {
            // Request seed (access type 0x01)
            var seedResponse = uds.SecurityAccess(0x01, Array.Empty<byte>());

            if (seedResponse.Length < 2)
            {
                Logger.Log.WriteLine("Security Access: seed too short, skipping");
                return;
            }

            // Extract seed (skip first byte which is the access type echo)
            var seed = new byte[seedResponse.Length - 1];
            Array.Copy(seedResponse, 1, seed, 0, seed.Length);

            if (IsAllZero(seed))
            {
                Logger.Log.WriteLine("Security Access: already unlocked (zero seed)");
                return;
            }

            Logger.Log.WriteLine($"Security Access seed: {BitConverter.ToString(seed)}");

            // Use VDO key finder to calculate key
            var key = VdoKeyFinder.FindKey(seed, accessLevel: 1);

            Logger.Log.WriteLine($"Security Access key: {BitConverter.ToString(key)}");

            // Send key (access type 0x02)
            uds.SecurityAccess(0x02, key);
            Logger.Log.WriteLine("Security Access: unlocked successfully");
        }
        catch (NegativeUdsResponseException ex)
        {
            Logger.Log.WriteLine($"Security Access failed: {ex.Message}");
            throw new InvalidOperationException(
                $"Security Access denied. The cluster may require a different authentication method. ({ex.Message})");
        }
    }

    private static bool IsAllZero(byte[] data)
    {
        foreach (var b in data)
            if (b != 0) return false;
        return true;
    }

    private static string FormatHexDump(byte[] data, uint dataStart, uint regionStart, uint regionLength)
    {
        var sb = new StringBuilder();
        var offset = (int)(regionStart - dataStart);
        var end = (int)Math.Min(offset + regionLength, data.Length);

        for (int i = offset; i < end; i += 16)
        {
            sb.Append($"{dataStart + i:X4}: ");

            // Hex bytes
            for (int j = 0; j < 16 && i + j < end; j++)
            {
                sb.Append($"{data[i + j]:X2} ");
            }

            // Padding if last line is short
            var remaining = end - i;
            if (remaining < 16)
                sb.Append(new string(' ', (16 - remaining) * 3));

            sb.Append(' ');

            // ASCII
            for (int j = 0; j < 16 && i + j < end; j++)
            {
                var b = data[i + j];
                sb.Append(b is >= 0x20 and <= 0x7E ? (char)b : '.');
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
