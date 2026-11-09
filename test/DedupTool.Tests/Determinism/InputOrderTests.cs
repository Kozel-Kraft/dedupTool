using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>R6 - determinism (spec 6.1): the same batch gives the same bytes whatever the input order.</summary>
public class InputOrderTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(42)]
    public void ShufflingTheInput_DoesNotChangeTheLoadFile(int seed)
    {
        var baseline = LoadFileWriter.ToCsv(Deduper.Run(SampleData.Load()).Rows);

        var shuffled = SampleData.Load();
        var random = new Random(seed);
        shuffled.Documents = shuffled.Documents.OrderBy(_ => random.Next()).ToList();
        var csv = LoadFileWriter.ToCsv(Deduper.Run(shuffled).Rows);

        Assert.Equal(baseline, csv);
        Assert.Equal(SampleData.ExpectedCsv(), csv); // and it is the CORRECT output, not just a stable one
    }

    [Fact]
    public void ReversingTheInput_DoesNotChangeTheLoadFile()
    {
        var baseline = LoadFileWriter.ToCsv(Deduper.Run(SampleData.Load()).Rows);

        var reversed = SampleData.Load();
        reversed.Documents.Reverse();

        var csv = LoadFileWriter.ToCsv(Deduper.Run(reversed).Rows);

        Assert.Equal(baseline, csv);
        Assert.Equal(SampleData.ExpectedCsv(), csv);
    }

    [Fact]
    public void Rows_AreSortedByDocIdOrdinally_NotByCulture()
    {
        var result = Deduper.Run(T.Batch(T.Loose("b", "1"), T.Loose("B", "2"), T.Loose("a", "3"), T.Loose("A", "4")));

        Assert.Equal(new[] { "A", "B", "a", "b" }, result.Rows.Select(r => r.DocId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ShufflingABatchWithNestedFamiliesAndCrossCustodianDuplicates_DoesNotChangeTheLoadFile(int seed)
    {
        Batch Build()
        {
            var (a, b) = T.SameEmail("E1", "E2");
            var (_, c) = T.SameEmail("E1", "E3");
            return T.Batch(
                a, T.Attach("N1", "E1", "eml"), T.Attach("P1", "N1", "pdf"),
                b, T.Attach("N2", "E2", "eml"), T.Attach("P2", "N2", "pdf"),
                c, T.Attach("N3", "E3", "eml"), T.Attach("P3", "N3", "pdf"),
                T.Email("E4", "c4"), T.Attach("P4", "E4", "pdf"),
                T.Loose("L9", "x", "c2"), T.Loose("L10", "x", "c3"), T.Loose("L5", "y"));
        }
        var baseline = LoadFileWriter.ToCsv(Deduper.Run(Build()).Rows);

        var shuffled = Build();
        var random = new Random(seed);
        shuffled.Documents = shuffled.Documents.OrderBy(_ => random.Next()).ToList();

        var result = Deduper.Run(shuffled);

        Assert.Equal(baseline, LoadFileWriter.ToCsv(result.Rows));
        // correctness, by hand: E1 family survives (lowest of E1/E2/E3), E4 is distinct, "L10" < "L9" survives, L5 is unique
        Assert.Equal(new[] { "E1", "E4", "L10", "L5", "N1", "P1", "P4" }, result.Rows.Select(r => r.DocId));
    }
}
