namespace OS.Tooling;

public static partial class HttpWorkloadBenchmark
{
    /// <summary>
    /// Copied host observations, with no runner/native/browser authority. The
    /// enclosing owner must join its runner and bind all source/device inputs.
    /// </summary>
    public sealed record Report(string Schema, string System, string Backend,
        string ImageSha256, string AssetSha256, long AssetBytes, long ClockFrequency,
        int WarmSequences, long RequestTimeoutTicks, bool Complete, string? Failure,
        IReadOnlyList<Sample> Samples, IReadOnlyList<Distribution> Distributions)
    {
        public bool ProductionPerformanceAccepted => false;
        public bool FullAndCleanupQualified => false;
        public bool BrowserQualified => false;
        public string RequestTimeoutScope => "headers-and-streamed-body";
    }
}
