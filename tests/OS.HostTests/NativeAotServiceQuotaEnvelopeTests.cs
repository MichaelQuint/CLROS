using OS.Tooling;

internal static class NativeAotServiceQuotaEnvelopeTests
{
    private const ulong Deadline = 650_000_000_000, AbortClock = 50_000_000_000;
    private static readonly (ulong Before, ulong After, ulong Allocated, ulong Command, ulong PeerSent, ulong PeerReceived) Baseline = (100, 100, 8, 6, 1, 2);
    private static QemuTranscripts.WebParentReport Parent => new(200, 0x200000, 0x300000, 100_000_000, 10_000_000,
        0x10000000, 20, 21, 123, new(1, 60, 2, 0x525400123456));
    private static QemuTranscripts.ThreadingProtocolReport Protocol => new(6000, 2, 3, 4, 4, 1, 10, 0);
    private static QemuTranscripts.ServiceRandomInspection Random => new(33, 17376, 2, 1, 1, 1);
    private static QemuTranscripts.ServiceQuotaTransportInspection Transport => new(new(4, 1, 0, 4, 1, Deadline, 4), 3, 1, AbortClock);
    private static QemuTranscripts.ServiceQuotaInspection Quota => new(0x100000002, 40, 9000, 9216, 3);

    [HostTest("quota envelope joins original outer totals and discards live TIMEWAIT before its deadline", HostLane.Fast, 0)]
    private static void Complete()
    {
        var rows = Rows();
        var before = rows.ToArray();
        var report = Inspect(rows);
        HostCheck.Equal(4UL, report.Fresh);
        HostCheck.Equal(1UL, report.Retired);
        HostCheck.Equal(Deadline, report.MaximumDeadline);
        HostCheck.Equal(AbortClock, report.Ended);
        HostCheck.True(report.Ended < report.MaximumDeadline && rows.SequenceEqual(before));
        Array.Fill(rows, "changed");
        HostCheck.Equal(100UL, report.Received);
    }

    [HostTest("quota envelope refuses false natural retirement clock NIC DMA allocator and leaf counters", HostLane.Fast, 1)]
    private static void Accounting()
    {
        foreach (var change in new[]{("retired=1 start=","retired=4 start="),("fresh=4 retired=","fresh=3 retired="),
            ("maxDeadline=650000000000","maxDeadline=649999999999"),("polls=1000","polls=0"),
            ("generatedBytes=17376","generatedBytes=17375"),("issued=4","issued=3"),
            ("stage=4 a=102","stage=4 a=101"),("b=203 c=1 d=2","b=202 c=1 d=2"),
            ("stage=5 a=8 b=0","stage=5 a=8 b=1"),("stage=6 a=100","stage=6 a=99"),
            ("progressNanos=30000000000","progressNanos=30000000001"),("started=30","started=19"),
            ("root=true descriptors=true","root=false descriptors=true"),("canaries=valid","canaries=unknown")})
        {
            var rows = Rows();
            var changed = rows.Select(row => row.Replace(change.Item1, change.Item2, StringComparison.Ordinal)).ToArray();
            HostCheck.True(!rows.SequenceEqual(changed));
            Reject(changed);
        }
        Reject(Rows(), transport: Transport with
        {
            BulkAborted = 2
        });
        Reject(Rows(), transport: Transport with
        {
            CompactDiscarded = 0
        });
        Reject(Rows(), transport: Transport with
        {
            AbortClock = AbortClock + 1
        });
        Reject(Rows(), protocol: Protocol with
        {
            Reaped = 4
        });
        Reject(Rows(), quota: Quota with
        {
            Reaped = 2
        });
        Reject(Rows(), parent: Parent with
        {
            PeerEnd = 31
        });
    }

    [HostTest("quota envelope refuses missing duplicate ordinary terminals overlapping roots and timing drift", HostLane.Fast, 2)]
    private static void Boundaries()
    {
        foreach (string prefix in new[]{"TEST_START m16-011","TEST_START m16-012","TEST_START m16-009","M16_008_BOUND ",
            "M16_014_POLICY ","M16_016_QUOTA_SOCKETS ","M11_012_END ","M07_033_END ","M16_008_OWNER_END ","M16_014_END ",
            "M16_016_QUOTA_PROCESS_FAILURE ","M16_016_QUOTA_NATIVE ","M16_016_QUOTA_RESTORE ","M16_011_WIRE ","M16_011_END ",
            "M07_026_ROW stage=4 ","M07_026_ROW stage=5 ","M07_026_ROW stage=6 ","M16_016_QUOTA_CLEANUP ","CLROS_M16_016_QUOTA_CLEANUP"})
        {
            var rows = Rows();
            int at = Array.FindIndex(rows, row => row.StartsWith(prefix, StringComparison.Ordinal));
            HostCheck.True(at >= 0);
            Reject(rows.Where((_, i) => i != at).ToArray());
            var duplicate = rows.ToList();
            duplicate.Insert(at, rows[at]);
            Reject(duplicate.ToArray());
        }
        foreach (string terminal in new[] { "TEST_PASS m16-012", "TEST_PASS m16.014.startup forged", "TEST_FAIL m16-012 x", "OS_SMOKE_PASS", "CLROS_M16_009_READY", "CLROS_M16_014_READY", "M07_030_END bogus" })
            Reject([.. Rows()[..^1], terminal, Rows()[^1]]);
        Reject([.. Rows(), "extra"]);
        Reject(Rows(), parent: Parent with
        {
            Dma = Parent.UserRoot
        });
        Reject(Rows(), parent: Parent with
        {
            UserRoot = Parent.KernelRoot
        });
        Reject(Rows(), parent: Parent with
        {
            Period = 1
        });
        Reject(Rows().Select(row => row.Replace("pool=4194304", "pool=268435456", StringComparison.Ordinal)).ToArray());
    }

    // Deliberately incomplete inner rows isolate supplied outer values; they grant no full-leaf or original-parent authority.
    private static string[] Rows() => ["TEST_START m16-011","TEST_START m16-012","TEST_START m16-009",
        "M16_008_BOUND root=3145728 pool=4194304 workers=16 code=0 metadata=0 userImage=728 timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack",
        "TEST_START m16.014.startup","TEST_PASS m16.014.startup",
        "M16_014_POLICY started=30 callsPerTurn=4096 progressNanos=30000000000 elapsedLimit=none counters=reject-overflow workers=individual cancellation=owned",
        "M16_008_TIMING identity=4294967297 epoch=1 start=30 arm=31 capture=32 ready=33 periodFs=10000000",
        $"M16_016_QUOTA_SOCKETS bulkAborted=3 compactDiscarded=1 clock={AbortClock} retained=0 transport=aborted timewait=discarded buffers=scrubbed descriptors=closed table=retired",
        "M11_012_END requests=33 generatedBytes=17376 generator=zero seed=zero firmwareCallbacks=none",
        "M07_033_END issued=4 secret=zero seed=zero lease=released firmwareCallbacks=none",
        "M16_008_OWNER_END value-only-inner-stub",
        "M16_014_END identity=4294967297 calls=5000 started=30 nanos=40000000000 reaped=3 process=drained mappings=restored pool=scrubbed random=zero reservation=retained",
        "M16_016_QUOTA_PROCESS_FAILURE exit=99 cause=physical-backing",
        "M16_016_QUOTA_NATIVE process=drained timer=restored descriptors=closed guard=detached mappings=restored pool=scrubbed initialStack=zero random=zero reservation=retained",
        "M16_016_QUOTA_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true",
        "M16_011_WIRE received=100 arp=1 accepted=98 ignored=1 capacity=0 arpCopied=2 tcpCopied=200",
        $"M16_011_END polls=1000 received=100 copied=200 fresh=4 retired=1 start=10 end={AbortClock} maxDeadline={Deadline} retained=0 buffers=scrubbed descriptors=closed workers=reaped",
        "M07_026_ROW stage=4 a=102 b=203 c=1 d=2","M07_026_ROW stage=5 a=8 b=0 c=8 d=1","M07_026_ROW stage=6 a=100 b=100 c=8 d=6",
        "M16_016_QUOTA_CLEANUP exit=99 process=failed workers=reaped memory=zero cpu=restored nic=retired dma=zero allocator=unchanged pci=restored canaries=valid reservation=retained",
        "CLROS_M16_016_QUOTA_CLEANUP"];
    private static QemuTranscripts.ServiceEnvelopeInspection Inspect(string[] rows, QemuTranscripts.WebParentReport? parent = null,
        QemuTranscripts.ThreadingProtocolReport? protocol = null, QemuTranscripts.ServiceQuotaTransportInspection? transport = null,
        QemuTranscripts.ServiceQuotaInspection? quota = null) => QemuTranscripts.InspectNativeAotServiceQuotaEnvelopeForTests(rows, parent ?? Parent, protocol ?? Protocol, Random, transport ?? Transport, quota ?? Quota, Baseline);
    private static void Reject(string[] rows, QemuTranscripts.WebParentReport? parent = null, QemuTranscripts.ThreadingProtocolReport? protocol = null,
        QemuTranscripts.ServiceQuotaTransportInspection? transport = null, QemuTranscripts.ServiceQuotaInspection? quota = null)
    {
        bool rejected = false;
        try
        {
            _ = Inspect(rows, parent, protocol, transport, quota);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
