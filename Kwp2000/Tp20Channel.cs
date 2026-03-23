using BitFab.KW1281Test.Interface;
using System;
using System.Collections.Generic;
using System.Threading;

namespace BitFab.KW1281Test.Kwp2000;

/// <summary>
/// VW TP 2.0 (Transport Protocol 2.0) channel implementation.
/// Manages a bidirectional communication channel between tester and ECU over CAN bus.
/// 
/// Protocol flow:
/// 1. Tester sends Channel Setup Request to CAN ID 0x200 + module_address
/// 2. Module responds with Channel Setup Response containing dynamic TX/RX CAN IDs
/// 3. Tester sends Channel Parameters Request on the new dynamic channel
/// 4. Module responds with Channel Parameters (timing + block size)
/// 5. KWP2000 data is exchanged using segmented TP 2.0 data frames
/// 6. Keep-alive (Connection Test) must be sent periodically to prevent timeout
/// 
/// Supports multi-channel operation through CanRouter: when multiple channels share
/// one CAN interface, frames are automatically routed to the correct channel.
/// </summary>
internal class Tp20Channel : IDisposable
{
    private readonly CanRouter _router;
    private readonly byte _moduleAddress;
    private readonly bool _ownsRouter;

    /// <summary>CAN ID used by tester to send to module (assigned by module during setup)</summary>
    private uint _txId;

    /// <summary>CAN ID used by module to send to tester (assigned by module during setup)</summary>
    private uint _rxId;

    /// <summary>Sequence counter for sent data frames (0x0-0xF)</summary>
    private byte _txSequence;

    /// <summary>Expected sequence counter for received data frames</summary>
    private byte _rxSequence;

    /// <summary>Negotiated channel parameters</summary>
    private Tp20ChannelParameters _params = new();

    /// <summary>Whether the channel is currently open</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Timeout for receiving CAN frames (milliseconds)</summary>
    public int ReceiveTimeoutMs { get; set; } = 2000;

    /// <summary>
    /// Create a TP 2.0 channel using a shared CanRouter (for multi-channel operation).
    /// </summary>
    public Tp20Channel(CanRouter router, byte moduleAddress)
    {
        _router = router;
        _moduleAddress = moduleAddress;
        _ownsRouter = false;
    }

    /// <summary>
    /// Create a TP 2.0 channel with a direct CanInterface (single-channel convenience).
    /// Wraps the interface in an internal CanRouter.
    /// </summary>
    public Tp20Channel(CanInterface canInterface, byte moduleAddress)
    {
        _router = new CanRouter(canInterface);
        _moduleAddress = moduleAddress;
        _ownsRouter = true;
    }

    /// <summary>
    /// Open a TP 2.0 channel to the specified module.
    /// Performs channel setup and parameter negotiation.
    /// </summary>
    public bool Open()
    {
        if (IsOpen)
        {
            return true;
        }

        Log.WriteLine($"Opening TP 2.0 channel to module 0x{_moduleAddress:X2}...");

        // Step 1: Send channel setup request to 0x200 + module address
        if (!SendChannelSetupRequest())
        {
            Log.WriteLine("Channel setup request failed");
            return false;
        }

        // Step 2: Receive channel setup response containing dynamic CAN IDs
        if (!ReceiveChannelSetupResponse())
        {
            Log.WriteLine("Channel setup response failed");
            return false;
        }

        // Step 3: Send channel parameters request
        if (!SendChannelParametersRequest())
        {
            Log.WriteLine("Channel parameters request failed");
            return false;
        }

        // Step 4: Receive channel parameters response
        if (!ReceiveChannelParametersResponse())
        {
            Log.WriteLine("Channel parameters response failed");
            return false;
        }

        // Register our RX CAN ID so the router buffers frames for us
        _router.RegisterChannel(_rxId);

        IsOpen = true;
        _txSequence = 0;
        _rxSequence = 0;
        Log.WriteLine($"TP 2.0 channel opened. TX=0x{_txId:X3}, RX=0x{_rxId:X3}, {_params}");
        return true;
    }

    /// <summary>
    /// Close the TP 2.0 channel gracefully.
    /// </summary>
    public void Close()
    {
        if (!IsOpen) return;

        try
        {
            Log.WriteLine("Closing TP 2.0 channel...");
            var disconnectData = new byte[] { (byte)Tp20OpCode.Disconnect };
            _router.SendMessage(new CanMessage(_txId, PadTo8(disconnectData)));
            _router.UnregisterChannel(_rxId);
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Error during channel close: {ex.Message}");
        }
        finally
        {
            IsOpen = false;
        }
    }

    /// <summary>
    /// Send a KWP2000 message over the TP 2.0 channel with segmentation.
    /// </summary>
    public bool SendData(byte[] data)
    {
        if (!IsOpen)
            throw new InvalidOperationException("TP 2.0 channel is not open");

        var maxPayload = 7; // First byte is TP header, remaining 7 are data
        var offset = 0;
        var packetsSinceAck = 0;

        while (offset < data.Length)
        {
            var remaining = data.Length - offset;
            var chunkSize = Math.Min(remaining, maxPayload);
            var isLast = (offset + chunkSize) >= data.Length;
            var needsAck = isLast || (_params.BlockSize > 0 && ++packetsSinceAck >= _params.BlockSize);

            // Build frame type + sequence nibble
            Tp20FrameType frameType;
            if (needsAck)
            {
                frameType = isLast
                    ? Tp20FrameType.WaitingForAck_LastPacket
                    : Tp20FrameType.WaitingForAck_MoreToFollow;
                packetsSinceAck = 0;
            }
            else
            {
                frameType = Tp20FrameType.NotWaitingForAck_MoreToFollow;
            }

            var headerByte = (byte)(((int)frameType << 4) | (_txSequence & 0x0F));
            var frameData = new byte[1 + chunkSize];
            frameData[0] = headerByte;
            Array.Copy(data, offset, frameData, 1, chunkSize);

            if (!_router.SendMessage(new CanMessage(_txId, PadTo8(frameData))))
            {
                Log.WriteLine("Failed to send TP 2.0 data frame");
                return false;
            }

            _txSequence = (byte)((_txSequence + 1) & 0x0F);
            offset += chunkSize;

            // If we need ACK, wait for it
            if (needsAck)
            {
                if (!WaitForAck())
                {
                    Log.WriteLine("Did not receive ACK for data frame");
                    return false;
                }
            }
            else if (_params.T3Ms > 0)
            {
                // Inter-frame delay
                Thread.Sleep(_params.T3Ms);
            }
        }

        return true;
    }

    /// <summary>
    /// Receive a complete KWP2000 message from the TP 2.0 channel (reassembly).
    /// </summary>
    public byte[]? ReceiveData()
    {
        if (!IsOpen)
            throw new InvalidOperationException("TP 2.0 channel is not open");

        var result = new List<byte>();

        while (true)
        {
            var frame = _router.ReceiveMessage(_rxId, ReceiveTimeoutMs);
            if (frame == null)
            {
                Log.WriteLine("Timeout waiting for TP 2.0 data frame");
                return null;
            }

            if (frame.DataLength < 1)
            {
                continue;
            }

            var header = frame.Data[0];
            var frameTypeNibble = (header >> 4) & 0x0F;
            var sequence = header & 0x0F;

            // Check if this is a channel-level message (0xAx range)
            if (frameTypeNibble == 0x0A)
            {
                // Connection test or parameter message - handle and continue
                HandleChannelMessage(frame);
                continue;
            }

            // Data frame - extract payload (bytes after header)
            var payloadLength = Math.Min(frame.DataLength - 1, 7);
            for (int i = 0; i < payloadLength; i++)
            {
                result.Add(frame.Data[1 + i]);
            }

            _rxSequence = (byte)((sequence + 1) & 0x0F);

            var frameType = (Tp20FrameType)frameTypeNibble;

            // Check if ACK is needed
            if (frameType == Tp20FrameType.WaitingForAck_MoreToFollow ||
                frameType == Tp20FrameType.WaitingForAck_LastPacket)
            {
                SendAck(_rxSequence);
            }

            // Check if this is the last frame
            if (frameType == Tp20FrameType.WaitingForAck_LastPacket ||
                frameType == Tp20FrameType.NotWaitingForAck_LastPacket)
            {
                break;
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Send a keep-alive (Connection Test) to prevent channel timeout.
    /// Should be called periodically (typically every ~2 seconds).
    /// </summary>
    public bool SendKeepAlive()
    {
        if (!IsOpen) return false;

        var data = new byte[] { (byte)Tp20OpCode.ConnectionTest };
        return _router.SendMessage(new CanMessage(_txId, PadTo8(data)));
    }

    // --- Channel Setup ---

    private bool SendChannelSetupRequest()
    {
        var setupId = (uint)(0x200 + _moduleAddress);

        // Channel setup request:
        // Byte 0: Destination module address (logical)
        // Byte 1: 0xC0 (channel setup opcode)
        // Byte 2: RX ID low byte (what we want module to use when talking to us)
        // Byte 3: RX ID high byte
        // Byte 4: TX ID low byte (what we will use to talk to module) - 0x0000 = let module decide
        // Byte 5: TX ID high byte
        // Byte 6: Application type (0x01 = KWP2000 diagnostics)
        var requestedRxId = (uint)(0x300 + _moduleAddress);
        var data = new byte[]
        {
            _moduleAddress,
            (byte)Tp20OpCode.ChannelSetupRequest,
            (byte)(requestedRxId & 0xFF),
            (byte)((requestedRxId >> 8) & 0xFF),
            0x00, 0x10, // TX ID range: 0x1000 (convention, module usually assigns different)
            0x01        // Application: KWP2000 diagnostics
        };

        return _router.SendMessage(new CanMessage(setupId, PadTo8(data)));
    }

    private bool ReceiveChannelSetupResponse()
    {
        // We expect a response on CAN ID 0x200 + tester_logical_address
        // In practice, responses come back on specific module response IDs
        // Use ReceiveUnregisteredMessage because _rxId isn't known yet
        var frame = _router.ReceiveUnregisteredMessage(ReceiveTimeoutMs);
        if (frame == null)
        {
            Log.WriteLine("No channel setup response received");
            return false;
        }

        if (frame.DataLength < 7)
        {
            Log.WriteLine($"Channel setup response too short: {frame.DataLength} bytes");
            return false;
        }

        var opcode = frame.Data[1];
        if ((opcode & 0xF0) != (byte)Tp20OpCode.ChannelSetupResponse)
        {
            // Check for negative response (0xD8 = channel setup refused)
            if (opcode == 0xD8)
            {
                Log.WriteLine("Channel setup refused by module");
                return false;
            }
            Log.WriteLine($"Unexpected channel setup response opcode: 0x{opcode:X2}");
            return false;
        }

        // Extract dynamic CAN IDs assigned by the module
        _rxId = (uint)(frame.Data[2] | (frame.Data[3] << 8));
        _txId = (uint)(frame.Data[4] | (frame.Data[5] << 8));

        Log.WriteLine($"Channel setup: RX ID=0x{_rxId:X3}, TX ID=0x{_txId:X3}");
        return true;
    }

    private bool SendChannelParametersRequest()
    {
        // Channel parameters request on the dynamic TX channel
        // Byte 0: 0xA0 (parameters request)
        // Byte 1: Block size (number of frames before ACK, 0x0F = 15)
        // Byte 2: T1 timing (ACK timeout)
        // Byte 3: 0xFF (unused)
        // Byte 4: T3 timing (inter-frame timing)
        // Byte 5: 0xFF (unused)
        var data = new byte[]
        {
            (byte)Tp20OpCode.ChannelParametersRequest,
            0x0F,                                               // BS = 15 frames
            Tp20ChannelParameters.EncodeTimingByte(100),        // T1 = 100ms
            0xFF,
            Tp20ChannelParameters.EncodeTimingByte(10),         // T3 = 10ms
            0xFF
        };

        return _router.SendMessage(new CanMessage(_txId, PadTo8(data)));
    }

    private bool ReceiveChannelParametersResponse()
    {
        var frame = _router.ReceiveMessage(_rxId, ReceiveTimeoutMs);
        if (frame == null)
        {
            Log.WriteLine("No channel parameters response received");
            return false;
        }

        if (frame.Id != _rxId)
        {
            Log.WriteLine($"Unexpected CAN ID in parameters response: 0x{frame.Id:X3}");
            return false;
        }

        if (frame.DataLength < 6 || frame.Data[0] != (byte)Tp20OpCode.ChannelParametersResponse)
        {
            Log.WriteLine($"Invalid channel parameters response");
            return false;
        }

        _params.BlockSize = frame.Data[1];
        _params.T1Ms = Tp20ChannelParameters.DecodeTimingByte(frame.Data[2]);
        _params.T3Ms = Tp20ChannelParameters.DecodeTimingByte(frame.Data[4]);

        Log.WriteLine($"Channel parameters: {_params}");
        return true;
    }

    // --- ACK handling ---

    private bool WaitForAck()
    {
        var frame = _router.ReceiveMessage(_rxId, _params.T1Ms > 0 ? _params.T1Ms : ReceiveTimeoutMs);
        if (frame == null || frame.DataLength < 1)
        {
            return false;
        }

        var header = frame.Data[0];
        var frameType = (Tp20FrameType)((header >> 4) & 0x0F);

        return frameType == Tp20FrameType.Ack_Ready;
    }

    private void SendAck(byte expectedSequence)
    {
        var ackByte = (byte)(((int)Tp20FrameType.Ack_Ready << 4) | (expectedSequence & 0x0F));
        var data = new byte[] { ackByte };
        _router.SendMessage(new CanMessage(_txId, PadTo8(data)));
    }

    private void HandleChannelMessage(CanMessage frame)
    {
        if (frame.DataLength < 1) return;

        var opcode = frame.Data[0];
        if (opcode == (byte)Tp20OpCode.ConnectionTest)
        {
            // Respond to keep-alive from module
            var response = new byte[] { (byte)Tp20OpCode.ConnectionTestResponse };
            _router.SendMessage(new CanMessage(_txId, PadTo8(response)));
        }
    }

    // --- Helpers ---

    /// <summary>
    /// Pad a byte array to 8 bytes (CAN frame standard length).
    /// </summary>
    internal static byte[] PadTo8(byte[] data)
    {
        if (data.Length >= 8) return data;
        var padded = new byte[8];
        Array.Copy(data, padded, data.Length);
        return padded;
    }

    public void Dispose()
    {
        Close();
        if (_ownsRouter)
        {
            _router.Dispose();
        }
    }
}
