using System.Text;

namespace DedupTool;

/// <summary>
/// Writes the load file consumed by the review platform ingest job.
/// Column order is fixed. Do not change without coordinating with the ingest team.
/// Format: RFC 4180 quoting, LF line endings, UTF-8 without BOM.
/// </summary>
public static class LoadFileWriter
{
    public const string Header = "DocID,FamilyID,ParentID,Custodian,DocType,FileName,SentDate,Hash";

    public static string ToCsv(IEnumerable<LoadFileRow> rows)
    {
        using var writer = new StringWriter();
        WriteTo(writer, rows);
        return writer.ToString();
    }

    public static void Write(string path, IEnumerable<LoadFileRow> rows)
    {
        // Streams row by row instead of building one huge string. Written to a temporary file and renamed at
        // the end, so a failure halfway never leaves a truncated load file for the ingest job to pick up.
        var temp = path + ".tmp";
        try
        {
            using (var writer = new StreamWriter(temp, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                WriteTo(writer, rows);
            }
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(temp); } catch (IOException) { /* nothing more to do; the original error is rethrown */ }
            throw;
        }
        Log.Info($"Load file written: {path}");
    }

    private static void WriteTo(TextWriter writer, IEnumerable<LoadFileRow> rows)
    {
        // Ingest job expects LF line endings regardless of platform, so '\n' is written explicitly.
        writer.Write(Header);
        writer.Write('\n');
        foreach (var r in rows)
        {
            Field(writer, r.DocId); writer.Write(',');
            Field(writer, r.FamilyId); writer.Write(',');
            Field(writer, r.ParentId); writer.Write(',');
            Field(writer, r.Custodian); writer.Write(',');
            Field(writer, r.DocType); writer.Write(',');
            Field(writer, r.FileName); writer.Write(',');
            Field(writer, r.SentDate); writer.Write(',');
            Field(writer, r.Hash);
            writer.Write('\n');
        }
    }

    // A field containing a comma, a quote or a line break is wrapped in quotes, and quotes inside are doubled.
    private static void Field(TextWriter writer, string value)
    {
        if (value.AsSpan().IndexOfAny(",\"\r\n") < 0)
        {
            writer.Write(value);
            return;
        }

        writer.Write('"');
        writer.Write(value.Replace("\"", "\"\""));
        writer.Write('"');
    }
}
