using System;
using System.Collections.Concurrent;
using System.Threading;

namespace BitFab.KW1281Test.Interface;

/// <summary>
/// Routes CAN frames to per-channel queues, enabling multiple TP 2.0 channels
/// to share a single CAN interface without losing frames intended for other channels.
/// 
/// When receiving for a specific channel, frames belonging to other registered channels
/// are buffered in their respective queues instead of being discarded.
/// </summary>
internal class CanRouter : IDisposable
{
    private readonly CanInterface _canInterface;
    private readonly ConcurrentDictionary<uint, ConcurrentQueue<CanMessage>> _channelQueues = new();
    private bool _disposed;

    public CanRouter(CanInterface canInterface)
    {
        _canInterface = canInterface ?? throw new ArgumentNullException(nameof(canInterface));
    }

    /// <summary>
    /// Register a channel's RX CAN ID for frame routing.
    /// Call after channel setup when the dynamic RX ID is known.
    /// </summary>
    public void RegisterChannel(uint rxCanId)
    {
        _channelQueues.TryAdd(rxCanId, new ConcurrentQueue<CanMessage>());
    }

    /// <summary>
    /// Unregister a channel's RX CAN ID. Buffered frames are discarded.
    /// </summary>
    public void UnregisterChannel(uint rxCanId)
    {
        _channelQueues.TryRemove(rxCanId, out _);
    }

    /// <summary>
    /// Receive a CAN frame for a specific registered channel.
    /// First checks the channel's buffer queue, then reads from the interface.
    /// Frames for other registered channels are buffered automatically.
    /// </summary>
    public CanMessage? ReceiveMessage(uint rxCanId, int timeoutMs)
    {
        // Check buffered messages first
        if (_channelQueues.TryGetValue(rxCanId, out var queue) && queue.TryDequeue(out var buffered))
        {
            return buffered;
        }

        // Read from interface, routing non-matching frames
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0) break;

            var frame = _canInterface.ReceiveCanMessage(remaining);
            if (frame == null) return null;

            if (frame.Id == rxCanId) return frame;

            // Buffer frame for its registered channel
            if (_channelQueues.TryGetValue(frame.Id, out var otherQueue))
            {
                otherQueue.Enqueue(frame);
            }
            // Unregistered CAN IDs are discarded
        }
        return null;
    }

    /// <summary>
    /// Receive a CAN frame that does not belong to any registered channel.
    /// Used during channel setup when the RX CAN ID is not yet known.
    /// Frames for registered channels are automatically buffered.
    /// </summary>
    public CanMessage? ReceiveUnregisteredMessage(int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (true)
        {
            var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0) break;

            var frame = _canInterface.ReceiveCanMessage(remaining);
            if (frame == null) return null;

            // If frame belongs to a registered channel, buffer it and keep reading
            if (_channelQueues.TryGetValue(frame.Id, out var queue))
            {
                queue.Enqueue(frame);
                continue;
            }

            // Unregistered frame — return it (likely a channel setup response)
            return frame;
        }
        return null;
    }

    /// <summary>
    /// Send a CAN message. Delegates directly to the underlying interface.
    /// </summary>
    public bool SendMessage(CanMessage message)
    {
        return _canInterface.SendCanMessage(message);
    }

    /// <summary>
    /// Set the CAN receive address filter (ATCRA) so the ELM327 accepts
    /// responses on the specified CAN ID after a send.
    /// Pass null to clear the filter (accept all via ATCF000/ATCM000).
    /// </summary>
    public bool SetRxFilter(uint? canId)
    {
        return _canInterface.SetRxFilter(canId);
    }

    /// <summary>
    /// Number of currently registered channels.
    /// </summary>
    public int RegisteredChannelCount => _channelQueues.Count;

    /// <summary>
    /// Check if a specific CAN ID has buffered frames.
    /// </summary>
    internal bool HasBufferedFrames(uint rxCanId)
    {
        return _channelQueues.TryGetValue(rxCanId, out var queue) && !queue.IsEmpty;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _channelQueues.Clear();
    }
}
