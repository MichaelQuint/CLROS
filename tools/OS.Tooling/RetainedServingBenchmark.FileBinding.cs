namespace OS.Tooling;

public static partial class RetainedServingBenchmark
{
    private sealed record FileBinding(string HistoricalPath, string ReadFrom, string PrivateCopy, long Bytes, string Sha256);
}
