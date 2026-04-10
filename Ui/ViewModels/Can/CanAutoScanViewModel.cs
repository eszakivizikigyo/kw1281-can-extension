using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using BitFab.KW1281Test.Interface;
using BitFab.KW1281Test.Kwp2000;
using BitFab.KW1281Test.Uds;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels.Can;

public partial class CanAutoScanViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isScanning;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private string _statusText = "Ready.";

    public ObservableCollection<ScanResultItem> Results { get; } = [];

    public CanAutoScanViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
    }

    private bool CanStart() => !IsScanning && _connectionService.State == ConnectionState.Connected
                                && _connectionService.Mode == ConnectionMode.Can;
    private bool CanStop() => IsScanning;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        IsScanning = true;
        Progress = 0;
        Results.Clear();
        StatusText = "Scanning...";
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            var canInterface = _connectionService.CanInterface!;
            await Task.Run(() =>
            {
                if (!canInterface.InitializeRawCan(500))
                {
                    Logger.Log.WriteLine("Raw CAN initialization failed — scan aborted");
                    throw new InvalidOperationException(
                        "This adapter does not support raw CAN mode. Use a genuine ELM327 v2.x, OBDLink, or STN1110-based adapter.");
                }

                Logger.Log.WriteLine("=== CAN AutoScan ===");

                // Phase 1: Scan VW TP 2.0 addresses 0x01-0x7F
                Logger.Log.WriteLine("Phase 1: Scanning VW TP 2.0 addresses 0x01-0x7F...");
                int tp20Found = 0;

                for (byte address = 0x01; address < 0x80; address++)
                {
                    ct.ThrowIfCancellationRequested();

                    Dispatcher.UIThread.Post(() =>
                    {
                        Progress = address;
                        StatusText = $"TP 2.0 scan 0x{address:X2}...";
                    });

                    using var channel = new Tp20Channel(canInterface, address);
                    if (!channel.Open())
                        continue;

                    tp20Found++;
                    var name = ControllerAddressExtensions.GetControllerName(address);
                    var protocol = "";
                    var ident = "";

                    try
                    {
                        using var kwp2000 = new Kwp2000CanDialog(channel);
                        var response = kwp2000.SendReceive(
                            DiagnosticService.readEcuIdentification,
                            new byte[] { 0x9B });
                        ident = Utils.DumpAscii(response.Body).Trim();
                        protocol = "KWP2000";
                    }
                    catch
                    {
                        try
                        {
                            using var channel2 = new Tp20Channel(canInterface, address);
                            if (channel2.Open())
                            {
                                using var uds = new UdsCanDialog(channel2);
                                var partData = uds.ReadDataByIdentifier(0xF187);
                                if (partData.Length > 2)
                                {
                                    ident = Utils.DumpAscii(partData[2..]).Trim();
                                }
                                protocol = "UDS";
                            }
                        }
                        catch
                        {
                            protocol = "TP2.0";
                        }
                    }

                    var addr = address;
                    Dispatcher.UIThread.Post(() =>
                    {
                        Results.Add(new ScanResultItem($"0x{addr:X2}", name, protocol, ident));
                    });

                    Logger.Log.WriteLine($"  0x{address:X2} {name,-20} [{protocol,-7}] {ident}");
                }

                // Phase 2: If no TP 2.0 modules found, try UDS over ISO-TP
                if (tp20Found == 0)
                {
                    Logger.Log.WriteLine("No TP 2.0 modules found. Trying UDS over ISO-TP...");

                    Dispatcher.UIThread.Post(() => StatusText = "Scanning UDS/ISO-TP...");

                    // Standard OBD/UDS address pairs: TX 0x7E0+i → RX 0x7E8+i
                    for (int i = 0; i < 8; i++)
                    {
                        ct.ThrowIfCancellationRequested();

                        uint txId = (uint)(0x7E0 + i);
                        uint rxId = (uint)(0x7E8 + i);

                        Dispatcher.UIThread.Post(() =>
                        {
                            Progress = 0x80 + i;
                            StatusText = $"UDS scan 0x{txId:X3}...";
                        });

                        using var transport = new ElmIsoTpTransport(canInterface, txId, rxId);
                        if (!transport.Open())
                            continue;

                        try
                        {
                            using var uds = new UdsCanDialog(transport);

                            // TesterPresent to check if module exists
                            uds.TesterPresent();

                            var ident = "";
                            try
                            {
                                var partData = uds.ReadDataByIdentifier(0xF187);
                                if (partData.Length > 2)
                                    ident = Utils.DumpAscii(partData[2..]).Trim();
                            }
                            catch { /* Module exists but doesn't support F187 */ }

                            var name = GetUdsModuleName(i);
                            var txAddr = txId;
                            Dispatcher.UIThread.Post(() =>
                            {
                                Results.Add(new ScanResultItem($"0x{txAddr:X3}", name, "UDS", ident));
                            });

                            Logger.Log.WriteLine($"  0x{txId:X3} {name,-20} [UDS    ] {ident}");
                        }
                        catch
                        {
                            // Module doesn't respond to UDS
                        }
                    }
                }
            }, ct);

            StatusText = $"Done. {Results.Count} module(s) found.";
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Cancelled. {Results.Count} module(s) found.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            Progress = 127;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private static string GetUdsModuleName(int index) => index switch
    {
        0 => "Engine",
        1 => "Transmission",
        2 => "ABS/ESP",
        3 => "Airbag",
        4 => "Climate/AC",
        5 => "Steering",
        6 => "Gateway",
        7 => "Instrument Cluster",
        _ => $"Module {index}"
    };

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _cts?.Cancel();
    }
}

public record ScanResultItem(string Address, string Name, string Protocol, string Identification);
