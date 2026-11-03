namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Separately copied final failed/canceled write attempt. Its bytes
    /// are never appended to the known prefix. Actual transmission is unknown,
    /// including a zero-length attempt; the original transferred count is-1.</summary>

    public sealed record ServiceQuotaHostWriteAttempt(int OperationId, long Offset, int Requested, string Outcome, string AttemptedBase64)
    {
        public int Transferred => -1;
    }
}
