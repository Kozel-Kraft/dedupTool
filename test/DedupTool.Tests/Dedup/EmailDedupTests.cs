using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>R5 - cross-mailbox email deduplication (spec 3.3): same message, different bytes.</summary>
public class EmailDedupTests
{
    private static readonly string[] Maria = { "maria.lee@example.com" };

    private static Document Base(string id, string custodian = "jsmith") => T.RawEmail(
        id, custodian, "john.smith@example.com", Maria, "Q3 budget review", "2024-03-05T14:22:00-05:00",
        "Hi Maria,\n\nAttached is the Q3 budget.\n\nThanks,\nJohn");

    private static DedupResult Run(params Document[] docs) => Deduper.Run(T.Batch(docs));

    private static void AssertDuplicates(Document a, Document b)
    {
        var result = Run(a, b);
        Assert.Equal(a.DocId, Assert.Single(result.Rows).DocId);
        Assert.Equal(1, result.Suppressed);
    }

    private static void AssertDistinct(Document a, Document b)
    {
        var result = Run(a, b);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(0, result.Suppressed);
    }

    [Fact]
    public void TheSamplePair_SenderCaseUtcOffsetAndLineEndingsDiffer_AreDuplicates()
    {
        var sent = Base("DOC-000001", "jsmith");
        var inbox = T.RawEmail("DOC-000009", "mlee", "John.Smith@example.com", Maria, "Q3 budget review",
            "2024-03-05T19:22:00Z", "Hi Maria,\r\n\r\nAttached is the Q3 budget.\r\n\r\nThanks,\r\nJohn");

        AssertDuplicates(sent, inbox);
    }

    [Fact]
    public void DuplicateEmail_TakesItsWholeFamilyWithIt_WhenAttachmentsMatch()
    {
        var result = Run(
            Base("DOC-000001"), T.Attach("A1", "DOC-000001", "xlsx"), T.Attach("A2", "DOC-000001", "pdf"),
            Base("DOC-000009", "mlee"), T.Attach("A3", "DOC-000009", "pdf"), T.Attach("A4", "DOC-000009", "xlsx"));

        Assert.Equal(new[] { "A1", "A2", "DOC-000001" }, result.Rows.Select(r => r.DocId).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(3, result.Suppressed);
    }

    [Fact]
    public void AForwardThatQuotesTheOriginal_IsNotADuplicate()
    {
        var original = Base("E1");
        var forward = T.RawEmail("E2", "jsmith", "john.smith@example.com", new[] { "ray.kim@example.com" }, "FW: Q3 budget review",
            "2024-03-06T09:10:00-05:00", "Ray - see below.\n\nHi Maria,\n\nAttached is the Q3 budget.\n\nThanks,\nJohn");

        AssertDistinct(original, forward);
    }

    [Fact]
    public void OnlyTheBodyDiffers_ByQuotingTheOriginalBelowANote_IsADifferentEmail()
    {
        // Single variable: same sender, recipient, subject and date; only the body changes.
        var other = Base("E2"); other.Body = "Ray - see below.\n\n" + other.Body;

        AssertDistinct(Base("E1"), other);
    }

    [Fact]
    public void AReply_IsNotADuplicate()
    {
        var reply = T.RawEmail("E2", "mlee", "maria.lee@example.com", new[] { "john.smith@example.com" }, "Re: Q3 budget review",
            "2024-03-05T20:05:00Z", "Looks fine.");

        AssertDistinct(Base("E1"), reply);
    }

    [Fact]
    public void SameSubjectWithAndWithoutRePrefix_AreDistinct()
    {
        var other = Base("E2"); other.Subject = "Re: Q3 budget review";

        AssertDistinct(Base("E1"), other);
    }

    [Fact]
    public void SameEmailWithDifferentAttachments_BothFamiliesAreKeptIntact()
    {
        var result = Run(
            Base("E1"), T.Attach("A1", "E1", "p"), T.Attach("A2", "E1", "q"),
            Base("E2", "mlee"), T.Attach("A3", "E2", "p"));      // one attachment was stripped from this copy

        Assert.Equal(5, result.Rows.Count);
        Assert.Equal(0, result.Suppressed);
    }

    [Fact]
    public void SameEmailWithSameAttachmentCountButDifferentContent_AreDistinct()
    {
        var result = Run(
            Base("E1"), T.Attach("A1", "E1", "p"),
            Base("E2", "mlee"), T.Attach("A2", "E2", "different"));

        Assert.Equal(4, result.Rows.Count);
    }

    [Fact]
    public void AttachmentsDeepInTheTree_TakePartInTheKey()
    {
        var result = Run(
            Base("E1"), T.Attach("N1", "E1", "eml"), T.Attach("P1", "N1", "pdf-a"),
            Base("E2", "mlee"), T.Attach("N2", "E2", "eml"), T.Attach("P2", "N2", "pdf-b"));

        Assert.Equal(6, result.Rows.Count);
    }

    [Fact]
    public void RecipientsInAnotherOrderWithCaseAndRepeats_AreTheSameSet()
    {
        var a = T.RawEmail("E1", "c1", "a@x.com", new[] { "b@x.com", "c@x.com" }, "s", "2024-01-01T00:00:00Z", "body");
        var b = T.RawEmail("E2", "c2", "a@x.com", new[] { "C@x.com", "B@X.com", "b@x.com" }, "s", "2024-01-01T00:00:00Z", "body");

        AssertDuplicates(a, b);
    }

    [Fact]
    public void ADifferentRecipient_IsADifferentEmail()
    {
        var a = T.RawEmail("E1", "c1", "a@x.com", new[] { "b@x.com" }, "s", "2024-01-01T00:00:00Z", "body");
        var b = T.RawEmail("E2", "c2", "a@x.com", new[] { "b@x.com", "c@x.com" }, "s", "2024-01-01T00:00:00Z", "body");

        AssertDistinct(a, b);
    }

    [Fact]
    public void OneSecondApart_IsADifferentEmail_NoDateTolerance()
    {
        var other = Base("E2"); other.SentDate = "2024-03-05T14:22:01-05:00";

        AssertDistinct(Base("E1"), other);
    }

    [Fact]
    public void SurroundingWhitespaceInSubjectAndTrailingSpacesInBody_AreIgnored()
    {
        var other = T.RawEmail("E2", "mlee", "john.smith@example.com", Maria, "  Q3 budget review ", "2024-03-05T14:22:00-05:00",
            "Hi Maria,   \n\nAttached is the Q3 budget.\n\nThanks,\nJohn\n\n");

        AssertDuplicates(Base("E1"), other);
    }

    [Fact]
    public void ADifferentWordInTheBody_IsADifferentEmail()
    {
        var other = Base("E2"); other.Body = other.Body!.Replace("Q3", "Q4");

        AssertDistinct(Base("E1"), other);
    }

    [Fact]
    public void FieldsAreNotJustConcatenated_ShiftingTextBetweenFieldsMakesADifferentKey()
    {
        var a = T.RawEmail("E1", "c1", "ab", new[] { "c" }, "s", "2024-01-01T00:00:00Z", "body");
        var b = T.RawEmail("E2", "c2", "a", new[] { "bc" }, "s", "2024-01-01T00:00:00Z", "body");
        var c = T.RawEmail("E3", "c3", "a@x.com", new[] { "b@x.com" }, "ab", "2024-01-01T00:00:00Z", "c");
        var d = T.RawEmail("E4", "c4", "a@x.com", new[] { "b@x.com" }, "a", "2024-01-01T00:00:00Z", "bc");

        AssertDistinct(a, b);
        AssertDistinct(c, d);
    }

    [Fact]
    public void ListedInAnyOrder_TheLowestDocIdSurvives()
    {
        var first = Run(Base("DOC-000009", "mlee"), Base("DOC-000001", "jsmith"));
        var second = Run(Base("DOC-000001", "jsmith"), Base("DOC-000009", "mlee"));

        Assert.Equal("DOC-000001", Assert.Single(first.Rows).DocId);
        Assert.Equal("DOC-000001", Assert.Single(second.Rows).DocId);
    }

    [Fact]
    public void EmailsWithoutADate_AreComparedByExactHashOnly()
    {
        var a = Base("E1"); a.SentDate = null;
        var b = Base("E2"); b.SentDate = null;
        AssertDistinct(a, b);                                   // identical text, but no date: not provable

        var c = Base("E3"); c.SentDate = null;
        var d = Base("E4"); d.SentDate = null; d.Sha256 = c.Sha256;
        AssertDuplicates(c, d);                                 // identical bytes: provable
    }

    [Fact]
    public void AnEmailIsNeverComparedAgainstALooseFile()
    {
        var email = Base("E1");
        var loose = T.Loose("L1", email.Sha256);

        AssertDistinct(email, loose);
    }
}
