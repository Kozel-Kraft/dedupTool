using System.Text;
using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>R3 - the load file is valid CSV and keeps the fixed contract (spec 5).</summary>
public class LoadFileWriterTests
{
    private static LoadFileRow Row(string fileName = "a.pdf") => new()
    {
        DocId = "D1", FamilyId = "D1", ParentId = "", Custodian = "c1", DocType = "loose",
        FileName = fileName, SentDate = "", Hash = "abc",
    };

    private static string[] Lines(string csv) => csv.Split('\n');

    [Fact]
    public void PlainRow_ColumnOrderMatchesTheHeader()
    {
        var csv = LoadFileWriter.ToCsv(new[] { Row() });

        Assert.Equal("DocID,FamilyID,ParentID,Custodian,DocType,FileName,SentDate,Hash\nD1,D1,,c1,loose,a.pdf,,abc\n", csv);
    }

    [Fact]
    public void FileNameWithComma_IsQuoted()
    {
        var csv = LoadFileWriter.ToCsv(new[] { Row("budget, final.xlsx") });

        Assert.Equal("D1,D1,,c1,loose,\"budget, final.xlsx\",,abc", Lines(csv)[1]);
    }

    [Fact]
    public void FileNameWithQuote_IsQuotedAndTheQuoteDoubled()
    {
        var csv = LoadFileWriter.ToCsv(new[] { Row("the \"big\" one.txt") });

        Assert.Equal("D1,D1,,c1,loose,\"the \"\"big\"\" one.txt\",,abc", Lines(csv)[1]);
    }

    [Fact]
    public void FileNameWithNewline_IsQuoted_SoItDoesNotSplitTheRow()
    {
        var csv = LoadFileWriter.ToCsv(new[] { Row("two\nlines.txt") });

        Assert.Equal("D1,D1,,c1,loose,\"two\nlines.txt\",,abc\n", csv.Substring(csv.IndexOf('\n') + 1));
    }

    [Fact]
    public void LineEndingsAreLfOnly()
    {
        var csv = LoadFileWriter.ToCsv(new[] { Row(), Row("b.txt") });

        Assert.DoesNotContain('\r', csv);
        Assert.EndsWith("\n", csv);
    }

    [Fact]
    public void Write_ProducesUtf8WithoutBom_AndKeepsNonAsciiNames()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dedup-{Guid.NewGuid():N}.csv");
        try
        {
            LoadFileWriter.Write(path, new[] { Row("informe-ñandú-日本.txt") });

            var bytes = File.ReadAllBytes(path);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "BOM found");
            Assert.Contains("informe-ñandú-日本.txt", new UTF8Encoding(false).GetString(bytes));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Spec5_AFieldWithABareCarriageReturn_IsQuoted()
    {
        var csv = LoadFileWriter.ToCsv(new[]
        {
            new LoadFileRow { DocId = "D1", FamilyId = "D1", ParentId = "", Custodian = "c", DocType = "loose", FileName = "a\rb.txt", SentDate = "", Hash = "h" },
        });

        Assert.Contains("\"a\rb.txt\"", csv);
    }
}
