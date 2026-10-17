using System.Globalization;
using System.Text;
using OS.Tooling;

internal static class NativeAotSocketContainmentGuestTests
{
    private const string Output = """
        TEST_START nativeaot.socket.containment
        SOCKET_CONTAINMENT_LISTEN port=8080 peers=3 rounds=15 interval_ms=5000 deadline_ms=100000
        SOCKET_CONTAINMENT_TIMEOUT elapsed_ms=63005 queued=32768 worker=260
        SOCKET_CONTAINMENT_HEALTHY role=1 rounds=15 elapsed_ms=70100 worker=289
        SOCKET_CONTAINMENT_HEALTHY role=2 rounds=15 elapsed_ms=70200 worker=290
        TEST_PASS nativeaot.socket.containment peers=3 timedout=1 queued=32768 eof=1 shutdown=1 worker_before=5 worker_timeout=260 worker=291 elapsed_ms=70500
        """ + "\n";
    private static string Fields(int role, ulong clock) => FormattableString.Invariant(
        $"slot={role} owner=4096 epoch=1 generation=1 index={role} local=167772687 localPort=8080 remote=167772674 remotePort={41000 + role} clock={clock}");
    private static string[] Rows() => [
        "M16_011_SLOT event=0 " + Fields(0, 1000) + " deadline=0 duration=0",
        "M16_011_SLOT event=0 " + Fields(1, 2000) + " deadline=0 duration=0",
        "M16_011_SLOT event=0 " + Fields(2, 3000) + " deadline=0 duration=0",
        "M16_012_POLL identity=0 clock=5000 ready=1 idle=0",
        "M07_028_DROP before=0 after=1 availableBefore=58 availableAfter=59 clock=35000000000 running=1",
        "M07_027_TIMEOUT " + Fields(0, 63_000_001_000) + " sharedFailed=0",
        "M16_011_SLOT event=1 " + Fields(0, 70_000_100_000) + " deadline=0 duration=0",
        "M16_011_SLOT event=1 " + Fields(1, 70_000_200_000) + " deadline=0 duration=0",
        "M16_011_SLOT event=1 " + Fields(2, 70_000_300_000) + " deadline=0 duration=0",
        "M16_012_POLL identity=0 clock=71000000000 ready=0 idle=1"];
    private static byte[] Raw(string[]? records = null, string stdout = Output, bool split = false)
    {
        byte[] payload = Encoding.UTF8.GetBytes(stdout); using MemoryStream raw = new();
        raw.Write("TEST_START m16-009\r\n"u8);
        raw.Write(Encoding.ASCII.GetBytes(string.Join("\r\n", records ?? Rows()) + "\r\n"));
        int offset = 0;
        foreach (int length in split ? new[] { 1, 19, payload.Length - 20 } : new[] { payload.Length })
        {
            raw.Write(Encoding.ASCII.GetBytes("M16_005_WRITE fd=1 bytes=" + length.ToString(CultureInfo.InvariantCulture) + " data="));
            raw.Write(payload.AsSpan(offset, length)); raw.Write("\r\n"u8); offset += length;
        }
        raw.Write("CLROS_M16_009_READY\r\nTEST_PASS m16-009\r\n"u8); return raw.ToArray();
    }

    [HostTest("containment guest decodes exact segmented stdout common readiness and transport values")]
    private static void Segmented()
    {
        foreach (bool split in new[] { false, true })
        {
            byte[] raw = Raw(split: split), original = (byte[])raw.Clone();
            var value = NativeAotSocketContainmentGuest.Read(raw);
            HostCheck.True(raw.SequenceEqual(original)); HostCheck.Equal(Output, value.Scheduling.Output.Stdout);
            HostCheck.Equal(split ? 3 : 1, value.Scheduling.Output.WriteLengths.Count);
            HostCheck.Equal(2, value.Scheduling.Readiness.Count); HostCheck.Equal(0, value.Scheduling.PendingWaits);
            HostCheck.Equal(3, value.Transport.Lifetimes.Count); HostCheck.Equal(291UL, value.Application.Worker);
            HostCheck.Equal(35_000_000_000UL, value.Transport.DropClock);
            HostCheck.True(((IList<QemuTranscripts.SocketContainmentLifetime>)value.Transport.Lifetimes).IsReadOnly);
        }
    }

    [HostTest("containment guest rejects invalid stdout framing and original-mode substitution")]
    private static void OutputAdmission()
    {
        Reject(Raw(stdout: Output.Replace("worker_before=5", "worker_before=260", StringComparison.Ordinal)));
        Reject(Raw(stdout: NativeAotSocketPayloadTests.FixtureOutput(true)));
        byte[] raw = Raw(); string text = Encoding.UTF8.GetString(raw);
        Reject(Encoding.UTF8.GetBytes(text.Replace("M16_005_WRITE fd=1", "M16_005_WRITE fd=2", StringComparison.Ordinal)));
        Reject(Encoding.UTF8.GetBytes(text.Replace(" data=TEST_START", " data=\ufffdEST_START", StringComparison.Ordinal)));
        foreach (bool mode in new[] { false, true })
        {
            bool rejected = false;
            try { _ = NativeAotSocketGuest.Read(mode, raw); }
            catch (Exception error) when (error is ToolFailure or InvalidDataException) { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    [HostTest("containment guest rejects orphan readiness and stale or incomplete transport observations")]
    private static void Bindings()
    {
        Reject(Raw(["M16_012_WAKE identity=4294967297 sequence=1 completion=1 state=1 clock=1", .. Rows()]));
        Reject(Raw(["M16_012_UNKNOWN identity=0", .. Rows()]));
        Reject(Raw(Rows().Select(row => row.Replace("before=0 after=1", "before=0 after=2", StringComparison.Ordinal)).ToArray()));
        Reject(Raw(Rows().Select(row => row.StartsWith("M07_027_TIMEOUT", StringComparison.Ordinal) ?
            row.Replace("generation=1", "generation=2", StringComparison.Ordinal) : row).ToArray()));
        Reject(Raw(Rows().Where(row => !row.StartsWith("M07_028_DROP", StringComparison.Ordinal)).ToArray()));
    }

    private static void Reject(byte[] raw)
    {
        bool rejected = false;
        try { _ = NativeAotSocketContainmentGuest.Read(raw); }
        catch (Exception error) when (error is ToolFailure or DecoderFallbackException) { rejected = true; }
        HostCheck.True(rejected);
    }
}
