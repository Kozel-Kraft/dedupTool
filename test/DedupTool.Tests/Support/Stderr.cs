namespace DedupTool.Tests;

/// <summary>Captures what the tool writes to stderr. Console.Error is process-wide: classes using this are in the "stderr" collection.</summary>
internal static class Stderr
{
    public static string Capture(Action action)
    {
        var previous = Console.Error;
        var captured = new StringWriter();
        Console.SetError(captured);
        try { action(); }
        finally { Console.SetError(previous); }
        return captured.ToString();
    }
}
