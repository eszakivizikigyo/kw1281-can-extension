using BitFab.KW1281Test.Ui.ViewModels.Shared;

namespace BitFab.KW1281Test.Ui.Tests;

[TestClass]
public class HexEditorViewModelTests
{
    [TestMethod]
    public void Initial_State_NoDataLoaded()
    {
        var vm = new HexEditorViewModel();
        Assert.AreEqual("No data loaded.", vm.StatusText);
        Assert.AreEqual(0, vm.Rows.Count);
    }

    [TestMethod]
    public void LoadData_SingleRow_CreatesOneRow()
    {
        var vm = new HexEditorViewModel();
        var data = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        vm.LoadData(data);

        Assert.AreEqual(1, vm.Rows.Count);
        Assert.AreEqual("4 bytes loaded.", vm.StatusText);
    }

    [TestMethod]
    public void LoadData_ExactlyOneRow_16Bytes()
    {
        var vm = new HexEditorViewModel();
        var data = new byte[16];
        for (var i = 0; i < 16; i++) data[i] = (byte)i;

        vm.LoadData(data);

        Assert.AreEqual(1, vm.Rows.Count);
        Assert.AreEqual("16 bytes loaded.", vm.StatusText);
    }

    [TestMethod]
    public void LoadData_TwoRows_17Bytes()
    {
        var vm = new HexEditorViewModel();
        vm.LoadData(new byte[17]);
        Assert.AreEqual(2, vm.Rows.Count);
    }

    [TestMethod]
    public void LoadData_256Bytes_Creates16Rows()
    {
        var vm = new HexEditorViewModel();
        vm.LoadData(new byte[256]);
        Assert.AreEqual(16, vm.Rows.Count);
        Assert.AreEqual("256 bytes loaded.", vm.StatusText);
    }

    [TestMethod]
    public void LoadData_BaseAddress_AppliedToRows()
    {
        var vm = new HexEditorViewModel();
        vm.LoadData(new byte[32], baseAddress: 0x1000);

        Assert.AreEqual(2, vm.Rows.Count);
        Assert.AreEqual((uint)0x1000, vm.Rows[0].Address);
        Assert.AreEqual("00001000", vm.Rows[0].AddressText);
        Assert.AreEqual((uint)0x1010, vm.Rows[1].Address);
        Assert.AreEqual("00001010", vm.Rows[1].AddressText);
    }

    [TestMethod]
    public void LoadData_ClearsPreviousData()
    {
        var vm = new HexEditorViewModel();
        vm.LoadData(new byte[32]);
        Assert.AreEqual(2, vm.Rows.Count);

        vm.LoadData(new byte[16]);
        Assert.AreEqual(1, vm.Rows.Count);
    }

    [TestMethod]
    public void Clear_RemovesAllRows()
    {
        var vm = new HexEditorViewModel();
        vm.LoadData(new byte[32]);
        Assert.AreEqual(2, vm.Rows.Count);

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(0, vm.Rows.Count);
        Assert.AreEqual("No data loaded.", vm.StatusText);
    }

    [TestMethod]
    public void LoadData_EmptyArray_NoRows()
    {
        var vm = new HexEditorViewModel();
        vm.LoadData([]);
        Assert.AreEqual(0, vm.Rows.Count);
        Assert.AreEqual("0 bytes loaded.", vm.StatusText);
    }

    [TestMethod]
    public void HexRow_AddressFormatted_8Digits()
    {
        var row = new HexRow(0x00FF, [0x41, 0x42]);
        Assert.AreEqual("000000FF", row.AddressText);
    }

    [TestMethod]
    public void HexRow_HexText_FormattedCorrectly()
    {
        var row = new HexRow(0, [0x41, 0x42, 0x43]);
        // 3 bytes + 13 empty slots with space separator at byte 7
        Assert.IsTrue(row.HexText.StartsWith("41 42 43"));
    }

    [TestMethod]
    public void HexRow_AsciiText_PrintableChars()
    {
        var row = new HexRow(0, [0x41, 0x42, 0x43]); // A, B, C
        Assert.IsTrue(row.AsciiText.StartsWith("ABC"));
    }

    [TestMethod]
    public void HexRow_AsciiText_NonPrintableReplacedWithDot()
    {
        var row = new HexRow(0, [0x00, 0x01, 0x7F]);
        Assert.IsTrue(row.AsciiText.StartsWith("..."));
    }

    [TestMethod]
    public void HexRow_FullRow_16Bytes()
    {
        var bytes = new byte[16];
        for (var i = 0; i < 16; i++) bytes[i] = (byte)(0x30 + i);
        var row = new HexRow(0, bytes);

        // Verify hex text contains all 16 bytes with space separator
        Assert.IsTrue(row.HexText.Contains("30 31 32 33 34 35 36 37"));
        Assert.IsTrue(row.HexText.Contains("38 39 3A 3B 3C 3D 3E 3F"));

        // ASCII for 0x30-0x3F = "0123456789:;<=>?"
        Assert.AreEqual("0123456789:;<=>?", row.AsciiText);
    }
}
