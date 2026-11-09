using DedupTool;
using Xunit;

namespace DedupTool.Tests;

/// <summary>R2 - SentDate is UTC "YYYY-MM-DDTHH:MM:SSZ" (spec 5); attachments carry the root email's date.</summary>
[Collection("stderr")]
public class SentDateTests
{
    private static string DateOf(string sentDate)
    {
        var result = Deduper.Run(T.Batch(T.Email("E1", sentDate: sentDate)));
        return result.Rows.Single().SentDate;
    }

    [Theory]
    [InlineData("2024-03-05T14:22:00-05:00", "2024-03-05T19:22:00Z")]
    [InlineData("2024-03-06T09:10:00-05:00", "2024-03-06T14:10:00Z")]
    [InlineData("2024-03-05T19:22:00Z", "2024-03-05T19:22:00Z")]
    [InlineData("2024-03-05T19:22:00+00:00", "2024-03-05T19:22:00Z")]
    [InlineData("2024-03-05T23:30:00-05:00", "2024-03-06T04:30:00Z")]   // crosses midnight
    [InlineData("2024-03-05T00:15:00+05:30", "2024-03-04T18:45:00Z")]   // positive, half-hour offset
    [InlineData("2024-03-05T14:22:00.987-05:00", "2024-03-05T19:22:00Z")] // no fractional seconds
    public void EmailDate_IsConvertedToExactUtcFormat(string input, string expected) =>
        Assert.Equal(expected, DateOf(input));

    [Fact]
    public void Attachment_CarriesTheRootEmailDate()
    {
        var result = Deduper.Run(T.Batch(
            T.Email("E1", sentDate: "2024-03-05T14:22:00-05:00"),
            T.Attach("A1", "E1", "h")));

        Assert.All(result.Rows, r => Assert.Equal("2024-03-05T19:22:00Z", r.SentDate));
    }

    [Fact]
    public void LooseFile_HasEmptyDate()
    {
        var result = Deduper.Run(T.Batch(T.Loose("L1", "h")));

        Assert.Equal("", result.Rows.Single().SentDate);
    }

    [Fact]
    public void EmailWithoutDate_GivesEmptyDateAndDoesNotThrow()
    {
        var email = T.Email("E1");
        email.SentDate = null;

        DedupResult result = null!;
        var stderr = Stderr.Capture(() => result = Deduper.Run(T.Batch(email, T.Attach("A1", "E1", "h"))));

        Assert.All(result.Rows, r => Assert.Equal("", r.SentDate));
        Assert.Contains("[WARN]", stderr);
        Assert.Contains("E1 is an email whose sentDate is missing", stderr);
    }

    [Fact]
    public void UnparseableDate_GivesEmptyDateAndDoesNotThrow()
    {
        var date = "<unset>";
        var stderr = Stderr.Capture(() => date = DateOf("not a date"));

        Assert.Equal("", date);
        Assert.Contains("[WARN]", stderr);
        Assert.Contains("not parseable ('not a date')", stderr);
    }
}
