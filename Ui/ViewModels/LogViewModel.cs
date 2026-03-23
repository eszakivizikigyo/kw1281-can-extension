using System.Collections.ObjectModel;
using BitFab.KW1281Test.Ui.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels;

public partial class LogViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _autoScroll = true;

    public ObservableCollection<LogEntry> Entries { get; }

    public LogViewModel(ObservableCollection<LogEntry> entries)
    {
        Entries = entries;
    }

    [RelayCommand]
    private void Clear()
    {
        Entries.Clear();
    }
}
