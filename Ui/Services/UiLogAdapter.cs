using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using BitFab.KW1281Test.Logging;
using BitFab.KW1281Test.Ui.Models;

namespace BitFab.KW1281Test.Ui.Services;

/// <summary>
/// Bridges ILog to an ObservableCollection for UI display.
/// Thread-safe: posts to the Avalonia UI thread.
/// </summary>
internal class UiLogAdapter : ILog
{
    public ObservableCollection<LogEntry> Entries { get; } = [];

    public void Write(string message, LogDest dest = LogDest.All)
    {
        var entry = new LogEntry(DateTime.Now, message);
        Dispatcher.UIThread.Post(() => Entries.Add(entry));
    }

    public void WriteLine(LogDest dest = LogDest.All)
    {
        var entry = new LogEntry(DateTime.Now, string.Empty);
        Dispatcher.UIThread.Post(() => Entries.Add(entry));
    }

    public void WriteLine(string message, LogDest dest = LogDest.All)
    {
        var entry = new LogEntry(DateTime.Now, message);
        Dispatcher.UIThread.Post(() => Entries.Add(entry));
    }

    public void Close() { }

    public void Dispose() { }
}
