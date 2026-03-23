using BitFab.KW1281Test.Ui.Services;
using BitFab.KW1281Test.Ui.ViewModels.Can;

namespace BitFab.KW1281Test.Ui.Tests;

[TestClass]
public class CanMonitorViewModelTests
{
    [TestMethod]
    public void Initial_State()
    {
        using var svc = new ConnectionService();
        var vm = new CanMonitorViewModel(svc);

        Assert.IsFalse(vm.IsMonitoring);
        Assert.AreEqual(0, vm.MessageCount);
        Assert.AreEqual("Ready.", vm.StatusText);
    }

    [TestMethod]
    public void CanStart_WhenDisconnected_ReturnsFalse()
    {
        using var svc = new ConnectionService();
        var vm = new CanMonitorViewModel(svc);
        Assert.IsFalse(vm.StartCommand.CanExecute(null));
    }
}

[TestClass]
public class CanAutoScanViewModelTests
{
    [TestMethod]
    public void Initial_State()
    {
        using var svc = new ConnectionService();
        var vm = new CanAutoScanViewModel(svc);

        Assert.IsFalse(vm.IsScanning);
        Assert.AreEqual(0, vm.Progress);
        Assert.AreEqual("Ready.", vm.StatusText);
        Assert.AreEqual(0, vm.Results.Count);
    }

    [TestMethod]
    public void CanStart_WhenDisconnected_ReturnsFalse()
    {
        using var svc = new ConnectionService();
        var vm = new CanAutoScanViewModel(svc);
        Assert.IsFalse(vm.StartCommand.CanExecute(null));
    }
}

[TestClass]
public class CanDiagViewModelTests
{
    [TestMethod]
    public void Initial_State()
    {
        using var svc = new ConnectionService();
        var vm = new CanDiagViewModel(svc);

        Assert.IsFalse(vm.IsBusy);
        Assert.AreEqual((byte)0x01, vm.ControllerAddress);
        Assert.AreEqual("KWP2000", vm.Protocol);
        Assert.AreEqual("Ready.", vm.StatusText);
    }

    [TestMethod]
    public void CanConnect_WhenDisconnected_ReturnsFalse()
    {
        using var svc = new ConnectionService();
        var vm = new CanDiagViewModel(svc);
        Assert.IsFalse(vm.ConnectCommand.CanExecute(null));
    }
}

[TestClass]
public class CanMultiEcuViewModelTests
{
    [TestMethod]
    public void Initial_State()
    {
        using var svc = new ConnectionService();
        var vm = new CanMultiEcuViewModel(svc);

        Assert.IsFalse(vm.IsBusy);
        Assert.AreEqual("Ready.", vm.StatusText);
        Assert.IsTrue(vm.Modules.Count > 0);
        Assert.AreEqual(0, vm.Results.Count);
    }

    [TestMethod]
    public void DefaultModules_ContainsExpectedEcus()
    {
        using var svc = new ConnectionService();
        var vm = new CanMultiEcuViewModel(svc);

        // Should have pre-seeded ECU entries
        Assert.IsTrue(vm.Modules.Count >= 5);
    }

    [TestMethod]
    public void CanScan_WhenDisconnected_ReturnsFalse()
    {
        using var svc = new ConnectionService();
        var vm = new CanMultiEcuViewModel(svc);
        Assert.IsFalse(vm.ScanCommand.CanExecute(null));
    }
}
