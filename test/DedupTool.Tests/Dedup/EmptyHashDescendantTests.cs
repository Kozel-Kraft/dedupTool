using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>
/// V1-1 - an attachment with no sha256 cannot be compared, so the email above it cannot be proven
/// identical to another one. Collapsing such emails would lose an attachment (the INGEST-4471 failure class).
/// Console.Error is process-wide, hence the collection (no parallel run with other classes touching it).
/// </summary>
[Collection("stderr")]
public class EmptyHashDescendantTests
{
    [Fact]
    public void IdenticalEmails_WhoseAttachmentsHaveNoHash_AreBothKept_AndWarn()
    {
        var (a, b) = T.SameEmail("A", "B");
        var batch = T.Batch(a, T.Attach("A-1", "A", ""), b, T.Attach("B-1", "B", ""));

        var previous = Console.Error;
        var captured = new StringWriter();
        Console.SetError(captured);
        DedupResult result;
        try { result = Deduper.Run(batch); }
        finally { Console.SetError(previous); }

        Assert.Equal(0, result.Suppressed);
        Assert.Equal(new[] { "A", "A-1", "B", "B-1" }, result.Rows.Select(r => r.DocId).ToArray());
        var log = captured.ToString();
        Assert.Contains("[WARN]", log);
        Assert.Contains("A has 1 attachment(s) with no sha256", log);
        Assert.Contains("B has 1 attachment(s) with no sha256", log);
    }

    [Fact]
    public void IdenticalEmails_WhoseAttachmentsHaveHashes_StillCollapse()
    {
        var (a, b) = T.SameEmail("A", "B");
        var batch = T.Batch(a, T.Attach("A-1", "A", "h1"), b, T.Attach("B-1", "B", "h1"));

        var result = Deduper.Run(batch);

        Assert.Equal(2, result.Suppressed);
        Assert.Equal(new[] { "A", "A-1" }, result.Rows.Select(r => r.DocId).ToArray());
    }
}
