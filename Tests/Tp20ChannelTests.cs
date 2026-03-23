using BitFab.KW1281Test.Kwp2000;

namespace BitFab.KW1281Test.Tests;

[TestClass]
public class Tp20ChannelTests
{
    // --- PadTo8 ---

    [TestMethod]
    public void PadTo8_ShortArray_PadsWithZeros()
    {
        var result = Tp20Channel.PadTo8(new byte[] { 0xA0, 0x0F });
        Assert.HasCount(8, result);
        Assert.AreEqual(0xA0, result[0]);
        Assert.AreEqual(0x0F, result[1]);
        for (int i = 2; i < 8; i++)
        {
            Assert.AreEqual(0x00, result[i], $"Expected 0 at index {i}");
        }
    }

    [TestMethod]
    public void PadTo8_ExactLength_ReturnsOriginal()
    {
        var input = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var result = Tp20Channel.PadTo8(input);
        Assert.AreSame(input, result);
    }

    [TestMethod]
    public void PadTo8_EmptyArray_Returns8Zeros()
    {
        var result = Tp20Channel.PadTo8(Array.Empty<byte>());
        Assert.HasCount(8, result);
        CollectionAssert.AreEqual(new byte[8], result);
    }

    [TestMethod]
    public void PadTo8_SingleByte_PadsWith7Zeros()
    {
        var result = Tp20Channel.PadTo8(new byte[] { 0xC0 });
        Assert.HasCount(8, result);
        Assert.AreEqual(0xC0, result[0]);
        Assert.AreEqual(0x00, result[7]);
    }

    [TestMethod]
    public void PadTo8_LongerThan8_ReturnsOriginal()
    {
        // Edge case: array > 8 bytes (shouldn't happen in CAN 2.0 but test the guard)
        var input = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var result = Tp20Channel.PadTo8(input);
        Assert.AreSame(input, result);
    }
}
