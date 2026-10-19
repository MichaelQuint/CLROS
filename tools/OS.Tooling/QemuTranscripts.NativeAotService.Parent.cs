using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Value-only outer wire, lifetime and cleanup accounting. Does not
    /// establish independent packets, runner ownership or guest qualification.</summary>
    public readonly record struct ServiceEnvelopeInspection(ulong Received, ulong Arp, ulong Accepted, ulong Ignored,
        ulong Capacity, ulong ArpCopied, ulong TcpCopied, ulong Polls, ulong Fresh, ulong Retired,
        ulong Started, ulong Ended, ulong MaximumDeadline);
    /// <summary>Joined original parent, complete service leaf and outer cleanup
    /// observations. Native ABI, independent peer/packets and runner remain separate.</summary>
    public readonly record struct ServiceCaptureReport(WebParentReport Parent, ServiceReplayReport Leaf, ServiceEnvelopeInspection Envelope);

    /// <summary>Borrow an original package, prepared program and complete raw
    /// capture. Hold current sources while checking the original firmware/APIC/
    /// NIC prefix, service syscall/lifecycle replay and finite outer cleanup.
    /// Post-exit diagnostic capture must end at TEST_PASS m16-012; the native
    /// return stub emits no ordinary boot-sequence or firmware suffix.
    /// Invalid or incomplete input throws; no app is executed and no runner,
    /// packet, native ABI or guest-acceptance authority is created.</summary>
    public static ServiceCaptureReport ValidateNativeAotServiceCapture(string root, byte[] package,
        NativeAotServiceProgram program, ReadOnlySpan<byte> raw)
    {
        Need(raw.Length is >FirmwarePrefixBytes and <=128 * 1024 * 1024, "service_capture_extent");
        using AuthorityFiles sources = new(); NativeAotServiceProgram.HoldSources(root, sources);
        _ = NativeAotServiceProgram.RequirePackage(package);
        ReadOnlySpan<byte> outer = "TEST_START m16-011\r\nTEST_START m16-012\r\n"u8;
        int at = raw.IndexOf("TEST_START m16-011\r\n"u8);
        Need(at >= FirmwarePrefixBytes && raw[at - 2] == 13 && raw[at - 1] == 10 && raw[at..].StartsWith(outer), "service_capture_outer_start");
        var parent = InspectNativeAotWebParentForTests(package, raw[..at]);
        var leaf = ValidateNativeAotServiceTail(root, package, program, SocketRawLeaf(raw[(at + outer.Length)..]), parent.UserRoot, parent.KernelRoot);
        var framed = NativeAotThreadingOutput.ReadService(raw[at..]);
        Need(framed.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "service_capture_terminal");
        string[] rows = framed.NormalizedTail[..^2].Split("\r\n", StringSplitOptions.None);
        string prefix = DecodeUtf8(raw[FirmwarePrefixBytes..at].ToArray());
        string[] parents = prefix[..^2].Split("\r\n", StringSplitOptions.None);
        ulong[] allocator = PrefixRow(parents, "M07_ARP A", 4), binding = PrefixRow(parents, "M07_ARP 0", 4);
        var peer = ReadRecord(parents.Single(row => row.StartsWith("M16_011_PEER ", StringComparison.Ordinal)), "M16_011_PEER",
            "mac:dec", "sent:dec", "received:dec", "start:dec", "end:dec");
        var envelope = InspectNativeAotServiceEnvelopeForTests(rows, parent, leaf,
            (allocator[0], allocator[1], allocator[2], binding[2], peer["sent"], peer["received"]));
        _ = NativeAotServiceProgram.RequirePackage(package);
        NativeAotServiceProgram.HoldSources(root, sources); sources.VerifyUnchanged(); return new(parent, leaf, envelope);
    }

    /// <summary>Value-only outer-envelope seam. Caller-supplied parent/leaf
    /// reports and allocator/peer baseline authenticate nothing themselves.
    /// Borrow bounded immutable normalized rows synchronously; require exact
    /// enabled011/012 wrappers, real terminal rows, matching totals and distinct
    /// retained roots/pool/DMA. Malformed or contradictory input throws.</summary>
    public static ServiceEnvelopeInspection InspectNativeAotServiceEnvelopeForTests(string[] rows, WebParentReport parent,
        ServiceReplayReport leaf, (ulong Before, ulong After, ulong Allocated, ulong Command, ulong PeerSent, ulong PeerReceived) baseline)
    {
        Need(rows is not null && rows.Length is >0 and <=1_000_000 && rows.All(row => row is not null &&
            row.Length is >0 and <=4096 && row.All(c => c is >= ' ' and <= '~')) &&
            rows.Sum(row => (long)row.Length + 2) <=128L * 1024 * 1024, "service_envelope_input");
        int One(string marker, bool prefix = true)
        {
            int[] found = Enumerable.Range(0, rows!.Length).Where(i => prefix ? rows[i].StartsWith(marker, StringComparison.Ordinal) : rows[i] == marker).ToArray();
            Need(found.Length == 1, "service_envelope_once"); return found[0];
        }
        Need(One("TEST_START m16-011", false) == 0 && One("TEST_START m16-012", false) == 1 &&
            One("TEST_START m16-009", false) == 2 && !rows!.Any(row => row.Contains("m16-013", StringComparison.Ordinal) ||
                row.StartsWith("CLROS_M16_013", StringComparison.Ordinal) || row.StartsWith("TEST_FAIL", StringComparison.Ordinal) ||
                row.StartsWith("OS_SMOKE_FAIL", StringComparison.Ordinal)), "service_envelope_profile");
        int transportAt = One("M07_030_END "), randomAt = One("M11_012_END "), sequenceAt = One("M07_033_END "),
            serviceAt = One("M16_014_END "), servicePass = One("TEST_PASS m16.014", false),
            restored = One("M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", false),
            leafReady = One("CLROS_M16_009_READY", false), leafPass = One("TEST_PASS m16-009", false),
            wireAt = One("M16_011_WIRE "), endAt = One("M16_011_END ");
        Need(transportAt < randomAt && sequenceAt == randomAt + 1 && sequenceAt < serviceAt && servicePass == serviceAt + 1 &&
            restored == servicePass + 1 && leafReady == restored + 1 && leafPass == leafReady + 1 && wireAt == leafPass + 1 && endAt == wireAt + 1,
            "service_envelope_terminal_order");
        var wire = ReadRecord(rows![wireAt], "M16_011_WIRE", "received:dec", "arp:dec", "accepted:dec", "ignored:dec", "capacity:dec", "arpCopied:dec", "tcpCopied:dec");
        const string cleanup = " retained=0 buffers=scrubbed descriptors=closed workers=reaped";
        Need(rows[endAt].EndsWith(cleanup, StringComparison.Ordinal), "service_envelope_cleanup");
        var end = ReadRecord(rows[endAt][..^cleanup.Length], "M16_011_END", "polls:dec", "received:dec", "copied:dec", "fresh:dec", "retired:dec", "start:dec", "end:dec", "maxDeadline:dec");
        int cursor = endAt + 1;
        Dictionary<string, ulong> Row(uint stage)
        {
            Need(cursor < rows.Length, "service_envelope_short_cleanup");
            var value = ReadRecord(rows[cursor++], "M07_026_ROW", "stage:dec", "a:dec", "b:dec", "c:dec", "d:dec");
            Need(value["stage"] == stage, "service_envelope_stage"); return value;
        }
        var stop = Row(4); var pool = Row(5); var allocator = Row(6);
        foreach (string marker in new[] { "CLROS_M07_026_READY", "TEST_PASS m07-026", "CLROS_M16_011_READY", "TEST_PASS m16-011", "CLROS_M16_012_READY", "TEST_PASS m16-012" })
            Need(cursor < rows.Length && rows[cursor++] == marker, "service_envelope_outer_terminal");
        Need(cursor == rows.Length, "service_envelope_extra_record");
        const string compactSuffix = " owner=retired", randomSuffix = " generator=zero seed=zero firmwareCallbacks=none",
            sequenceSuffix = " secret=zero seed=zero lease=released firmwareCallbacks=none",
            serviceSuffix = " process=drained mappings=restored pool=scrubbed random=zero reservation=retained";
        Need(rows[transportAt].EndsWith(compactSuffix, StringComparison.Ordinal) && rows[randomAt].EndsWith(randomSuffix, StringComparison.Ordinal) &&
            rows[sequenceAt].EndsWith(sequenceSuffix, StringComparison.Ordinal) && rows[serviceAt].EndsWith(serviceSuffix, StringComparison.Ordinal), "service_envelope_owner_end");
        var compact = ReadRecord(rows[transportAt][..^compactSuffix.Length], "M07_030_END", "transfers:dec", "retired:dec", "bulkHighWater:dec", "compactHighWater:dec", "bytesPerRecord:dec", "clock:dec", "deadline:dec");
        var random = ReadRecord(rows[randomAt][..^randomSuffix.Length], "M11_012_END", "requests:dec", "generatedBytes:dec");
        var secret = ReadRecord(rows[sequenceAt][..^sequenceSuffix.Length], "M07_033_END", "issued:dec");
        var service = ReadRecord(rows[serviceAt][..^serviceSuffix.Length], "M16_014_END", "identity:dec", "calls:dec", "started:dec", "nanos:dec", "reaped:dec");
        const string policySuffix = " elapsedLimit=none counters=reject-overflow workers=individual cancellation=owned";
        string policy = rows[One("M16_014_POLICY ")]; Need(policy.EndsWith(policySuffix, StringComparison.Ordinal), "service_envelope_policy");
        var budget = ReadRecord(policy[..^policySuffix.Length], "M16_014_POLICY", "started:dec", "callsPerTurn:dec", "progressNanos:dec");
        Need(leaf.Transport.Connections >= 10 && end["fresh"] == (ulong)leaf.Transport.Connections && end["retired"] == end["fresh"] &&
            compact["transfers"] == (ulong)leaf.Transport.Transfers && compact["retired"] == (ulong)leaf.Transport.Retired &&
            compact["bulkHighWater"] == (ulong)leaf.Transport.BulkHighWater && compact["compactHighWater"] == (ulong)leaf.Transport.CompactHighWater &&
            compact["deadline"] == leaf.Transport.MaximumDeadline && compact["clock"] == end["end"] && secret["issued"] == leaf.Transport.IssuedSequences &&
            random["requests"] == leaf.Random.Requests && random["generatedBytes"] == leaf.Random.GeneratedBytes &&
            compact["bytesPerRecord"] == (ulong)System.Runtime.CompilerServices.Unsafe.SizeOf<OS.Net.TcpTimeWaitTable.Entry>() &&
            budget["callsPerTurn"] == LinuxSyscallGateway.MaximumThreadCalls && budget["progressNanos"] == LinuxThreadExecutionBudget.ServiceProgressNanoseconds &&
            end["maxDeadline"] == leaf.Transport.MaximumDeadline && end["end"] >= end["maxDeadline"] && end["polls"] > 0 &&
            end["start"] <= parent.PeerStart && parent.PeerStart > 0 && parent.PeerEnd >= parent.PeerStart && budget["started"] >= parent.PeerEnd &&
            service["started"] == budget["started"] && service["nanos"] >= service["started"] && end["end"] >= service["nanos"] && end["end"] <= long.MaxValue &&
            service["nanos"] - service["started"] > LinuxThreadExecutionBudget.RuntimeNanoseconds && service["calls"] > LinuxSyscallGateway.MaximumThreadCalls &&
            service["calls"] <= (ulong)leaf.Protocol.Calls && (uint)service["identity"] is >=1 and <=16 && service["identity"] >> 32 != 0 &&
            service["reaped"] == (ulong)leaf.Protocol.Reaped, "service_envelope_lifetimes");
        Need(end["received"] == wire["received"] && end["copied"] == wire["tcpCopied"] &&
            (decimal)wire["received"] == (decimal)wire["arp"] + wire["accepted"] + wire["ignored"] + wire["capacity"] && wire["capacity"] == 0 &&
            baseline.PeerSent == 1 && baseline.PeerReceived > 0 && (decimal)stop["a"] == (decimal)baseline.PeerReceived + wire["received"] &&
            (decimal)stop["b"] == (decimal)baseline.PeerSent + wire["arpCopied"] + wire["tcpCopied"] && stop["c"] is >=1 and <=1024 && stop["d"] == 2 &&
            pool["a"] == 8 && pool["b"] == 0 && pool["c"] == 8 && pool["d"] == 1 &&
            allocator["a"] == baseline.Before && allocator["b"] == baseline.After && allocator["c"] == baseline.Allocated && allocator["d"] == baseline.Command,
            "service_envelope_nic_cleanup");
        const string boundSuffix = " timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack";
        string line = rows[One("M16_008_BOUND ")]; Need(line.EndsWith(boundSuffix, StringComparison.Ordinal), "service_envelope_bound");
        var bound = ReadRecord(line[..^boundSuffix.Length], "M16_008_BOUND", "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
        Need(bound["root"] == parent.KernelRoot && new[] { parent.UserRoot, parent.KernelRoot, parent.Dma, bound["pool"] }.All(value => value >= 4096 && value % 4096 == 0) &&
            parent.UserRoot <= 0xfffff000 && parent.KernelRoot <= 0xfffff000 && parent.Dma <= 0x100000000UL - 32768 &&
            bound["pool"] <= 0x100000000UL - WebThreadPoolPages * 4096UL && parent.Period > 0, "service_envelope_geometry");
        DistinctExtents([(parent.UserRoot, 4096), (parent.KernelRoot, 4096), (parent.Dma, 32768), (bound["pool"], WebThreadPoolPages * 4096UL)], "service_envelope_storage");
        foreach (string timing in rows.Where(row => row.StartsWith("M16_008_TIMING ", StringComparison.Ordinal) || row.StartsWith("M16_008_SYSCALL_TIME ", StringComparison.Ordinal)))
            Need(timing.EndsWith(" periodFs=" + parent.Period.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal), "service_envelope_timing_period");
        return new(wire["received"], wire["arp"], wire["accepted"], wire["ignored"], wire["capacity"], wire["arpCopied"], wire["tcpCopied"],
            end["polls"], end["fresh"], end["retired"], end["start"], end["end"], end["maxDeadline"]);
    }
}
