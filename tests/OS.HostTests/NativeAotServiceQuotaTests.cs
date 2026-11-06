using OS.Tooling;

internal static class NativeAotServiceQuotaTests
{
    // A deliberately mechanical value-only ledger. No package, executable,
    // scheduler, raw framing, HTTP, PCAP or guest authority is supplied.
    private static string[] Rows() =>
    [
        "M16_016_TOPOLOGY identity=4294967297 ordinal=1 nr=9 pool=9216 backed=9000 free=216 freeRuns=2 largestFreeRun=200 regions=3 regionLimit=256 tables=6 tableLimit=64",
        "M16_008_CALL identity=4294967297 nr=9 a0=0 a1=1048576 a2=3 a3=34 a4=18446744073709551615 a5=0 rip=4198402 rsp=1879048184 flags=514 fs=0 gs=0 clock=100 valueBits=18446744073709551604 action=1 status=1 ordinal=1 waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4198402 returnRsp=1879048184 returnFlags=514 returnFs=0 returnGs=0 returnValueBits=18446744073709551604",
        "M16_016_QUOTA_ABORT identity=4294967297 ordinal=1 nr=9 backed=9000 pool=9216 exit=99 policy=terminate cause=physical-backing",
        "M16_008_EXIT identity=4294967297 clearTidResult=0",
        "M16_008_SUPERVISOR identity=4294967297 action=4 transfers=2 ready=0 active=1",
        "M16_008_REAP old=4294967297 next=8589934593 context=zero stack=zero slot=zero",
        "M16_016_QUOTA_SOCKETS bulkAborted=2 compactDiscarded=0 clock=200 retained=0 transport=aborted timewait=discarded buffers=scrubbed descriptors=closed table=retired",
        "M16_016_MEMORY poolPages=9216 peakPages=9000 endPages=0",
        "M16_008_BUDGET_END nanos=150",
        "M16_008_OWNER_END reaped=1 transfers=2 irqs=1 exit=99 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained",
        "M16_014_END identity=4294967297 calls=1 started=10 nanos=200 reaped=1 process=drained mappings=restored pool=scrubbed random=zero reservation=retained",
        "M16_016_QUOTA_PROCESS_FAILURE exit=99 cause=physical-backing",
        "M16_016_QUOTA_NATIVE process=drained timer=restored descriptors=closed guard=detached mappings=restored pool=scrubbed initialStack=zero random=zero reservation=retained",
        "M16_016_QUOTA_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true",
        "M16_011_WIRE received=2 arp=0 accepted=2 ignored=0 capacity=0 arpCopied=0 tcpCopied=2",
        "M16_011_END polls=3 received=2 copied=2 fresh=2 retired=0 start=5 end=200 maxDeadline=240000000000 retained=0 buffers=scrubbed descriptors=closed workers=reaped",
        "M07_026_ROW stage=4 a=2 b=3 c=1 d=2",
        "M07_026_ROW stage=5 a=8 b=0 c=8 d=1",
        "M07_026_ROW stage=6 a=100 b=100 c=10 d=6",
        "M16_016_QUOTA_CLEANUP exit=99 process=failed workers=reaped memory=zero cpu=restored nic=retired dma=zero allocator=unchanged pci=restored canaries=valid reservation=retained",
        "CLROS_M16_016_QUOTA_CLEANUP"
    ];

    [HostTest("service quota leaf binds final physical denial and exact negative teardown without guest authority")]
    private static void Ledger()
    {
        string[] rows = Rows(), held = rows.ToArray();
        var value = QemuTranscripts.InspectNativeAotServiceQuotaForTests(rows);
        HostCheck.Equal(4294967297UL, value.Identity);
        HostCheck.Equal(1UL, value.Ordinal);
        HostCheck.Equal(9000UL, value.BackedPages);
        HostCheck.Equal(9216UL, value.PoolPages);
        HostCheck.Equal(1UL, value.Reaped);
        HostCheck.True(rows.SequenceEqual(held));
    }

    [HostTest("service quota leaf rejects crossed denial counters sufficient backing and malformed physical topology")]
    private static void DenialControls()
    {
        foreach (var change in new[] {
            (0, "identity=4294967297", "identity=4294967298"), (0, "ordinal=1", "ordinal=2"),
            (0, "free=216", "free=256"), (0, "largestFreeRun=200", "largestFreeRun=217"),
            (0, "freeRuns=2", "freeRuns=0"), (0, "tables=6", "tables=65"),
            (0, "largestFreeRun=200", "largestFreeRun=1"), (0, "freeRuns=2", "freeRuns=216"),
            (0, "regions=3", "regions=0"), (0, "tables=6", "tables=3"),
            (0, "tableLimit=64", "tableLimit=3"), (0, "tableLimit=64", "tableLimit=129"),
            (0, "regionLimit=256", "regionLimit=64"),
            (1, "a1=1048576", "a1=4096"), (1, "valueBits=18446744073709551604", "valueBits=0"),
            (2, "ordinal=1", "ordinal=2"), (2, "backed=9000", "backed=8999"),
            (2, "exit=99", "exit=0"), (2, "cause=physical-backing", "cause=errno") })
        {
            string[] rows = Rows();
            rows[change.Item1] = rows[change.Item1].Replace(change.Item2, change.Item3, StringComparison.Ordinal);
            Reject(rows);
        }
        string[] enough = Rows();
        enough[0] = enough[0].Replace("backed=9000 free=216", "backed=8960 free=256", StringComparison.Ordinal);
        enough[2] = enough[2].Replace("backed=9000", "backed=8960", StringComparison.Ordinal);
        Reject(enough);
    }

    [HostTest("service quota leaf rejects missing duplicated reordered and nonfinal teardown witnesses")]
    private static void CleanupControls()
    {
        for (int i = 0; i < Rows().Length; i++)
        {
            // SUPERVISOR remains the complete protocol replay's responsibility.
            if (i == 4)
                continue;
            var rows = Rows().ToList();
            rows.RemoveAt(i);
            Reject(rows.ToArray());
        }
        foreach (int index in new[] { 0, 2, 5, 6, 7, 8, 9, 10, 11, 12, 13, 19, 20 })
        {
            var rows = Rows().ToList();
            rows.Insert(index, rows[index]);
            Reject(rows.ToArray());
        }
        foreach (var change in new[] {
            (5, "next=8589934593", "next=8589934594"), (7, "endPages=0", "endPages=1"),
            (8, "nanos=150", "nanos=201"), (9, "exit=99", "exit=0"), (9, "userBacked=0", "userBacked=1"),
            (10, "reaped=1", "reaped=2"), (13, "root=true", "root=false"),
            (17, "b=0", "b=1"), (18, "b=100", "b=99"), (19, "dma=zero", "dma=live") })
        {
            string[] rows = Rows();
            rows[change.Item1] = rows[change.Item1].Replace(change.Item2, change.Item3, StringComparison.Ordinal);
            Reject(rows);
        }
        foreach (string extra in new[] { Rows()[1], "TEST_PASS m16-012", "OS_SMOKE_PASS", "TEST_FAIL m16.014", "M16_016_QUOTA_RESTORE forged=true" })
        {
            var rows = Rows().ToList();
            rows.Insert(19, extra);
            Reject(rows.ToArray());
        }
        Reject([.. Rows(), "M16_008_CALL forged=1"]);
        Reject(["M16_016_QUOTA_FORGED accepted=true", .. Rows()]);
        var earlyExit = Rows().ToList();
        string moved = earlyExit[3];
        earlyExit.RemoveAt(3);
        earlyExit.Insert(0, moved);
        Reject(earlyExit.ToArray());
        var lateExit = Rows().ToList();
        lateExit.Insert(7, "M16_008_EXIT identity=4294967298 clearTidResult=0");
        Reject(lateExit.ToArray());
        Reject([]);
        Reject([null!]);
    }

    private static void Reject(string[] rows)
    {
        bool rejected = false;
        try
        {
            _ = QemuTranscripts.InspectNativeAotServiceQuotaForTests(rows);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected, "invalid quota leaf accepted");
    }
}
