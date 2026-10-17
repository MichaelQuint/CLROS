namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Original handle/tuple lifetime observed in the containment case.
    /// Times are network-owner monotonic nanoseconds, not host PCAP timestamps.</summary>
    public readonly record struct SocketContainmentLifetime(ulong Slot, ulong Owner, ulong Epoch, ulong Generation,
        ulong Index, ulong RemotePort, ulong Created, ulong Retired, ulong Timeout);

    /// <summary>Read-only derived transport observations. These values do not
    /// establish descriptor, syscall, scheduler, packet, package or guest authority.</summary>
    public sealed record SocketContainmentTransport(IReadOnlyList<SocketContainmentLifetime> Lifetimes,
        ulong TimeoutClock, ulong DropClock, ulong AvailableBefore, ulong AvailableAfter);

    private sealed class ContainmentLifetime(Dictionary<string, ulong> first)
    {
        internal readonly Dictionary<string, ulong> First = first;
        internal ulong Retired, Timeout;
    }

    /// <summary>Value-only control seam for the exact three-peer timeout/VLAN
    /// case. Borrow caller rows, bind full live handle/tuple identities, require
    /// one peer-local timeout and actual single-buffer drop/repost before later
    /// retirement of both healthy old peers. Reject stale, duplicate, early or
    /// incomplete observations. Does not accept a guest lifecycle by itself.</summary>
    public static SocketContainmentTransport InspectSocketContainmentTransportForTests(string[] rows)
        => SocketContainmentLifetimes(rows);

    private static SocketContainmentTransport SocketContainmentLifetimes(string[] rows)
    {
        Need(rows is not null && rows.Length is > 0 and <= 65536, "socket_containment_transport_extent");
        List<ContainmentLifetime> all = [];
        Dictionary<ulong, ContainmentLifetime> active = [];
        ulong owner = 0, epoch = 0, clock = 0, timeout = 0, drop = 0, availableBefore = 0, availableAfter = 0;
        foreach (string row in rows!)
        {
            bool slot = row.StartsWith("M16_011_SLOT", StringComparison.Ordinal);
            bool expired = row.StartsWith("M07_027_TIMEOUT", StringComparison.Ordinal);
            bool rejected = row.StartsWith("M07_028_DROP", StringComparison.Ordinal);
            Need(!row.StartsWith("M16_011_TIMEWAIT", StringComparison.Ordinal), "socket_containment_unexpected_timewait");
            if (!slot && !expired && !rejected) continue;
            var r = slot ? ReadRecord(row, "M16_011_SLOT", ["event:dec", .. SocketLifetimeFields]) :
                expired ? ReadRecord(row, "M07_027_TIMEOUT", [.. SocketLifetimeFields[..10], "sharedFailed:dec"]) :
                ReadRecord(row, "M07_028_DROP", "before:dec", "after:dec", "availableBefore:dec", "availableAfter:dec", "clock:dec", "running:dec");
            Need(r["clock"] >= clock && r["clock"] <= long.MaxValue, "socket_containment_transport_clock"); clock = r["clock"];
            if (rejected)
            {
                Need(drop == 0 && timeout == 0 && active.Count == 3 && r["before"] == 0 && r["after"] == 1 && r["running"] == 1 &&
                    r["availableBefore"] <= ushort.MaxValue && r["availableAfter"] == ((r["availableBefore"] + 1) & 65535), "socket_containment_drop");
                drop = clock; availableBefore = r["availableBefore"]; availableAfter = r["availableAfter"]; continue;
            }
            Need(r["slot"] < 8 && r["index"] < 8 && r["owner"] >= 4096 && r["owner"] % 8 == 0 &&
                r["epoch"] != 0 && r["generation"] == 1 && r["local"] == 0x0a00020f && r["localPort"] == 8080 &&
                r["remote"] == 0x0a000202 && r["remotePort"] is >= 41000 and <= 41002, "socket_containment_transport_identity");
            if (owner == 0) { owner = r["owner"]; epoch = r["epoch"]; }
            Need(r["owner"] == owner && r["epoch"] == epoch, "socket_containment_transport_owner");
            if (slot && r["event"] == 0)
            {
                Need(all.Count < 3 && !active.ContainsKey(r["slot"]) && all.All(value => value.First["index"] != r["index"] &&
                    value.First["remotePort"] != r["remotePort"]) && r["deadline"] == 0 && r["duration"] == 0, "socket_containment_transport_fresh");
                var value = new ContainmentLifetime(r); all.Add(value); active.Add(r["slot"], value); continue;
            }
            Need(active.TryGetValue(r["slot"], out var held) && held!.Retired == 0 &&
                new[] { "owner", "epoch", "generation", "index", "local", "localPort", "remote", "remotePort" }.All(field => r[field] == held.First[field]),
                "socket_containment_transport_stale");
            if (expired)
            {
                Need(timeout == 0 && drop != 0 && active.Count == 3 && r["sharedFailed"] == 0 && r["remotePort"] == 41000 &&
                    clock - held!.First["clock"] >= 60_000_000_000, "socket_containment_timeout");
                held!.Timeout = timeout = clock;
            }
            else
            {
                Need(r["event"] == 1 && timeout != 0 && r["deadline"] == 0 && r["duration"] == 0 &&
                    (r["remotePort"] == 41000 ? held!.Timeout == timeout : clock - held!.First["clock"] >= 70_000_000_000),
                    "socket_containment_transport_retire");
                held!.Retired = clock; active.Remove(r["slot"]);
            }
        }
        Need(all.Count == 3 && active.Count == 0 && timeout != 0 && drop != 0 && all.All(value => value.Retired != 0), "socket_containment_transport_incomplete");
        SocketContainmentLifetime[] lifetimes = all.Select(value => new SocketContainmentLifetime(value.First["slot"], owner, epoch,
            value.First["generation"], value.First["index"], value.First["remotePort"], value.First["clock"], value.Retired, value.Timeout)).ToArray();
        return new(Array.AsReadOnly(lifetimes), timeout, drop, availableBefore, availableAfter);
    }
}
