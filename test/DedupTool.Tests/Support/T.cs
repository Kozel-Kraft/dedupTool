using DedupTool;

namespace DedupTool.Tests;

/// <summary>Test data builders. Defaults make every email distinct unless a test says otherwise.</summary>
internal static class T
{
    public static Document Email(
        string id, string custodian = "c1", string subject = "subject", string sentDate = "2024-03-05T10:00:00Z",
        string from = "a@x.com", string[]? to = null, string? body = null, string? hash = null) => new()
    {
        DocId = id, Custodian = custodian, DocType = "email", FileName = id + ".eml",
        Sha256 = hash ?? "email-" + id, SizeBytes = 1,
        SentDate = sentDate, From = from, To = (to ?? new[] { "b@x.com" }).ToList(),
        Subject = subject + " " + id, Body = body ?? "body " + id,
    };

    /// <summary>An email with exactly the given fields (no decoration), for equality tests.</summary>
    public static Document RawEmail(
        string id, string custodian, string from, string[] to, string subject, string sentDate, string body, string? hash = null) => new()
    {
        DocId = id, Custodian = custodian, DocType = "email", FileName = id + ".eml",
        Sha256 = hash ?? "raw-" + id, SizeBytes = 1,
        SentDate = sentDate, From = from, To = to.ToList(), Subject = subject, Body = body,
    };

    public static Document Attach(string id, string parent, string hash, string custodian = "c1", string? fileName = null) => new()
    {
        DocId = id, Custodian = custodian, DocType = "attachment", ParentId = parent,
        FileName = fileName ?? id + ".bin", Sha256 = hash, SizeBytes = 1,
    };

    public static Document Loose(string id, string hash, string custodian = "c1", string? fileName = null) => new()
    {
        DocId = id, Custodian = custodian, DocType = "loose", FileName = fileName ?? id + ".txt", Sha256 = hash, SizeBytes = 1,
    };

    public static Batch Batch(params Document[] docs) => new() { BatchId = "t", Documents = docs.ToList() };

    /// <summary>Two emails that are the same message (same fields) with different raw bytes.</summary>
    public static (Document first, Document second) SameEmail(string id1, string id2)
    {
        var a = Email(id1, "c1", "same", body: "same body");
        var b = Email(id2, "c2", "same", body: "same body");
        a.Subject = "same"; b.Subject = "same";
        return (a, b);
    }
}
