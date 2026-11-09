using System.Text.Json;
using DedupTool;

namespace DedupTool.Tests;

/// <summary>The exercise sample batch and its hand-written expected output (copied to the test output folder).</summary>
internal static class SampleData
{
    public static string DataPath(string name) => Path.Combine(AppContext.BaseDirectory, "Data", name);

    public static string ExpectedCsv() => File.ReadAllText(DataPath("sample-expected.csv")).Replace("\r\n", "\n");

    public static Batch Load() =>
        JsonSerializer.Deserialize<Batch>(File.ReadAllText(DataPath("batch-sample.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
}
