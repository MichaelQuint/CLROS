using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Join borrowed, value-only quota leaf reports to the outer NIC,
    /// DMA, allocator and CPU-root observations. Require deliberate abort rather
    /// than natural TIMEWAIT expiry or an ordinary success terminal. Inputs are
    /// read synchronously without mutation. Invalid or incomplete values throw;
    /// supplied reports authenticate no original framing, parent, program,
    /// native code, peer, packets, process or guest acceptance.</summary>
    public static ServiceEnvelopeInspection InspectNativeAotServiceQuotaEnvelopeForTests(string[] rows,
        WebParentReport parent, ThreadingProtocolReport protocol, ServiceRandomInspection random,
        ServiceQuotaTransportInspection transport, ServiceQuotaInspection quota,
        (ulong Before, ulong After, ulong Allocated, ulong Command, ulong PeerSent, ulong PeerReceived) baseline)
    {
        Need(rows is not null && rows.Length is >0 and <=1_000_000 && rows.All(row => row is not null &&
            row.Length is >0 and <=4096 && row.All(c => c is >= ' ' and <= '~')) &&
            rows.Sum(row => (long)row.Length + 2) <=128L *1024 *1024, "service_quota_envelope_input");
        int One(string marker, bool prefix = true)
        {
            int[] found = Enumerable.Range(0, rows!.Length).Where(i => prefix ? rows[i].StartsWith(marker, StringComparison.Ordinal) : rows[i] == marker).ToArray();
            Need(found.Length == 1, "service_quota_envelope_once"); return found[0];
        }
        Need(One("TEST_START m16-011", false) == 0 && One("TEST_START m16-012", false) == 1 &&
            One("TEST_START m16-009", false) == 2 && !rows!.Any(row => row.StartsWith("TEST_PASS", StringComparison.Ordinal) && row != "TEST_PASS m16.014.startup" ||
                row.StartsWith("TEST_FAIL", StringComparison.Ordinal) || row.StartsWith("OS_SMOKE", StringComparison.Ordinal) ||
                row.StartsWith("CLROS_M16_009", StringComparison.Ordinal) || row.StartsWith("CLROS_M16_011", StringComparison.Ordinal) ||
                row.StartsWith("CLROS_M16_012", StringComparison.Ordinal) || row == "CLROS_M16_014_READY" || row.Contains("m16-013", StringComparison.Ordinal) ||
                row.StartsWith("M07_030_END", StringComparison.Ordinal)), "service_quota_envelope_profile");
        int socketsAt = One("M16_016_QUOTA_SOCKETS "), randomAt = One("M11_012_END "), secretAt = One("M07_033_END "),
            ownerAt = One("M16_008_OWNER_END "), serviceAt = One("M16_014_END "),
            failureAt = One("M16_016_QUOTA_PROCESS_FAILURE "), nativeAt = One("M16_016_QUOTA_NATIVE "),
            restoreAt = One("M16_016_QUOTA_RESTORE "), wireAt = One("M16_011_WIRE "), endAt = One("M16_011_END "),
            cleanupAt = One("M16_016_QUOTA_CLEANUP "), terminalAt = One("CLROS_M16_016_QUOTA_CLEANUP", false);
        Need(socketsAt < randomAt && secretAt == randomAt + 1 && secretAt < ownerAt && serviceAt == ownerAt + 1 &&
            failureAt == serviceAt + 1 && nativeAt == failureAt + 1 && restoreAt == nativeAt + 1 && wireAt == restoreAt + 1 &&
            endAt == wireAt + 1 && cleanupAt == endAt + 4 && terminalAt == cleanupAt + 1 && terminalAt == rows!.Length - 1,
            "service_quota_envelope_order");
        Need(rows![failureAt] == "M16_016_QUOTA_PROCESS_FAILURE exit=99 cause=physical-backing" &&
            rows[nativeAt] == "M16_016_QUOTA_NATIVE process=drained timer=restored descriptors=closed guard=detached mappings=restored pool=scrubbed initialStack=zero random=zero reservation=retained" &&
            rows[restoreAt] == "M16_016_QUOTA_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true" &&
            rows[cleanupAt] == "M16_016_QUOTA_CLEANUP exit=99 process=failed workers=reaped memory=zero cpu=restored nic=retired dma=zero allocator=unchanged pci=restored canaries=valid reservation=retained",
            "service_quota_envelope_cleanup");
        Dictionary<string, ulong> Suffixed(int at, string prefix, string suffix, params string[] fields)
        { Need(rows[at].EndsWith(suffix, StringComparison.Ordinal), "service_quota_envelope_suffix"); return ReadRecord(rows[at][..^suffix.Length], prefix, fields); }
        var sockets = Suffixed(socketsAt, "M16_016_QUOTA_SOCKETS", " retained=0 transport=aborted timewait=discarded buffers=scrubbed descriptors=closed table=retired",
            "bulkAborted:dec", "compactDiscarded:dec", "clock:dec");
        var entropy = Suffixed(randomAt, "M11_012_END", " generator=zero seed=zero firmwareCallbacks=none", "requests:dec", "generatedBytes:dec");
        var secret = Suffixed(secretAt, "M07_033_END", " secret=zero seed=zero lease=released firmwareCallbacks=none", "issued:dec");
        var service = Suffixed(serviceAt, "M16_014_END", " process=drained mappings=restored pool=scrubbed random=zero reservation=retained",
            "identity:dec", "calls:dec", "started:dec", "nanos:dec", "reaped:dec");
        var budget = Suffixed(One("M16_014_POLICY "), "M16_014_POLICY", " elapsedLimit=none counters=reject-overflow workers=individual cancellation=owned",
            "started:dec", "callsPerTurn:dec", "progressNanos:dec");
        var wire = ReadRecord(rows[wireAt], "M16_011_WIRE", "received:dec", "arp:dec", "accepted:dec", "ignored:dec", "capacity:dec", "arpCopied:dec", "tcpCopied:dec");
        var end = Suffixed(endAt, "M16_011_END", " retained=0 buffers=scrubbed descriptors=closed workers=reaped",
            "polls:dec", "received:dec", "copied:dec", "fresh:dec", "retired:dec", "start:dec", "end:dec", "maxDeadline:dec");
        var history = transport.History;
        Need(history.Connections >=4 && end["fresh"] == (ulong)history.Connections && transport.BulkAborted <= end["fresh"] &&
            end["retired"] == end["fresh"] - transport.BulkAborted && end["maxDeadline"] == history.MaximumDeadline &&
            sockets["bulkAborted"] == transport.BulkAborted && sockets["compactDiscarded"] == transport.CompactDiscarded &&
            sockets["clock"] == transport.AbortClock && end["end"] == transport.AbortClock && end["polls"] >0 &&
            entropy["requests"] == random.Requests && entropy["generatedBytes"] == random.GeneratedBytes && secret["issued"] == history.IssuedSequences &&
            budget["callsPerTurn"] == LinuxSyscallGateway.MaximumThreadCalls && budget["progressNanos"] == LinuxThreadExecutionBudget.ServiceProgressNanoseconds &&
            end["start"] <= parent.PeerStart && parent.PeerStart >0 && parent.PeerEnd >= parent.PeerStart && budget["started"] >= parent.PeerEnd &&
            service["started"] == budget["started"] && service["nanos"] >= service["started"] && service["nanos"] <= end["end"] && end["end"] <= long.MaxValue &&
            protocol.Calls >0 && service["calls"] >0 && service["calls"] <= (ulong)protocol.Calls &&
            (uint)service["identity"] is >=1 and <=16 && service["identity"] >>32 !=0 &&
            protocol.Reaped >0 && service["reaped"] == (ulong)protocol.Reaped && service["reaped"] == quota.Reaped,
            "service_quota_envelope_lifetimes");
        Dictionary<string, ulong> Row(int offset, uint stage)
        { var row = ReadRecord(rows[endAt + offset], "M07_026_ROW", "stage:dec", "a:dec", "b:dec", "c:dec", "d:dec"); Need(row["stage"] == stage, "service_quota_envelope_stage"); return row; }
        var stop = Row(1, 4); var dma = Row(2, 5); var allocator = Row(3, 6);
        Need(end["received"] == wire["received"] && end["copied"] == wire["tcpCopied"] &&
            (decimal)wire["received"] == (decimal)wire["arp"] + wire["accepted"] + wire["ignored"] + wire["capacity"] && wire["capacity"] ==0 &&
            baseline.PeerSent ==1 && baseline.PeerReceived >0 && (decimal)stop["a"] == (decimal)baseline.PeerReceived + wire["received"] &&
            (decimal)stop["b"] == (decimal)baseline.PeerSent + wire["arpCopied"] + wire["tcpCopied"] && stop["c"] is >=1 and <=1024 && stop["d"] ==2 &&
            dma["a"] ==8 && dma["b"] ==0 && dma["c"] ==8 && dma["d"] ==1 && allocator["a"] == baseline.Before &&
            allocator["b"] == baseline.After && allocator["c"] == baseline.Allocated && allocator["d"] == baseline.Command,
            "service_quota_envelope_nic_cleanup");
        var bound = Suffixed(One("M16_008_BOUND "), "M16_008_BOUND", " timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack",
            "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
        Need(bound["root"] == parent.KernelRoot && new[] { parent.UserRoot, parent.KernelRoot, parent.Dma, bound["pool"] }.All(value => value >=4096 && value %4096 ==0) &&
            parent.UserRoot <=0xfffff000 && parent.KernelRoot <=0xfffff000 && parent.Dma <=0x100000000UL -32768 &&
            bound["pool"] <=0x100000000UL -WebThreadPoolPages *4096UL && parent.Period >0, "service_quota_envelope_geometry");
        DistinctExtents([(parent.UserRoot,4096), (parent.KernelRoot,4096), (parent.Dma,32768), (bound["pool"],WebThreadPoolPages *4096UL)], "service_quota_envelope_storage");
        foreach (string timing in rows.Where(row => row.StartsWith("M16_008_TIMING ", StringComparison.Ordinal) || row.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal)))
            Need(timing.EndsWith(" periodFs=" + parent.Period.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal), "service_quota_envelope_timing_period");
        return new(wire["received"], wire["arp"], wire["accepted"], wire["ignored"], wire["capacity"], wire["arpCopied"], wire["tcpCopied"],
            end["polls"], end["fresh"], end["retired"], end["start"], end["end"], end["maxDeadline"]);
    }
}
