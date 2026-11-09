using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>Loose (top-level, non-email) files: duplicates by sha256 (spec 3.1).</summary>
public class LooseFileDedupTests
{
    [Fact]
    public void IdenticalLooseDocuments_LowestDocIdSurvives_EvenWhenListedLast()
    {
        // Listed B first: a "first one wins" rule would keep B; the rule is lowest DocId.
        var result = Deduper.Run(T.Batch(T.Loose("B", "h1", "c2"), T.Loose("A", "h1", "c1")));

        Assert.Single(result.Rows);
        Assert.Equal("A", result.Rows[0].DocId);
        Assert.Equal(1, result.Suppressed);
    }

    [Fact]
    public void DistinctDocuments_AllKept()
    {
        var result = Deduper.Run(T.Batch(T.Loose("A", "h1"), T.Loose("B", "h2")));

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(0, result.Suppressed);
    }

    [Fact]
    public void AttachmentIsNeverComparedAgainstALooseFile()
    {
        var result = Deduper.Run(T.Batch(
            T.Loose("L1", "same-bytes"),
            T.Email("E1"), T.Attach("A1", "E1", "same-bytes")));

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(0, result.Suppressed);
    }

    [Fact]
    public void LooseFilesWithoutAHash_AreNeverTreatedAsDuplicatesOfEachOther()
    {
        // Empty is not a hash: grouping them would silently suppress unrelated files.
        var result = Deduper.Run(T.Batch(T.Loose("L1", ""), T.Loose("L2", "")));

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(0, result.Suppressed);
    }
}
