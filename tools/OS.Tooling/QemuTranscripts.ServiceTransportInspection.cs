namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Derived bulk/compact lifetime and secret-owner accounting. This
    /// checks serialized observations only; packets, secrets, native ownership,
    /// process and guest execution remain separate evidence requirements.</summary>

    public readonly record struct ServiceTransportInspection(int Connections, int Transfers, int Retired, int BulkHighWater, int CompactHighWater, ulong MaximumDeadline, ulong IssuedSequences);
}
