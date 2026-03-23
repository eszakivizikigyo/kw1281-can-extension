using System;

namespace BitFab.KW1281Test.Ui.Models;

public enum LogLevel { Info, Tx, Rx, Warning, Error }

public record LogEntry(DateTime Timestamp, string Message, LogLevel Level = LogLevel.Info);
