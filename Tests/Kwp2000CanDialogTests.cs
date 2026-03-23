using BitFab.KW1281Test.Kwp2000;
using BitFab.KW1281Test.Cluster;

namespace BitFab.KW1281Test.Tests;

[TestClass]
public class Kwp2000CanDialogTests
{
    // --- Interface compliance ---

    [TestMethod]
    public void KW2000Dialog_ImplementsIKwp2000Dialog()
    {
        Assert.IsTrue(typeof(IKwp2000Dialog).IsAssignableFrom(typeof(KW2000Dialog)));
    }

    [TestMethod]
    public void Kwp2000CanDialog_ImplementsIKwp2000Dialog()
    {
        Assert.IsTrue(typeof(IKwp2000Dialog).IsAssignableFrom(typeof(Kwp2000CanDialog)));
    }

    [TestMethod]
    public void Kwp2000CanDialog_ImplementsIDisposable()
    {
        Assert.IsTrue(typeof(IDisposable).IsAssignableFrom(typeof(Kwp2000CanDialog)));
    }

    // --- IKwp2000Dialog interface members ---

    [TestMethod]
    public void IKwp2000Dialog_HasExpectedMethods()
    {
        var methods = typeof(IKwp2000Dialog).GetMethods();
        var methodNames = methods.Select(m => m.Name).ToArray();

        CollectionAssert.Contains(methodNames, "SendReceive");
        CollectionAssert.Contains(methodNames, "StartDiagnosticSession");
        CollectionAssert.Contains(methodNames, "EcuReset");
        CollectionAssert.Contains(methodNames, "ReadMemoryByAddress");
        CollectionAssert.Contains(methodNames, "WriteMemoryByAddress");
        CollectionAssert.Contains(methodNames, "DumpMem");
    }

    // --- BoschRBxCluster accepts IKwp2000Dialog ---

    [TestMethod]
    public void BoschRBxCluster_AcceptsIKwp2000Dialog()
    {
        var ctor = typeof(BoschRBxCluster).GetConstructors().First();
        var ctorParams = ctor.GetParameters();

        Assert.HasCount(1, ctorParams);
        Assert.AreEqual(typeof(IKwp2000Dialog), ctorParams[0].ParameterType);
    }
}
