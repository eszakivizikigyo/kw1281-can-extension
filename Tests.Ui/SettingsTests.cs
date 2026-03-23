using System;
using System.IO;
using System.Text.Json;
using BitFab.KW1281Test.Ui.Services;

namespace BitFab.KW1281Test.Ui.Tests;

[TestClass]
public class AppSettingsTests
{
    [TestMethod]
    public void Default_Values()
    {
        var settings = new AppSettings();

        Assert.IsNull(settings.LastPort);
        Assert.AreEqual(10400, settings.BaudRate);
        Assert.AreEqual("KLine", settings.Mode);
        Assert.AreEqual((byte)0x17, settings.ControllerAddress);
        Assert.AreEqual("Default", settings.ThemeVariant);
        Assert.AreEqual(1100, settings.WindowWidth);
        Assert.AreEqual(700, settings.WindowHeight);
    }

    [TestMethod]
    public void Json_RoundTrip()
    {
        var original = new AppSettings
        {
            LastPort = "COM3",
            BaudRate = 9600,
            Mode = "Can",
            ControllerAddress = 0x01,
            ThemeVariant = "Dark",
            WindowWidth = 1200,
            WindowHeight = 800
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual("COM3", deserialized.LastPort);
        Assert.AreEqual(9600, deserialized.BaudRate);
        Assert.AreEqual("Can", deserialized.Mode);
        Assert.AreEqual((byte)0x01, deserialized.ControllerAddress);
        Assert.AreEqual("Dark", deserialized.ThemeVariant);
        Assert.AreEqual(1200, deserialized.WindowWidth);
        Assert.AreEqual(800, deserialized.WindowHeight);
    }

    [TestMethod]
    public void Deserialize_EmptyJson_ReturnsDefaults()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("{}");
        Assert.IsNotNull(settings);
        Assert.AreEqual(10400, settings.BaudRate);
        Assert.AreEqual("KLine", settings.Mode);
    }

    [TestMethod]
    public void Deserialize_PartialJson_PreservesDefaults()
    {
        var json = """{"LastPort":"COM5"}""";
        var settings = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.IsNotNull(settings);
        Assert.AreEqual("COM5", settings.LastPort);
        Assert.AreEqual(10400, settings.BaudRate); // default preserved
    }
}
