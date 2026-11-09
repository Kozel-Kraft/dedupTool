using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>Batch-level invariants: nothing lost (in = out + suppressed) and no dangling ParentID.</summary>
public class BatchInvariantsTests
{
    [Fact]
    public void NoRowEverPointsAtAMissingParent()
    {
        var (a, b) = T.SameEmail("E1", "E3");
        var result = Deduper.Run(T.Batch(
            a, T.Attach("A1", "E1", "p"),
            T.Email("E2"), T.Attach("A2", "E2", "p"),
            b, T.Attach("A3", "E3", "p"),
            T.Loose("L1", "x"), T.Loose("L2", "x")));

        var ids = result.Rows.Select(r => r.DocId).ToHashSet();
        Assert.Contains(result.Rows, r => r.ParentId != ""); // guard: the check below is not vacuous
        Assert.All(result.Rows.Where(r => r.ParentId != ""), r => Assert.Contains(r.ParentId, ids));
    }

    [Fact]
    public void InputEqualsOutputPlusSuppressed()
    {
        // Suppressed: one loose duplicate (L2) + one duplicate email with its attachment (E6, X6) = 3, stated explicitly.
        var (a, b) = T.SameEmail("E5", "E6");
        var batch = T.Batch(
            T.Email("E1"), T.Attach("A1", "E1", "p"), T.Attach("A2", "E1", "q"),
            T.Loose("L1", "x"), T.Loose("L2", "x"),
            a, T.Attach("X5", "E5", "z"), b, T.Attach("X6", "E6", "z"));

        var result = Deduper.Run(batch);

        Assert.Equal(3, result.Suppressed);
        Assert.Equal(6, result.Rows.Count);
        Assert.Equal(batch.Documents.Count, result.Rows.Count + result.Suppressed);
    }
}
