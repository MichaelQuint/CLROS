namespace OS.Tooling;

public static partial class HttpWorkloadBenchmark
{
    /// <summary>
    /// One original request. BodyReceived ends network receipt; BodyWritten and
    /// BodyFlushed observe separate host operations. Null stamps mean unreached
    /// stages. Complete requires exact body/metadata validation after recording.
    /// </summary>
    public sealed record Sample(int Ordinal, int Sequence, string Case, string Method,
        string Path, string[] RequestHeaders, string[] ResponseHeaders,
        long Started, long? HeadersReceived, long? BodyReceived, long? BodyWriteStarted,
        long? BodyWritten, long? BodyFlushed, long Finished, int? Status, long Bytes,
        long CapturedBytes, string? Sha256, string BodyFile, bool BodyComplete,
        bool Complete, string? Failure);
}
