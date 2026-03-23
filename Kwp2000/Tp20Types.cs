using System;

namespace BitFab.KW1281Test.Kwp2000;

/// <summary>
/// VW TP 2.0 (Transport Protocol 2.0) opcode types.
/// These appear in the first nibble of the first data byte in TP 2.0 frames.
/// </summary>
internal enum Tp20OpCode : byte
{
    /// <summary>Channel setup request (broadcast to module)</summary>
    ChannelSetupRequest = 0xC0,

    /// <summary>Positive channel setup response from module</summary>
    ChannelSetupResponse = 0xD0,

    /// <summary>Channel parameters request</summary>
    ChannelParametersRequest = 0xA0,

    /// <summary>Channel parameters response</summary>
    ChannelParametersResponse = 0xA1,

    /// <summary>Connection test (keep-alive) - no ACK expected</summary>
    ConnectionTest = 0xA3,

    /// <summary>Connection test response</summary>
    ConnectionTestResponse = 0xA1,

    /// <summary>Disconnect request</summary>
    Disconnect = 0xA8,
}

/// <summary>
/// TP 2.0 data transfer frame types.
/// The first nibble of the first byte in data frames encodes the type.
/// </summary>
internal enum Tp20FrameType : byte
{
    /// <summary>Waiting for ACK, more packets to follow (0x0)</summary>
    WaitingForAck_MoreToFollow = 0x0,

    /// <summary>Waiting for ACK, this is the last packet (0x1)</summary>
    WaitingForAck_LastPacket = 0x1,

    /// <summary>Not waiting for ACK, more packets to follow (0x2)</summary>
    NotWaitingForAck_MoreToFollow = 0x2,

    /// <summary>Not waiting for ACK, this is the last packet (0x3)</summary>
    NotWaitingForAck_LastPacket = 0x3,

    /// <summary>ACK, not ready for next packet (0x9)</summary>
    Ack_NotReady = 0x9,

    /// <summary>ACK, ready for next packet (0xB)</summary>
    Ack_Ready = 0xB,
}

/// <summary>
/// Timing parameters for a TP 2.0 channel, negotiated during setup.
/// </summary>
internal class Tp20ChannelParameters
{
    /// <summary>Block size: number of packets before an ACK is expected (0 = no limit)</summary>
    public byte BlockSize { get; set; }

    /// <summary>T1 timeout: time to wait for ACK after sending block, in milliseconds</summary>
    public int T1Ms { get; set; }

    /// <summary>T3 timeout: interval between consecutive CAN frames, in milliseconds</summary>
    public int T3Ms { get; set; }

    /// <summary>
    /// Decode a timing byte from TP 2.0 channel parameters response.
    /// Format: upper nibble = multiplier type, lower nibble = value.
    /// </summary>
    public static int DecodeTimingByte(byte b)
    {
        var unit = (b >> 4) & 0x0F;
        var value = b & 0x0F;

        return unit switch
        {
            0 => value,           // 0.1 ms units → but we work in ms, so minimum 1ms
            1 => value,           // 1 ms units
            2 => value * 10,      // 10 ms units
            3 => value * 100,     // 100 ms units
            _ => value            // Fallback
        };
    }

    /// <summary>
    /// Encode a timing value into a TP 2.0 timing byte.
    /// </summary>
    public static byte EncodeTimingByte(int ms)
    {
        if (ms <= 0)
            return 0x10;                                          // 1ms * 0

        if (ms < 10 || (ms < 16 && ms % 10 != 0))
            return (byte)(0x10 | (ms & 0x0F));                    // 1ms units (1-15ms)

        if (ms < 160 && ms % 10 == 0)
            return (byte)(0x20 | ((ms / 10) & 0x0F));             // 10ms units (10-150ms)

        if (ms % 100 == 0)
            return (byte)(0x30 | ((ms / 100) & 0x0F));            // 100ms units (100-1500ms)

        // Fallback: closest 10ms unit
        return (byte)(0x20 | (Math.Min(ms / 10, 0x0F) & 0x0F));
    }

    public override string ToString()
    {
        return $"BS={BlockSize}, T1={T1Ms}ms, T3={T3Ms}ms";
    }
}
