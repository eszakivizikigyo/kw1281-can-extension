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
        var level = ClassifyMessage(message);
        var entry = new LogEntry(DateTime.Now, message, level);
        Dispatcher.UIThread.Post(() => Entries.Add(entry));
    }

    public void WriteLine(LogDest dest = LogDest.All)
    {
        var entry = new LogEntry(DateTime.Now, string.Empty);
        Dispatcher.UIThread.Post(() => Entries.Add(entry));
    }

    public void WriteLine(string message, LogDest dest = LogDest.All)
    {
        var level = ClassifyMessage(message);
        var entry = new LogEntry(DateTime.Now, message, level);
        Dispatcher.UIThread.Post(() => Entries.Add(entry));
    }

    public void Close() { }

    public void Dispose() { }

    private static LogLevel ClassifyMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return LogLevel.Info;

        if (message.StartsWith("TX", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("> TX", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("Sending", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Tx;

        if (message.StartsWith("RX", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("< RX", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("Received", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Rx;

        if (message.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
            message.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("error", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Error;

        if (message.StartsWith("Warning", StringComparison.OrdinalIgnoreCase))
            return LogLevel.Warning;

        return LogLevel.Info;
    }
}
