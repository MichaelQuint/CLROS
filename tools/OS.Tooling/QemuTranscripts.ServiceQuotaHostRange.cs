namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Original operation identity and directional offset. Transferred
    /// is the actual successful count,0 for zero/EOF, or-1 for failed/canceled
    /// I/O. Requested and Outcome remain explicit; no clock/HTTP meaning is added.</summary>

    public readonly record struct ServiceQuotaHostRange(int OperationId, long Offset, int Requested, int Transferred, string Outcome);
}
