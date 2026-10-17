using System.Globalization;
using System.Text;
using OS.Tooling;

internal static class NativeAotSocketContainmentProtocolTests
{
    private const string Output = """
        TEST_START nativeaot.socket.containment
        SOCKET_CONTAINMENT_LISTEN port=8080 peers=3 rounds=15 interval_ms=5000 deadline_ms=100000
        SOCKET_CONTAINMENT_TIMEOUT elapsed_ms=63005 queued=32768 worker=260
        SOCKET_CONTAINMENT_HEALTHY role=1 rounds=15 elapsed_ms=70100 worker=289
        SOCKET_CONTAINMENT_HEALTHY role=2 rounds=15 elapsed_ms=70200 worker=290
        TEST_PASS nativeaot.socket.containment peers=3 timedout=1 queued=32768 eof=1 shutdown=1 worker_before=5 worker_timeout=260 worker=291 elapsed_ms=70500
        """ + "\n";
    private static ulong Bits(long value) => unchecked((ulong)value);
    private static string Fields(int role, ulong clock) => FormattableString.Invariant(
        $"slot={role} owner=4096 epoch=1 generation=1 index={role} local=167772687 localPort=8080 remote=167772674 remotePort={41000 + role} clock={clock}");

    // Compose the independently checked common scheduler vector. These rows
    // never construct a sealed program, original package or execution receipt.
    private static List<string> Vector()
    {
        var v = new NativeAotThreadingTranscriptTests.Vector(false).Start();
        for (int i = 0; i < v.Rows.Count; i++) v.Rows[i] = v.Rows[i].Replace("entropyBytes=32", "entropyBytes=256", StringComparison.Ordinal)
            .Replace("packageBytes=1320176", "packageBytes=1585456", StringComparison.Ordinal).Replace("workers=4", "workers=16", StringComparison.Ordinal);
        v.Rows.Insert(3, "M16_011_BOUND connections=8 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        v.Call(291, 5, [0x80000]); v.Call(41, 6, [2, 0x80001, 6]);
        v.Call(72, 0, [6, 4, 0x802]); v.Call(49, 0, [6, 0x500200, 16]); v.Call(50, 0, [6, 3]);
        var worker = v.Clone(2, 2); var runtime = v.Clone(3, 3);
        v.Irq(10, 3, 3); v.Select(worker); v.Wait(0x500200); v.Drain(20); v.Supervisor(3, 2, 3);
        v.Select(runtime); v.Wait(0x500204); v.Drain(30); v.Supervisor(3, 1, 3); v.Select(v.Main);
        for (int role = 0; role < 3; role++)
        {
            v.Rows.Add("M16_011_SLOT event=0 " + Fields(role, (ulong)(1000 + role * 1000)) + " deadline=0 duration=0");
            v.Call(288, 7 + role, [6, 0x500700, 0x500780, 0x80800]);
            v.Call(45, 1, [(ulong)(7 + role), 0x500800, 1, 0, 0, 0]);
        }
        for (int chunk = 0; chunk < 4; chunk++) v.Call(44, 8192, [7, 0x500000, (ulong)(65536 - chunk * 8192), 0, 0, 0]);
        v.Call(44, -11, [7, 0x500000, 32768, 0, 0, 0]);
        for (int round = 0; round < 15; round++)
        {
            if (round == 13)
            {
                v.Clock = 63_000_001_000;
                v.Rows.Add("M07_027_TIMEOUT " + Fields(0, v.Clock) + " sharedFailed=0");
                v.Rows.Add("M16_011_SLOT event=1 " + Fields(0, v.Clock) + " deadline=0 duration=0");
                v.Call(45, -110, [7, 0x500800, 1, 0, 0, 0]); v.Call(45, 0, [7, 0x500800, 1, 0, 0, 0]);
                v.Call(44, -32, [7, 0x500800, 1, 0, 0, 0]);
            }
            v.Clock = 10_000 + (ulong)round * 5_000_000_000;
            if (round == 7) v.Rows.Add("M07_028_DROP before=0 after=1 availableBefore=58 availableAfter=59 delivered=0 clock=35000010000 running=1");
            for (ulong fd = 8; fd < 10; fd++)
            { v.Call(45, 1, [fd, 0x500800, 1, 0, 0, 0]); v.Call(44, 1, [fd, 0x500800, 1, 0, 0, 0]); }
        }
        v.Clock = 70_001_000_000;
        for (int role = 1; role < 3; role++) v.Rows.Add("M16_011_SLOT event=1 " + Fields(role, v.Clock) + " deadline=0 duration=0");
        for (ulong fd = 6; fd < 10; fd++) v.Call(3, 0, [fd]);
        v.Wake(0x500200); v.Wake(0x500204); v.Irq(v.Clock + 1, 3, 3);
        v.Select(worker); v.Resume(0); v.Call(60, 0, [0], 4); v.Drain(v.Clock + 1); v.Exit(4, 2, 3);
        v.Select(runtime); v.Resume(0); v.Call(60, 0, [0], 4); v.Drain(v.Clock + 1); v.Exit(4, 1, 2);
        v.Select(v.Main); int length = Encoding.UTF8.GetByteCount(Output);
        v.Rows.Add($"M16_005_WRITE fd=1 bytes={length} data={Output}"); v.Call(1, length, [1, 0x500000, (ulong)length]);
        v.Call(231, 0, [0], 2); v.Drain(v.Clock); v.Exit(2, 0, 1);
        for (int fd = 0; fd <= 5; fd++) v.Rows.Add($"M16_011_CLOSE fd={fd} valueBits=0"); v.End(3); return v.Rows;
    }

    private static QemuTranscripts.ThreadingProtocolReport Inspect(IEnumerable<string> rows) =>
        QemuTranscripts.InspectNativeAotSocketContainmentProtocolForTests(NativeAotThreadingTranscriptTests.Elf(),
            Encoding.UTF8.GetBytes(string.Join("\r\n", rows) + "\r\n"), address => address == 0x400200 || address == 0x400202,
            address => address == 0x400202, 8192, 12288);

    private static void Reject(IEnumerable<string> rows, string reason)
    {
        string? actual = null;
        try { _ = Inspect(rows); } catch (ToolFailure error) { actual = error.Message; }
        HostCheck.True(actual?.Contains(reason, StringComparison.Ordinal) == true, reason + " actual=" + actual);
    }

    private static int CallAt(List<string> rows, ulong nr, long result) => rows.FindIndex(row => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal) &&
        row.Contains(" nr=" + nr.ToString(CultureInfo.InvariantCulture) + " ", StringComparison.Ordinal) &&
        row.Contains(" valueBits=" + Bits(result).ToString(CultureInfo.InvariantCulture) + " ", StringComparison.Ordinal));

    private static string Result(string row, long before, long after) => row.Replace("valueBits=" + Bits(before) + " ", "valueBits=" + Bits(after) + " ", StringComparison.Ordinal)
        .Replace("returnValueBits=" + Bits(before), "returnValueBits=" + Bits(after), StringComparison.Ordinal);

    [HostTest("containment protocol replays three original descriptor lifetimes exact errors and full off-stack reap")]
    private static void Complete()
    { var value = Inspect(Vector()); HostCheck.Equal(2, value.Clones); HostCheck.Equal(3, value.Reaped); HostCheck.Equal(2, value.Irqs); }

    [HostTest("containment protocol bounds the entire guarded pool below four GiB")]
    private static void PoolExtent()
    {
        static string[] At(IEnumerable<string> rows,ulong pool)=>rows.Select(row=>row.Replace("pool=2097152",
            FormattableString.Invariant($"pool={pool}"),StringComparison.Ordinal)).ToArray();
        Reject(At(Vector(),0xfff40000),"threading_parent_geometry");
        HostCheck.Equal(3,Inspect(At(Vector(),0xfff1e000)).Reaped);
    }

    [HostTest("containment protocol requires ETIMEDOUT then EOF then EPIPE on only the saturated peer")]
    private static void ErrorSequence()
    {
        foreach (var change in new[] { (45UL, -110L, -104L), (45UL, -110L, 0L), (44UL, -32L, -108L), (44UL, -32L, -11L) })
        {
            var rows = Vector(); int at = CallAt(rows, change.Item1, change.Item2); rows[at] = Result(rows[at], change.Item2, change.Item3);
            Reject(rows, "socket_containment_");
        }
        var wrongPeer = Vector(); int timeout = CallAt(wrongPeer, 45, -110);
        wrongPeer[timeout] = wrongPeer[timeout].Replace(" a0=7 ", " a0=8 ", StringComparison.Ordinal); Reject(wrongPeer, "socket_containment_io_result");
        var ordered = Vector(); int eof = CallAt(ordered, 45, 0); (ordered[eof], ordered[eof - 1]) = (ordered[eof - 1], ordered[eof]);
        Reject(ordered, "ordinal");
    }

    [HostTest("containment protocol binds queued stdout bytes saturation and healthy per-descriptor counts")]
    private static void Counts()
    {
        var rows = Vector().Select(row => row.Replace("queued=32768", "queued=32769", StringComparison.Ordinal)); Reject(rows, "socket_containment_connection_io");
        var shortSend = Vector(); int at = CallAt(shortSend, 44, 8192); shortSend[at] = Result(shortSend[at], 8192, 8191); Reject(shortSend, "socket_containment_saturation");
        var badReceive = Vector(); at = badReceive.FindIndex(row => row.Contains(" nr=45 a0=8 ", StringComparison.Ordinal));
        badReceive[at] = Result(badReceive[at], 1, 0); Reject(badReceive, "socket_containment_eof_call");
        var backlog = Vector(); at = CallAt(backlog, 50, 0); backlog[at] = backlog[at].Replace(" a1=3 ", " a1=4 ", StringComparison.Ordinal); Reject(backlog, "socket_listen");
    }

    [HostTest("containment protocol rejects errno before original timeout observation and old-mode substitution")]
    private static void Observations()
    {
        var rows = Vector(); int at = rows.FindIndex(row => row.StartsWith("M07_027_TIMEOUT ", StringComparison.Ordinal));
        string[] observations = rows.GetRange(at, 2).ToArray(); rows.RemoveRange(at, 2);
        int error = CallAt(rows, 45, -110); rows.InsertRange(error + 1, observations); Reject(rows, "socket_containment_timeout_call");
        var original = Vector().Select(row => row.Replace("packageBytes=1585456", "packageBytes=2117424", StringComparison.Ordinal)); Reject(original, "threading_order_M16_007_POLICY");
        var stale = Vector().Select(row => row.StartsWith("M07_027_TIMEOUT ", StringComparison.Ordinal) ? row.Replace("generation=1", "generation=2", StringComparison.Ordinal) : row);
        Reject(stale, "socket_containment_transport_");
    }

    [HostTest("containment protocol retains common user envelope scheduler FIFO and scrubbed owner cleanup")]
    private static void Scheduler()
    {
        var rows = Vector(); int at = CallAt(rows, 44, 8192); rows[at] = rows[at].Replace(" rip=4194818 ", " rip=4194819 ", StringComparison.Ordinal); Reject(rows, "threading_call_binding");
        var ready = Vector().Select(row => row.Replace("transfers=2 ready=3", "transfers=2 ready=2", StringComparison.Ordinal)); Reject(ready, "threading_supervisor");
        var dirty = Vector().Select(row => row.Replace("pool=scrubbed", "pool=dirty", StringComparison.Ordinal)); Reject(dirty, "threading_owner_cleanup");
        var dirtyReap = Vector().Select(row => row.Replace("context=zero", "context=dirty", StringComparison.Ordinal)); Reject(dirtyReap, "threading_reap_scrub");
    }
}
