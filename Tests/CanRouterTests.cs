using BitFab.KW1281Test.Interface;
using BitFab.KW1281Test.Kwp2000;

namespace BitFab.KW1281Test.Tests;

[TestClass]
public class CanRouterTests
{
    // --- Type structure ---

    [TestMethod]
    public void CanRouter_ImplementsIDisposable()
    {
        Assert.IsTrue(typeof(IDisposable).IsAssignableFrom(typeof(CanRouter)));
    }

    [TestMethod]
    public void CanRouter_HasRegisterChannelMethod()
    {
        var method = typeof(CanRouter).GetMethod("RegisterChannel");
        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(void), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.HasCount(1, parameters);
        Assert.AreEqual(typeof(uint), parameters[0].ParameterType);
    }

    [TestMethod]
    public void CanRouter_HasUnregisterChannelMethod()
    {
        var method = typeof(CanRouter).GetMethod("UnregisterChannel");
        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(void), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.HasCount(1, parameters);
        Assert.AreEqual(typeof(uint), parameters[0].ParameterType);
    }

    [TestMethod]
    public void CanRouter_HasReceiveMessageMethod()
    {
        var method = typeof(CanRouter).GetMethod("ReceiveMessage");
        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(CanMessage), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.HasCount(2, parameters);
        Assert.AreEqual(typeof(uint), parameters[0].ParameterType);
        Assert.AreEqual(typeof(int), parameters[1].ParameterType);
    }

    [TestMethod]
    public void CanRouter_HasReceiveUnregisteredMessageMethod()
    {
        var method = typeof(CanRouter).GetMethod("ReceiveUnregisteredMessage");
        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(CanMessage), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.HasCount(1, parameters);
        Assert.AreEqual(typeof(int), parameters[0].ParameterType);
    }

    [TestMethod]
    public void CanRouter_HasSendMessageMethod()
    {
        var method = typeof(CanRouter).GetMethod("SendMessage");
        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(bool), method.ReturnType);

        var parameters = method.GetParameters();
        Assert.HasCount(1, parameters);
        Assert.AreEqual(typeof(CanMessage), parameters[0].ParameterType);
    }

    [TestMethod]
    public void CanRouter_HasRegisteredChannelCountProperty()
    {
        var prop = typeof(CanRouter).GetProperty("RegisteredChannelCount");
        Assert.IsNotNull(prop);
        Assert.AreEqual(typeof(int), prop.PropertyType);
        Assert.IsTrue(prop.CanRead);
    }

    // --- Tp20Channel backward compatibility ---

    [TestMethod]
    public void Tp20Channel_HasCanRouterConstructor()
    {
        var ctor = typeof(Tp20Channel).GetConstructor(
            new[] { typeof(CanRouter), typeof(byte) });
        Assert.IsNotNull(ctor, "Tp20Channel should have a (CanRouter, byte) constructor");
    }

    [TestMethod]
    public void Tp20Channel_HasCanInterfaceConstructor()
    {
        var ctor = typeof(Tp20Channel).GetConstructor(
            new[] { typeof(CanInterface), typeof(byte) });
        Assert.IsNotNull(ctor, "Tp20Channel should retain backward-compatible (CanInterface, byte) constructor");
    }
}
