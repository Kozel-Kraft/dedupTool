using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>End-to-end golden file for the exercise sample. sample-expected.csv was written by hand from the spec, not produced by the tool.</summary>
public class SampleBatchTests
{
    private static string RunCli(string batchPath, out string stdout, out int exitCode)
    {
        var output = Path.Combine(Path.GetTempPath(), $"dedup-{Guid.NewGuid():N}.csv");
        var writer = new StringWriter();
        try
        {
            exitCode = DedupCli.Run(new[] { batchPath, output }, writer);
            stdout = writer.ToString();
            return File.Exists(output) ? File.ReadAllText(output) : "";
        }
        finally
        {
            File.Delete(output);
        }
    }

    [Fact]
    public void Sample_ProducesTheExpectedLoadFile_ByteForByte()
    {
        var csv = RunCli(SampleData.DataPath("batch-sample.json"), out var stdout, out var exit);

        var expected = File.ReadAllText(SampleData.DataPath("sample-expected.csv")).Replace("\r\n", "\n");
        Assert.Equal(0, exit);
        Assert.Equal(expected, csv);
        Assert.Equal("batch-sample: 16 documents in, 12 out, 4 suppressed as duplicates.", stdout.Trim());
    }
}
