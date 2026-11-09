using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>R1 - families first (INGEST-4471): an attachment never decides alone.</summary>
public class FamilyIntegrityTests
{
    [Fact]
    public void SharedAttachmentOnDifferentEmails_IsKeptInBothFamilies()
    {
        var result = Deduper.Run(T.Batch(
            T.Email("E1", "jsmith"), T.Attach("A1", "E1", "shared", "jsmith"),
            T.Email("E2", "mlee"), T.Attach("A2", "E2", "shared", "mlee")));

        Assert.Equal(4, result.Rows.Count);
        Assert.Equal(0, result.Suppressed);
        Assert.Equal("E1", result.Rows.Single(r => r.DocId == "A1").ParentId);
        Assert.Equal("E2", result.Rows.Single(r => r.DocId == "A2").ParentId);
        Assert.Equal("mlee", result.Rows.Single(r => r.DocId == "A2").Custodian); // each custodian keeps its own copy
    }

    [Fact]
    public void SuppressedFamily_TakesAllItsMembersWithIt()
    {
        // The two emails have DIFFERENT raw hashes: they collapse by content (3.3), not by hash.
        var (a, b) = T.SameEmail("E1", "E2");
        Assert.NotEqual(a.Sha256, b.Sha256);

        var result = Deduper.Run(T.Batch(
            a, T.Attach("A1", "E1", "p1"), T.Attach("A1b", "E1", "p2"),
            b, T.Attach("A2", "E2", "p1"), T.Attach("A2b", "E2", "p2")));

        Assert.Equal(new[] { "A1", "A1b", "E1" }, result.Rows.Select(r => r.DocId).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(3, result.Suppressed);
    }

    [Fact]
    public void Characterisation_AnAttachmentWhoseParentIsALooseFile_FormsAFamilyWithAnEmptyDate()
    {
        // Not specified (the spec has no loose file with children). Current behaviour, pinned here.
        var result = Deduper.Run(T.Batch(T.Loose("L1", "h"), T.Attach("A1", "L1", "x")));

        Assert.Equal(2, result.Rows.Count);
        var child = result.Rows.Single(r => r.DocId == "A1");
        Assert.Equal("L1", child.FamilyId);
        Assert.Equal("L1", child.ParentId);
        Assert.Equal("", child.SentDate);
    }
}
