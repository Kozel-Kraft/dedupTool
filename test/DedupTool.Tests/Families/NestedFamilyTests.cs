using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>R4 - nested attachments: FamilyID is the root, ParentID the immediate parent, date the root's.</summary>
public class NestedFamilyTests
{
    [Fact]
    public void NestedAttachment_GetsRootFamilyImmediateParentAndRootDate()
    {
        // E1 (email) -> N1 (an email attached to E1, no email fields) -> P1 (pdf inside N1)
        var result = Deduper.Run(T.Batch(
            T.Email("E1", sentDate: "2024-03-05T14:22:00-05:00"),
            T.Attach("N1", "E1", "nested-eml"),
            T.Attach("P1", "N1", "pdf")));

        var p1 = result.Rows.Single(r => r.DocId == "P1");
        Assert.Equal("E1", p1.FamilyId);                       // root, not N1
        Assert.Equal("N1", p1.ParentId);                       // immediate parent, not the root
        Assert.Equal("2024-03-05T19:22:00Z", p1.SentDate);     // root's date; N1 has none of its own
    }

    [Fact]
    public void ChildrenListedBeforeTheirParents_StillResolve()
    {
        var result = Deduper.Run(T.Batch(
            T.Attach("P1", "N1", "pdf"),
            T.Attach("N1", "E1", "nested-eml"),
            T.Email("E1")));

        Assert.All(result.Rows, r => Assert.Equal("E1", r.FamilyId));
    }

    [Fact] // stress test (50,000 levels): checks no stack overflow; no time threshold
    public void VeryDeepChain_DoesNotOverflowTheStack()
    {
        var docs = new List<Document> { T.Email("D0000000") };
        for (var i = 1; i <= 50_000; i++) docs.Add(T.Attach($"D{i:D7}", $"D{i - 1:D7}", $"h{i}"));

        var result = Deduper.Run(new Batch { Documents = docs });

        Assert.Equal(50_001, result.Rows.Count);
        Assert.All(result.Rows, r => Assert.Equal("D0000000", r.FamilyId));
    }

    [Fact]
    public void DeepAttachmentWithSharedHash_IsNotSuppressedAlone()
    {
        // The INGEST-4471 bug one level down: P2 has the same bytes as P1 but lives in another family.
        var result = Deduper.Run(T.Batch(
            T.Email("E1"), T.Attach("N1", "E1", "eml-1"), T.Attach("P1", "N1", "pdf"),
            T.Email("E2"), T.Attach("N2", "E2", "eml-2"), T.Attach("P2", "N2", "pdf")));

        Assert.Equal(6, result.Rows.Count);
        Assert.Equal(0, result.Suppressed);
    }

    [Fact]
    public void DuplicateFamilyWithNestedTree_IsSuppressedWhole()
    {
        var (a, b) = T.SameEmail("E1", "E2");
        Assert.NotEqual(a.Sha256, b.Sha256); // collapse by content, not by raw hash

        var result = Deduper.Run(T.Batch(
            a, T.Attach("N1", "E1", "eml"), T.Attach("P1", "N1", "pdf"),
            b, T.Attach("N2", "E2", "eml"), T.Attach("P2", "N2", "pdf")));

        Assert.Equal(new[] { "E1", "N1", "P1" }, result.Rows.Select(r => r.DocId).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(3, result.Suppressed);
    }
}
