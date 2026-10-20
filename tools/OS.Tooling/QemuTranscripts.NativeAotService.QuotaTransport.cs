namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied observed history and deliberate abort counts. Retired in
    /// History counts only real expiry; discarded compact records are separate.
    /// These values authenticate no NIC, buffers, packets, native code or guest.</summary>
    public readonly record struct ServiceQuotaTransportInspection(ServiceTransportInspection History,
        ulong BulkAborted, ulong CompactDiscarded, ulong AbortClock);

    /// <summary>Replay supplied lifetime/generation/tuple/deadline/high-water
    /// observations, then require the exact live owners discarded by explicit
    /// quota teardown and the subsequent ISN-secret release. Borrow rows without
    /// mutation; malformed, incomplete or graceful-substitute records throw.
    /// Real fd-close replay, original source/native/parent ownership and
    /// independent packet/peer/process cleanup joins remain mandatory.</summary>
    public static ServiceQuotaTransportInspection InspectNativeAotServiceQuotaTransportForTests(string[] rows) =>
        ReadServiceQuotaTransport(rows,out _);

    private static ServiceQuotaTransportInspection ReadServiceQuotaTransport(string[] rows,out HashSet<int> metadata)
    {
        var history=ReadServiceTransport(rows,out metadata,quotaFailure:true);
        const string suffix=" retained=0 transport=aborted timewait=discarded buffers=scrubbed descriptors=closed table=retired";
        string final=rows.Single(row=>row.StartsWith("M16_016_QUOTA_SOCKETS",StringComparison.Ordinal));
        var values=ReadRecord(final[..^suffix.Length],"M16_016_QUOTA_SOCKETS","bulkAborted:dec","compactDiscarded:dec","clock:dec");
        return new(history,values["bulkAborted"],values["compactDiscarded"],values["clock"]);
    }
}
