namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>One-pair byte compatibility only, never a connection identity.
    /// Counts are actual compared bytes, not transmitted/allocated lengths.
    /// A compatible result's minimum attempted-write prefix includes any holes
    /// preceding observed tail bytes; it is0 for incompatible/absent tails.
    /// Empty or identical matches remain non-identifying.</summary>

    public readonly record struct ServiceQuotaStreamPairInspection(bool Compatible, int RequestBytesCompared, int ResponseBytesCompared, int MinimumAttemptedWritePrefixBytes)
    {
        public bool HasComparedBytes => RequestBytesCompared != 0 || ResponseBytesCompared != 0;
    }
}
