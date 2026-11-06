namespace OS.Tooling;

public static partial class HttpWorkloadBenchmark
{
    /// <summary>Descriptive network/body-file observations; first sequence and warm groups stay separate.</summary>
    public sealed record Distribution(string Case, string Phase, int Count,
        double MinimumMs, double MedianMs, double MeanMs, double MaximumMs,
        double[] NetworkSamplesMs, double[] HostWriteSamplesMs, double[] HostFlushSamplesMs);
}
