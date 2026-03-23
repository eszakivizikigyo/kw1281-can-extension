using BitFab.KW1281Test.Kwp2000;

namespace BitFab.KW1281Test.Tests;

[TestClass]
public class Tp20TypesTests
{
    // --- DecodeTimingByte ---

    [TestMethod]
    [DataRow(0x10, 0)]   // 1ms * 0 = 0ms
    [DataRow(0x11, 1)]   // 1ms * 1 = 1ms
    [DataRow(0x1F, 15)]  // 1ms * 15 = 15ms
    [DataRow(0x21, 10)]  // 10ms * 1 = 10ms
    [DataRow(0x2A, 100)] // 10ms * 10 = 100ms
    [DataRow(0x31, 100)] // 100ms * 1 = 100ms
    [DataRow(0x33, 300)] // 100ms * 3 = 300ms
    public void DecodeTimingByte_ReturnsExpectedMs(int input, int expectedMs)
    {
        Assert.AreEqual(expectedMs, Tp20ChannelParameters.DecodeTimingByte((byte)input));
    }

    [TestMethod]
    [DataRow(0x00, 0)]   // unit 0, value 0 = 0ms
    [DataRow(0x01, 1)]   // unit 0, value 1 = 1ms (0.1ms units, min 1)
    [DataRow(0x0F, 15)]  // unit 0, value 15 = 15ms
    [DataRow(0x2F, 150)] // 10ms * 15 = 150ms
    [DataRow(0x3F, 1500)] // 100ms * 15 = 1500ms
    public void DecodeTimingByte_AllUnits(int input, int expectedMs)
    {
        Assert.AreEqual(expectedMs, Tp20ChannelParameters.DecodeTimingByte((byte)input));
    }

    [TestMethod]
    [DataRow(0x40)] // Unknown unit 4
    [DataRow(0xF1)] // Unknown unit 15
    public void DecodeTimingByte_UnknownUnit_FallsBackToValue(int input)
    {
        var expected = input & 0x0F;
        Assert.AreEqual(expected, Tp20ChannelParameters.DecodeTimingByte((byte)input));
    }

    // --- EncodeTimingByte ---

    [TestMethod]
    [DataRow(1, 0x11)]   // 1ms → 1ms unit, value 1
    [DataRow(5, 0x15)]   // 5ms → 1ms unit, value 5
    [DataRow(15, 0x1F)]  // 15ms → 1ms unit, value 15
    [DataRow(10, 0x21)]  // 10ms → 10ms unit, value 1
    [DataRow(50, 0x25)]  // 50ms → 10ms unit, value 5
    [DataRow(100, 0x2A)] // 100ms → 10ms unit, value 10
    [DataRow(200, 0x32)] // 200ms → 100ms unit, value 2
    [DataRow(500, 0x35)] // 500ms → 100ms unit, value 5
    public void EncodeTimingByte_ReturnsExpectedByte(int inputMs, int expected)
    {
        Assert.AreEqual((byte)expected, Tp20ChannelParameters.EncodeTimingByte(inputMs));
    }

    [TestMethod]
    public void EncodeTimingByte_ZeroMs_Returns1msUnit0()
    {
        var result = Tp20ChannelParameters.EncodeTimingByte(0);
        Assert.AreEqual(0x10, result); // 1ms * 0
    }

    [TestMethod]
    public void EncodeTimingByte_NegativeMs_Returns1msUnit0()
    {
        var result = Tp20ChannelParameters.EncodeTimingByte(-5);
        Assert.AreEqual(0x10, result);
    }

    [TestMethod]
    [DataRow(20, 0x22)]   // 20ms → 10ms * 2
    [DataRow(30, 0x23)]   // 30ms → 10ms * 3
    [DataRow(150, 0x2F)]  // 150ms → 10ms * 15
    public void EncodeTimingByte_10msMultiples(int inputMs, int expected)
    {
        Assert.AreEqual((byte)expected, Tp20ChannelParameters.EncodeTimingByte(inputMs));
    }

    [TestMethod]
    [DataRow(300, 0x33)]   // 300ms → 100ms * 3
    [DataRow(1000, 0x3A)]  // 1000ms → 100ms * 10
    [DataRow(1500, 0x3F)]  // 1500ms → 100ms * 15
    public void EncodeTimingByte_100msMultiples(int inputMs, int expected)
    {
        Assert.AreEqual((byte)expected, Tp20ChannelParameters.EncodeTimingByte(inputMs));
    }

    // --- Roundtrip ---

    [TestMethod]
    [DataRow(1)]
    [DataRow(10)]
    [DataRow(50)]
    [DataRow(100)]
    [DataRow(500)]
    public void EncodeDecodeTiming_Roundtrip(int ms)
    {
        var encoded = Tp20ChannelParameters.EncodeTimingByte(ms);
        var decoded = Tp20ChannelParameters.DecodeTimingByte(encoded);
        Assert.AreEqual(ms, decoded);
    }

    [TestMethod]
    [DataRow(20)]
    [DataRow(150)]
    [DataRow(300)]
    [DataRow(1000)]
    [DataRow(1500)]
    public void EncodeDecodeTiming_Roundtrip_Extended(int ms)
    {
        var encoded = Tp20ChannelParameters.EncodeTimingByte(ms);
        var decoded = Tp20ChannelParameters.DecodeTimingByte(encoded);
        Assert.AreEqual(ms, decoded);
    }

    // --- ToString ---

    [TestMethod]
    public void ChannelParameters_ToString()
    {
        var p = new Tp20ChannelParameters { BlockSize = 15, T1Ms = 100, T3Ms = 10 };
        Assert.AreEqual("BS=15, T1=100ms, T3=10ms", p.ToString());
    }

    [TestMethod]
    public void ChannelParameters_DefaultValues()
    {
        var p = new Tp20ChannelParameters();
        Assert.AreEqual(0, p.BlockSize);
        Assert.AreEqual(0, p.T1Ms);
        Assert.AreEqual(0, p.T3Ms);
    }

    // --- Tp20OpCode enum values ---

    [TestMethod]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Test", "MSTEST0025")]
    public void Tp20OpCode_Values()
    {
        Assert.AreEqual(Tp20OpCode.ChannelSetupRequest, (Tp20OpCode)0xC0);
        Assert.AreEqual(Tp20OpCode.ChannelSetupResponse, (Tp20OpCode)0xD0);
        Assert.AreEqual(Tp20OpCode.ChannelParametersRequest, (Tp20OpCode)0xA0);
        Assert.AreEqual(Tp20OpCode.ChannelParametersResponse, (Tp20OpCode)0xA1);
        Assert.AreEqual(Tp20OpCode.ConnectionTest, (Tp20OpCode)0xA3);
        Assert.AreEqual(Tp20OpCode.Disconnect, (Tp20OpCode)0xA8);
    }

    // --- Tp20FrameType enum values ---

    [TestMethod]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Test", "MSTEST0025")]
    public void Tp20FrameType_Values()
    {
        Assert.AreEqual(Tp20FrameType.WaitingForAck_MoreToFollow, (Tp20FrameType)0x0);
        Assert.AreEqual(Tp20FrameType.WaitingForAck_LastPacket, (Tp20FrameType)0x1);
        Assert.AreEqual(Tp20FrameType.NotWaitingForAck_MoreToFollow, (Tp20FrameType)0x2);
        Assert.AreEqual(Tp20FrameType.NotWaitingForAck_LastPacket, (Tp20FrameType)0x3);
        Assert.AreEqual(Tp20FrameType.Ack_NotReady, (Tp20FrameType)0x9);
        Assert.AreEqual(Tp20FrameType.Ack_Ready, (Tp20FrameType)0xB);
    }
}
