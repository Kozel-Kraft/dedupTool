using DedupTool;

internal static class Program
{
    public static int Main(string[] args) => DedupCli.Run(args, Console.Out);
}
