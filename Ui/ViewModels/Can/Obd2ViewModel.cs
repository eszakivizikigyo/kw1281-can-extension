using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using BitFab.KW1281Test.Interface;
using BitFab.KW1281Test.Ui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace BitFab.KW1281Test.Ui.ViewModels.Can;

public partial class Obd2ViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QueryOnceCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartLiveCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(DetectPidsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReadDtcCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearDtcCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Ready.";

    [ObservableProperty]
    private int _refreshIntervalMs = 500;

    [ObservableProperty]
    private Obd2Target _selectedTarget = Obd2Target.Engine;

    public ObservableCollection<Obd2PidRow> Pids { get; } = [];
    public ObservableCollection<string> DtcList { get; } = [];
    public Obd2Target[] AvailableTargets { get; } = Enum.GetValues<Obd2Target>();

    public Obd2ViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
        _connectionService.StateChanged += OnConnectionStateChanged;
        // Pre-populate standard PIDs
        foreach (var def in Obd2PidDefinition.StandardPids)
        {
            Pids.Add(new Obd2PidRow(def));
        }
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            QueryOnceCommand.NotifyCanExecuteChanged();
            StartLiveCommand.NotifyCanExecuteChanged();
            StopCommand.NotifyCanExecuteChanged();
            DetectPidsCommand.NotifyCanExecuteChanged();
            ReadDtcCommand.NotifyCanExecuteChanged();
            ClearDtcCommand.NotifyCanExecuteChanged();
        });
    }

    private bool CanExecute() => !IsBusy && _connectionService.State == ConnectionState.Connected
                                  && _connectionService.Mode == ConnectionMode.Can;
    private bool CanStop() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task DetectPidsAsync()
    {
        IsBusy = true;
        StatusText = "Detecting supported PIDs...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            await Task.Run(() =>
            {
                using var transport = OpenObd2Transport(canInterface);
                if (transport == null)
                    throw new InvalidOperationException("No ECU responded on OBD2 address (0x7DF).");

                // Query PID 0x00 (supported PIDs 01-20)
                var supported = QuerySupportedPids(transport);

                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var row in Pids)
                    {
                        row.IsSupported = supported.Contains(row.Definition.Pid);
                    }
                    StatusText = $"Detected {supported.Count} supported PID(s).";
                });
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"PID detect failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task QueryOnceAsync()
    {
        IsBusy = true;
        StatusText = "Querying PIDs...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            var selectedPids = Pids.Where(p => p.IsSelected).ToList();

            if (selectedPids.Count == 0)
            {
                StatusText = "No PIDs selected.";
                return;
            }

            await Task.Run(() =>
            {
                using var transport = OpenObd2Transport(canInterface);
                if (transport == null)
                    throw new InvalidOperationException("No ECU responded on OBD2 address.");

                QuerySelectedPids(transport, selectedPids);
            });

            StatusText = "Done.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"OBD2 query failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task StartLiveAsync()
    {
        IsBusy = true;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        StatusText = "Live monitoring...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            var selectedPids = Pids.Where(p => p.IsSelected).ToList();

            if (selectedPids.Count == 0)
            {
                StatusText = "No PIDs selected.";
                return;
            }

            await Task.Run(() =>
            {
                using var transport = OpenObd2Transport(canInterface);
                if (transport == null)
                    throw new InvalidOperationException("No ECU responded on OBD2 address.");

                int cycle = 0;
                while (!ct.IsCancellationRequested)
                {
                    cycle++;
                    QuerySelectedPids(transport, selectedPids);

                    Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Live — cycle {cycle}";
                    });

                    ct.WaitHandle.WaitOne(RefreshIntervalMs);
                }
            }, ct);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Live monitoring stopped.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"OBD2 live failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _cts?.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ReadDtcAsync()
    {
        IsBusy = true;
        StatusText = "Reading DTC...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            await Task.Run(() =>
            {
                using var transport = OpenObd2Transport(canInterface);
                if (transport == null)
                    throw new InvalidOperationException("No ECU responded.");

                // Mode 03: Request stored DTCs
                var response = SendObd2(transport, 0x03, []);
                if (response == null || response.Length < 1)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        DtcList.Clear();
                        DtcList.Add("No DTCs stored.");
                    });
                    return;
                }

                // First byte = number of DTCs, then pairs of bytes
                var dtcCount = response[0];
                var dtcs = new List<string>();
                for (int i = 1; i + 1 < response.Length; i += 2)
                {
                    var dtcString = DecodeDtc(response[i], response[i + 1]);
                    if (dtcString != "P0000")
                        dtcs.Add(dtcString);
                }

                Dispatcher.UIThread.Post(() =>
                {
                    DtcList.Clear();
                    if (dtcs.Count == 0)
                        DtcList.Add("No DTCs stored.");
                    else
                        foreach (var dtc in dtcs)
                            DtcList.Add(dtc);
                    StatusText = $"{dtcs.Count} DTC(s) found.";
                });

                Logger.Log.WriteLine($"OBD2 DTCs: {string.Join(", ", dtcs)}");
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"DTC read failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ClearDtcAsync()
    {
        IsBusy = true;
        StatusText = "Clearing DTC...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            await Task.Run(() =>
            {
                using var transport = OpenObd2Transport(canInterface);
                if (transport == null)
                    throw new InvalidOperationException("No ECU responded.");

                // Mode 04: Clear DTCs
                transport.SendData([0x04]);
                transport.ReceiveData();
                Logger.Log.WriteLine("OBD2: DTCs cleared.");

                Dispatcher.UIThread.Post(() =>
                {
                    DtcList.Clear();
                    DtcList.Add("DTCs cleared.");
                });
            });

            StatusText = "DTCs cleared.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"DTC clear failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────

    private ElmIsoTpTransport? OpenObd2Transport(CanInterface canInterface)
    {
        var (txId, rxId) = SelectedTarget switch
        {
            Obd2Target.Engine       => (0x7E0u, 0x7E8u),
            Obd2Target.Transmission => (0x7E1u, 0x7E9u),
            _ => (0x7E0u, 0x7E8u)
        };

        Logger.Log.WriteLine($"OBD2 target: {SelectedTarget} TX=0x{txId:X3} RX=0x{rxId:X3}");
        var transport = new ElmIsoTpTransport(canInterface, txId, rxId);
        if (transport.Open())
            return transport;

        transport.Dispose();
        return null;
    }

    private void QuerySelectedPids(ICanTransport transport, List<Obd2PidRow> pids)
    {
        foreach (var row in pids)
        {
            try
            {
                var response = SendObd2(transport, row.Definition.Service, [row.Definition.Pid]);
                if (response != null && response.Length >= 1 + row.Definition.ResponseBytes)
                {
                    // response[0] = PID echo, data starts at [1]
                    var data = response[1..];
                    var value = row.Definition.Decode(data);

                    Dispatcher.UIThread.Post(() =>
                    {
                        row.RawHex = BitConverter.ToString(data[..row.Definition.ResponseBytes]).Replace("-", " ");
                        row.Value = value;
                        row.FormattedValue = $"{value:F1} {row.Definition.Unit}";
                        row.LastUpdate = DateTime.Now;
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    row.FormattedValue = $"Error: {ex.Message}";
                });
            }
        }
    }

    private static byte[]? SendObd2(ICanTransport transport, byte service, byte[] data)
    {
        var payload = new byte[1 + data.Length];
        payload[0] = service;
        Array.Copy(data, 0, payload, 1, data.Length);

        if (!transport.SendData(payload))
            return null;

        var response = transport.ReceiveData();
        if (response == null || response.Length < 2)
            return null;

        // Check for positive response (service + 0x40)
        if (response[0] != (byte)(service + 0x40))
            return null;

        // Return data after the service byte
        return response[1..];
    }

    private static HashSet<byte> QuerySupportedPids(ICanTransport transport)
    {
        var supported = new HashSet<byte>();

        // PID 0x00: supported PIDs 01-20
        var resp = SendObd2(transport, 0x01, [0x00]);
        if (resp != null && resp.Length >= 5)
            DecodeSupportedBitmap(resp, 1, 0x01, supported);

        // PID 0x20: supported PIDs 21-40
        if (supported.Contains(0x20))
        {
            resp = SendObd2(transport, 0x01, [0x20]);
            if (resp != null && resp.Length >= 5)
                DecodeSupportedBitmap(resp, 1, 0x21, supported);
        }

        // PID 0x40: supported PIDs 41-60
        if (supported.Contains(0x40))
        {
            resp = SendObd2(transport, 0x01, [0x40]);
            if (resp != null && resp.Length >= 5)
                DecodeSupportedBitmap(resp, 1, 0x41, supported);
        }

        return supported;
    }

    private static void DecodeSupportedBitmap(byte[] data, int offset, byte startPid, HashSet<byte> supported)
    {
        for (int byteIdx = 0; byteIdx < 4; byteIdx++)
        {
            var b = data[offset + byteIdx];
            for (int bit = 7; bit >= 0; bit--)
            {
                if ((b & (1 << bit)) != 0)
                {
                    supported.Add((byte)(startPid + (byteIdx * 8) + (7 - bit)));
                }
            }
        }
    }

    private static string DecodeDtc(byte high, byte low)
    {
        char prefix = ((high >> 6) & 0x03) switch
        {
            0 => 'P',
            1 => 'C',
            2 => 'B',
            3 => 'U',
            _ => 'P'
        };
        int digit1 = (high >> 4) & 0x03;
        int digit2 = high & 0x0F;
        int digit3 = (low >> 4) & 0x0F;
        int digit4 = low & 0x0F;
        return $"{prefix}{digit1}{digit2:X}{digit3:X}{digit4:X}";
    }
}

/// <summary>
/// A single OBD2 PID row displayed in the UI DataGrid.
/// </summary>
public partial class Obd2PidRow : ObservableObject
{
    public Obd2PidDefinition Definition { get; }

    [ObservableProperty] private bool _isSelected = true;
    [ObservableProperty] private bool _isSupported = true;
    [ObservableProperty] private double _value;
    [ObservableProperty] private string _formattedValue = "—";
    [ObservableProperty] private string _rawHex = "";
    [ObservableProperty] private DateTime _lastUpdate;

    public string PidHex => $"0x{Definition.Pid:X2}";
    public string Name => Definition.Name;
    public string Unit => Definition.Unit;

    public Obd2PidRow(Obd2PidDefinition definition)
    {
        Definition = definition;
    }
}

public enum Obd2Target
{
    Engine,
    Transmission
}
