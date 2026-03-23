using System;
using System.Threading;
using System.Threading.Tasks;

namespace BitFab.KW1281Test.Ui.Services;

public interface IConnectionService
{
    ConnectionState State { get; }
    ConnectionMode Mode { get; }
    string? StatusText { get; }

    event EventHandler? StateChanged;

    Task ConnectKLineAsync(string port, int baudRate, byte controllerAddress, CancellationToken ct);
    Task ConnectCanAsync(string port, CancellationToken ct);
    Task DisconnectAsync();
}
