using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>R8 / CLI contract - failures only: a clear message on stderr, a non-zero exit code, and no new load file.</summary>
[Collection("stderr")] // Console.Error is process-wide
public class CliFailureTests : CliTestBase
{

    [Fact]
    public void WrongNumberOfArguments_ExitsWithUsageCode2()
    {
        Assert.Equal(2, DedupCli.Run(new[] { "only-one.json" }, new StringWriter()));
    }

    [Fact]
    public void MissingInputFile_Exit1_NoOutputNoSummary_AndTheMessageNamesTheFile()
    {
        var (exit, stdout, stderr, wrote) = CliCapturingStderr(Path.Combine(Dir, "nope.json"));

        Assert.Equal(1, exit);
        Assert.False(wrote);
        Assert.Equal("", stdout);
        Assert.Contains("[ERROR]", stderr);
        Assert.Contains("nope.json", stderr);
    }

    [Fact]
    public void InvalidJson_Exit1_NoOutput_AndTheMessageNamesTheFileAndTheCause()
    {
        var (exit, _, stderr, wrote) = CliCapturingStderr(Write("bad.json", "{ this is not json"));

        Assert.Equal(1, exit);
        Assert.False(wrote);
        Assert.Contains("bad.json", stderr);
        Assert.Contains("not a valid batch file", stderr);
    }

    [Fact]
    public void JsonNull_ExitsNonZero()
    {
        var (exit, stdout, wrote) = Cli(Write("null.json", "null"));

        Assert.Equal(1, exit);
        Assert.False(wrote);
        Assert.Equal("", stdout);
    }

    [Fact]
    public void DocumentsNull_ExitsNonZero()
    {
        var (exit, stdout, wrote) = Cli(Write("nodocs.json", "{\"batchId\":\"x\",\"documents\":null}"));

        Assert.Equal(1, exit);
        Assert.False(wrote);
        Assert.Equal("", stdout);
    }

    [Fact]
    public void ParentCycle_ExitsNonZero_WithoutAnOutputFile()
    {
        var json = """
            {"batchId":"x","documents":[
              {"docId":"A","custodian":"c","docType":"attachment","parentId":"B","fileName":"a","sha256":"1","sizeBytes":1},
              {"docId":"B","custodian":"c","docType":"attachment","parentId":"A","fileName":"b","sha256":"2","sizeBytes":1}]}
            """;

        var (exit, stdout, wrote) = Cli(Write("cycle.json", json));

        Assert.NotEqual(0, exit);
        Assert.False(wrote);
        Assert.Equal("", stdout);
    }

    [Fact]
    public void UnwritableOutputPath_ExitsNonZero()
    {
        var batch = Write("ok.json", "{\"batchId\":\"x\",\"documents\":[]}");

        var (exit, stdout, _) = Cli(batch, Path.Combine(Dir, "no-such-folder", "out.csv"));

        Assert.Equal(1, exit);
        Assert.Equal("", stdout);
        Assert.Equal(new[] { "ok.json" }, Directory.GetFiles(Dir).Select(Path.GetFileName)); // nothing written anywhere
    }

    [Fact]
    public void NullElementInsideDocuments_IsRejectedAsInvalidInput_ExitCode1_NoOutput()
    {
        var (exit, _, stderr, wrote) = CliCapturingStderr(Write("nullelem.json", """{"batchId":"x","documents":[null]}"""));

        Assert.Equal(1, exit);
        Assert.False(wrote);
        Assert.Contains("null", stderr);
        Assert.Contains("position", stderr);
    }

    [Fact]
    public void PreviousOutputFile_IsLeftUntouched_WhenTheBatchIsRejected()
    {
        // Characterises the real behaviour (V2-2, developer's call): a failed run does not delete or
        // modify an earlier load file. Whoever loads the output must check the exit code.
        var output = Path.Combine(Dir, "out.csv");
        File.WriteAllText(output, "OLD CONTENT");
        var cycle = """
            {"batchId":"x","documents":[
              {"docId":"A","custodian":"c","docType":"attachment","parentId":"B","fileName":"a","sha256":"1","sizeBytes":1},
              {"docId":"B","custodian":"c","docType":"attachment","parentId":"A","fileName":"b","sha256":"2","sizeBytes":1}]}
            """;

        var (exit, _, _) = Cli(Write("cycle2.json", cycle), output);

        Assert.Equal(1, exit);
        Assert.Equal("OLD CONTENT", File.ReadAllText(output));
    }
}
