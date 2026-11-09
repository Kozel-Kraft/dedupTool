using System.Security.Cryptography;
using System.Text;

namespace DedupTool;

/// <summary>
/// Equality key for "the same email" (spec 3.3). Copies of one message collected from different
/// mailboxes have different raw bytes (headers, encodings, line endings), so the sha256 cannot be used;
/// the key is built from the message content instead.
///
/// Key = from, to (as a set), subject, sentDate (as a UTC instant), body, and the sorted sha256 of
/// everything below the email in its family. Including the attachments means two copies carrying
/// different attachments are different families and both are kept intact.
///
/// Principle: do not over-normalise. A false positive suppresses evidence; a false negative only costs
/// reviewer time. So: no date tolerance, "Re:"/"FW:" are kept, quoted text is not stripped.
///
/// Fields are encoded with length prefixes so concatenation cannot collide ("ab"+"c" vs "a"+"bc"),
/// and the encoded text is reduced to a SHA-256 fingerprint so email bodies are not kept as map keys.
/// </summary>
public static class EmailIdentity
{
    public static string Key(Document email, IReadOnlyCollection<string>? hashesBelow)
    {
        var descendants = (hashesBelow ?? Array.Empty<string>())
            .Select(h => h.ToLowerInvariant())
            .OrderBy(h => h, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        var date = UtcDate.ToLoadFile(email.SentDate);
        if (date == null && email.Sha256.Length == 0)
        {
            // No usable date and no hash: nothing to compare on, so the email is its own group.
            Log.Warn($"{email.DocId} has neither a usable sentDate nor a sha256, so it cannot be compared; it is kept.");
            return "U:" + email.DocId;
        }

        if (date == null)
        {
            // Without a usable date sameness cannot be proven from content: only identical bytes count.
            Add(sb, "raw");
            Add(sb, email.Sha256.ToLowerInvariant());
        }
        else
        {
            Add(sb, "content");
            Add(sb, Address(email.From));
            AddList(sb, (email.To ?? new List<string>()).Select(Address).Where(a => a.Length > 0).Distinct().OrderBy(a => a, StringComparer.Ordinal).ToList());
            Add(sb, (email.Subject ?? "").Trim());
            Add(sb, date);
            Add(sb, Body(email.Body));
        }
        AddList(sb, descendants);

        return "E:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    public static string Address(string? value) => (value ?? "").Trim().ToLowerInvariant();

    /// <summary>Line endings unified to LF, trailing spaces removed from every line, leading/trailing blank space removed.</summary>
    public static string Body(string? body)
    {
        var lines = (body ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(l => l.TrimEnd());
        return string.Join("\n", lines).Trim();
    }

    private static void Add(StringBuilder sb, string value) => sb.Append(value.Length).Append(':').Append(value).Append('|');

    private static void AddList(StringBuilder sb, List<string> values)
    {
        Add(sb, values.Count.ToString());
        foreach (var v in values) Add(sb, v);
    }
}
