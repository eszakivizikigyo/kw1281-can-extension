using System;
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

public partial class CanMultiEcuViewModel : ViewModelBase
{
    private readonly ConnectionService _connectionService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Ready.";

    public ObservableCollection<EcuItem> Modules { get; } =
    [
        new(0x01, "Engine (ECU)", true),
        new(0x02, "Transmission", true),
        new(0x09, "Central Electronics", true),
        new(0x17, "Instrument Cluster", true),
        new(0x19, "CAN Gateway", true),
        new(0x46, "Central Comfort", true),
        new(0x56, "Radio", true),
    ];

    public ObservableCollection<EcuResultItem> Results { get; } = [];

    public CanMultiEcuViewModel(ConnectionService connectionService)
    {
        _connectionService = connectionService;
        _connectionService.StateChanged += OnConnectionStateChanged;
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ScanCommand.NotifyCanExecuteChanged();
        });
    }

    private bool CanScan() => !IsBusy && _connectionService.State == ConnectionState.Connected
                               && _connectionService.Mode == ConnectionMode.Can;

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        IsBusy = true;
        Results.Clear();
        StatusText = "Opening multi-ECU session...";

        try
        {
            var canInterface = _connectionService.CanInterface!;
            var selectedModules = new System.Collections.Generic.List<(byte Address, string Name)>();
            foreach (var m in Modules)
            {
                if (m.IsSelected)
                    selectedModules.Add((m.Address, m.Name));
            }

            await Task.Run(() =>
            {
                if (!canInterface.InitializeRawCan(500))
                    throw new InvalidOperationException("This adapter does not support raw CAN mode.");

                using var router = new CanRouter(canInterface);
                using var session = new Tp20Session(router);

                Logger.Log.WriteLine("=== CAN Multi-ECU Diagnostic Session ===");

                // Phase 1: Open channels
                var openModules = new System.Collections.Generic.List<(byte Address, string Name)>();
                foreach (var (address, name) in selectedModules)
                {
                    Logger.Log.Write($"  Trying 0x{address:X2} ({name})...");
                    try
                    {
                        session.OpenChannel(address);
                        openModules.Add((address, name));
                        Logger.Log.WriteLine(" OPEN");
                    }
                    catch
                    {
                        Logger.Log.WriteLine(" no response");
                    }
                }

                if (openModules.Count == 0)
                {
                    Logger.Log.WriteLine("No modules responded.");
                    return;
                }

                Logger.Log.WriteLine($"{openModules.Count} channel(s) open.");

                // Phase 2: Read identification from each open module
                foreach (var (address, name) in openModules)
                {
                    var channel = session.GetChannel(address);
                    if (channel == null) continue;

                    Logger.Log.WriteLine($"--- 0x{address:X2} {name} ---");

                    var ident = "";
                    var protocol = "";

                    try
                    {
                        using var kwp2000 = new Kwp2000CanDialog(channel);
                        var response = kwp2000.SendReceive(
                            DiagnosticService.readEcuIdentification,
                            [0x9B]);
                        ident = Utils.DumpAscii(response.Body).Trim();
                        protocol = "KWP2000";
                    }
                    catch
                    {
                        try
                        {
                            using var uds = new UdsCanDialog(channel);
                            var partData = uds.ReadDataByIdentifier(0xF187);
                            if (partData.Length > 2)
                            {
                                ident = Utils.DumpAscii(partData[2..]).Trim();
                            }
                            protocol = "UDS";
                        }
                        catch
                        {
                            protocol = "TP2.0";
                            ident = "(no identification)";
                        }
                    }

                    Logger.Log.WriteLine($"  [{protocol}] {ident}");

                    var addr = address;
                    var n = name;
                    var p = protocol;
                    var id = ident;
                    Dispatcher.UIThread.Post(() =>
                    {
                        Results.Add(new EcuResultItem($"0x{addr:X2}", n, p, id));
                    });

                    session.SendKeepAliveAll();
                }

                Logger.Log.WriteLine($"Multi-ECU session completed. {session.ChannelCount} channel(s) were open.");
            });

            StatusText = $"Done. {Results.Count} module(s) identified.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            Logger.Log.WriteLine($"Multi-ECU session failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class EcuItem : ObservableObject
{
    public byte Address { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;

    public EcuItem(byte address, string name, bool isSelected)
    {
        Address = address;
        Name = name;
        _isSelected = isSelected;
    }
}

public record EcuResultItem(string Address, string Name, string Protocol, string Identification);
