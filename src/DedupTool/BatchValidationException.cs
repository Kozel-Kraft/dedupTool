namespace DedupTool;

/// <summary>
/// The batch is malformed in a way the tool cannot safely work around (cycle in parentId,
/// missing parent, repeated docId). The message names the DocIDs involved.
/// Program logs it at Error level and exits non-zero without writing a load file.
/// </summary>
public sealed class BatchValidationException : Exception
{
    public BatchValidationException(string message) : base(message) { }
}
