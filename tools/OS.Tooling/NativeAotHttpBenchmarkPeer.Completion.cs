namespace OS.Tooling;

public static partial class NativeAotHttpBenchmarkPeer
{
    /// <summary>
    /// Joined diagnostic observations only. The caller retains original input
    /// authorities; this value does not authenticate copied reports or qualify
    /// production performance, browser startup or normal full-workload cleanup.
    /// </summary>
    public sealed record Completion(int Port, string Directory,
        HttpWorkloadBenchmark.Report Observations, ProcessResult Runner)
    {
        public bool ProductionPerformanceAccepted => false;
        public bool FullAndCleanupQualified => false;
        public bool BrowserQualified => false;
    }
}
