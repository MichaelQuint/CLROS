namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Replay supplied lifetime/generation/tuple/deadline/high-water
    /// observations, then require the exact live owners discarded by explicit
    /// quota teardown and the subsequent ISN-secret release. Borrow rows without
    /// mutation; malformed, incomplete or graceful-substitute records throw.
    /// Real fd-close replay, original source/native/parent ownership and
    /// independent packet/peer/process cleanup joins remain mandatory.</summary>

    public static ServiceQuotaTransportInspection InspectNativeAotServiceQuotaTransportForTests(string[] rows) => ReadServiceQuotaTransport(rows, out _);
    private static ServiceQuotaTransportInspection ReadServiceQuotaTransport(string[] rows, out HashSet<int> metadata)
    {
        var history = ReadServiceTransport(rows, out metadata, quotaFailure: true);
        const string suffix = " retained=0 transport=aborted timewait=discarded buffers=scrubbed descriptors=closed table=retired";
        string final = rows.Single(row => row.StartsWith("M16_016_QUOTA_SOCKETS", StringComparison.Ordinal));
        var values = ReadRecord(final[..^suffix.Length], "M16_016_QUOTA_SOCKETS", "bulkAborted:dec", "compactDiscarded:dec", "clock:dec");
        // Complete history/abort/erasure validation above precedes projection.
        List<ServiceQuotaTransportLifetime> fresh = [];
        for (int at = 0; at < rows.Length; at++)
        {
            if (!rows[at].StartsWith("M16_011_SLOT ", StringComparison.Ordinal))
                continue;
            var row = SocketTransport(rows[at]);
            if (row["event"] != 0)
                continue;
            fresh.Add(new(at, row["slot"], row["owner"], row["epoch"], row["generation"], row["index"], row["local"], row["localPort"], row["remote"], row["remotePort"], row["clock"], row["deadline"], row["duration"]));
        }

        Need(fresh.Count == history.Connections, "service_quota_transport_census_count");
        return new(history, values["bulkAborted"], values["compactDiscarded"], values["clock"])
        {
            FreshLifetimes = Array.AsReadOnly(fresh.ToArray())
        };
    }
}
