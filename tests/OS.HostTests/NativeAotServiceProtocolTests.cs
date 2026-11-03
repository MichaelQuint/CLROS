using OS.Tooling;

internal static class NativeAotServiceProtocolTests
{
    private static NativeAotThreadingTranscriptTests.Vector Vector(bool configuredMemory = false, bool largeMapping = false)
    {
        var v = new NativeAotThreadingTranscriptTests.Vector(false).Start();
        v.Rows[1] = v.Rows[1].Replace("entropyBytes=32", "entropyBytes=0", StringComparison.Ordinal);
        if (configuredMemory)
            v.Rows[1] = v.Rows[1].Replace("poolPages=8192", "poolPages=9216", StringComparison.Ordinal);
        v.Rows[2] = v.Rows[2].Replace("packageBytes=1320176", "packageBytes=8501192", StringComparison.Ordinal);
        v.Rows[4] = v.Rows[4].Replace("workers=4", "workers=16", StringComparison.Ordinal);
        v.Rows[5] = "M16_014_POLICY started=0 callsPerTurn=4096 progressNanos=30000000000 elapsedLimit=none counters=reject-overflow workers=individual cancellation=owned";
        v.Rows.Insert(5, "TEST_START m16.014");
        v.Rows.Insert(3, "M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        v.Rows.Insert(v.Rows.FindIndex(row => row.StartsWith("M16_014_POLICY ", StringComparison.Ordinal)) + 1,
            "M16_012_POLL identity=0 clock=0 ready=1 idle=0");
        if (largeMapping)
        {
            v.Call(9, 0x140000000, [0, 33 * 1048576, 3, 0x22, uint.MaxValue, 0]);
            v.Call(11, 0, [0x140000000, 33 * 1048576]);
        }
        for (int turn = 1; turn <= 16; turn++)
        {
            for (int call = 0; call < 500; call++)
                v.Call(39, 1, []);
            v.Irq((ulong)turn * 20_000_000_000, 1, 1);
        }
        v.Rows.Add($"M16_014_PROGRESS identity={v.Main.Id} calls={v.Main.Ordinal} started=0 nanos={v.Clock} epochs={v.Epoch} active=1 owner=live cleanup=pending");
        v.Rows.Add("CLROS_M16_014_READY");
        var first = v.Clone(2, 2);
        var second = v.Clone(3, 3);
        v.Irq(v.Clock + 1_000_000, 3, 3);
        v.Select(first);
        v.Wait(0x500100);
        v.Drain(v.Clock + 1_000_000);
        v.Supervisor(3, 2, 3);
        v.Select(second);
        v.Wait(0x500104);
        v.Drain(v.Clock + 1_000_000);
        v.Supervisor(3, 1, 3);
        v.Select(v.Main);
        v.Call(41, 5, [2, 1, 6]);
        v.Call(49, 0, [5, 0x500800, 16]);
        v.Call(50, 0, [5, 512]);
        for (int i = 0; i < 10; i++)
        {
            v.Call(43, 6, [5, 0, 0]);
            v.Call(45, 1, [6, 0x500800, 1, 0, 0, 0]);
            v.Call(44, 1, [6, 0x500800, 1, 0, 0, 0]);
            v.Call(3, 0, [6]);
        }
        v.Call(3, 0, [5]);
        v.Output();
        v.Call(231, 0, [0], 2);
        v.Drain(v.Clock);
        v.Exit(2, 2, 3);
        v.Select(first);
        v.Exit(4, 1, 2);
        v.Select(second);
        v.Exit(4, 0, 1);
        foreach (int fd in Enumerable.Range(0, 5))
            v.Rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");
        v.End(3);
        v.Rows.InsertRange(v.Rows.Count - 3, [
            $"M16_014_END identity={v.Main.Id} calls={v.Main.Ordinal} started=0 nanos={v.Clock} reaped=3 process=drained mappings=restored pool=scrubbed random=zero reservation=retained",
            "TEST_PASS m16.014"]);
        return v;
    }
    private static QemuTranscripts.ThreadingProtocolReport Inspect(NativeAotThreadingTranscriptTests.Vector v, string[]? rows = null, bool configuredMemory = false) =>
        QemuTranscripts.InspectNativeAotServiceProtocolForTests(NativeAotThreadingTranscriptTests.Elf(), rows ?? v.Rows.ToArray(),
            v.Lengths, address => address == 0x400202, address => address == 0x400202, 8192, 12288, 31, 15, v.Main.Id, 1, configuredMemory);
    private static NativeAotThreadingTranscriptTests.Vector QuotaVector()
    {
        var v = new NativeAotThreadingTranscriptTests.Vector(false).Start();
        v.Rows[1] = v.Rows[1].Replace("entropyBytes=32", "entropyBytes=0", StringComparison.Ordinal).Replace("poolPages=8192", "poolPages=9216", StringComparison.Ordinal);
        v.Rows[2] = v.Rows[2].Replace("packageBytes=1320176", "packageBytes=" + NativeAotThreadingTranscriptTests.Elf().Length, StringComparison.Ordinal);
        v.Rows[4] = v.Rows[4].Replace("workers=4", "workers=16", StringComparison.Ordinal);
        v.Rows[5] = "M16_014_POLICY started=0 callsPerTurn=4096 progressNanos=30000000000 elapsedLimit=none counters=reject-overflow workers=individual cancellation=owned";
        v.Rows.Insert(5, "TEST_START m16.014");
        v.Rows.Insert(3, "M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        v.Rows.Insert(v.Rows.FindIndex(row => row.StartsWith("M16_014_POLICY ", StringComparison.Ordinal)) + 1, "M16_012_POLL identity=0 clock=0 ready=1 idle=0");
        var first = v.Clone(2, 2);
        var second = v.Clone(3, 3);
        v.Irq(10, 3, 3);
        v.Select(first);
        v.Wait(0x500100);
        v.Drain(20);
        v.Supervisor(3, 2, 3);
        v.Select(second);
        v.Wait(0x500104);
        v.Drain(30);
        v.Supervisor(3, 1, 3);
        v.Select(v.Main);
        v.Call(41, 5, [2, 1, 6]);
        v.Call(49, 0, [5, 0x500800, 16]);
        v.Call(50, 0, [5, 512]);
        void SocketRow(int nr, ulong offset, uint bytes, ulong sent, ulong received) => v.Rows.Add(
            $"M16_016_SOCKET identity={v.Main.Id} ordinal={v.Main.Ordinal} nr={nr} fd=6 fdGeneration=1 description=7 inode=65537 slot=0 owner=4096 epoch=1 generation=1 index=0 local=167772687 localPort=8080 remote=167772674 remotePort=43000 offset={offset} bytes={bytes} peek=0 sent={sent} received={received}");
        v.Call(43, 6, [5, 0, 0]);
        SocketRow(43, 0, 0, 0, 0);
        v.Call(45, 1, [6, 0x500800, 1, 0, 0, 0]);
        SocketRow(45, 0, 1, 0, 1);
        v.Call(44, 1, [6, 0x500800, 1, 0, 0, 0]);
        SocketRow(44, 0, 1, 1, 1);
        v.Output();
        ulong frozenBudget = v.Clock;
        v.Clock++;
        v.Call(9, -12, [0, 1048576, 3, 0x22, uint.MaxValue, 0]);
        v.Rows.Add($"M16_016_QUOTA_ABORT identity={v.Main.Id} ordinal={v.Main.Ordinal} nr=9 backed=9216 pool=9216 exit=99 policy=terminate cause=physical-backing");
        v.Drain(v.Clock);
        v.Exit(4, 2, 3);
        v.Select(first);
        v.Exit(4, 1, 2);
        v.Select(second);
        v.Exit(4, 0, 1);
        foreach (int fd in Enumerable.Range(0, 7))
            v.Rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");
        v.Rows.Add("M16_016_MEMORY poolPages=9216 peakPages=9216 endPages=0");
        v.Rows.Add($"M16_008_BUDGET_END nanos={frozenBudget}");
        v.Rows.Add($"M16_008_OWNER_END reaped=3 transfers={v.Transfers} irqs={v.Epoch} exit=99 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained");
        v.Rows.Add($"M16_014_END identity={v.Main.Id} calls={v.Main.Ordinal} started=0 nanos={v.Clock + 2} reaped=3 process=drained mappings=restored pool=scrubbed random=zero reservation=retained");
        v.Rows.AddRange(["M16_016_QUOTA_PROCESS_FAILURE exit=99 cause=physical-backing",
            "M16_016_QUOTA_NATIVE process=drained timer=restored descriptors=closed guard=detached mappings=restored pool=scrubbed initialStack=zero random=zero reservation=retained",
            "M16_016_QUOTA_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true"]);
        return v;
    }

    private static QemuTranscripts.ThreadingProtocolReport InspectQuota(NativeAotThreadingTranscriptTests.Vector v, string[]? rows = null) =>
        QemuTranscripts.InspectNativeAotServiceQuotaProtocolForTests(NativeAotThreadingTranscriptTests.Elf(), rows ?? v.Rows.ToArray(), v.Lengths,
            Enumerable.Repeat(1U, v.Lengths.Count).ToArray(), address => address == 0x400202, address => address == 0x400202, 8192, 12288, 31, 15, v.Main.Id, 1);

    [HostTest("quota complete replay converts only logged Return into native Shutdown99 and consumes every genuine exit reap and fd close")]
    private static void QuotaComplete()
    {
        var v = QuotaVector();
        string[] before = v.Rows.ToArray();
        var report = InspectQuota(v);
        HostCheck.True(report.Calls < 4096 && report.Clones == 2 && report.Reaped == 3 && report.Irqs == 1 && report.Drains == 3);
        HostCheck.True(!v.Rows.Any(row => row.StartsWith("M16_014_PROGRESS ", StringComparison.Ordinal) || row == "TEST_PASS m16.014"));
        HostCheck.True(v.Rows.SequenceEqual(before));
    }

    [HostTest("quota full replay accepts frozen admission before denial only with exact post-reap finish while ordinary service stays strict")]
    private static void QuotaClockDomains()
    {
        var v = QuotaVector();
        var result = InspectQuota(v);
        HostCheck.Equal(3, result.Reaped);
        int budget = v.Rows.FindIndex(r => r.StartsWith("M16_008_BUDGET_END ", StringComparison.Ordinal));
        int service = v.Rows.FindIndex(r => r.StartsWith("M16_014_END ", StringComparison.Ordinal));
        HostCheck.Equal("M16_008_BUDGET_END nanos=30", v.Rows[budget]);
        HostCheck.True(v.Rows.Any(r => r.Contains("clock=31 valueBits=18446744073709551604", StringComparison.Ordinal)) &&
            v.Rows.Any(r => r.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal) && r.Contains("nanos=31", StringComparison.Ordinal)) &&
            v.Rows[service].Contains("started=0 nanos=33 reaped=3", StringComparison.Ordinal));
        void Refuse(string[] rows, string? reason = null)
        {
            bool rejected = false;
            try
            {
                InspectQuota(v, rows);
            }
            catch (ToolFailure error)
            {
                if (reason is not null)
                    HostCheck.Equal(reason, error.Reason);
                rejected = true;
            }
            HostCheck.True(rejected);
        }
        var boundary = v.Rows.ToArray();
        boundary[service] = boundary[service].Replace("nanos=33", "nanos=31", StringComparison.Ordinal);
        HostCheck.Equal(3, InspectQuota(v, boundary).Reaped);
        var advancedDrain = v.Rows.ToArray();
        int drain = Array.FindLastIndex(advancedDrain, r => r.StartsWith("M16_009_DRAIN ", StringComparison.Ordinal));
        advancedDrain[drain] = advancedDrain[drain].Replace("nanos=31", "nanos=32", StringComparison.Ordinal);
        Refuse(advancedDrain, "threading_drain_binding");
        Refuse(v.Rows.Where((_, at) => at != service).ToArray());
        var duplicate = v.Rows.ToList();
        duplicate.Insert(service, v.Rows[service]);
        Refuse(duplicate.ToArray());
        var reordered = v.Rows.ToArray();
        (reordered[service - 1], reordered[service]) = (reordered[service], reordered[service - 1]);
        Refuse(reordered);
        foreach (var (from, to) in new[]{("nanos=33","nanos=30"),
            ($"identity={v.Main.Id}",$"identity={v.Main.Id+(1UL<<32)}"),($"calls={v.Main.Ordinal}",$"calls={v.Main.Ordinal+1}"),
            ("started=0","started=1"),("reaped=3","reaped=2")})
        {
            var changed = v.Rows.ToArray();
            changed[service] = changed[service].Replace(from, to, StringComparison.Ordinal);
            HostCheck.True(changed[service] != v.Rows[service]);
            Refuse(changed, "service_quota_owner_accounting");
        }
        var suffix = v.Rows.ToArray();
        suffix[service] = suffix[service].Replace("random=zero", "random=retained", StringComparison.Ordinal);
        Refuse(suffix, "service_quota_owner_cleanup");
        var lateBudget = v.Rows.ToArray();
        lateBudget[budget] = "M16_008_BUDGET_END nanos=34";
        Refuse(lateBudget, "service_quota_owner_accounting");
        var ordinary = Vector();
        var oldRows = ordinary.Rows.ToArray();
        int oldBudget = Array.FindIndex(oldRows, r => r.StartsWith("M16_008_BUDGET_END ", StringComparison.Ordinal));
        oldRows[oldBudget] = $"M16_008_BUDGET_END nanos={ordinary.Clock - 1}";
        Reject(ordinary, oldRows);
    }

    [HostTest("quota replay rejects substituted denial return native close exit generation and positive completion")]
    private static void QuotaFailures()
    {
        var v = QuotaVector();
        void Refuse(string[] rows)
        {
            bool rejected = false;
            try
            {
                InspectQuota(v, rows);
            }
            catch (ToolFailure) { rejected = true; }
            HostCheck.True(rejected);
        }
        foreach (int at in Enumerable.Range(0, v.Rows.Count).Where(i => v.Rows[i].StartsWith("M16_016_SOCKET ", StringComparison.Ordinal)))
        {
            var missing = v.Rows.ToList();
            missing.RemoveAt(at);
            Refuse(missing.ToArray());
            var extra = v.Rows.ToList();
            extra.Insert(at, extra[at]);
            Refuse(extra.ToArray());
            foreach (var change in new[]{("fdGeneration=1","fdGeneration=2"),("description=7","description=8"),
                ("offset=0","offset=1"),("received=1","received=2"),("sent=1","sent=2")})
            {
                var changed = v.Rows.ToArray();
                changed[at] = changed[at].Replace(change.Item1, change.Item2, StringComparison.Ordinal);
                if (changed[at] != v.Rows[at])
                    Refuse(changed);
            }
        }
        foreach (string prefix in new[]{"M16_016_QUOTA_ABORT ","M16_008_EXIT ","M16_008_REAP ","M16_011_CLOSE fd=5 ",
            "M16_016_MEMORY ","M16_008_BUDGET_END ","M16_008_OWNER_END ","M16_016_QUOTA_PROCESS_FAILURE ","M16_016_QUOTA_NATIVE ","M16_016_QUOTA_RESTORE "})
            Refuse(v.Rows.Where(row => !row.StartsWith(prefix, StringComparison.Ordinal)).ToArray());
        foreach (var change in new[]{("nr=9 backed=9216","nr=10 backed=9216"),("backed=9216 pool=9216","backed=0 pool=9216"),
            ("exit=99 userReserved=0","exit=0 userReserved=0"),("M16_011_CLOSE fd=5 valueBits=0","M16_011_CLOSE fd=5 valueBits=18446744073709551607"),
            ("root=true descriptors=true","root=false descriptors=true")})
        {
            var changed = v.Rows.Select(row => row.Replace(change.Item1, change.Item2, StringComparison.Ordinal)).ToArray();
            HostCheck.True(!changed.SequenceEqual(v.Rows));
            Refuse(changed);
        }
        int abort = v.Rows.FindIndex(row => row.StartsWith("M16_016_QUOTA_ABORT ", StringComparison.Ordinal));
        int reap = v.Rows.FindIndex(row => row.StartsWith("M16_008_REAP ", StringComparison.Ordinal));
        int close = v.Rows.FindIndex(row => row.StartsWith("M16_011_CLOSE fd=5 ", StringComparison.Ordinal));
        var wrongOrdinal = v.Rows.ToArray();
        wrongOrdinal[abort] = wrongOrdinal[abort].Replace($"ordinal={v.Main.Ordinal}", $"ordinal={v.Main.Ordinal + 1}", StringComparison.Ordinal);
        Refuse(wrongOrdinal);
        var wrongGeneration = v.Rows.ToArray();
        wrongGeneration[reap] = wrongGeneration[reap].Replace($"next={v.Main.Id + (1UL << 32)}", $"next={v.Main.Id}", StringComparison.Ordinal);
        Refuse(wrongGeneration);
        var wrongCloseOrder = v.Rows.ToArray();
        (wrongCloseOrder[close], wrongCloseOrder[close + 1]) = (wrongCloseOrder[close + 1], wrongCloseOrder[close]);
        Refuse(wrongCloseOrder);
        var afterAbort = v.Rows.ToList();
        afterAbort.Insert(abort + 1, v.Rows[abort - 1].Replace($"ordinal={v.Main.Ordinal}", $"ordinal={v.Main.Ordinal + 1}", StringComparison.Ordinal));
        Refuse(afterAbort.ToArray());
        var userExit = v.Rows.ToArray();
        userExit[abort - 1] = userExit[abort - 1].Replace("nr=9 ", "nr=231 ", StringComparison.Ordinal);
        Refuse(userExit);
        Refuse([.. v.Rows, "TEST_PASS m16-009"]);
    }

    private static void Reject(NativeAotThreadingTranscriptTests.Vector v, string[] rows)
    {
        bool rejected = false;
        try
        {
            Inspect(v, rows);
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("service complete scheduler replay retains one generation past4096 calls and300 seconds then genuinely reaps all workers")]
    private static void Complete()
    {
        var v = Vector();
        string[] before = v.Rows.ToArray();
        var result = Inspect(v);
        HostCheck.True(result.Calls > 8000 && result.Irqs == 17 && result.Clones == 2 && result.Reaped == 3 && result.Drains == 3);
        HostCheck.True(v.Rows.SequenceEqual(before));
    }

    [HostTest("configured service replay requires its exact larger policy while legacy memory keeps its original quota")]
    private static void ConfiguredMemory()
    {
        var configured = Vector(configuredMemory: true, largeMapping: true);
        var result = Inspect(configured, configuredMemory: true);
        HostCheck.True(result.Calls > 8000 && result.Reaped == 3);
        Reject(configured, configured.Rows.ToArray());
        var legacy = Vector(largeMapping: true);
        Reject(legacy, legacy.Rows.ToArray());
        bool rejected = false;
        try
        {
            Inspect(Vector(), configuredMemory: true);
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }

    [HostTest("service full replay rejects fabricated progress cancellation cleanup and missing off-stack reap")]
    private static void OwnershipFailures()
    {
        var v = Vector();
        const string bootstrap = "M16_012_POLL identity=0 clock=0 ready=1 idle=0";
        Reject(v, v.Rows.Where(row => row != bootstrap).ToArray());
        foreach (var change in new[] { ("active=1 owner=live", "active=2 owner=live"), ("callsPerTurn=4096", "callsPerTurn=4097"),
            ("random=zero reservation=retained", "random=retained reservation=retained"), ("reaped=3 process=drained", "reaped=2 process=drained"),
            ("context=zero stack=zero slot=zero", "context=zero stack=retained slot=zero") })
        {
            string[] changed = v.Rows.Select(row => row.Replace(change.Item1, change.Item2, StringComparison.Ordinal)).ToArray();
            HostCheck.True(!changed.SequenceEqual(v.Rows));
            Reject(v, changed);
        }
        Reject(v, v.Rows.Where(row => !row.StartsWith("M16_014_PROGRESS ", StringComparison.Ordinal) && row != "CLROS_M16_014_READY").ToArray());
        Reject(v, v.Rows.Where(row => !row.StartsWith("M16_008_REAP ", StringComparison.Ordinal)).ToArray());
    }

    [HostTest("service replay requires hardware quota renewal exact cumulative peaks and the full elapsed witness")]
    private static void CountersAndTime()
    {
        var v = Vector();
        int progress = v.Rows.FindIndex(row => row.StartsWith("M16_014_PROGRESS ", StringComparison.Ordinal));
        string peak = v.Rows[progress];
        int start = peak.IndexOf(" calls=", StringComparison.Ordinal) + 7, end = peak.IndexOf(' ', start);
        foreach (string changed in new[] { peak[..start]+"4096"+peak[end..],
            v.Rows[progress].Replace("nanos=320000000000","nanos=300000000000",StringComparison.Ordinal),
            v.Rows[progress].Replace($"identity={v.Main.Id}",$"identity={v.Main.Id+(1UL<<32)}",StringComparison.Ordinal) })
        {
            var rows = v.Rows.ToArray();
            rows[progress] = changed;
            Reject(v, rows);
        }
        Reject(v, v.Rows.Where(row => !row.StartsWith("M16_009_IRQ ", StringComparison.Ordinal) &&
            !row.StartsWith("M16_008_SUPERVISOR ", StringComparison.Ordinal)).ToArray());
    }
}
