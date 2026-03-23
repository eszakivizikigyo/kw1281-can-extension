using System;
using System.Threading;
using System.Threading.Tasks;
using BitFab.KW1281Test.Interface;

namespace BitFab.KW1281Test.Ui.Services;

internal class ConnectionService : IConnectionService, IDisposable
{
    private IInterface? _interface;
    private CanInterface? _canInterface;
    private Tester? _tester;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public ConnectionMode Mode { get; private set; }
    public string? StatusText { get; private set; }

    public Tester? Tester => _tester;
    public CanInterface? CanInterface => _canInterface;

    public event EventHandler? StateChanged;

    public async Task ConnectKLineAsync(string port, int baudRate, byte controllerAddress, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            SetState(ConnectionState.Connecting);
            Mode = ConnectionMode.KLine;

            await Task.Run(() =>
            {
                _interface = InterfaceFactory.OpenPort(port, baudRate);
                _tester = new Tester(_interface, controllerAddress);
                var info = _tester.Kwp1281Wakeup();
                StatusText = info.Text;
            }, ct);

            SetState(ConnectionState.Connected);
        }
        catch
        {
            await CleanupAsync();
            SetState(ConnectionState.Disconnected);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ConnectCanAsync(string port, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            SetState(ConnectionState.Connecting);
            Mode = ConnectionMode.Can;

            await Task.Run(() =>
            {
                _canInterface = new CanInterface(port);
                if (!_canInterface.Initialize())
                {
                    throw new InvalidOperationException("Failed to initialize CAN interface");
                }
                StatusText = "CAN interface initialized";
            }, ct);

            SetState(ConnectionState.Connected);
        }
        catch
        {
            await CleanupAsync();
            SetState(ConnectionState.Disconnected);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            SetState(ConnectionState.Disconnecting);
            await CleanupAsync();
            SetState(ConnectionState.Disconnected);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private Task CleanupAsync()
    {
        return Task.Run(() =>
        {
            try { _tester?.EndCommunication(); } catch { }
            try { _canInterface?.Dispose(); } catch { }
            try { _interface?.Dispose(); } catch { }
            _tester = null;
            _canInterface = null;
            _interface = null;
            StatusText = null;
        });
    }

    private void SetState(ConnectionState newState)
    {
        State = newState;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        CleanupAsync().GetAwaiter().GetResult();
        _semaphore.Dispose();
    }
}
