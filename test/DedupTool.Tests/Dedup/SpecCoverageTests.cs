using System.Text.Json;
using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>V4-1 - spec clauses that had no test of their own (3.1, 3.2, 3.3, 4, 5).</summary>
public class SpecCoverageTests
{
    [Fact]
    public void Spec3_2_TwoAttachmentsWithTheSameHashInOneFamily_AreBothKept()
    {
        var result = Deduper.Run(T.Batch(T.Email("E1"), T.Attach("A1", "E1", "same"), T.Attach("A2", "E1", "same")));

        Assert.Equal(new[] { "A1", "A2", "E1" }, result.Rows.Select(r => r.DocId));
        Assert.Equal(0, result.Suppressed);
    }

    [Fact]
    public void Spec3_1_HashesDifferingOnlyInCase_AreTheSameHash()
    {
        var result = Deduper.Run(T.Batch(T.Loose("L2", "ABCDEF"), T.Loose("L1", "abcdef")));

        Assert.Equal("L1", Assert.Single(result.Rows).DocId);
        Assert.Equal(1, result.Suppressed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Spec3_1_TheSurvivingLooseFile_IsTheLowestDocIdOrdinal_NotTheFirstToArrive(bool reverseInput)
    {
        // Ordinal: "DOC-10" sorts before "DOC-9" ("1" < "9"), unlike a numeric or natural order.
        var docs = new[] { T.Loose("DOC-9", "h"), T.Loose("DOC-10", "h") };
        if (reverseInput) Array.Reverse(docs);

        var result = Deduper.Run(T.Batch(docs));

        Assert.Equal("DOC-10", Assert.Single(result.Rows).DocId);
    }

    [Fact]
    public void Spec3_3_ThreeCopiesOfTheSameEmail_OneFamilySurvives_TwoAreSuppressed()
    {
        var (a, b) = T.SameEmail("E1", "E2");
        var (_, c) = T.SameEmail("E1", "E3");

        var result = Deduper.Run(T.Batch(
            a, T.Attach("A1", "E1", "p"),
            b, T.Attach("A2", "E2", "p"),
            c, T.Attach("A3", "E3", "p")));

        Assert.Equal(new[] { "A1", "E1" }, result.Rows.Select(r => r.DocId));
        Assert.Equal(4, result.Suppressed);
    }

    [Fact]
    public void Decision_D014_Spec4IsNotMet_MleeLosesFourDocuments_TheSurvivingCopiesBelongToOtherCustodians()
    {
        // D-014: global dedup (3.4) wins over "each custodian's set is self-contained" (4). Known, documented.
        var batch = JsonSerializer.Deserialize<Batch>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "batch-sample.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var kept = Deduper.Run(batch).Rows.Select(r => r.DocId).ToHashSet();
        var lost = batch.Documents.Where(d => d.Custodian == "mlee" && !kept.Contains(d.DocId)).Select(d => d.DocId).OrderBy(x => x, StringComparer.Ordinal);

        Assert.Equal(new[] { "DOC-000009", "DOC-000010", "DOC-000011", "DOC-000013" }, lost);
        // the surviving copy of the loose file DOC-000013 is DOC-000008, held by someone else
        var twin = batch.Documents.Single(d => d.DocId == "DOC-000008");
        Assert.NotEqual("mlee", twin.Custodian);
        Assert.Contains(twin.DocId, kept);
    }
}
