using BitFab.KW1281Test.Logging;
using System.Collections.Generic;

namespace BitFab.KW1281Test;

/// <summary>
/// Global static logger and shared state accessor. Both CLI and UI set Log at startup.
/// </summary>
internal static class Logger
{
    public static ILog Log { get; set; } = new ConsoleLog();

    public static List<string> CommandAndArgs { get; set; } = [];
}
