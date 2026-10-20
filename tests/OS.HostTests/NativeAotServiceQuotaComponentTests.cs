using OS.Tooling;

internal static class NativeAotServiceQuotaComponentTests
{
    private const ulong Quiet = 240000000001, Main = 0x100000001, Child = 0x100000002;
    private const string Backing = "M16_016_STACK_BACKING cooperativePages=354 workerMappedBytes=1048576 switchMappedBytes=65536 irqMappedBytes=16384 mainSupervisorUsableBound=262144 reservation=retained";

    // Independent copied rows; not a full scheduler/native/owned guest capture.
    private static string[] Rows(bool reuse = false, bool accept = false)
    {
        List<string> rows = [
            "M16_008_BOUND root=12288 pool=2097152 workers=16 code=53248 metadata=114688 userImage=728 timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack",
            "M07_033_BOUND quietUntil=240000000001 clock=1 tickNanos=4000 quietNanos=240000000000 secret=independent lease=network"];
        int ordinal = 0;
        void Call(ulong nr, long value) => rows.Add($"M16_008_CALL identity={Main} nr={nr} a0=5 a1=0 a2=0 a3=0 a4=0 a5=0 rip=4194818 rsp=1879048128 flags=514 fs=0 gs=0 clock={Quiet} valueBits={unchecked((ulong)value)} action=1 status=1 ordinal={++ordinal} waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=1879048128 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={unchecked((ulong)value)}");
        string Identity(int slot, int generation = 1) => $"slot={slot} owner=8388608 epoch=1 generation={generation} index={slot}";
        string Slot(int slot, int generation, int eventId, ulong clock) => $"M16_011_SLOT event={eventId} {Identity(slot, generation)} local=167772687 localPort=8080 remote=167772674 remotePort={35000 + slot + (generation - 1) * 10} clock={clock} deadline=0 duration=0";
        string Loan(int slot, int generation, int eventId, int end) => $"M16_016_SOCKET_LEASE event={eventId} {Identity(slot, generation)} rxBytes=8192 txBytes=32768 end={end}";
        void SocketSnapshot(int a, int r, int live, int peak, int end) => rows.Add($"M16_016_SOCKET_LEASES backingBytes=327680 acquired={a} returned={r} live={live} rxBytes={live * 8192} txBytes={live * 32768} peakLive={peak} peakRxBytes={peak * 8192} peakTxBytes={peak * 32768} end={end} backing=retained");
        void StackSnapshot(int a, int r, int live, int end) => rows.Add($"M16_016_STACK_LEASES acquired={a} returned={r} live={live} stackBytes={live * 65536} contextBytes={live * 688} peakLive={(a == 1 ? 1 : 2)} peakStackBytes={(a == 1 ? 65536 : 131072)} peakContextBytes={(a == 1 ? 688 : 1376)} end={end}");
        Call(39, 1); rows.Add(Backing);
        rows.Add($"M16_016_STACK_LEASE identity={Main} index=0 stack=2342912 stackBytes=65536 context=2154496 contextBytes=688");
        StackSnapshot(1, 0, 1, 0);
        for (int i = 0; i < 4; i++)
        {
            rows.Add(Slot(i, 1, 0, Quiet + (ulong)i));
            if (i < 3) rows.Add(Loan(i, 1, 0, 0)); // Fourth is a pending SYN without window loans.
            rows.Add($"M07_030_HIGHWATER bulk={i + 1} compact=0 clock={Quiet + (ulong)i}");
        }
        SocketSnapshot(3, 0, 3, 3, 0);
        if (accept)
        {
            Call(43, 6);
            rows.Add($"M16_016_SOCKET identity={Main} ordinal={ordinal} nr=43 fd=6 fdGeneration=1 description=7 inode=65537 slot=0 owner=8388608 epoch=1 generation=1 index=0 local=167772687 localPort=8080 remote=167772674 remotePort=35000 offset=0 bytes=0 peek=0 sent=0 received=0");
        }
        Call(56, 2);
        rows.Add($"M16_016_STACK_LEASE identity={Child} index=1 stack=2416640 stackBytes=65536 context=2155184 contextBytes=688");
        StackSnapshot(2, 0, 2, 0);
        if (reuse)
        {
            rows.Add(Loan(0, 1, 1, 0)); rows.Add(Slot(0, 1, 1, Quiet + 4)); SocketSnapshot(3, 1, 2, 3, 0);
            rows.Add(Slot(0, 2, 0, Quiet + 5)); rows.Add(Loan(0, 2, 0, 0)); SocketSnapshot(4, 1, 3, 3, 0);
        }
        Call(9, -12);
        rows.Add($"M16_016_QUOTA_ABORT identity={Main} ordinal={ordinal} nr=9 backed=9216 pool=9216 exit=99 policy=terminate cause=physical-backing");
        foreach (ulong id in new[] { Main, Child })
        {
            rows.Add($"M16_008_EXIT identity={id} clearTidResult=0");
            rows.Add($"M16_008_REAP old={id} next={id + (1UL << 32)} context=zero stack=zero slot=zero");
        }
        StackSnapshot(2, 2, 0, 1);
        for (int fd = 0; fd < 3; fd++) rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");
        rows.Add(Loan(0, reuse ? 2 : 1, 1, 1)); rows.Add(Loan(1, 1, 1, 1)); rows.Add(Loan(2, 1, 1, 1));
        SocketSnapshot(reuse ? 4 : 3, reuse ? 4 : 3, 0, 3, 1);
        rows.Add($"M16_016_QUOTA_SOCKETS bulkAborted=4 compactDiscarded=0 clock={Quiet + 6} retained=0 transport=aborted timewait=discarded buffers=scrubbed descriptors=closed table=retired");
        rows.Add($"M07_033_END issued={(reuse ? 5 : 4)} secret=zero seed=zero lease=released firmwareCallbacks=none");
        rows.Add("M16_008_OWNER_END reaped=2 transfers=8 irqs=1 exit=99 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained");
        return rows.ToArray();
    }

    [HostTest("quota components preserve retained backing separately from actual stack and socket lease peaks and end zero")]
    private static void CopiedMeasurements()
    {
        string[] rows = Rows(); string[] before = rows.ToArray();
        var result = QemuTranscripts.InspectNativeAotServiceQuotaComponentsForTests(rows);
        HostCheck.True(rows.SequenceEqual(before) && result.Backing.CooperativePages == 354 && result.Backing.WorkerMappedBytes == 1048576);
        HostCheck.True(result.StackLeases.Count == 2 && result.StackSnapshots[^1].End && result.StackSnapshots[^1].Live == 0 &&
            result.StackSnapshots[^1].PeakStackBytes == 131072 && result.StackSnapshots[^1].PeakContextBytes == 1376);
        HostCheck.True(result.SocketSnapshots[^1].BackingBytes == 327680 && result.SocketSnapshots[^1].Live == 0 &&
            result.SocketSnapshots[^1].PeakReceiveBytes == 24576 && result.SocketSnapshots[^1].PeakSendBytes == 98304);
        foreach (var lease in result.StackLeases)
            HostCheck.True(rows[lease.RecordIndex].StartsWith("M16_016_STACK_LEASE ", StringComparison.Ordinal) &&
                rows[lease.CreateCallRecordIndex].StartsWith("M16_008_CALL ", StringComparison.Ordinal) &&
                rows[lease.ReapRecordIndex].Contains("old=" + lease.Identity, StringComparison.Ordinal));
        var retained = result.StackLeases[0]; rows[retained.RecordIndex] = "changed after inspection";
        HostCheck.True(result.StackLeases[0] == retained);
        bool immutable = false;
        try { ((IList<QemuTranscripts.ServiceQuotaStackLease>)result.StackLeases)[0] = default; } catch (NotSupportedException) { immutable = true; }
        HostCheck.True(immutable);
    }

    [HostTest("quota components bind exact fresh socket generations including pending SYN and normal return before reuse")]
    private static void SocketGenerations()
    {
        var result = QemuTranscripts.InspectNativeAotServiceQuotaComponentsForTests(Rows(true));
        HostCheck.True(result.SocketSnapshots[^1].Acquired == 4 && result.SocketSnapshots[^1].Returned == 4 && result.SocketLeases.Count == 8);
        HostCheck.True(result.SocketLeases.Count(l => l.Lifetime.Slot == 0 && l.Lifetime.Generation == 1) == 2 &&
            result.SocketLeases.Count(l => l.Lifetime.Slot == 0 && l.Lifetime.Generation == 2) == 2 &&
            result.SocketLeases.All(l => l.Lifetime.Slot != 3));
        var valid = Rows(true);
        Mutate(valid, "M16_016_SOCKET_LEASE event=0 slot=0 owner=8388608 epoch=1 generation=2", "generation=2", "generation=1");
        Mutate(valid, "M16_016_SOCKET_LEASE event=1 slot=0", "index=0", "index=1");
        Mutate(valid, "M16_016_SOCKET_LEASE event=1 slot=0", "end=0", "end=1");
    }

    [HostTest("quota components reject missing duplicate unknown arithmetic aliases forged ends and reordered native witnesses")]
    private static void Refusals()
    {
        string[] valid = Rows();
        foreach (string prefix in new[] { "M16_016_STACK_BACKING ", "M16_016_STACK_LEASE ", "M16_016_STACK_LEASES ", "M16_016_SOCKET_LEASE ", "M16_016_SOCKET_LEASES " })
        {
            int at = Array.FindIndex(valid, r => r.StartsWith(prefix, StringComparison.Ordinal));
            Reject(valid.Where((_, i) => i != at).ToArray());
            var duplicate = valid.ToList(); duplicate.Insert(at, valid[at]); Reject(duplicate.ToArray());
        }
        foreach (var change in new[] {
            ("M16_016_STACK_BACKING ", "workerMappedBytes=1048576", "workerMappedBytes=65536"),
            ("M16_016_STACK_LEASE identity=4294967298", "context=2155184", "context=2154496"),
            ("M16_016_STACK_LEASE identity=4294967298", "stack=2416640", "stack=2342912"),
            ("M16_016_STACK_LEASE identity=4294967298", "identity=4294967298", "identity=8589934594"),
            ("M16_016_STACK_LEASES acquired=2 returned=2", "end=1", "end=0"),
            ("M16_016_STACK_LEASES acquired=2 returned=0", "peakStackBytes=131072", "peakStackBytes=65536"),
            ("M16_016_SOCKET_LEASE event=0", "rxBytes=8192", "rxBytes=327680"),
            ("M16_016_SOCKET_LEASES backingBytes=327680 acquired=3 returned=3", "live=0", "live=1"),
            ("M16_016_SOCKET_LEASES backingBytes=327680 acquired=3 returned=0", "peakLive=3", "peakLive=4"),
            ("M16_008_REAP old=4294967298", "next=8589934594", "next=4294967298") })
            Mutate(valid, change.Item1, change.Item2, change.Item3);
        Reject([..valid, "M16_016_STACK_UNKNOWN end=1"]);
        var wrongOrder = valid.ToList(); int final = wrongOrder.FindIndex(r => r.StartsWith("M16_016_STACK_LEASES acquired=2 returned=2", StringComparison.Ordinal));
        string row = wrongOrder[final]; wrongOrder.RemoveAt(final); wrongOrder.Insert(wrongOrder.FindIndex(r => r.StartsWith("M16_008_REAP ", StringComparison.Ordinal)), row); Reject(wrongOrder.ToArray());
        Reject([]); Reject(null!); Reject([..valid, ""]);
    }

    [HostTest("quota socket CALL reader retains original indices beside dedicated lease metadata and rejects aliases")]
    private static void SocketCallClassification()
    {
        var rows = Rows(accept: true);
        var census = QemuTranscripts.InspectNativeAotServiceQuotaTransportForTests(rows);
        var observed = QemuTranscripts.InspectNativeAotServiceQuotaSocketIoForTests(rows, census.FreshLifetimes);
        HostCheck.True(observed.Count == 1 && observed[0].Accept && observed[0].RecordIndex == observed[0].CallRecordIndex + 1 &&
            rows[observed[0].RecordIndex].StartsWith("M16_016_SOCKET identity=", StringComparison.Ordinal));
        foreach (var pair in new[] { ("M16_016_SOCKET_LEASE ", "M16_016_SOCKET_LEASEx "), ("M16_016_SOCKET_LEASES ", "M16_016_SOCKET_LEASESx ") })
        {
            string[] bad = rows.Select(r => r.Replace(pair.Item1, pair.Item2, StringComparison.Ordinal)).ToArray();
            Reject(bad);
            bool refused = false;
            try { QemuTranscripts.InspectNativeAotServiceQuotaSocketIoForTests(bad, census.FreshLifetimes); } catch (ToolFailure) { refused = true; }
            HostCheck.True(refused);
        }
    }

    private static void Mutate(string[] input, string prefix, string oldValue, string newValue)
    {
        var changed = input.ToArray(); int at = Array.FindIndex(changed, r => r.StartsWith(prefix, StringComparison.Ordinal));
        HostCheck.True(at >= 0 && changed[at].Contains(oldValue, StringComparison.Ordinal));
        changed[at] = changed[at].Replace(oldValue, newValue, StringComparison.Ordinal); Reject(changed);
    }
    private static void Reject(string[] rows)
    {
        bool refused = false; try { QemuTranscripts.InspectNativeAotServiceQuotaComponentsForTests(rows); } catch (ToolFailure) { refused = true; }
        HostCheck.True(refused);
    }
}
