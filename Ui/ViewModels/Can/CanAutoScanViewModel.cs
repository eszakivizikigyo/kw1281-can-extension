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
                canInterface.SetCanSpeed(500);

                Logger.Log.WriteLine("=== CAN AutoScan ===");
                Logger.Log.WriteLine("Scanning VW TP 2.0 addresses 0x01-0x7F...");

                for (byte address = 0x01; address < 0x80; address++)
                {
                    ct.ThrowIfCancellationRequested();

                    Dispatcher.UIThread.Post(() =>
                    {
                        Progress = address;
                        StatusText = $"Scanning 0x{address:X2}...";
                    });

                    using var channel = new Tp20Channel(canInterface, address);
                    if (!channel.Open())
                        continue;

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

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _cts?.Cancel();
    }
}

public record ScanResultItem(string Address, string Name, string Protocol, string Identification);
