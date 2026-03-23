using BitFab.KW1281Test.Interface;

namespace BitFab.KW1281Test.Tests;

[TestClass]
public class CanInterfaceTests
{
    // --- ParseCanMessage: space-separated format (ATS1 mode) ---

    [TestMethod]
    public void ParseCanMessage_SpaceSeparated_StandardId()
    {
        var msg = CanInterface.ParseCanMessage("7E8 06 41 00 BE 3E B8 13");
        Assert.IsNotNull(msg);
        Assert.AreEqual(0x7E8u, msg.Id);
        Assert.IsFalse(msg.IsExtended);
        Assert.AreEqual(7, msg.DataLength);
        CollectionAssert.AreEqual(new byte[] { 0x06, 0x41, 0x00, 0xBE, 0x3E, 0xB8, 0x13 }, msg.Data);
    }

    [TestMethod]
    public void ParseCanMessage_SpaceSeparated_WithAllDataBytes()
    {
        // ELM327 returns all data bytes including PCI length byte
        var msg = CanInterface.ParseCanMessage("7E8 06 41 00 BE 3E B8 13");
        Assert.IsNotNull(msg);
        Assert.AreEqual(0x7E8u, msg.Id);
        Assert.AreEqual(7, msg.DataLength);
    }

    [TestMethod]
    public void ParseCanMessage_SpaceSeparated_IdOnly()
    {
        var msg = CanInterface.ParseCanMessage("7DF");
        Assert.IsNotNull(msg);
        Assert.AreEqual(0x7DFu, msg.Id);
        Assert.AreEqual(0, msg.DataLength);
    }

    // --- ParseCanMessage: compact format (ATS0 mode) ---

    [TestMethod]
    public void ParseCanMessage_Compact_StandardId()
    {
        // "7E8064100BE3EB813" = ID:7E8, Data: 06 41 00 BE 3E B8 13
        var msg = CanInterface.ParseCanMessage("7E8064100BE3EB813");
        Assert.IsNotNull(msg);
        Assert.AreEqual(0x7E8u, msg.Id);
        Assert.IsFalse(msg.IsExtended);
        Assert.AreEqual(7, msg.DataLength);
        CollectionAssert.AreEqual(
            new byte[] { 0x06, 0x41, 0x00, 0xBE, 0x3E, 0xB8, 0x13 }, msg.Data);
    }

    [TestMethod]
    public void ParseCanMessage_Compact_SingleDataByte()
    {
        // "7E8FF" = ID:7E8, Data: FF
        var msg = CanInterface.ParseCanMessage("7E8FF");
        Assert.IsNotNull(msg);
        Assert.AreEqual(0x7E8u, msg.Id);
        Assert.AreEqual(1, msg.DataLength);
        CollectionAssert.AreEqual(new byte[] { 0xFF }, msg.Data);
    }

    [TestMethod]
    public void ParseCanMessage_Compact_NoData()
    {
        // "7E8" = ID only, 3 chars, (3-3)%2==0 → valid, 0 data bytes
        var msg = CanInterface.ParseCanMessage("7E8");
        Assert.IsNotNull(msg);
        Assert.AreEqual(0x7E8u, msg.Id);
        Assert.AreEqual(0, msg.DataLength);
    }

    // --- ParseCanMessage: edge cases ---

    [TestMethod]
    public void ParseCanMessage_Null_ReturnsNull()
    {
        Assert.IsNull(CanInterface.ParseCanMessage(null!));
    }

    [TestMethod]
    public void ParseCanMessage_Empty_ReturnsNull()
    {
        Assert.IsNull(CanInterface.ParseCanMessage(""));
    }

    [TestMethod]
    public void ParseCanMessage_Whitespace_ReturnsNull()
    {
        Assert.IsNull(CanInterface.ParseCanMessage("   "));
    }

    [TestMethod]
    public void ParseCanMessage_InvalidHex_ReturnsNull()
    {
        Assert.IsNull(CanInterface.ParseCanMessage("XYZ"));
    }

    [TestMethod]
    public void ParseCanMessage_SpaceSeparated_SkipsNonHexParts()
    {
        // If a part isn't exactly 2 hex chars it gets skipped
        var msg = CanInterface.ParseCanMessage("7E8 06 41 ABC 00");
        Assert.IsNotNull(msg);
        Assert.AreEqual(0x7E8u, msg.Id);
        // "ABC" is 3 chars → skipped, so data bytes: 06, 41, 00
        CollectionAssert.AreEqual(new byte[] { 0x06, 0x41, 0x00 }, msg.Data);
    }

    // --- ParseCanMessage: OBD-II typical responses ---

    [TestMethod]
    public void ParseCanMessage_ObdResponse_EngineRPM()
    {
        // Typical response: Mode 01 PID 0C (engine RPM)
        var msg = CanInterface.ParseCanMessage("7E8 04 41 0C 1A F8");
        Assert.IsNotNull(msg);
        Assert.AreEqual(0x7E8u, msg.Id);
        CollectionAssert.AreEqual(new byte[] { 0x04, 0x41, 0x0C, 0x1A, 0xF8 }, msg.Data);
    }

    [TestMethod]
    public void ParseCanMessage_Compact_ObdResponse()
    {
        // Same as above in compact format
        var msg = CanInterface.ParseCanMessage("7E804410C1AF8");
        Assert.IsNotNull(msg);
        Assert.AreEqual(0x7E8u, msg.Id);
        CollectionAssert.AreEqual(new byte[] { 0x04, 0x41, 0x0C, 0x1A, 0xF8 }, msg.Data);
    }
}
