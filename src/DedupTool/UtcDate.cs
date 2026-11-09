using System.Globalization;

namespace DedupTool;

/// <summary>Converts a header date (ISO 8601 with offset) to the load-file format (spec 5).</summary>
public static class UtcDate
{
    public const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    /// <summary>
    /// Returns the instant in UTC as exactly YYYY-MM-DDTHH:MM:SSZ (no fractions, no offset), or null
    /// if the text is missing or cannot be parsed. A string with no offset is read as UTC rather than
    /// as the machine's local time, so the output never depends on where the tool runs.
    /// </summary>
    public static string? ToLoadFile(string? sentDate) =>
        TryParse(sentDate, out var instant) ? instant.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture) : null;

    public static bool TryParse(string? sentDate, out DateTimeOffset instant)
    {
        instant = default;
        return !string.IsNullOrWhiteSpace(sentDate) &&
               DateTimeOffset.TryParse(sentDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out instant);
    }
}
