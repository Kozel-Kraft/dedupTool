using System.Diagnostics;
using System.Text.Json;

namespace DedupTool;

/// <summary>
/// The command-line contract: <c>DedupTool &lt;batch.json&gt; &lt;loadfile.csv&gt;</c>.
/// stdout carries only the one-line summary (spec 6.3); diagnostics go to stderr through <see cref="Log"/>.
/// Kept apart from Main so the whole flow can be tested.
///
/// Exit codes: 0 success; 1 the input cannot be processed (unreadable or invalid file, malformed batch,
/// unwritable output): the message on stderr says why and NO NEW load file is written. A load file left from an
/// earlier run is neither deleted nor modified, so a caller must check the exit code before loading it; 2 wrong usage;
/// 3 unexpected internal failure (full exception logged) or a batch too large for memory (clear message). The summary line is printed on success only.
/// </summary>
public static class DedupCli
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static int Run(string[] args, TextWriter stdout)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: DedupTool <batch.json> <loadfile.csv>");
            return 2;
        }

        try
        {
            return Execute(args[0], args[1], stdout);
        }
        catch (BatchValidationException ex)
        {
            Log.Error($"Cannot process '{args[0]}': {ex.Message}");
            return 1;
        }
        catch (JsonException ex)
        {
            Log.Error($"'{args[0]}' is not a valid batch file: {ex.Message}");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error($"File access failed: {ex.Message}");
            return 1;
        }
        catch (OutOfMemoryException)
        {
            Log.Error($"Out of memory while processing '{args[0]}': the batch does not fit in the memory available. " +
                      "Free memory (roughly 3x the JSON size is needed) or split the batch.");
            return 3;
        }
        catch (Exception ex)
        {
            Log.Error($"Unexpected failure: {ex}");
            return 3;
        }
    }

    private static int Execute(string batchPath, string loadFilePath, TextWriter stdout)
    {
        var clock = Stopwatch.StartNew();
        Log.Info($"Reading batch: {batchPath}. Log level: {Log.Minimum}");

        // Deserialize straight from the stream: no second copy of the whole file as a string.
        Batch? batch;
        using (var input = File.OpenRead(batchPath))
        {
            batch = JsonSerializer.Deserialize<Batch>(input, JsonOptions);
        }
        if (batch == null) throw new BatchValidationException("the file contains JSON null instead of a batch.");
        if (batch.Documents == null) throw new BatchValidationException("the batch has no 'documents' array.");
        Log.Info($"Batch '{batch.BatchId}' loaded in {clock.ElapsedMilliseconds} ms.");

        var result = Deduper.Run(batch);
        LoadFileWriter.Write(loadFilePath, result.Rows);

        Log.Info($"Total time: {clock.ElapsedMilliseconds} ms.");
        stdout.WriteLine($"{batch.BatchId}: {batch.Documents.Count} documents in, {result.Rows.Count} out, {result.Suppressed} suppressed as duplicates.");
        return 0;
    }
}
