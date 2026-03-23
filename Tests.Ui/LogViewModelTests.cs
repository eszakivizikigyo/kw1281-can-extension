using System.Collections.ObjectModel;
using System.Threading.Tasks;
using BitFab.KW1281Test.Ui.Models;
using BitFab.KW1281Test.Ui.ViewModels;

namespace BitFab.KW1281Test.Ui.Tests;

[TestClass]
public class LogViewModelTests
{
    [TestMethod]
    public void Initial_State_AllFiltersEnabled()
    {
        var entries = new ObservableCollection<LogEntry>();
        var vm = new LogViewModel(entries);

        Assert.IsTrue(vm.AutoScroll);
        Assert.IsTrue(vm.ShowInfo);
        Assert.IsTrue(vm.ShowTx);
        Assert.IsTrue(vm.ShowRx);
        Assert.IsTrue(vm.ShowErrors);
    }

    [TestMethod]
    public void Clear_RemovesAllEntries()
    {
        var entries = new ObservableCollection<LogEntry>
        {
            new(DateTime.Now, "Test 1"),
            new(DateTime.Now, "Test 2"),
        };
        var vm = new LogViewModel(entries);

        vm.ClearCommand.Execute(null);

        Assert.AreEqual(0, vm.Entries.Count);
    }

    [TestMethod]
    public void Entries_SharesReferenceWithConstructorParam()
    {
        var entries = new ObservableCollection<LogEntry>();
        var vm = new LogViewModel(entries);

        entries.Add(new LogEntry(DateTime.Now, "Hello"));

        Assert.AreEqual(1, vm.Entries.Count);
        Assert.AreEqual("Hello", vm.Entries[0].Message);
    }

    [TestMethod]
    public void FilterProperties_CanBeToggled()
    {
        var vm = new LogViewModel(new ObservableCollection<LogEntry>());

        vm.ShowInfo = false;
        Assert.IsFalse(vm.ShowInfo);

        vm.ShowTx = false;
        Assert.IsFalse(vm.ShowTx);

        vm.ShowRx = false;
        Assert.IsFalse(vm.ShowRx);

        vm.ShowErrors = false;
        Assert.IsFalse(vm.ShowErrors);
    }

    [TestMethod]
    public async Task SaveAsync_NoStorageProvider_DoesNotThrow()
    {
        var vm = new LogViewModel(new ObservableCollection<LogEntry>());
        vm.StorageProviderFactory = null;

        // Should silently return without error
        await vm.SaveCommand.ExecuteAsync(null);
    }
}
