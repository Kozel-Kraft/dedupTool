using DedupTool;

namespace DedupTool.Tests;

/// <summary>Runs the real CLI against files in a private temp folder.</summary>
public abstract class CliTestBase : IDisposable
{
    protected readonly string Dir = Directory.CreateTempSubdirectory("dedup-cli-").FullName;

    public void Dispose() => Directory.Delete(Dir, recursive: true);

    protected string Write(string name, string content)
    {
        var path = Path.Combine(Dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    protected (int exit, string stdout, bool wroteOutput) Cli(string batchPath, string? outputPath = null)
    {
        var output = outputPath ?? Path.Combine(Dir, "out.csv");
        var stdout = new StringWriter();
        var exit = DedupCli.Run(new[] { batchPath, output }, stdout);
        return (exit, stdout.ToString(), File.Exists(output));
    }

    protected (int exit, string stdout, string stderr, bool wroteOutput) CliCapturingStderr(string batchPath)
    {
        (int exit, string stdout, bool wroteOutput) r = (0, "", false);
        var stderr = Stderr.Capture(() => r = Cli(batchPath));
        return (r.exit, r.stdout, stderr, r.wroteOutput);
    }
}
