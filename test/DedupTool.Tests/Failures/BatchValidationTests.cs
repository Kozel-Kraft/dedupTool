using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>Malformed batches are rejected with a message that names the documents (cycle, missing parent, docId).</summary>
public class BatchValidationTests
{
    [Fact]
    public void ParentIdCycle_FailsWithAMessageNamingTheDocuments()
    {
        var ex = Assert.Throws<BatchValidationException>(() => Deduper.Run(T.Batch(
            T.Attach("A", "B", "h1"), T.Attach("B", "A", "h2"))));

        Assert.Equal("parentId cycle: A -> B -> A.", ex.Message);
    }

    [Fact]
    public void DocumentThatIsItsOwnParent_IsACycle()
    {
        var ex = Assert.Throws<BatchValidationException>(() => Deduper.Run(T.Batch(T.Attach("A", "A", "h"))));

        Assert.Contains("cycle", ex.Message);
    }

    [Fact]
    public void MissingParent_FailsWithAMessageNamingBothDocuments()
    {
        var ex = Assert.Throws<BatchValidationException>(() => Deduper.Run(T.Batch(
            T.Email("E1"), T.Attach("A1", "GHOST", "h"))));

        Assert.Contains("A1", ex.Message);
        Assert.Contains("GHOST", ex.Message);
    }

    [Fact]
    public void RepeatedDocId_IsRejected_NamingTheId()
    {
        var ex = Assert.Throws<BatchValidationException>(() => Deduper.Run(T.Batch(T.Loose("D1", "a"), T.Loose("D1", "b"))));

        Assert.Contains("D1", ex.Message);
    }

    [Fact]
    public void MissingDocId_IsRejected()
    {
        var doc = T.Loose("x", "a");
        doc.DocId = "";

        var ex = Assert.Throws<BatchValidationException>(() => Deduper.Run(T.Batch(doc)));

        Assert.Contains("no docId", ex.Message);
        Assert.Contains("#0", ex.Message);
    }
}
