namespace OS.Tooling;

public static partial class HttpWorkloadBenchmark
{
    /// <summary>Explicit observation controls; these do not change any guest profile or deadline.</summary>
    public sealed class Options
    {
        public required string System
        {
            get; init;
        }
        public required string Backend
        {
            get; init;
        }
        public int WarmSequences { get; init; } = 10;
        public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(180);

        internal void Validate()
        {
            if (System is not ("CLROS" or "Alpine") || Backend is not ("whpx" or "tcg") ||
                WarmSequences is < 0 or > 10 || RequestTimeout <= TimeSpan.Zero ||
                RequestTimeout > TimeSpan.FromSeconds(180))
                throw new ToolFailure("http_benchmark_options");
        }
    }
}
