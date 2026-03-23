using System.Collections.ObjectModel;
using BitFab.KW1281Test.Ui.Models;

namespace BitFab.KW1281Test.Ui.Tests;

[TestClass]
public class LogEntryTests
{
    [TestMethod]
    public void Default_Level_IsInfo()
    {
        var entry = new LogEntry(DateTime.Now, "test");
        Assert.AreEqual(LogLevel.Info, entry.Level);
    }

    [TestMethod]
    public void Explicit_Level_IsPreserved()
    {
        var entry = new LogEntry(DateTime.Now, "TX data", LogLevel.Tx);
        Assert.AreEqual(LogLevel.Tx, entry.Level);
    }

    [TestMethod]
    public void Record_Equality()
    {
        var ts = DateTime.Now;
        var a = new LogEntry(ts, "msg", LogLevel.Rx);
        var b = new LogEntry(ts, "msg", LogLevel.Rx);
        Assert.AreEqual(a, b);
    }

    [TestMethod]
    public void Record_Inequality_DifferentLevel()
    {
        var ts = DateTime.Now;
        var a = new LogEntry(ts, "msg", LogLevel.Info);
        var b = new LogEntry(ts, "msg", LogLevel.Error);
        Assert.AreNotEqual(a, b);
    }
}

[TestClass]
public class NavigationItemTests
{
    [TestMethod]
    public void Default_Properties()
    {
        var item = new NavigationItem { Title = "Test" };
        Assert.AreEqual("Test", item.Title);
        Assert.IsNull(item.Tag);
        Assert.IsNull(item.IconGlyph);
    }

    [TestMethod]
    public void Children_CanBeSet()
    {
        var parent = new NavigationItem
        {
            Title = "Parent",
            Children = new ObservableCollection<NavigationItem>
            {
                new NavigationItem { Title = "Child1", Tag = "C1" },
                new NavigationItem { Title = "Child2", Tag = "C2" },
            }
        };

        Assert.AreEqual(2, parent.Children!.Count);
        Assert.AreEqual("C1", parent.Children[0].Tag);
    }
}
