using System.Globalization;
using Avalonia.Media;
using BitFab.KW1281Test.Ui.Converters;

namespace BitFab.KW1281Test.Ui.Tests;

[TestClass]
public class HexValueConverterTests
{
    private readonly HexValueConverter _converter = HexValueConverter.Instance;
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    [TestMethod]
    public void Convert_Byte_ReturnsHex2()
    {
        var result = _converter.Convert((byte)0xAB, typeof(string), null, _culture);
        Assert.AreEqual("0xAB", result);
    }

    [TestMethod]
    public void Convert_Byte_Zero()
    {
        var result = _converter.Convert((byte)0, typeof(string), null, _culture);
        Assert.AreEqual("0x00", result);
    }

    [TestMethod]
    public void Convert_UShort_ReturnsHex4()
    {
        var result = _converter.Convert((ushort)0x1234, typeof(string), null, _culture);
        Assert.AreEqual("0x1234", result);
    }

    [TestMethod]
    public void Convert_UInt_ReturnsHex4()
    {
        var result = _converter.Convert((uint)0xBEEF, typeof(string), null, _culture);
        Assert.AreEqual("0xBEEF", result);
    }

    [TestMethod]
    public void Convert_Int_ReturnsHex()
    {
        var result = _converter.Convert(255, typeof(string), null, _culture);
        Assert.AreEqual("0xFF", result);
    }

    [TestMethod]
    public void Convert_Null_ReturnsNull()
    {
        var result = _converter.Convert(null, typeof(string), null, _culture);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ConvertBack_HexString_ToByte()
    {
        var result = _converter.ConvertBack("0xAB", typeof(byte), null, _culture);
        Assert.AreEqual((byte)0xAB, result);
    }

    [TestMethod]
    public void ConvertBack_HexString_ToUShort()
    {
        var result = _converter.ConvertBack("0x1234", typeof(ushort), null, _culture);
        Assert.AreEqual((ushort)0x1234, result);
    }

    [TestMethod]
    public void ConvertBack_HexString_ToUInt()
    {
        var result = _converter.ConvertBack("0xBEEF", typeof(uint), null, _culture);
        Assert.AreEqual((uint)0xBEEF, result);
    }

    [TestMethod]
    public void ConvertBack_HexString_ToInt()
    {
        var result = _converter.ConvertBack("0xFF", typeof(int), null, _culture);
        Assert.AreEqual(255, result);
    }

    [TestMethod]
    public void ConvertBack_LowerCase_ToByte()
    {
        var result = _converter.ConvertBack("0xab", typeof(byte), null, _culture);
        Assert.AreEqual((byte)0xAB, result);
    }

    [TestMethod]
    public void ConvertBack_NonHex_ReturnsOriginal()
    {
        var result = _converter.ConvertBack("hello", typeof(byte), null, _culture);
        Assert.AreEqual("hello", result);
    }

    [TestMethod]
    public void ConvertBack_Null_ReturnsNull()
    {
        var result = _converter.ConvertBack(null, typeof(byte), null, _culture);
        Assert.IsNull(result);
    }
}

[TestClass]
public class LogLevelToBrushConverterTests
{
    private readonly LogLevelToBrushConverter _converter = LogLevelToBrushConverter.Instance;
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    [TestMethod]
    [DataRow("TX: 06 01 09")]
    [DataRow("> TX: data")]
    [DataRow("Sending data")]
    public void Convert_TxMessage_ReturnsBlueBrush(string msg)
    {
        var result = _converter.Convert(msg, typeof(IBrush), null, _culture) as SolidColorBrush;
        Assert.IsNotNull(result);
        Assert.AreEqual(0x33, result.Color.R);
        Assert.AreEqual(0x99, result.Color.G);
        Assert.AreEqual(0xFF, result.Color.B);
    }

    [TestMethod]
    [DataRow("RX: 0F 01 FC")]
    [DataRow("< RX: response")]
    [DataRow("Received data")]
    public void Convert_RxMessage_ReturnsGreenBrush(string msg)
    {
        var result = _converter.Convert(msg, typeof(IBrush), null, _culture) as SolidColorBrush;
        Assert.IsNotNull(result);
        Assert.AreEqual(0x33, result.Color.R);
        Assert.AreEqual(0xCC, result.Color.G);
        Assert.AreEqual(0x66, result.Color.B);
    }

    [TestMethod]
    [DataRow("Error: timeout")]
    [DataRow("Failed to connect")]
    [DataRow("Connection error occurred")]
    public void Convert_ErrorMessage_ReturnsRedBrush(string msg)
    {
        var result = _converter.Convert(msg, typeof(IBrush), null, _culture) as SolidColorBrush;
        Assert.IsNotNull(result);
        Assert.AreEqual(0xFF, result.Color.R);
        Assert.AreEqual(0x44, result.Color.G);
        Assert.AreEqual(0x44, result.Color.B);
    }

    [TestMethod]
    public void Convert_NormalMessage_ReturnsWhiteBrush()
    {
        var result = _converter.Convert("Connected to ECU", typeof(IBrush), null, _culture);
        Assert.AreEqual(Brushes.White, result);
    }

    [TestMethod]
    public void Convert_Null_ReturnsWhiteBrush()
    {
        var result = _converter.Convert(null, typeof(IBrush), null, _culture);
        Assert.AreEqual(Brushes.White, result);
    }
}

[TestClass]
public class BoolToColorConverterTests
{
    private readonly BoolToColorConverter _converter = BoolToColorConverter.Instance;
    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    [TestMethod]
    public void Convert_True_ReturnsGreenBrush()
    {
        var result = _converter.Convert(true, typeof(IBrush), null, _culture) as SolidColorBrush;
        Assert.IsNotNull(result);
        Assert.AreEqual(0x33, result.Color.R);
        Assert.AreEqual(0xCC, result.Color.G);
        Assert.AreEqual(0x66, result.Color.B);
    }

    [TestMethod]
    public void Convert_False_ReturnsRedBrush()
    {
        var result = _converter.Convert(false, typeof(IBrush), null, _culture) as SolidColorBrush;
        Assert.IsNotNull(result);
        Assert.AreEqual(0xFF, result.Color.R);
        Assert.AreEqual(0x44, result.Color.G);
        Assert.AreEqual(0x44, result.Color.B);
    }

    [TestMethod]
    public void Convert_Null_ReturnsRedBrush()
    {
        var result = _converter.Convert(null, typeof(IBrush), null, _culture) as SolidColorBrush;
        Assert.IsNotNull(result);
        Assert.AreEqual(0xFF, result.Color.R);
    }
}
