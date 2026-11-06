using System.Text;
using OS.Tooling;

internal static class NativeAotServiceStartupReplayTests
{
    private const ulong Writer = 0x100000001, Binder = 0x100000005;
    private const string Prefix = "CLROS_APP_ENTRY minimal.api.service\nCLROS_APP_CCTOR minimal.api.service\n" +
        "CLROS_APP_CTOR minimal.api.service\nCLROS_APP_BUILT minimal.api.service\n";
    private const string Listening = "CLROS_SERVICE_LISTEN contract=v1 endpoint=0.0.0.0:8080\n";
    private const string GcBegin = "CLROS_SERVICE_GC_BEGIN limit=16777216 baselineHeap=30920 baselineCommitted=151552 liveBytes=2097152 liveHeap=2129504 liveCommitted=4165632 pressureBytes=12582912 pressureHeap=12615680 pressureCommitted=14680064 pressureCollections=3 oom=1\n";
    private const string GcBudget = "M16_016_BUDGET gc=16777216 nativeReserve=20971520 static=16777216 pool=37748736 entitlement=67108864\r\n";
    private const string GcMemory = "M16_016_MEMORY poolPages=9216 peakPages=5120 endPages=0\r\n";
    private const string NativeBegin = "CLROS_SERVICE_NATIVE_BEGIN capacityBytes=33554432 retainedBytes=4194304 gcCommitted=4194304 quota=1 errno=12 releasedBytes=4194304 recoveredBytes=1048576\n";
    private const string GcOwner = "M16_008_OWNER_END reaped=18 transfers=64 irqs=1 exit=0 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained\r\n";
    private const string LifecycleEnd = "CLROS_SERVICE_LIFECYCLE_END CLROS_LIFECYCLE_STATUS active=0 waiting=0 waits=2 canceled=2 echoes=1 streams=1 rejected=1 read=65536 written=524288\n";
    private const string ApplicationEnd = "CLROS_SERVICE_APP_END calls=4097 randomRequests=1 randomBytes=8192 iterations=1 elapsedMillis=300001 worker=joined\n";
    private static readonly string[] Markers = ["TEST_START m16.014.startup", "M16_014_ENTER ", "M16_014_CONFIG ",
        "M16_014_STARTUP ", "M16_014_BIND ", "M16_014_LISTEN ", "M16_014_LIVE ", "CLROS_M16_014_LISTENING", "TEST_PASS m16.014.startup"];

    [HostTest("service v2 GC parser binds framed measurements and exact entitlement without execution authority")]
    private static void GcMeasurements()
    {
        foreach (bool segmented in new[] { false, true })
        {
            byte[] raw = Encoding.UTF8.GetBytes(GcRaw(segmented)), before = raw.ToArray();
            var report = QemuTranscripts.InspectNativeAotServiceGcForTests(raw, 67108864, lifecycle: true);
            HostCheck.True(report.HasValue);
            var value = report!.Value;
            HostCheck.Equal(16777216UL, value.GcLimitBytes);
            HostCheck.Equal(20971520UL, value.NativeReserveBytes);
            HostCheck.Equal(16777216UL, value.StaticBytes);
            HostCheck.Equal(37748736UL, value.PoolBytes);
            HostCheck.Equal(67108864UL, value.EntitlementBytes);
            HostCheck.Equal(30920UL, value.Begin.BaselineHeapBytes);
            HostCheck.Equal(20971520UL, value.PeakBackedBytes);
            HostCheck.Equal(4194304UL, value.NativeBegin.RetainedBytes);
            HostCheck.Equal(4194304UL, value.NativeEnd.GcCommittedBytes);
            HostCheck.Equal(2097152UL, value.End.LiveBytes);
            HostCheck.Equal(4165632UL, value.End.LiveCommittedBytes);
            HostCheck.Equal(12582912UL, value.Begin.PressureBytes);
            HostCheck.Equal(12615680UL, value.Begin.PressureHeapBytes);
            HostCheck.Equal(14680064UL, value.End.PressureCommittedBytes);
            HostCheck.Equal(3UL, value.End.PressureCollections);
            HostCheck.Equal(LifecycleEnd["CLROS_SERVICE_LIFECYCLE_END ".Length..^1], value.TerminalStatus);
            HostCheck.True(raw.AsSpan().SequenceEqual(before));
            Array.Fill(raw, (byte)0);
            HostCheck.Equal(2129504UL, value.Begin.LiveHeapBytes);
        }
        HostCheck.True(QemuTranscripts.InspectNativeAotServiceGcForTests(Encoding.UTF8.GetBytes(Raw()), 0) is null);
        RejectGc(GcRaw(), lifecycle: false);
        RejectGc(Raw() + GcBudget, lifecycle: false);
        RejectGc(Raw() + Frame("CLROS_SERVICE_GC_REJECT runtime\n"), lifecycle: false);
    }

    [HostTest("service native pressure rejects missing duplicate forged unbounded unreturned and uncovered observations")]
    private static void NativePressureControls()
    {
        foreach (string native in new[] { "", NativeBegin + NativeBegin, NativeBegin + "CLROS_SERVICE_NATIVE_UNKNOWN x=1\n",
            NativeBegin.Replace("capacityBytes=33554432", "capacityBytes=67108864", StringComparison.Ordinal),
            NativeBegin.Replace("retainedBytes=4194304", "retainedBytes=0", StringComparison.Ordinal),
            NativeBegin.Replace("retainedBytes=4194304", "retainedBytes=4194305", StringComparison.Ordinal),
            NativeBegin.Replace("retainedBytes=4194304", "retainedBytes=33554432", StringComparison.Ordinal),
            NativeBegin.Replace("retainedBytes=4194304", "retainedBytes=04194304", StringComparison.Ordinal),
            NativeBegin.Replace("gcCommitted=4194304", "gcCommitted=0", StringComparison.Ordinal),
            NativeBegin.Replace("gcCommitted=4194304", "gcCommitted=4097", StringComparison.Ordinal),
            NativeBegin.Replace("gcCommitted=4194304", "gcCommitted=16781312", StringComparison.Ordinal),
            NativeBegin.Replace("quota=1", "quota=0", StringComparison.Ordinal),
            NativeBegin.Replace("errno=12", "errno=22", StringComparison.Ordinal),
            NativeBegin.Replace("releasedBytes=4194304", "releasedBytes=3145728", StringComparison.Ordinal),
            NativeBegin.Replace("recoveredBytes=1048576", "recoveredBytes=0", StringComparison.Ordinal),
            NativeBegin.Replace("\n", " extra=1\n", StringComparison.Ordinal) })
            RejectGc(GcRaw(nativeBegin: native));
        string nativeEnd = NativeBegin.Replace("NATIVE_BEGIN", "NATIVE_END", StringComparison.Ordinal);
        RejectGc(GcRaw(nativeEnd: ""));
        RejectGc(GcRaw(nativeEnd: nativeEnd + nativeEnd));
        RejectGc(GcRaw(nativeBegin: NativeBegin.Replace("retainedBytes=4194304", "retainedBytes=17825792", StringComparison.Ordinal)
            .Replace("releasedBytes=4194304", "releasedBytes=17825792", StringComparison.Ordinal)));
        RejectGc(GcRaw(prefix: Prefix + NativeBegin, nativeBegin: ""));
        RejectGc(GcRaw() + Frame(NativeBegin, 1));
        RejectGc(GcRaw() + NativeBegin);
        RejectGc(Raw() + Frame(NativeBegin), lifecycle: false);
        foreach (string retained in new[] { "1048576", "16777216" })
            HostCheck.True(QemuTranscripts.InspectNativeAotServiceGcForTests(Encoding.UTF8.GetBytes(GcRaw(nativeBegin:
                NativeBegin.Replace("retainedBytes=4194304", "retainedBytes=" + retained, StringComparison.Ordinal)
                    .Replace("releasedBytes=4194304", "releasedBytes=" + retained, StringComparison.Ordinal))), 67108864, true).HasValue);
    }

    [HostTest("service GC memory peak rejects missing duplicate misplaced understated nonzero-end and wrong-channel records")]
    private static void GcMemoryControls()
    {
        foreach (string memory in new[]{"",GcMemory+GcMemory,
            GcMemory.Replace("poolPages=9216","poolPages=4096",StringComparison.Ordinal),
            GcMemory.Replace("peakPages=5120","peakPages=0",StringComparison.Ordinal),
            GcMemory.Replace("peakPages=5120","peakPages=9217",StringComparison.Ordinal),
            GcMemory.Replace("peakPages=5120","peakPages=3583",StringComparison.Ordinal),
            GcMemory.Replace("peakPages=5120","peakPages=05120",StringComparison.Ordinal),
            GcMemory.Replace("endPages=0","endPages=1",StringComparison.Ordinal),
            GcMemory.Replace("\r\n"," extra=1\r\n",StringComparison.Ordinal)})
            RejectGc(GcRaw(memory: memory));
        foreach (string peak in new[] { "3584", "9216" })
            HostCheck.True(QemuTranscripts.InspectNativeAotServiceGcForTests(Encoding.UTF8.GetBytes(GcRaw(memory: GcMemory.Replace("5120", peak, StringComparison.Ordinal))), 67108864, true).HasValue);
        HostCheck.True(QemuTranscripts.InspectNativeAotServiceGcForTests(Encoding.UTF8.GetBytes(GcRaw(memory: GcMemory + "M16_008_BUDGET_END nanos=300000000001\r\n")), 67108864, true).HasValue);
        RejectGc(GcMemory + GcRaw(memory: ""));
        RejectGc(GcRaw(memory: Frame(GcMemory)));
        RejectGc(GcRaw(memory: Frame(GcMemory, 1)));
        RejectGc(Raw() + GcMemory, lifecycle: false);
        RejectGc(GcRaw(memory: "") + GcMemory);
        RejectGc(GcRaw().Replace(GcOwner, "", StringComparison.Ordinal));
        RejectGc(GcRaw(memory: GcMemory + "UNRELATED_RECORD\r\n"));
    }

    [HostTest("service v2 GC pressure requires bounded retained bytes actual collections and exhaustion before recovery")]
    private static void GcPressureControls()
    {
        foreach (string begin in new[]{
            GcBegin.Replace("pressureBytes=12582912", "pressureBytes=1048576", StringComparison.Ordinal),
            GcBegin.Replace("pressureBytes=12582912 pressureHeap=12615680 pressureCommitted=14680064", "pressureBytes=15728640 pressureHeap=15761408 pressureCommitted=16777216", StringComparison.Ordinal)})
            HostCheck.True(QemuTranscripts.InspectNativeAotServiceGcForTests(Encoding.UTF8.GetBytes(GcRaw(begin: begin)), 67108864, true).HasValue);
        foreach (string begin in new[]{
            GcBegin.Replace("pressureBytes=12582912", "pressureBytes=0", StringComparison.Ordinal),
            GcBegin.Replace("pressureBytes=12582912", "pressureBytes=12582913", StringComparison.Ordinal),
            GcBegin.Replace("pressureBytes=12582912", "pressureBytes=16777216", StringComparison.Ordinal),
            GcBegin.Replace("pressureHeap=12615680", "pressureHeap=12582911", StringComparison.Ordinal),
            GcBegin.Replace("pressureCommitted=14680064", "pressureCommitted=12615679", StringComparison.Ordinal),
            GcBegin.Replace("pressureCommitted=14680064", "pressureCommitted=16777217", StringComparison.Ordinal),
            GcBegin.Replace("pressureCollections=3", "pressureCollections=0", StringComparison.Ordinal),
            GcBegin.Replace("pressureCollections=3", "pressureCollections=2147483648", StringComparison.Ordinal),
            GcBegin.Replace("pressureCollections=3", "pressureCollections=03", StringComparison.Ordinal),
            GcBegin.Replace(" oom=1", "", StringComparison.Ordinal),
            GcBegin.Replace("oom=1", "oom=0", StringComparison.Ordinal),
            GcBegin.Replace("oom=1", "oom=2", StringComparison.Ordinal)})
            RejectGc(GcRaw(begin: begin));
    }

    [HostTest("service v2 GC parser rejects substituted unaligned overflowing missing duplicate and misplaced budgets")]
    private static void GcBudgetControls()
    {
        foreach (string budget in new[] { "", GcBudget + GcBudget,
            GcBudget.Replace("gc=16777216", "gc=16777215", StringComparison.Ordinal),
            GcBudget.Replace("nativeReserve=20971520", "nativeReserve=0", StringComparison.Ordinal),
            GcBudget.Replace("pool=37748736", "pool=67108864", StringComparison.Ordinal),
            GcBudget.Replace("static=16777216", "static=0", StringComparison.Ordinal),
            GcBudget.Replace("static=16777216", "static=16777217", StringComparison.Ordinal),
            GcBudget.Replace("static=16777216", "static=33558528", StringComparison.Ordinal),
            GcBudget.Replace("static=16777216", "static=18446744073709547520", StringComparison.Ordinal),
            GcBudget.Replace("entitlement=67108864", "entitlement=67112960", StringComparison.Ordinal),
            GcBudget.Replace("gc=16777216", "gc=016777216", StringComparison.Ordinal),
            GcBudget.Replace("\r\n", " extra=1\r\n", StringComparison.Ordinal) })
            RejectGc(GcRaw(budget: budget));
        RejectGc(GcRaw(), entitlement: 67108865);
        RejectGc(GcRaw(), entitlement: 33554432);
        RejectGc(GcRaw(budget: GcBudget.Replace("entitlement=67108864", "entitlement=100663296", StringComparison.Ordinal)), entitlement: 100663296);
        RejectGc(GcRaw(budget: "") + GcBudget);
        RejectGc(GcRaw(budget: Frame(GcBudget)));
    }

    [HostTest("service v2 GC parser rejects unknown duplicate forged invalid and reordered application observations")]
    private static void GcMeasurementControls()
    {
        foreach (string begin in new[] { "", GcBegin + GcBegin, GcBegin + "CLROS_SERVICE_GC_UNKNOWN x=1\n",
            GcBegin.Replace("baselineHeap=30920", "baselineHeap=030920", StringComparison.Ordinal),
            GcBegin.Replace("baselineHeap=30920", "baselineHeap=151553", StringComparison.Ordinal),
            GcBegin.Replace("baselineCommitted=151552", "baselineCommitted=16777217", StringComparison.Ordinal),
            GcBegin.Replace("liveBytes=2097152", "liveBytes=2097151", StringComparison.Ordinal),
            GcBegin.Replace("liveHeap=2129504", "liveHeap=2097151", StringComparison.Ordinal),
            GcBegin.Replace("liveCommitted=4165632", "liveCommitted=2129503", StringComparison.Ordinal),
            GcBegin.Replace("liveCommitted=4165632", "liveCommitted=16777217", StringComparison.Ordinal),
            GcBegin.Replace("limit=16777216", "limit=18446744073709551616", StringComparison.Ordinal),
            GcBegin.Replace("\n", " extra=1\n", StringComparison.Ordinal) })
            RejectGc(GcRaw(begin: begin));
        string end = GcBegin.Replace("GC_BEGIN", "GC_END", StringComparison.Ordinal);
        foreach (string tail in new[] { LifecycleEnd + ApplicationEnd, LifecycleEnd + end + end + ApplicationEnd,
            end + LifecycleEnd + ApplicationEnd, LifecycleEnd + ApplicationEnd + end,
            LifecycleEnd.Replace("active=0", "active=1", StringComparison.Ordinal) + end + ApplicationEnd,
            LifecycleEnd + end.Replace("liveBytes=2097152", "liveBytes=0", StringComparison.Ordinal) + ApplicationEnd })
            RejectGc(GcRaw(tail: tail));
        RejectGc(GcRaw(prefix: GcBegin + Prefix, begin: ""));
        RejectGc(GcRaw() + Frame(GcBegin, 1));
        RejectGc(GcRaw() + end);
    }

    [HostTest("service v2 startup requires explicit mode and exact copied fd2 listening with three environment entries")]
    private static void LifecycleStartup()
    {
        foreach (bool segmented in new[] { false, true })
        {
            string raw = Raw(segmented, prefix: Prefix + GcBegin, lifecycle: true, entryMemory: EntryMemory);
            var report = Read(raw, lifecycle: true);
            HostCheck.Equal(Writer, report.WriterIdentity);
            HostCheck.Equal(6, report.ListenerDescriptor);
            HostCheck.Equal((uint)Encoding.UTF8.GetByteCount(NativeAotServicePackage.LifecycleSettingsJson), report.SettingsBytes);
            Reject(raw);
            Reject(Raw(segmented), lifecycle: true);
        }
    }

    [HostTest("service v2 startup rejects a GC witness interleaved into the four required constructor lines")]
    private static void LifecyclePrefixOrder()
    {
        string prefix = Prefix.Replace("CLROS_APP_CCTOR", GcBegin + "CLROS_APP_CCTOR", StringComparison.Ordinal);
        string raw = Raw(prefix: prefix, lifecycle: true, entryMemory: EntryMemory);
        Reject(raw, lifecycle: true);
    }

    [HostTest("service v2 startup rejects crossed or unknown listening versions and inconsistent environment metadata")]
    private static void LifecycleStartupControls()
    {
        string raw = Raw(lifecycle: true, entryMemory: EntryMemory);
        foreach (string bad in new[] {
            raw.Replace("contract=v2", "contract=v1", StringComparison.Ordinal), raw.Replace("contract=v2", "contract=v3", StringComparison.Ordinal),
            Change(raw, "M16_014_ENTER ", r => r.Replace("envc=3", "envc=2", StringComparison.Ordinal)),
            Change(raw, "M16_014_LIVE ", r => r.Replace("envc=3", "envc=4", StringComparison.Ordinal)),
            Change(raw, "M16_014_ENTER ", r => r.Replace("envc=3", "envc=4294967299", StringComparison.Ordinal)) })
            Reject(bad, lifecycle: true);
    }

    [HostTest("service startup replay binds scalar and segmented vector prefixes configuration and fd2 listening", HostLane.Fast, 0)]
    private static void Startup()
    {
        foreach (bool segmented in new[] { false, true })
        {
            byte[] raw = Encoding.UTF8.GetBytes(Raw(segmented)), before = raw.ToArray();
            var report = QemuTranscripts.InspectNativeAotServiceStartupForTests(raw);
            HostCheck.Equal(Writer, report.WriterIdentity);
            HostCheck.Equal(6, report.ListenerDescriptor);
            HostCheck.Equal((uint)Encoding.UTF8.GetByteCount(NativeAotServicePackage.SettingsJson), report.SettingsBytes);
            HostCheck.Equal(3, report.SettingsDescriptor);
            HostCheck.Equal(Writer, report.SettingsIdentity);
            HostCheck.Equal(3UL, report.SettingsOrdinal);
            HostCheck.True(raw.AsSpan().SequenceEqual(before));
            Array.Fill(raw, (byte)0);
            HostCheck.Equal(Writer, report.WriterIdentity);
        }
    }

    [HostTest("configured startup binds the first memory snapshot to its adjacent first call and preserves legacy spacing", HostLane.Fast, 1)]
    private static void ConfiguredEntryMemory()
    {
        const string memory = EntryMemory;
        string raw = Raw(lifecycle: true, entryMemory: memory);
        HostCheck.Equal(Writer, Read(raw, lifecycle: true).WriterIdentity);
        Reject(Raw(lifecycle: true), lifecycle: true);
        foreach (string changed in new[] { memory + memory, memory.Replace("nr=12", "nr=9", StringComparison.Ordinal),
            memory.Replace("reserved=0", "reserved=0 extra=1", StringComparison.Ordinal), Frame(memory) })
            Reject(Raw(lifecycle: true, entryMemory: changed), lifecycle: true);
        Reject(Raw(entryMemory: memory));
        _ = Read(Raw());
    }

    [HostTest("service startup replay rejects missing duplicate unknown reordered and payload-forged native observations", HostLane.Fast, 1)]
    private static void MarkersAndPayloads()
    {
        string raw = Raw();
        foreach (string marker in Markers)
        {
            string row = raw.Split("\r\n").Single(line => line.StartsWith(marker, StringComparison.Ordinal));
            Reject(raw.Replace(row + "\r\n", "", StringComparison.Ordinal));
            Reject(raw.Replace(row + "\r\n", row + "\r\n" + row + "\r\n", StringComparison.Ordinal));
            // Identical marker text inside a genuine copied fd2 envelope cannot become native metadata.
            Reject(raw.Replace(row + "\r\n", Frame(row + "\n"), StringComparison.Ordinal));
        }
        Reject(raw + "M16_014_UNKNOWN value=1\r\n");
        Reject(raw + "M10_005_LOGS stdout=32 stderr=32 writers=8 writev=32 invalidErrno=9\r\n");
        Reject(raw + "TEST_FAIL m16.014.startup substituted\r\n");
        Reject(raw[..^1]);
        string progress = "M16_014_PROGRESS identity=4294967297 calls=4097 elapsed=300001\r\n";
        _ = Read(raw + progress); // Progress payload remains the shared replay consumer's responsibility.
        Reject(raw.Replace("TEST_PASS m16.014.startup\r\n", progress + "TEST_PASS m16.014.startup\r\n", StringComparison.Ordinal));
        Reject(Raw(prefix: Prefix.Replace("minimal.api.service", "minimal.api.smoke", StringComparison.Ordinal)));
        Reject(Raw(prefix: Prefix.Replace("CLROS_APP_CTOR minimal.api.service", "CLROS_APP_CTOR minimal.api.smoke", StringComparison.Ordinal)));
        Reject(Raw(listening: "CLROS_SERVICE_LISTEN\n"));
        Reject(Raw(listenChannel: 1));
    }

    [HostTest("service startup replay rejects substituted canonical policy identities descriptors and live counters", HostLane.Fast, 2)]
    private static void Substitutions()
    {
        string raw = Raw();
        var changes = new (string Row, string Before, string After)[]
        {
            ("M16_014_ENTER ", "identity=4294967297", "identity=4294967301"),
            ("M16_014_ENTER ", "rip=4198402", "rip=4198403"),
            ("M16_014_ENTER ", "argc=5", "argc=6"), ("M16_014_ENTER ", "envc=2", "envc=3"),
            ("M16_014_ENTER ", "argc=5", "argc=05"),
            ("M16_014_CONFIG ", "fd=3", "fd=4"),
            ("M16_014_CONFIG ", "source=immutable-package", "source=unverified-package"),
            ("M16_014_STARTUP ", "writer=4294967297", "writer=4294967301"),
            ("M16_014_STARTUP ", "stages=4", "stages=3"),
            ("M16_014_BIND ", "identity=4294967301", "identity=4294967297"),
            ("M16_014_BIND ", "fd=6", "fd=7"), ("M16_014_LISTEN ", "fd=6", "fd=7"),
            ("M16_014_LISTEN ", "address=0", "address=1"), ("M16_014_LISTEN ", "port=8080", "port=8081"),
            ("M16_014_LIVE ", "argc=5", "argc=6"), ("M16_014_LIVE ", "envc=2", "envc=3"),
            ("M16_014_LIVE ", "bindFd=6", "bindFd=7"), ("M16_014_LIVE ", "stdoutBytes=0", "stdoutBytes=1"),
            ("M16_014_LIVE ", "active=2", "active=0"), ("M16_014_LIVE ", "clock=60", "clock=59"),
            ("M16_014_LIVE ", "clock=60", "clock=71")
        };
        foreach (var change in changes)
            Reject(Change(raw, change.Row, row => row.Replace(change.Before, change.After, StringComparison.Ordinal)));
        uint settings = (uint)Encoding.UTF8.GetByteCount(NativeAotServicePackage.SettingsJson);
        foreach (string marker in new[] { "M16_014_ENTER ", "M16_014_CONFIG ", "M16_014_LIVE " })
            Reject(Change(raw, marker, row => row.Replace((marker == "M16_014_CONFIG " ? "bytes=" : "configBytes=") + settings,
                (marker == "M16_014_CONFIG " ? "bytes=" : "configBytes=") + (settings + 1), StringComparison.Ordinal)));
        Reject(Change(raw, "M16_014_LIVE ", row => row.Replace("stderrBytes=" + (Prefix.Length + Listening.Length),
            "stderrBytes=" + (Prefix.Length + Listening.Length - 1), StringComparison.Ordinal)));
    }

    [HostTest("service startup replay rejects orphan copies wrong calls returns generations and premature readiness", HostLane.Fast, 3)]
    private static void CallBindings()
    {
        string raw = Raw();
        string WriteCall(string row) => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && row.Contains("ordinal=4 ", StringComparison.Ordinal) ? row : "";
        foreach (var change in new[] { ("nr=1", "nr=39"), ("a0=2", "a0=1"), ("action=1 status=1", "action=2 status=1"),
            ("action=1 status=1", "action=1 status=2") })
        {
            string modified = ChangeWhere(raw, row => WriteCall(row).Length != 0, row => row.Replace(change.Item1, change.Item2, StringComparison.Ordinal));
            Reject(modified);
        }
        Reject(ChangeWhere(raw, row => WriteCall(row).Length != 0, row => row.Replace("returnValueBits=" + Built().Length,
            "returnValueBits=" + (Built().Length - 1), StringComparison.Ordinal)));
        Reject(ChangeWhere(raw, row => WriteCall(row).Length != 0, row => row.Replace("a2=" + Built().Length, "a2=1", StringComparison.Ordinal)));
        Reject(ChangeWhere(Raw(segmented: true), row => WriteCall(row).Length != 0,
            row => row.Replace("a2=4", "a2=0", StringComparison.Ordinal)));
        Reject(ChangeWhere(raw, row => WriteCall(row).Length != 0, row => row.Replace("identity=" + Writer,
            "identity=" + (Writer + (1UL << 32)), StringComparison.Ordinal).Replace("ordinal=4 ", "ordinal=1 ", StringComparison.Ordinal)));
        Reject(ChangeWhere(raw, row => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && row.Contains(" nr=17 ", StringComparison.Ordinal),
            row => row.Replace("a0=3", "a0=4", StringComparison.Ordinal)));
        Reject(ChangeWhere(raw, row => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && row.Contains(" nr=49 ", StringComparison.Ordinal),
            row => row.Replace("valueBits=0 action=1", "valueBits=18446744073709551518 action=1", StringComparison.Ordinal)
                .Replace("returnValueBits=0", "returnValueBits=18446744073709551518", StringComparison.Ordinal)));
        Reject(ChangeWhere(raw, row => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) && row.Contains(" nr=50 ", StringComparison.Ordinal),
            row => row.Replace("a0=6", "a0=7", StringComparison.Ordinal)));
        string liveRow = raw.Split("\r\n").Single(row => row.StartsWith("M16_014_LIVE ", StringComparison.Ordinal));
        string reordered = raw.Replace(liveRow + "\r\n", "", StringComparison.Ordinal)
            .Replace("M16_014_LISTEN fd=6", liveRow + "\r\nM16_014_LISTEN fd=6", StringComparison.Ordinal);
        Reject(reordered);
    }

    private static string Built() => "CLROS_APP_BUILT minimal.api.service\n";
    private static string Frame(string text, uint channel = 2) => $"M16_005_WRITE fd={channel} bytes={Encoding.UTF8.GetByteCount(text)} data={text}\r\n";
    private static string GcRaw(bool segmented = false, string budget = GcBudget, string begin = GcBegin,
        string? tail = null, string prefix = Prefix, string memory = GcMemory, string nativeBegin = NativeBegin, string? nativeEnd = null)
    {
        nativeEnd ??= NativeBegin.Replace("NATIVE_BEGIN", "NATIVE_END", StringComparison.Ordinal);
        tail ??= LifecycleEnd + GcBegin.Replace("GC_BEGIN", "GC_END", StringComparison.Ordinal) + ApplicationEnd;
        tail = tail.Replace(ApplicationEnd, nativeEnd + ApplicationEnd, StringComparison.Ordinal);
        string ending = segmented ? string.Concat(tail.Chunk(7).Select(part => Frame(new string(part)))) : Frame(tail);
        return budget + Raw(segmented, prefix: prefix + begin + nativeBegin, listening: Listening.Replace("contract=v1", "contract=v2", StringComparison.Ordinal))
            .Replace("envc=2", "envc=3", StringComparison.Ordinal) + ending + memory + GcOwner;
    }
    private static void RejectGc(string raw, ulong entitlement = 67108864, bool lifecycle = true)
    {
        bool rejected = false;
        try
        {
            _ = QemuTranscripts.InspectNativeAotServiceGcForTests(Encoding.UTF8.GetBytes(raw), entitlement, lifecycle);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
    private const string EntryMemory = "M16_014_MEMORY nr=12 regions=0 regionLimit=256 backed=0 pool=9216 reserved=0\r\n";

    [HostTest("quota startup binds exact v3 settings environment listening and first-call topology across framed writes")]
    private static void QuotaStartup()
    {
        foreach (bool segmented in new[] { false, true })
        {
            string raw = Raw(segmented, quotaFailure: true);
            var value = QemuTranscripts.InspectNativeAotServiceStartupForTests(Encoding.UTF8.GetBytes(raw), NativeAotServiceContract.QuotaFailure);
            HostCheck.Equal(Writer, value.WriterIdentity);
            HostCheck.Equal((uint)Encoding.UTF8.GetByteCount(NativeAotServicePackage.QuotaSettingsJson), value.SettingsBytes);
            Reject(raw);
            Reject(raw, lifecycle: true);
        }
    }

    [HostTest("quota startup rejects downgraded contract environment wrong channel and missing or substituted topology")]
    private static void QuotaStartupRejections()
    {
        string valid = Raw(quotaFailure: true);
        string topology = $"M16_016_TOPOLOGY identity={Writer} ordinal=1 nr=12 pool=9216 backed=0 free=9216 freeRuns=1 largestFreeRun=9216 regions=0 regionLimit=256 tables=4 tableLimit=128\r\n";
        foreach (string raw in new[] { valid.Replace("envc=4", "envc=3", StringComparison.Ordinal),
            Raw(quotaFailure: true, listening: Listening.Replace("contract=v1", "contract=v2", StringComparison.Ordinal)),
            Raw(quotaFailure: true, listenChannel: 1), valid.Replace(topology, "", StringComparison.Ordinal),
            valid.Replace(topology, topology.Replace("ordinal=1", "ordinal=2", StringComparison.Ordinal), StringComparison.Ordinal),
            valid.Replace(topology, topology.Replace("backed=0", "backed=1", StringComparison.Ordinal), StringComparison.Ordinal) })
        {
            bool rejected = false;
            try
            {
                _ = QemuTranscripts.InspectNativeAotServiceStartupForTests(Encoding.UTF8.GetBytes(raw), NativeAotServiceContract.QuotaFailure);
            }
            catch (ToolFailure)
            {
                rejected = true;
            }
            HostCheck.True(rejected);
        }
    }

    private static string Raw(bool segmented = false, string? prefix = null, string listening = Listening, uint listenChannel = 2,
        bool lifecycle = false, string? entryMemory = null, bool quotaFailure = false)
    {
        if (quotaFailure)
        {
            lifecycle = true;
            entryMemory ??= EntryMemory;
            entryMemory += $"M16_016_TOPOLOGY identity={Writer} ordinal=1 nr=12 pool=9216 backed=0 free=9216 freeRuns=1 largestFreeRun=9216 regions=0 regionLimit=256 tables=4 tableLimit=128\r\n";
        }
        prefix ??= Prefix;
        int last = prefix.LastIndexOf("CLROS_APP_BUILT", StringComparison.Ordinal);
        string beforeBuilt = prefix[..last], built = prefix[last..];
        uint settings = (uint)Encoding.UTF8.GetByteCount(quotaFailure ? NativeAotServicePackage.QuotaSettingsJson :
            lifecycle ? NativeAotServicePackage.LifecycleSettingsJson : NativeAotServicePackage.SettingsJson);
        int environmentCount = quotaFailure ? 4 : lifecycle ? 3 : 2;
        if (lifecycle && listening == Listening)
            listening = Listening.Replace("contract=v1", quotaFailure ? "contract=v3-quota" : "contract=v2", StringComparison.Ordinal);
        List<string> rows = ["TEST_START m16.014\r\n", "TEST_START m16.014.startup\r\n",
            $"M16_014_ENTER identity={Writer} rip=4198402 argc=5 envc={environmentCount} configBytes={settings}\r\n"];
        Dictionary<ulong, ulong> ordinals = [];
        ulong clock = 0;
        void Call(ulong identity, ulong nr, ulong fd, ulong count, ulong value)
        {
            ordinals.TryGetValue(identity, out ulong ordinal);
            ordinals[identity] = ++ordinal;
            clock += 10;
            rows.Add($"M16_008_CALL identity={identity} nr={nr} a0={fd} a1=4096 a2={count} a3=0 a4=0 a5=0 rip=4198402 rsp=1879048184 flags=514 fs=0 gs=0 clock={clock} valueBits={value} action=1 status=1 ordinal={ordinal}" +
                $" waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4198402 returnRsp=1879048184 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={value}\r\n");
        }
        void Write(ulong identity, string text, string metadata = "", uint channel = 2)
        {
            string[] parts = segmented ? [text[..1], text[1..9], text[9..17], text[17..]] : [text];
            foreach (string part in parts)
                rows.Add(Frame(part, channel));
            rows.Add(metadata);
            Call(identity, segmented ? 20UL : 1UL, channel, segmented ? (ulong)parts.Length : (ulong)text.Length, (ulong)text.Length);
        }
        if (entryMemory is not null)
            rows.Add(entryMemory);
        Call(Writer, 12, 0, 0, 0);
        Write(Writer, beforeBuilt);
        rows.Add($"M16_014_CONFIG fd=3 bytes={settings} source=immutable-package copied=user\r\n");
        Call(Writer, 17, 3, 4096, settings);
        Write(Writer, built, $"M16_014_STARTUP writer={Writer} stages=4\r\n");
        rows.Add($"M16_014_BIND identity={Binder} fd=6\r\n");
        Call(Binder, 49, 6, 16, 0);
        rows.Add("M16_014_LISTEN fd=6 address=0 port=8080 owner=live\r\n");
        Call(Binder, 50, 6, 512, 0);
        string live = $"M16_014_LIVE argc=5 envc={environmentCount} configBytes={settings} bindFd=6 stdoutBytes={(listenChannel == 1 ? listening.Length : 0)} stderrBytes={prefix.Length + (listenChannel == 2 ? listening.Length : 0)} active=2 clock={clock} owner=retained externalHttp=required\r\n" +
            "CLROS_M16_014_LISTENING\r\nTEST_PASS m16.014.startup\r\n";
        Write(Binder, listening, live, listenChannel);
        return string.Concat(rows);
    }
    private static string Change(string raw, string prefix, Func<string, string> change) =>
        ChangeWhere(raw, row => row.StartsWith(prefix, StringComparison.Ordinal), change);
    private static string ChangeWhere(string raw, Func<string, bool> select, Func<string, string> change)
    {
        string[] rows = raw.Split("\r\n");
        int at = Array.FindIndex(rows, row => select(row));
        HostCheck.True(at >= 0);
        string before = rows[at];
        rows[at] = change(before);
        HostCheck.True(rows[at] != before);
        return string.Join("\r\n", rows);
    }
    private static QemuTranscripts.ServiceStartupInspection Read(string raw, bool lifecycle = false) =>
        QemuTranscripts.InspectNativeAotServiceStartupForTests(Encoding.UTF8.GetBytes(raw), lifecycle);
    private static void Reject(string raw, bool lifecycle = false)
    {
        bool rejected = false;
        try
        {
            _ = Read(raw, lifecycle);
        }
        catch (ToolFailure)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
