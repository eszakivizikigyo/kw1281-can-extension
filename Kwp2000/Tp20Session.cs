using BitFab.KW1281Test.Interface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BitFab.KW1281Test.Kwp2000;

/// <summary>
/// Manages multiple simultaneous TP 2.0 channels through a shared CanRouter.
/// Enables multi-ECU communication where several controllers are kept open
/// and can be addressed without re-establishing the TP 2.0 connection each time.
/// </summary>
internal class Tp20Session : IDisposable
{
    private readonly CanRouter _router;
    private readonly Dictionary<byte, Tp20Channel> _channels = new();
    private bool _disposed;

    public Tp20Session(CanRouter router)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
    }

    /// <summary>
    /// Open a TP 2.0 channel to the specified module address.
    /// If a channel is already open to this address, returns the existing channel.
    /// </summary>
    public Tp20Channel OpenChannel(byte moduleAddress)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Tp20Session));

        if (_channels.TryGetValue(moduleAddress, out var existing) && existing.IsOpen)
        {
            return existing;
        }

        // Clean up any previous closed channel for this address
        if (_channels.ContainsKey(moduleAddress))
        {
            _channels[moduleAddress].Dispose();
            _channels.Remove(moduleAddress);
        }

        var channel = new Tp20Channel(_router, moduleAddress);
        if (!channel.Open())
        {
            channel.Dispose();
            throw new UnableToProceedException();
        }

        _channels[moduleAddress] = channel;
        return channel;
    }

    /// <summary>
    /// Get an already-open channel for the specified module address.
    /// Returns null if no channel is open to that address.
    /// </summary>
    public Tp20Channel? GetChannel(byte moduleAddress)
    {
        if (_channels.TryGetValue(moduleAddress, out var channel) && channel.IsOpen)
        {
            return channel;
        }
        return null;
    }

    /// <summary>
    /// Close and remove the channel for the specified module address.
    /// </summary>
    public void CloseChannel(byte moduleAddress)
    {
        if (_channels.Remove(moduleAddress, out var channel))
        {
            channel.Dispose();
        }
    }

    /// <summary>
    /// Send keep-alive (Connection Test) to all open channels.
    /// Should be called periodically to prevent channel timeouts.
    /// </summary>
    public void SendKeepAliveAll()
    {
        foreach (var channel in _channels.Values)
        {
            if (channel.IsOpen)
            {
                channel.SendKeepAlive();
            }
        }
    }

    /// <summary>
    /// Module addresses that currently have open channels.
    /// </summary>
    public IReadOnlyCollection<byte> OpenAddresses =>
        _channels.Where(kv => kv.Value.IsOpen).Select(kv => kv.Key).ToArray();

    /// <summary>
    /// Number of currently open channels.
    /// </summary>
    public int ChannelCount => _channels.Count(kv => kv.Value.IsOpen);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var channel in _channels.Values)
        {
            channel.Dispose();
        }
        _channels.Clear();
    }
}
