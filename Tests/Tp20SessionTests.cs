using BitFab.KW1281Test.Interface;
using BitFab.KW1281Test.Kwp2000;

namespace BitFab.KW1281Test.Tests;

[TestClass]
public class Tp20SessionTests
{
    // --- Type structure ---

    [TestMethod]
    public void Tp20Session_ImplementsIDisposable()
    {
        Assert.IsTrue(typeof(IDisposable).IsAssignableFrom(typeof(Tp20Session)));
    }

    [TestMethod]
    public void Tp20Session_HasCanRouterConstructor()
    {
        var ctor = typeof(Tp20Session).GetConstructor(new[] { typeof(CanRouter) });
        Assert.IsNotNull(ctor, "Tp20Session should have a (CanRouter) constructor");
    }

    [TestMethod]
    public void Tp20Session_HasOpenChannelMethod()
    {
        var method = typeof(Tp20Session).GetMethod("OpenChannel");
        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(Tp20Channel), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.HasCount(1, parameters);
        Assert.AreEqual(typeof(byte), parameters[0].ParameterType);
    }

    [TestMethod]
    public void Tp20Session_HasGetChannelMethod()
    {
        var method = typeof(Tp20Session).GetMethod("GetChannel");
        Assert.IsNotNull(method);

        var parameters = method.GetParameters();
        Assert.HasCount(1, parameters);
        Assert.AreEqual(typeof(byte), parameters[0].ParameterType);
    }

    [TestMethod]
    public void Tp20Session_HasCloseChannelMethod()
    {
        var method = typeof(Tp20Session).GetMethod("CloseChannel");
        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(void), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.HasCount(1, parameters);
        Assert.AreEqual(typeof(byte), parameters[0].ParameterType);
    }

    [TestMethod]
    public void Tp20Session_HasSendKeepAliveAllMethod()
    {
        var method = typeof(Tp20Session).GetMethod("SendKeepAliveAll");
        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(void), method.ReturnType);
        Assert.HasCount(0, method.GetParameters());
    }

    [TestMethod]
    public void Tp20Session_HasChannelCountProperty()
    {
        var prop = typeof(Tp20Session).GetProperty("ChannelCount");
        Assert.IsNotNull(prop);
        Assert.AreEqual(typeof(int), prop.PropertyType);
        Assert.IsTrue(prop.CanRead);
    }

    [TestMethod]
    public void Tp20Session_HasOpenAddressesProperty()
    {
        var prop = typeof(Tp20Session).GetProperty("OpenAddresses");
        Assert.IsNotNull(prop);
        Assert.IsTrue(prop.CanRead);
    }
}
