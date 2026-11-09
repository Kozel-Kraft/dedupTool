namespace DedupTool;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
}

/// <summary>
/// Minimal diagnostic logger. Writes to stderr only: stdout (the summary line)
/// and the load file are contracts and must never be polluted by diagnostics.
///
/// Level is read from the DEDUPTOOL_LOG environment variable
/// (Debug | Info | Warn | Error). Default: Info.
/// Debug emits one line per document; with ~2M documents guard call sites with
/// <see cref="IsEnabled"/> so no strings are built when it is off.
/// </summary>
public static class Log
{
    public static LogLevel Minimum { get; set; } = FromEnvironment();

    private static LogLevel FromEnvironment() =>
        Enum.TryParse<LogLevel>(Environment.GetEnvironmentVariable("DEDUPTOOL_LOG"), ignoreCase: true, out var level)
            ? level
            : LogLevel.Info;

    public static bool IsEnabled(LogLevel level) => level >= Minimum;

    public static void Debug(string message) => Write(LogLevel.Debug, message);
    public static void Info(string message) => Write(LogLevel.Info, message);
    public static void Warn(string message) => Write(LogLevel.Warn, message);
    public static void Error(string message) => Write(LogLevel.Error, message);

    private static void Write(LogLevel level, string message)
    {
        if (!IsEnabled(level)) return;
        Console.Error.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} [{level.ToString().ToUpperInvariant()}] {message}");
    }
}
