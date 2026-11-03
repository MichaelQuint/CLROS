using System.Text;
using OS.Tooling;

internal static class NativeAotServiceMemoryTests
{
    // Independent value vectors only: these do not model a complete original
    // syscall/owner protocol and cannot confer executable or guest authority.
    private const string Gc = "limit=16777216 baselineHeap=30920 baselineCommitted=151552 liveBytes=2097152 liveHeap=2129504 liveCommitted=4194304 pressureBytes=12582912 pressureHeap=12615680 pressureCommitted=14680064 pressureCollections=3 oom=1\n";
    private const string Native = "capacityBytes=33554432 retainedBytes=25165824 gcCommitted=4194304 quota=1 errno=12 releasedBytes=25165824 recoveredBytes=1048576\n";
    private const string Drain = "CLROS_SERVICE_LIFECYCLE_END CLROS_LIFECYCLE_STATUS active=0 waiting=0 waits=2 canceled=2 echoes=1 streams=1 rejected=1 read=65536 written=524288\n";
    private const ulong NoMemory = unchecked((ulong)-12L);

    private static string Frame(string text, uint channel = 2) => $"M16_005_WRITE fd={channel} bytes={Encoding.UTF8.GetByteCount(text)} data={text}\r\n";
    private static string Call(ulong ordinal, ulong nr, ulong value, ulong a0 = 0, ulong a1 = 1048576, ulong a2 = 3,
        ulong a3 = 34, ulong a4 = 4294967295, ulong a5 = 0, ulong rip = 4198402) =>
        $"M16_008_CALL identity=4294967297 nr={nr} a0={a0} a1={a1} a2={a2} a3={a3} a4={a4} a5={a5} rip={rip} rsp=1879048184 flags=514 fs=0 gs=0 clock={ordinal} valueBits={value} action=1 status=1 ordinal={ordinal}" +
        $" waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip={rip} returnRsp=1879048184 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={value}\r\n";
    private static string Snapshot(ulong nr, ulong backed, uint pool = 9216) =>
        $"M16_014_MEMORY nr={nr} regions=8 regionLimit=256 backed={backed} pool={pool} reserved=17179869184\r\n";

    private static string Raw(bool segmented = false, ulong nativeBacked = 9024, ulong ordinary = 3000, bool remap = false, bool gcArena = false)
    {
        var text = new StringBuilder("M16_016_BUDGET gc=16777216 nativeReserve=20971520 static=16777216 pool=37748736 entitlement=67108864\r\nTEST_START m16.014.startup\r\n");
        ulong ordinal = 0, current = nativeBacked - 24 * 256;
        void Output(string value)
        {
            foreach (string part in segmented ? value.Chunk(7).Select(c => new string(c)) : new[] { value })
                text.Append(Frame(part));
        }
        void Observe(ulong nr, ulong backed, ulong result, ulong a0 = 0, ulong bytes = 1048576,
            ulong protection = 3, ulong flags = 34, ulong rip = 4198402)
        {
            current = backed;
            text.Append(Snapshot(nr, current));
            text.Append(Call(++ordinal, nr, result, a0, bytes, protection, flags, rip: rip));
        }
        void Pressure()
        {
            ulong[] addresses = new ulong[24];
            for (int i = 0; i < addresses.Length; i++)
            {
                addresses[i] = 0x200000000UL + (ulong)i * 1048576;
                Observe(9, current + 256, addresses[i]);
            }
            Observe(9, current, NoMemory);
            foreach (ulong address in addresses)
                Observe(11, current - 256, 0, address);
            Observe(9, current + 256, addresses[0]);
            Observe(11, current - 256, 0, addresses[0]);
        }
        Observe(12, current, 0, bytes: 0);
        if (gcArena)
        {
            Observe(9, current, 0x100185000, bytes: 83890176, protection: 0, rip: 0x4705da);
            Observe(11, current, 0, 0x100185000, 4096, rip: 0x4706d1);
            Observe(28, current, 0, 0x100186000, 83886080, protection: 16, rip: 0x470581);
        }
        Output("CLROS_APP_ENTRY minimal.api.service\nCLROS_APP_BUILT minimal.api.service\nCLROS_NOTE é\nCLROS_SERVICE_GC_BEGIN " + Gc);
        Pressure();
        Output("CLROS_SERVICE_NATIVE_BEGIN " + Native);
        Output("CLROS_SERVICE_LISTEN contract=v2 endpoint=0.0.0.0:8080\n");
        ulong baseline = current;
        if (ordinary > baseline)
        {
            if (gcArena)
            {
                Observe(10, ordinary, 0, 0x100186000, (ordinary - baseline) * 4096, rip: 0x47068a);
                Observe(9, baseline, 0x100186000, 0x100186000, (ordinary - baseline) * 4096, protection: 0, flags: 0x32, rip: 0x4705da);
            }
            else
            {
                Observe(remap ? 25UL : 9UL, ordinary, 0x200000000, bytes: (ordinary - baseline) * 4096);
                Observe(11, baseline, 0, 0x200000000, (ordinary - baseline) * 4096);
            }
        }
        Output(Drain + "CLROS_SERVICE_GC_END " + Gc);
        Pressure();
        Output("CLROS_SERVICE_NATIVE_END " + Native);
        Output("CLROS_SERVICE_APP_END calls=4097 randomRequests=1 randomBytes=8192 iterations=1 elapsedMillis=300001 worker=joined\n");
        text.Append($"M16_016_MEMORY poolPages=9216 peakPages={Math.Max(nativeBacked, ordinary)} endPages=0\r\n");
        text.Append("M16_008_OWNER_END reaped=18 transfers=64 irqs=1 exit=0 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained\r\n");
        return text.ToString();
    }
    private static QemuTranscripts.ServiceMemoryInspection Read(string raw) =>
        QemuTranscripts.InspectNativeAotServiceMemoryForTests(Encoding.UTF8.GetBytes(raw), 67108864, true)!.Value;
    private static void Reject(string raw, bool lifecycle = true)
    {
        bool rejected = false;
        try
        {
            _ = QemuTranscripts.InspectNativeAotServiceMemoryForTests(Encoding.UTF8.GetBytes(raw), 67108864, lifecycle);
        }
        catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
    private static string ChangeSnapshot(string raw, int ordinal, Func<string, string> change)
    {
        string[] rows = raw.Split("\r\n", StringSplitOptions.None);
        int found = 0;
        for (int i = 0; i < rows.Length; i++)
            if (rows[i].StartsWith("M16_014_MEMORY ", StringComparison.Ordinal) && found++ == ordinal)
            {
                rows[i] = change(rows[i]);
                return string.Join("\r\n", rows.Where((row, index) => index != i || row.Length != 0));
            }
        throw new InvalidOperationException("snapshot vector");
    }

    [HostTest("service memory snapshots separate ordinary backing from both native pressure windows across split UTF8 framing")]
    private static void Windows()
    {
        foreach (bool segmented in new[] { false, true })
        {
            string raw = Raw(segmented);
            byte[] bytes = Encoding.UTF8.GetBytes(raw), original = bytes.ToArray();
            var value = QemuTranscripts.InspectNativeAotServiceMemoryForTests(bytes, 67108864, true)!.Value;
            HostCheck.Equal(105, value.OperationSnapshots);
            HostCheck.Equal(12288000UL, value.OrdinaryPeakBackedBytes);
            HostCheck.Equal(8UL, value.MaximumRegionCount);
            HostCheck.Equal(2, value.NativePhysicalShortfalls);
            HostCheck.Equal(0, value.NativeUnclassifiedFailures);
            HostCheck.Equal(29065216UL, value.ConservativePoolBytes);
            HostCheck.Equal(45842432UL, value.ConservativeEntitlementBytes);
            HostCheck.True(value.ConservativeHeadroomFits);
            HostCheck.True(bytes.AsSpan().SequenceEqual(original));
            Array.Fill(bytes, (byte)0);
            HostCheck.Equal(12288000UL, value.OrdinaryPeakBackedBytes);
        }
        HostCheck.Equal(11796480UL, Read(Raw(ordinary: 0)).OrdinaryPeakBackedBytes);
        HostCheck.Equal(105, Read(Raw(remap: true)).OperationSnapshots);
    }

    [HostTest("service memory distinguishes unclassified native ENOMEM and reports conservative bounds without granting headroom")]
    private static void Bounds()
    {
        var unknown = Read(Raw(nativeBacked: 7900));
        HostCheck.Equal(0, unknown.NativePhysicalShortfalls);
        HostCheck.Equal(2, unknown.NativeUnclassifiedFailures);
        var exceeded = Read(Raw(ordinary: 6000));
        HostCheck.True(!exceeded.ConservativeHeadroomFits);
        HostCheck.True(!exceeded.NativeReserveFits);
        HostCheck.Equal(41353216UL, exceeded.ConservativePoolBytes);
    }

    [HostTest("service memory subtracts only dedicated still-owned GC backing while retaining a conservative native reserve bound")]
    private static void DedicatedGcArena()
    {
        foreach (bool segmented in new[] { false, true })
        {
            var value = Read(Raw(segmented, gcArena: true));
            HostCheck.Equal(108, value.OperationSnapshots);
            HostCheck.Equal(12288000UL, value.OrdinaryPeakBackedBytes);
            HostCheck.Equal(11796480UL, value.OrdinaryNonGcUpperBoundBytes);
            HostCheck.Equal(28573696UL, value.ConservativePoolBytes);
            HostCheck.Equal(45350912UL, value.ConservativeEntitlementBytes);
            HostCheck.True(value.NativeReserveFits && value.ConservativeHeadroomFits && value.GcArena.HasValue);
            HostCheck.Equal(491520UL, value.GcArena!.Value.PeakBackedBytes);
            HostCheck.Equal(0UL, value.GcArena.Value.LastBackedBytes);
        }
        var unknown = Read(Raw());
        HostCheck.True(unknown.GcArena is null);
        HostCheck.Equal(unknown.OrdinaryPeakBackedBytes, unknown.OrdinaryNonGcUpperBoundBytes);
    }

    [HostTest("service memory requires unique adjacent canonical bounded snapshots on every configured memory return")]
    private static void ObservationControls()
    {
        foreach (Func<string, string> change in new Func<string, string>[] { _ => "", s => s + "\r\n" + s,
            s => s.Replace("nr=12", "nr=9", StringComparison.Ordinal), s => s.Replace("regionLimit=256", "regionLimit=64", StringComparison.Ordinal),
            s => s.Replace("regions=8", "regions=257", StringComparison.Ordinal), s => s.Replace("pool=9216", "pool=4096", StringComparison.Ordinal),
            s => s.Replace("backed=2880", "backed=9217", StringComparison.Ordinal), s => s.Replace("backed=2880", "backed=02880", StringComparison.Ordinal),
            s => s.Replace("reserved=17179869184", "reserved=17179869185", StringComparison.Ordinal),
            s => s.Replace("reserved=17179869184", "reserved=4096", StringComparison.Ordinal), s => s + " extra=1" })
            Reject(ChangeSnapshot(Raw(), 0, change));
        Reject(ChangeSnapshot(Raw(), 0, s => s + "\r\nUNRELATED_ROW"));
        Reject(ChangeSnapshot(Raw(remap: true), 52, _ => ""));
        Reject(Raw().Replace("peakPages=9024", "peakPages=9023", StringComparison.Ordinal));
    }

    [HostTest("service native failed mapping must preserve the preceding backing reservation and interval counters")]
    private static void FailurePreservation()
    {
        foreach (var change in new[] { ("backed=9024", "backed=9023"), ("regions=8", "regions=7"), ("reserved=17179869184", "reserved=17179865088") })
            Reject(ChangeSnapshot(Raw(), 25, s => s.Replace(change.Item1, change.Item2, StringComparison.Ordinal)));
        Reject(Raw().Replace("valueBits=" + NoMemory, "valueBits=0", StringComparison.Ordinal));
        Reject(ChangeSnapshot(Raw(), 25, _ => ""));
        Reject(ChangeSnapshot(Raw(), 78, _ => ""));
    }

    [HostTest("service memory rejects forged channels while v1 retains only legacy failed-call observations")]
    private static void ChannelsAndLegacy()
    {
        foreach (uint channel in new[] { 1U, 2U })
            Reject(Raw() + Frame(Snapshot(9, 2880), channel));
        string failed = Snapshot(9, 8000, 8192) + Call(1, 9, NoMemory);
        HostCheck.True(QemuTranscripts.InspectNativeAotServiceMemoryForTests(Encoding.UTF8.GetBytes(failed), 0) is null);
        Reject(Snapshot(9, 8000, 8192) + Call(1, 9, 0x100000000), false);
        Reject(Call(1, 9, NoMemory), false);
        Reject(failed.Replace("regionLimit=256", "regionLimit=64", StringComparison.Ordinal), false);
        Reject(Raw(), false);
    }
}
