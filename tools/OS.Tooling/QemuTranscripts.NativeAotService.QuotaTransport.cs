namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied observed history and deliberate abort counts. Retired in
    /// History counts only real expiry; discarded compact records are separate.
    /// These values authenticate no NIC, buffers, packets, native code or guest.</summary>
    public readonly record struct ServiceQuotaTransportInspection(ServiceTransportInspection History,
        ulong BulkAborted, ulong CompactDiscarded, ulong AbortClock)
    {
        /// <summary>Every validated fresh transport reservation in supplied
        /// normalized row order; copied/read-only, including repeated ports.</summary>
        public IReadOnlyList<ServiceQuotaTransportLifetime> FreshLifetimes { get; init; } =
            Array.Empty<ServiceQuotaTransportLifetime>();
    }

    /// <summary>Copied fresh SLOT event0 identity. RowIndex is zero-based in the
    /// supplied normalized row array, not a syscall ordinal or raw serial byte.
    /// Addresses/ports and all counters retain original decimal value units;
    /// Clock/Deadline/Duration are guest nanoseconds. A pending SYN reservation
    /// is not an accepted socket or completed HTTP exchange. Repeated ports in
    /// distinct generations are retained; no host/PCAP/clock mapping is implied.</summary>
    public readonly record struct ServiceQuotaTransportLifetime(int RowIndex, ulong Slot, ulong Owner,
        ulong Epoch, ulong Generation, ulong Index, ulong Local, ulong LocalPort, ulong Remote,
        ulong RemotePort, ulong Clock, ulong Deadline, ulong Duration);

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
        // Complete history/abort/erasure validation above precedes projection.
        List<ServiceQuotaTransportLifetime> fresh=[];
        for(int at=0;at<rows.Length;at++)
        {
            if(!rows[at].StartsWith("M16_011_SLOT ",StringComparison.Ordinal))continue;
            var row=SocketTransport(rows[at]);if(row["event"]!=0)continue;
            fresh.Add(new(at,row["slot"],row["owner"],row["epoch"],row["generation"],row["index"],
                row["local"],row["localPort"],row["remote"],row["remotePort"],row["clock"],row["deadline"],row["duration"]));
        }
        Need(fresh.Count==history.Connections,"service_quota_transport_census_count");
        return new(history,values["bulkAborted"],values["compactDiscarded"],values["clock"])
        { FreshLifetimes=Array.AsReadOnly(fresh.ToArray()) };
    }
}
