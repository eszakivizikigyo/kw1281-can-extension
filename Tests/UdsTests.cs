using BitFab.KW1281Test.Uds;
using System.Diagnostics.CodeAnalysis;

namespace BitFab.KW1281Test.Tests;

[TestClass]
[SuppressMessage("Test", "MSTEST0025")]
public class UdsTests
{
    // --- UdsService enum values ---

    [TestMethod]
    public void UdsService_DiagnosticSessionControl_Is0x10()
    {
        Assert.AreEqual((byte)0x10, (byte)UdsService.DiagnosticSessionControl);
    }

    [TestMethod]
    public void UdsService_ECUReset_Is0x11()
    {
        Assert.AreEqual((byte)0x11, (byte)UdsService.ECUReset);
    }

    [TestMethod]
    public void UdsService_ReadDataByIdentifier_Is0x22()
    {
        Assert.AreEqual((byte)0x22, (byte)UdsService.ReadDataByIdentifier);
    }

    [TestMethod]
    public void UdsService_SecurityAccess_Is0x27()
    {
        Assert.AreEqual((byte)0x27, (byte)UdsService.SecurityAccess);
    }

    [TestMethod]
    public void UdsService_TesterPresent_Is0x3E()
    {
        Assert.AreEqual((byte)0x3E, (byte)UdsService.TesterPresent);
    }

    [TestMethod]
    public void UdsService_ReadDTCInformation_Is0x19()
    {
        Assert.AreEqual((byte)0x19, (byte)UdsService.ReadDTCInformation);
    }

    [TestMethod]
    public void UdsService_ClearDiagnosticInformation_Is0x14()
    {
        Assert.AreEqual((byte)0x14, (byte)UdsService.ClearDiagnosticInformation);
    }

    [TestMethod]
    public void UdsService_WriteDataByIdentifier_Is0x2E()
    {
        Assert.AreEqual((byte)0x2E, (byte)UdsService.WriteDataByIdentifier);
    }

    [TestMethod]
    public void UdsService_RoutineControl_Is0x31()
    {
        Assert.AreEqual((byte)0x31, (byte)UdsService.RoutineControl);
    }

    [TestMethod]
    public void UdsService_ReadMemoryByAddress_Is0x23()
    {
        Assert.AreEqual((byte)0x23, (byte)UdsService.ReadMemoryByAddress);
    }

    // --- UdsNrc enum values ---

    [TestMethod]
    public void UdsNrc_ResponsePending_Is0x78()
    {
        Assert.AreEqual((byte)0x78, (byte)UdsNrc.RequestCorrectlyReceivedResponsePending);
    }

    [TestMethod]
    public void UdsNrc_ServiceNotSupported_Is0x11()
    {
        Assert.AreEqual((byte)0x11, (byte)UdsNrc.ServiceNotSupported);
    }

    [TestMethod]
    public void UdsNrc_SecurityAccessDenied_Is0x33()
    {
        Assert.AreEqual((byte)0x33, (byte)UdsNrc.SecurityAccessDenied);
    }

    [TestMethod]
    public void UdsNrc_SubFunctionNotSupportedInActiveSession_Is0x7E()
    {
        Assert.AreEqual((byte)0x7E, (byte)UdsNrc.SubFunctionNotSupportedInActiveSession);
    }

    [TestMethod]
    public void UdsNrc_ServiceNotSupportedInActiveSession_Is0x7F()
    {
        Assert.AreEqual((byte)0x7F, (byte)UdsNrc.ServiceNotSupportedInActiveSession);
    }

    [TestMethod]
    public void UdsNrc_ConditionsNotCorrect_Is0x22()
    {
        Assert.AreEqual((byte)0x22, (byte)UdsNrc.ConditionsNotCorrect);
    }

    // --- NegativeUdsResponseException ---

    [TestMethod]
    public void NegativeUdsResponseException_ContainsServiceAndNrc()
    {
        var ex = new NegativeUdsResponseException(
            UdsService.ReadDataByIdentifier, UdsNrc.RequestOutOfRange);

        Assert.AreEqual(UdsService.ReadDataByIdentifier, ex.RequestedService);
        Assert.AreEqual(UdsNrc.RequestOutOfRange, ex.Nrc);
        Assert.Contains("ReadDataByIdentifier", ex.Message);
        Assert.Contains("RequestOutOfRange", ex.Message);
    }

    [TestMethod]
    public void NegativeUdsResponseException_MessageContainsNrcHexValue()
    {
        var ex = new NegativeUdsResponseException(
            UdsService.TesterPresent, UdsNrc.ServiceNotSupported);

        Assert.Contains("0x11", ex.Message);
    }

    // --- UdsCanDialog type checks ---

    [TestMethod]
    public void UdsCanDialog_ImplementsIDisposable()
    {
        Assert.IsTrue(typeof(IDisposable).IsAssignableFrom(typeof(UdsCanDialog)));
    }

    [TestMethod]
    public void UdsCanDialog_HasExpectedPublicMethods()
    {
        var methods = typeof(UdsCanDialog).GetMethods()
            .Where(m => m.DeclaringType == typeof(UdsCanDialog))
            .Select(m => m.Name)
            .ToArray();

        CollectionAssert.Contains(methods, "SendReceive");
        CollectionAssert.Contains(methods, "DiagnosticSessionControl");
        CollectionAssert.Contains(methods, "ECUReset");
        CollectionAssert.Contains(methods, "TesterPresent");
        CollectionAssert.Contains(methods, "ReadDataByIdentifier");
        CollectionAssert.Contains(methods, "WriteDataByIdentifier");
        CollectionAssert.Contains(methods, "SecurityAccess");
        CollectionAssert.Contains(methods, "ReadDTCInformation");
        CollectionAssert.Contains(methods, "ClearDiagnosticInformation");
        CollectionAssert.Contains(methods, "ReadMemoryByAddress");
        CollectionAssert.Contains(methods, "RoutineControl");
    }

    // --- GetByteSize helper ---

    [TestMethod]
    [DataRow(0x00u, (byte)1)]
    [DataRow(0xFFu, (byte)1)]
    [DataRow(0x100u, (byte)2)]
    [DataRow(0xFFFFu, (byte)2)]
    [DataRow(0x10000u, (byte)3)]
    [DataRow(0xFFFFFFu, (byte)3)]
    [DataRow(0x1000000u, (byte)4)]
    [DataRow(0xFFFFFFFFu, (byte)4)]
    public void GetByteSize_ReturnsCorrectSize(uint value, byte expected)
    {
        Assert.AreEqual(expected, UdsCanDialog.GetByteSize(value));
    }
}
