using BitFab.KW1281Test.Ui.Services;
using BitFab.KW1281Test.Ui.ViewModels;

namespace BitFab.KW1281Test.Ui.Tests;

[TestClass]
public class ConnectionViewModelTests
{
    [TestMethod]
    public void Initial_State()
    {
        using var svc = new ConnectionService();
        var settings = new AppSettings();
        var vm = new ConnectionViewModel(svc, settings);

        Assert.IsFalse(vm.IsConnected);
        Assert.IsFalse(vm.IsBusy);
        Assert.AreEqual("Disconnected", vm.StatusText);
        Assert.AreEqual(10400, vm.BaudRate);
        Assert.AreEqual(ConnectionMode.KLine, vm.Mode);
        Assert.AreEqual((byte)0x17, vm.ControllerAddress);
    }

    [TestMethod]
    public void Settings_AppliedFromConstructor()
    {
        using var svc = new ConnectionService();
        var settings = new AppSettings
        {
            BaudRate = 9600,
            Mode = "Can",
            ControllerAddress = 0x01,
        };
        var vm = new ConnectionViewModel(svc, settings);

        Assert.AreEqual(9600, vm.BaudRate);
        Assert.AreEqual(ConnectionMode.Can, vm.Mode);
        Assert.AreEqual((byte)0x01, vm.ControllerAddress);
    }

    [TestMethod]
    public void CanConnect_WhenDisconnected_NeedsPort()
    {
        using var svc = new ConnectionService();
        var vm = new ConnectionViewModel(svc, new AppSettings());

        // No port selected = can't connect
        vm.SelectedPort = null;
        Assert.IsFalse(vm.ConnectCommand.CanExecute(null));
    }

    [TestMethod]
    public void CanDisconnect_WhenDisconnected_ReturnsFalse()
    {
        using var svc = new ConnectionService();
        var vm = new ConnectionViewModel(svc, new AppSettings());

        Assert.IsFalse(vm.DisconnectCommand.CanExecute(null));
    }

    [TestMethod]
    public void IsKLineMode_ReflectsMode()
    {
        using var svc = new ConnectionService();
        var vm = new ConnectionViewModel(svc, new AppSettings());

        vm.Mode = ConnectionMode.KLine;
        Assert.IsTrue(vm.IsKLineMode);
        Assert.IsFalse(vm.IsCanMode);

        vm.Mode = ConnectionMode.Can;
        Assert.IsFalse(vm.IsKLineMode);
        Assert.IsTrue(vm.IsCanMode);
    }

    [TestMethod]
    public void SaveSettings_PersistsValues()
    {
        using var svc = new ConnectionService();
        var vm = new ConnectionViewModel(svc, new AppSettings());

        vm.SelectedPort = "COM5";
        vm.BaudRate = 9600;
        vm.Mode = ConnectionMode.Can;
        vm.ControllerAddress = 0x01;

        var settings = new AppSettings();
        vm.SaveSettings(settings);

        Assert.AreEqual("COM5", settings.LastPort);
        Assert.AreEqual(9600, settings.BaudRate);
        Assert.AreEqual("Can", settings.Mode);
        Assert.AreEqual((byte)0x01, settings.ControllerAddress);
    }

    [TestMethod]
    public void RefreshPorts_DoesNotThrow()
    {
        using var svc = new ConnectionService();
        var vm = new ConnectionViewModel(svc, new AppSettings());

        // Should not throw even with no serial ports
        vm.RefreshPortsCommand.Execute(null);
    }
}
