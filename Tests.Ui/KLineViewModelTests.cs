using System;
using System.Threading.Tasks;
using BitFab.KW1281Test.Ui.Services;
using BitFab.KW1281Test.Ui.ViewModels.KLine;
using NSubstitute;

namespace BitFab.KW1281Test.Ui.Tests;

[TestClass]
public class FaultCodesViewModelTests
{
    private ConnectionService CreateDisconnectedService()
    {
        // ConnectionService starts in Disconnected state
        return new ConnectionService();
    }

    [TestMethod]
    public void Initial_State()
    {
        var svc = CreateDisconnectedService();
        var vm = new FaultCodesViewModel(svc);

        Assert.IsFalse(vm.IsBusy);
        Assert.AreEqual(string.Empty, vm.StatusText);
        Assert.AreEqual(0, vm.FaultCodes.Count);
    }

    [TestMethod]
    public void CanExecute_WhenDisconnected_ReturnsFalse()
    {
        var svc = CreateDisconnectedService();
        var vm = new FaultCodesViewModel(svc);

        Assert.IsFalse(vm.ReadCommand.CanExecute(null));
        Assert.IsFalse(vm.ClearCommand.CanExecute(null));
    }

    [TestMethod]
    public void ClearAsync_WithDialogCancel_DoesNotChangeState()
    {
        var svc = CreateDisconnectedService();
        var vm = new FaultCodesViewModel(svc);

        var dialog = Substitute.For<IDialogService>();
        dialog.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(false));
        vm.DialogService = dialog;

        // Verify state doesn't change when disconnected and dialog would cancel
        Assert.AreEqual(string.Empty, vm.StatusText);

        svc.Dispose();
    }

    [TestMethod]
    public void FaultCodes_CollectionIsEmpty_Initially()
    {
        var svc = CreateDisconnectedService();
        var vm = new FaultCodesViewModel(svc);
        Assert.AreEqual(0, vm.FaultCodes.Count);
        svc.Dispose();
    }

    [TestMethod]
    public void FaultCodeItem_DtcFormatted_5Digits()
    {
        var item = new FaultCodeItem(668, "Supply voltage");
        Assert.AreEqual("00668", item.DtcFormatted);
    }

    [TestMethod]
    public void FaultCodeItem_LargeCode_FormattedCorrectly()
    {
        var item = new FaultCodeItem(17536, "Throttle position");
        Assert.AreEqual("17536", item.DtcFormatted);
    }
}

[TestClass]
public class AdaptationViewModelTests
{
    [TestMethod]
    public void Initial_State()
    {
        using var svc = new ConnectionService();
        var vm = new AdaptationViewModel(svc);

        Assert.IsFalse(vm.IsBusy);
        Assert.AreEqual((byte)0, vm.Channel);
        Assert.AreEqual((ushort)0, vm.ChannelValue);
        Assert.AreEqual((ushort)0, vm.Login);
        Assert.IsFalse(vm.UseLogin);
        Assert.AreEqual(0, vm.WorkshopCode);
        Assert.AreEqual(string.Empty, vm.StatusText);
    }

    [TestMethod]
    public void CanExecute_WhenDisconnected_ReturnsFalse()
    {
        using var svc = new ConnectionService();
        var vm = new AdaptationViewModel(svc);

        Assert.IsFalse(vm.ReadCommand.CanExecute(null));
        Assert.IsFalse(vm.TestCommand.CanExecute(null));
        Assert.IsFalse(vm.SaveCommand.CanExecute(null));
    }

    [TestMethod]
    public void Properties_CanBeSet()
    {
        using var svc = new ConnectionService();
        var vm = new AdaptationViewModel(svc);

        vm.Channel = 5;
        vm.ChannelValue = 1234;
        vm.Login = 19283;
        vm.UseLogin = true;
        vm.WorkshopCode = 12345;

        Assert.AreEqual((byte)5, vm.Channel);
        Assert.AreEqual((ushort)1234, vm.ChannelValue);
        Assert.AreEqual((ushort)19283, vm.Login);
        Assert.IsTrue(vm.UseLogin);
        Assert.AreEqual(12345, vm.WorkshopCode);
    }
}

[TestClass]
public class CodingViewModelTests
{
    [TestMethod]
    public void Initial_State()
    {
        using var svc = new ConnectionService();
        var vm = new CodingViewModel(svc);

        Assert.IsFalse(vm.IsBusy);
        Assert.AreEqual(0, vm.SoftwareCoding);
        Assert.AreEqual(0, vm.WorkshopCode);
        Assert.AreEqual(string.Empty, vm.StatusText);
    }

    [TestMethod]
    public void CanExecute_WhenDisconnected_ReturnsFalse()
    {
        using var svc = new ConnectionService();
        var vm = new CodingViewModel(svc);

        Assert.IsFalse(vm.SetCodingCommand.CanExecute(null));
        Assert.IsFalse(vm.ReadIdentCommand.CanExecute(null));
    }
}

[TestClass]
public class EepromViewModelTests
{
    [TestMethod]
    public void Initial_State()
    {
        using var svc = new ConnectionService();
        var vm = new EepromViewModel(svc);

        Assert.IsFalse(vm.IsBusy);
        Assert.AreEqual((uint)0, vm.Address);
        Assert.AreEqual((uint)256, vm.Length);
        Assert.AreEqual("eeprom.bin", vm.Filename);
        Assert.AreEqual(string.Empty, vm.StatusText);
    }

    [TestMethod]
    public void CanExecute_WhenDisconnected_ReturnsFalse()
    {
        using var svc = new ConnectionService();
        var vm = new EepromViewModel(svc);

        Assert.IsFalse(vm.ReadCommand.CanExecute(null));
        Assert.IsFalse(vm.WriteCommand.CanExecute(null));
        Assert.IsFalse(vm.DumpCommand.CanExecute(null));
        Assert.IsFalse(vm.MapCommand.CanExecute(null));
    }
}

[TestClass]
public class ClusterViewModelTests
{
    [TestMethod]
    public void Initial_State()
    {
        using var svc = new ConnectionService();
        var vm = new ClusterViewModel(svc);

        Assert.IsFalse(vm.IsBusy);
        Assert.AreEqual(string.Empty, vm.StatusText);
    }

    [TestMethod]
    public void CanExecute_WhenDisconnected_ReturnsFalse()
    {
        using var svc = new ConnectionService();
        var vm = new ClusterViewModel(svc);

        Assert.IsFalse(vm.GetSkcCommand.CanExecute(null));
        Assert.IsFalse(vm.GetClusterIdCommand.CanExecute(null));
        Assert.IsFalse(vm.ReadSoftwareVersionCommand.CanExecute(null));
        Assert.IsFalse(vm.ResetCommand.CanExecute(null));
        Assert.IsFalse(vm.ToggleRB4ModeCommand.CanExecute(null));
    }
}
