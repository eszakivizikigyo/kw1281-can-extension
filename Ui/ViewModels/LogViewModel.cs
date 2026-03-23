using System;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Platform.Storage;
using BitFab.KW1281Test.Ui.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BitFab.KW1281Test.Ui.ViewModels;

public partial class LogViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private bool _showInfo = true;

    [ObservableProperty]
    private bool _showTx = true;

    [ObservableProperty]
    private bool _showRx = true;

    [ObservableProperty]
    private bool _showErrors = true;

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

    public Func<IStorageProvider>? StorageProviderFactory { get; set; }

    [RelayCommand]
    private async System.Threading.Tasks.Task SaveAsync()
    {
        if (StorageProviderFactory == null) return;

        var sp = StorageProviderFactory();
        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Log",
            DefaultExtension = "txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Text Files") { Patterns = ["*.txt"] },
                new FilePickerFileType("All Files") { Patterns = ["*"] },
            ],
            SuggestedFileName = $"kw1281test_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        });

        if (file == null) return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        foreach (var entry in Entries)
        {
            await writer.WriteLineAsync($"{entry.Timestamp:HH:mm:ss.fff} [{entry.Level}] {entry.Message}");
        }
    }
}
