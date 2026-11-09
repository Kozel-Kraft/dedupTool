using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>CLI behaviour on valid or tolerated input: exit 0, a summary, and the expected file.</summary>
[Collection("stderr")]
public class CliBehaviourTests : CliTestBase
{
    [Fact]
    public void EmptyBatch_IsValid_AndWritesOnlyTheHeader()
    {
        var batch = Write("empty.json", "{\"batchId\":\"x\",\"documents\":[]}");

        var (exit, stdout, wrote) = Cli(batch);

        Assert.Equal(0, exit);
        Assert.True(wrote);
        Assert.Equal("x: 0 documents in, 0 out, 0 suppressed as duplicates.", stdout.Trim());
        Assert.Equal(LoadFileWriter.Header + "\n", File.ReadAllText(Path.Combine(Dir, "out.csv")));
    }

    [Fact]
    public void NoTemporaryFileIsLeftBehind_AfterASuccessfulWrite()
    {
        var batch = Write("ok.json", "{\"batchId\":\"x\",\"documents\":[]}");

        Cli(batch);

        Assert.Equal(new[] { "ok.json", "out.csv" }, Directory.GetFiles(Dir).Select(Path.GetFileName).OrderBy(x => x));
    }

    [Fact]
    public void NullDocType_IsReadAsEmptyText_AndTheRowIsWritten()
    {
        var json = """{"batchId":"x","documents":[{"docId":"A","custodian":"c","docType":null,"fileName":"f","sha256":"1","sizeBytes":1}]}""";

        var (exit, _, wrote) = Cli(Write("nulltype.json", json));

        Assert.Equal(0, exit);
        Assert.True(wrote);
        Assert.Equal(LoadFileWriter.Header + "\nA,A,,c,,f,,1\n", File.ReadAllText(Path.Combine(Dir, "out.csv"))); // DocType is empty
    }

    [Fact]
    public void NullTextFieldsFromJson_DoNotCrashTheTool()
    {
        var json = """
            {"batchId":"x","documents":[
              {"docId":"A","custodian":null,"docType":"loose","parentId":null,"fileName":null,"sha256":null,"sizeBytes":1}]}
            """;

        var (exit, _, wrote) = Cli(Write("nulls.json", json));

        Assert.Equal(0, exit);
        Assert.True(wrote);
        Assert.Equal(LoadFileWriter.Header + "\nA,A,,,loose,,,\n", File.ReadAllText(Path.Combine(Dir, "out.csv")));
    }
}
