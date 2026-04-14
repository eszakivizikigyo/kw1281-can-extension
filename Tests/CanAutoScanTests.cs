namespace BitFab.KW1281Test.Tests;

[TestClass]
public class CanAutoScanTests
{
    // --- GetControllerName ---

    [TestMethod]
    public void GetControllerName_KnownAddress_ReturnsEnumName()
    {
        Assert.AreEqual("Ecu", ControllerAddressExtensions.GetControllerName(0x01));
    }

    [TestMethod]
    public void GetControllerName_Cluster_ReturnsCluster()
    {
        Assert.AreEqual("Cluster", ControllerAddressExtensions.GetControllerName(0x17));
    }

    [TestMethod]
    public void GetControllerName_CanGateway_ReturnsCanGateway()
    {
        Assert.AreEqual("CanGateway", ControllerAddressExtensions.GetControllerName(0x19));
    }

    [TestMethod]
    public void GetControllerName_Radio_ReturnsRadio()
    {
        Assert.AreEqual("Radio", ControllerAddressExtensions.GetControllerName(0x56));
    }

    [TestMethod]
    public void GetControllerName_UnknownAddress_ReturnsModuleHex()
    {
        Assert.AreEqual("Module 0x42", ControllerAddressExtensions.GetControllerName(0x42));
    }

    [TestMethod]
    public void GetControllerName_AllKnownAddresses_ReturnNonEmptyNames()
    {
        byte[] knownAddresses = { 0x01, 0x02, 0x03, 0x04, 0x08, 0x09, 0x0B, 0x15, 0x17, 0x18, 0x19, 0x25, 0x35, 0x37, 0x46, 0x56, 0x69, 0x6E, 0x7C };
        foreach (var address in knownAddresses)
        {
            var name = ControllerAddressExtensions.GetControllerName(address);
            Assert.IsFalse(string.IsNullOrEmpty(name),
                $"GetControllerName(0x{address:X2}) returned null or empty");
            Assert.DoesNotStartWith(name, "Module",
                $"GetControllerName(0x{address:X2}) returned generic name: {name}");
        }
    }

    [TestMethod]
    public void GetControllerName_AddressZero_ReturnsModuleHex()
    {
        // Address 0x00 is not in ControllerAddress enum
        Assert.AreEqual("Module 0x00", ControllerAddressExtensions.GetControllerName(0x00));
    }

    [TestMethod]
    public void GetControllerName_MaxAddress_ReturnsModuleHex()
    {
        Assert.AreEqual("Module 0x7F", ControllerAddressExtensions.GetControllerName(0x7F));
    }
}
