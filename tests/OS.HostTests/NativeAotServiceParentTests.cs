using OS.Tooling;

internal static class NativeAotServiceParentTests
{
    private const ulong Deadline = 650_000_000_000, Finished = Deadline + 1;
    private static readonly (ulong Before, ulong After, ulong Allocated, ulong Command, ulong PeerSent, ulong PeerReceived) Baseline = (100, 100, 8, 6, 1, 2);
    private static QemuTranscripts.WebParentReport Parent => new(200, 0x200000, 0x300000, 100_000_000, 10_000_000,
        0x10000000, 20, 21, 123, new(1, 60, 2, 0x525400123456));
    private static QemuTranscripts.ServiceReplayReport Leaf => new(new(6000, 2, 3, 4, 4, 1, 10, 0),
        new(0x100000001, 6, 31, 3, 0x100000001, 3), new(33, 17376, 2, 1, 1, 1), new(10, 10, 10, 2, 10, Deadline, 10));

    [HostTest("service parent envelope joins complete011012 outer cleanup and borrowed inner totals", HostLane.Fast, 0)]
    private static void Complete()
    {
        string[] rows = Rows(), before = rows.ToArray();
        var value = Inspect(rows); HostCheck.Equal(10UL, value.Fresh); HostCheck.Equal(10UL, value.Retired);
        HostCheck.Equal(100UL, value.Received); HostCheck.Equal(200UL, value.TcpCopied);
        HostCheck.Equal(Deadline, value.MaximumDeadline); HostCheck.Equal(Finished, value.Ended);
        HostCheck.Equal(10UL, value.Started); HostCheck.True(rows.SequenceEqual(before));
        Array.Fill(rows, "changed"); HostCheck.Equal(Finished, value.Ended);
    }

    [HostTest("service parent envelope refuses missing duplicated reordered finite and incomplete terminals", HostLane.Fast, 1)]
    private static void Terminals()
    {
        string[] rows = Rows();
        foreach (string prefix in new[] { "TEST_START m16-011", "TEST_START m16-012", "TEST_START m16-009", "M07_030_END ",
            "M11_012_END ", "M07_033_END ", "M16_014_END ", "TEST_PASS m16.014", "M16_009_RESTORE ", "CLROS_M16_009_READY", "TEST_PASS m16-009",
            "M16_011_WIRE ", "M16_011_END ", "M07_026_ROW stage=4 ", "M07_026_ROW stage=5 ", "M07_026_ROW stage=6 ",
            "CLROS_M07_026_READY", "TEST_PASS m07-026", "CLROS_M16_011_READY", "TEST_PASS m16-011", "CLROS_M16_012_READY", "TEST_PASS m16-012" })
        {
            int at = Array.FindIndex(rows, row => row.StartsWith(prefix, StringComparison.Ordinal)); HostCheck.True(at >= 0);
            Reject(rows.Where((_, i) => i != at).ToArray());
            var duplicate = rows.ToList(); duplicate.Insert(at, rows[at]); Reject(duplicate.ToArray());
        }
        var swapped = rows.ToArray(); (swapped[^1], swapped[^2]) = (swapped[^2], swapped[^1]); Reject(swapped);
        Reject([..rows, "TEST_PASS m16-013"]); Reject([..rows, "extra"]);
        Reject(rows.Select(row => row.Replace("workers=reaped", "workers=retained", StringComparison.Ordinal)).ToArray());
        Reject(rows.Select(row => row.Replace("root=true descriptors=true", "root=false descriptors=true", StringComparison.Ordinal)).ToArray());
        var restoration = rows.ToArray(); int restoreAt = Array.FindIndex(rows, row => row.StartsWith("M16_009_RESTORE ", StringComparison.Ordinal));
        (restoration[restoreAt], restoration[restoreAt-1]) = (restoration[restoreAt-1], restoration[restoreAt]); Reject(restoration);
    }

    [HostTest("service parent envelope rejects borrowed report counter deadline peer clock and NIC mismatches", HostLane.Fast, 2)]
    private static void Accounting()
    {
        foreach (var change in new[] { ("fresh=10", "fresh=9"), ("retired=10", "retired=9"), ("maxDeadline=650000000000", "maxDeadline=649999999999"),
            ("start=10", "start=22"), ("end=650000000001", "end=649999999999"), ("polls=1000", "polls=0"),
            ("generatedBytes=17376", "generatedBytes=17375"), ("issued=10", "issued=9"),
            ("stage=4 a=102", "stage=4 a=101"), ("stage=4 a=102 b=203", "stage=4 a=102 b=202"),
            ("stage=5 a=8 b=0", "stage=5 a=8 b=1"), ("stage=6 a=100", "stage=6 a=99"),
            ("progressNanos=30000000000", "progressNanos=30000000001"), ("started=30", "started=19") })
        {
            string[] rows = Rows(); var changed = rows.Select(row => row.Replace(change.Item1, change.Item2, StringComparison.Ordinal)).ToArray();
            HostCheck.True(!rows.SequenceEqual(changed)); Reject(changed);
        }
        Reject(Rows(), leaf: Leaf with { Random = Leaf.Random with { Requests = 34 } });
        Reject(Rows(), leaf: Leaf with { Transport = Leaf.Transport with { BulkHighWater = 3 } });
        Reject(Rows(), leaf: Leaf with { Protocol = Leaf.Protocol with { Reaped = 4 } });
        Reject(Rows(), parent: Parent with { PeerEnd = 31 });
    }

    [HostTest("service parent envelope binds original timing period and disjoint roots worker pool and DMA", HostLane.Fast, 3)]
    private static void Storage()
    {
        foreach (var parent in new[] { Parent with { UserRoot = Parent.KernelRoot }, Parent with { Dma = Parent.UserRoot },
            Parent with { Dma = 0x400000 }, Parent with { UserRoot = 0 }, Parent with { KernelRoot = 0x300001 }, Parent with { Period = 1 } })
            Reject(Rows(), parent: parent);
        Reject(Rows().Select(row => row.Replace("pool=4194304", "pool=268435456", StringComparison.Ordinal)).ToArray());
        Reject(Rows().Select(row => row.Replace("root=3145728", "root=3149824", StringComparison.Ordinal)).ToArray());
    }

    // Inner stubs and borrowed reports isolate the outer envelope; they confer no full-leaf or parent authority.
    private static string[] Rows() => ["TEST_START m16-011", "TEST_START m16-012", "TEST_START m16-009",
        "M16_008_BOUND root=3145728 pool=4194304 workers=16 code=0 metadata=0 userImage=728 timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack",
        "TEST_START m16.014", "M16_014_POLICY started=30 callsPerTurn=4096 progressNanos=30000000000 elapsedLimit=none counters=reject-overflow workers=individual cancellation=owned",
        "M16_008_TIMING identity=4294967297 epoch=1 start=30 arm=31 capture=32 ready=33 periodFs=10000000",
        $"M07_030_END transfers=10 retired=10 bulkHighWater=2 compactHighWater=10 bytesPerRecord={System.Runtime.CompilerServices.Unsafe.SizeOf<OS.Net.TcpTimeWaitTable.Entry>()} clock={Finished} deadline={Deadline} owner=retired",
        "M11_012_END requests=33 generatedBytes=17376 generator=zero seed=zero firmwareCallbacks=none",
        "M07_033_END issued=10 secret=zero seed=zero lease=released firmwareCallbacks=none",
        "M16_014_END identity=4294967297 calls=5000 started=30 nanos=400000000000 reaped=3 process=drained mappings=restored pool=scrubbed random=zero reservation=retained",
        "TEST_PASS m16.014", "M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", "CLROS_M16_009_READY", "TEST_PASS m16-009",
        "M16_011_WIRE received=100 arp=1 accepted=98 ignored=1 capacity=0 arpCopied=2 tcpCopied=200",
        $"M16_011_END polls=1000 received=100 copied=200 fresh=10 retired=10 start=10 end={Finished} maxDeadline={Deadline} retained=0 buffers=scrubbed descriptors=closed workers=reaped",
        "M07_026_ROW stage=4 a=102 b=203 c=1 d=2", "M07_026_ROW stage=5 a=8 b=0 c=8 d=1", "M07_026_ROW stage=6 a=100 b=100 c=8 d=6",
        "CLROS_M07_026_READY", "TEST_PASS m07-026", "CLROS_M16_011_READY", "TEST_PASS m16-011", "CLROS_M16_012_READY", "TEST_PASS m16-012"];
    private static QemuTranscripts.ServiceEnvelopeInspection Inspect(string[] rows, QemuTranscripts.WebParentReport? parent = null,
        QemuTranscripts.ServiceReplayReport? leaf = null) => QemuTranscripts.InspectNativeAotServiceEnvelopeForTests(rows, parent ?? Parent, leaf ?? Leaf, Baseline);
    private static void Reject(string[] rows, QemuTranscripts.WebParentReport? parent = null, QemuTranscripts.ServiceReplayReport? leaf = null)
    { bool rejected = false; try { _ = Inspect(rows, parent, leaf); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }
}
