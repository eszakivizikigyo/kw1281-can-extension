using BitFab.KW1281Test.Interface;

namespace BitFab.KW1281Test.Tests;

[TestClass]
public class CanMessageTests
{
    // --- Constructor validation ---

    [TestMethod]
    public void Constructor_StandardId_MaxValid()
    {
        var msg = new CanMessage(0x7FF, new byte[] { 0x01 });
        Assert.AreEqual(0x7FFu, msg.Id);
        Assert.IsFalse(msg.IsExtended);
    }

    [TestMethod]
    public void Constructor_StandardId_ExceedsMax_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CanMessage(0x800, new byte[] { 0x01 }));
    }

    [TestMethod]
    public void Constructor_ExtendedId_MaxValid()
    {
        var msg = new CanMessage(0x1FFFFFFF, new byte[] { 0x01 }, isExtended: true);
        Assert.AreEqual(0x1FFFFFFFu, msg.Id);
        Assert.IsTrue(msg.IsExtended);
    }

    [TestMethod]
    public void Constructor_ExtendedId_ExceedsMax_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CanMessage(0x20000000, new byte[] { 0x01 }, isExtended: true));
    }

    [TestMethod]
    public void Constructor_DataExceeds8Bytes_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CanMessage(0x100, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
    }

    [TestMethod]
    public void Constructor_NullData_DefaultsToEmpty()
    {
        var msg = new CanMessage(0x100, null!);
        Assert.AreEqual(0, msg.DataLength);
    }

    [TestMethod]
    public void Constructor_EmptyData_IsValid()
    {
        var msg = new CanMessage(0x7DF, Array.Empty<byte>());
        Assert.AreEqual(0, msg.DataLength);
    }

    [TestMethod]
    public void Constructor_Default_HasEmptyData()
    {
        var msg = new CanMessage();
        Assert.AreEqual(0u, msg.Id);
        Assert.IsFalse(msg.IsExtended);
        Assert.AreEqual(0, msg.DataLength);
    }

    // --- DataLength ---

    [TestMethod]
    public void DataLength_ReturnsCorrectCount()
    {
        var msg = new CanMessage(0x7DF, new byte[] { 0x02, 0x01, 0x00 });
        Assert.AreEqual(3, msg.DataLength);
    }

    // --- ToString ---

    [TestMethod]
    public void ToString_StandardId_FormatsCorrectly()
    {
        var msg = new CanMessage(0x7DF, new byte[] { 0x02, 0x01, 0x00 });
        Assert.AreEqual("CAN ID: 7DF [3] 02 01 00", msg.ToString());
    }

    [TestMethod]
    public void ToString_ExtendedId_FormatsCorrectly()
    {
        var msg = new CanMessage(0x18DAF110, new byte[] { 0x02, 0x01, 0x00 }, isExtended: true);
        Assert.AreEqual("CAN ID: 18DAF110 [3] 02 01 00", msg.ToString());
    }

    [TestMethod]
    public void ToString_EmptyData_FormatsCorrectly()
    {
        var msg = new CanMessage(0x000, Array.Empty<byte>());
        Assert.AreEqual("CAN ID: 000 [0] ", msg.ToString());
    }
}
