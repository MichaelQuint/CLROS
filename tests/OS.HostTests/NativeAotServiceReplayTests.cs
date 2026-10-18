using System.Text;
using OS.Tooling;

internal static class NativeAotServiceReplayTests
{
    private const ulong Worker = 0x100000001;
    private const string Suffix = " waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0" +
        " returnRip=4198402 returnRsp=1879048184 returnFlags=514 returnFs=0 returnGs=0 returnValueBits=1";
    private static string Call(ulong identity, ulong ordinal) => $"M16_008_CALL identity={identity} nr=39 ordinal={ordinal}" + Suffix;

    [HostTest("service replay retains cumulative calls beyond4096 without relaxing finite or generation boundaries")]
    private static void Cumulative()
    {
        var rows = Enumerable.Range(1, 4097).Select(n => Call(Worker, (ulong)n)).ToList();
        rows.Insert(1, Call(Worker + 1, 1)); rows.Add(Call(Worker + (1UL << 32), 1));
        string[] original = rows.ToArray(), before = rows.ToArray();
        var normalized = QemuTranscripts.ValidateLinuxServiceCallOrdinals(original);
        var waits = QemuTranscripts.ReadLinuxServiceCallWaits(original); var returns = QemuTranscripts.ReadLinuxServiceCallReturns(original);
        HostCheck.Equal(4099, waits.Count); HostCheck.Equal(4099, returns.Count);
        HostCheck.Equal(4097UL, waits[^2].Ordinal); HostCheck.Equal(Worker + (1UL << 32), waits[^1].Identity); HostCheck.Equal(1UL, waits[^1].Ordinal);
        HostCheck.Equal("M16_008_CALL identity=4294967297 nr=39", normalized[0]); HostCheck.True(original.SequenceEqual(before));
        Reject(() => QemuTranscripts.ValidateLinuxCallOrdinals(original, allowEpoll: true));
        Reject(() => QemuTranscripts.ReadLinuxCallWaits(original, allowEpoll: true));
        Reject(() => QemuTranscripts.ReadLinuxCallReturns(original, allowEpoll: true));
        Array.Fill(original, "changed"); HostCheck.Equal(Worker, waits[0].Identity); HostCheck.Equal(4198402UL, returns[0].Rip);
        HostCheck.True(waits is not QemuTranscripts.LinuxCallWaitObservation[] && returns is not QemuTranscripts.LinuxCallReturnObservation[]);
    }

    [HostTest("service replay still rejects missing duplicate reordered noncanonical and overflowing call ordinals")]
    private static void OrdinalFailures()
    {
        foreach (string[] rows in new[] { new[] { Call(Worker, 0) }, new[] { Call(Worker, 2) }, new[] { Call(Worker, ulong.MaxValue) },
            new[] { Call(Worker, 1), Call(Worker, 1) }, new[] { Call(Worker, 1), Call(Worker, 3) },
            new[] { Call(Worker, 1), Call(Worker + (1UL << 32), 2) },
            new[] { Call(Worker, 1).Replace("ordinal=1 ", "ordinal=01 ", StringComparison.Ordinal) },
            new[] { Call(Worker, 1).Replace("ordinal=1 ", "ordinal=18446744073709551616 ", StringComparison.Ordinal) } })
            Reject(() => QemuTranscripts.ValidateLinuxServiceCallOrdinals(rows));
    }

    [HostTest("service cumulative readers retain exact current wait and restored-return shape requirements")]
    private static void SuffixFailures()
    {
        string call = Call(Worker, 1);
        foreach (string changed in new[] { call.Replace("waitSource=0 waitDeadline=0", "waitSource=0 waitDeadline=1", StringComparison.Ordinal),
            call.Replace("waitSequence=0", "waitSequence=1", StringComparison.Ordinal),
            call.Replace("signalDepth=0", "signalDepth=1", StringComparison.Ordinal),
            call.Replace(" returnRip=4198402", " returnRip=04198402", StringComparison.Ordinal), call + " extra=0" })
            Reject(() => QemuTranscripts.ReadLinuxServiceCallReturns([changed]));
        Reject(() => QemuTranscripts.ReadLinuxServiceCallWaits([$"M16_008_CALL identity={Worker} nr=39 ordinal=1"]));
        Reject(() => QemuTranscripts.ReadLinuxServiceCallReturns([$"M16_008_CALL identity={Worker} nr=39 ordinal=1 waitSource=0 waitDeadline=0 waitSequence=0"]));
        string epoll = call.Replace("waitSource=0 waitDeadline=0 waitSequence=0", "waitSource=3 waitDeadline=500 waitSequence=7", StringComparison.Ordinal);
        var observation = QemuTranscripts.ReadLinuxServiceCallWaits([epoll]).Single(); HostCheck.Equal(3U, observation.Source); HostCheck.Equal(500UL, observation.Deadline);
    }

    [HostTest("service random replay binds partial results error controls counters and erased-owner end to original calls")]
    private static void RandomAccounting()
    {
        string[] rows = RandomRows(), before = rows.ToArray();
        var report = QemuTranscripts.InspectNativeAotServiceRandomForTests(rows);
        HostCheck.Equal(33UL, report.Requests); HostCheck.Equal(17376UL, report.GeneratedBytes);
        HostCheck.Equal(2, report.PartialRequests); HostCheck.Equal(1, report.ZeroRequests);
        HostCheck.Equal(1, report.InvalidFlagRequests); HostCheck.Equal(1, report.FaultRequests);
        HostCheck.True(rows.SequenceEqual(before)); Array.Fill(rows, "changed"); HostCheck.Equal(17376UL, report.GeneratedBytes);
    }

    [HostTest("service random replay rejects detached reordered duplicated incomplete and substituted metadata")]
    private static void RandomBindings()
    {
        string[] rows = RandomRows();
        foreach (string token in new[] { "M11_012_SEED ", "M07_033_SEED ", "M11_012_RANDOM ", "M11_012_END " })
        {
            int at = Array.FindIndex(rows, row => row.StartsWith(token, StringComparison.Ordinal));
            Reject(() => QemuTranscripts.InspectNativeAotServiceRandomForTests(rows.Where((_, i) => i !=at).ToArray()));
            var doubled = rows.ToList(); doubled.Insert(at, rows[at]);
            Reject(() => QemuTranscripts.InspectNativeAotServiceRandomForTests(doubled.ToArray()));
        }
        int first = Array.FindIndex(rows, row => row.StartsWith("M16_008_CALL ", StringComparison.Ordinal));
        foreach (var substitution in new[] { ("nr=318", "nr=39"), ("a1=0", "a1=1"), ("a2=1", "a2=2"),
            ("valueBits=0 action=1", "valueBits=1 action=1"), ("returnValueBits=0", "returnValueBits=1"),
            ("ordinal=1 ", "ordinal=2 "), ("action=1 status=1", "action=2 status=1"), ("action=1 status=1", "action=1 status=2") })
        {
            var changed = rows.ToArray(); changed[first] = changed[first].Replace(substitution.Item1, substitution.Item2, StringComparison.Ordinal);
            HostCheck.True(changed[first] !=rows[first]); Reject(() => QemuTranscripts.InspectNativeAotServiceRandomForTests(changed));
        }
        var reordered = rows.ToArray(); (reordered[0], reordered[1]) = (reordered[1], reordered[0]);
        Reject(() => QemuTranscripts.InspectNativeAotServiceRandomForTests(reordered));
        var afterEnd = rows.Append(rows[first]).ToArray(); Reject(() => QemuTranscripts.InspectNativeAotServiceRandomForTests(afterEnd));
        Reject(() => QemuTranscripts.InspectNativeAotServiceRandomForTests(rows.Append("M11_012_UNKNOWN value=0").ToArray()));
    }

    [HostTest("service random replay rejects incorrect caps errors counter jumps noncanonical values and missing controls")]
    private static void RandomFailures()
    {
        string[] rows = RandomRows();
        foreach (var change in new[] { ("requests=33 generatedBytes=17376", "requests=34 generatedBytes=17376"),
            ("requests=33 generatedBytes=17376", "requests=33 generatedBytes=17375"),
            ("requested=16384 flags=1 valueBits=8192", "requested=16384 flags=1 valueBits=16384"),
            ("requested=0 flags=1", "requested=00 flags=1"),
            ("requests=0 generatedBytes=0", "requests=18446744073709551616 generatedBytes=0"),
            ("valueBits=18446744073709551602", "valueBits=18446744073709551605"),
            ("generator=zero", "generator=retained"), ("acquisitions=2", "acquisitions=1") })
        {
            var changed = rows.Select(row => row.Replace(change.Item1, change.Item2, StringComparison.Ordinal)).ToArray();
            HostCheck.True(!changed.SequenceEqual(rows)); Reject(() => QemuTranscripts.InspectNativeAotServiceRandomForTests(changed));
        }
        // Consistently rewriting metadata and calls cannot turn Linux's full
        // return into the adapter's required8192-byte partial controls.
        var full = rows.Select(row => row.Replace("valueBits=8192", "valueBits=16384", StringComparison.Ordinal)
            .Replace("returnValueBits=8192", "returnValueBits=16384", StringComparison.Ordinal)).ToArray();
        Reject(() => QemuTranscripts.InspectNativeAotServiceRandomForTests(full));
        foreach (var control in new[] {
            ("requested=16384 flags=1", "requested=8193 flags=1", "a1=16384 a2=1", "a1=8193 a2=1"),
            ("requested=1 flags=8", "requested=1 flags=4", "a1=1 a2=8", "a1=1 a2=4"),
            ("requested=0 flags=1", "requested=0 flags=0", "a1=0 a2=1", "a1=0 a2=0"),
            ("requested=32 flags=1 valueBits=18446744073709551602", "requested=16 flags=1 valueBits=18446744073709551602",
                "a0=0 a1=32 a2=1", "a0=0 a1=16 a2=1") })
        {
            var changed = rows.Select(row => row.Replace(control.Item1, control.Item2, StringComparison.Ordinal)
                .Replace(control.Item3, control.Item4, StringComparison.Ordinal)).ToArray();
            Reject(() => QemuTranscripts.InspectNativeAotServiceRandomForTests(changed));
        }
        Reject(() => QemuTranscripts.InspectNativeAotServiceRandomForTests([]));
        Reject(() => QemuTranscripts.InspectNativeAotServiceRandomForTests(null!));
    }

    [HostTest("service framing increases only serial extent and preserves channel payload boundaries and finite limits")]
    private static void ServiceFraming()
    {
        byte[] large = new byte[8 *1024 *1024 +1]; Array.Fill(large, (byte)'x');
        var extended = NativeAotThreadingOutput.ReadService(large);
        HostCheck.Equal(large.Length, extended.NormalizedTail.Length); HostCheck.Equal("", extended.Stdout);
        Reject(() => NativeAotThreadingOutput.ReadWeb(large));
        string fake = "M11_012_SEED forged=1\nM16_008_CALL forged=1\n";
        byte[] raw = Encoding.UTF8.GetBytes($"M16_005_WRITE fd=2 bytes={Encoding.UTF8.GetByteCount(fake)} data={fake}\r\n" +
            "M16_005_WRITE fd=1 bytes=3 data=ok\n\r\n");
        byte[] before = raw.ToArray(); var framed = NativeAotThreadingOutput.ReadService(raw);
        HostCheck.Equal(fake, framed.Stderr); HostCheck.Equal("ok\n", framed.Stdout);
        HostCheck.True(!framed.NormalizedTail.Contains("forged", StringComparison.Ordinal) && raw.SequenceEqual(before));
        HostCheck.True(framed.WriteChannels.SequenceEqual(new uint[] { 2, 1 }));
        foreach (string bad in new[] { "M16_005_WRITE fd=1 bytes=8193 data=", "M16_005_WRITE fd=3 bytes=1 data=x\r\n",
            "M16_005_WRITE fd=2 bytes=01 data=x\r\n", "M16_005_WRITE fd=1 bytes=1 data=x\n" })
            Reject(() => NativeAotThreadingOutput.ReadService(Encoding.UTF8.GetBytes(bad)));
    }

    [HostTest("service readiness binds cumulative calls beyond4096 to the exact wait winner completion and resumed generation")]
    private static void ServiceReadiness()
    {
        var rows = Enumerable.Range(1, 4097).Select(n => Call(Worker, (ulong)n)).ToList();
        rows.Add(Call(Worker, 4098).Replace("nr=39", "nr=281", StringComparison.Ordinal)
            .Replace("waitSource=0 waitDeadline=0 waitSequence=0", "waitSource=3 waitDeadline=500 waitSequence=7", StringComparison.Ordinal));
        rows.Add($"M16_012_WAKE identity={Worker} sequence=7 completion=2 state=1 clock=500");
        rows.Add($"M16_012_COMPLETE identity={Worker} sequence=7 completion=2 valueBits=0 clock=500");
        rows.Add($"M16_008_RESUME identity={Worker} valueBits=0 status=1");
        byte[] raw = Encoding.ASCII.GetBytes(string.Join("\r\n", rows) +"\r\n"), before = raw.ToArray();
        var capture = NativeAotSocketGuest.ReadService(raw);
        HostCheck.Equal(4098, capture.Waits.Count); HostCheck.Equal(4098UL, capture.Waits[^1].Ordinal);
        HostCheck.Equal(0, capture.PendingWaits); HostCheck.Equal(2, capture.Readiness.Count); HostCheck.True(raw.SequenceEqual(before));
        foreach (var change in new[] { ("clock=500", "clock=499"), ("sequence=7 completion=2", "sequence=8 completion=2"),
            ($"M16_008_RESUME identity={Worker}", $"M16_008_RESUME identity={Worker + (1UL <<32)}"),
            ("valueBits=0 status=1", "valueBits=1 status=1") })
        {
            byte[] changed = Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(raw).Replace(change.Item1, change.Item2, StringComparison.Ordinal));
            Reject(() => NativeAotSocketGuest.ReadService(changed));
        }
        string noWinner = string.Join("\r\n", rows.Where(row => !row.StartsWith("M16_012_WAKE ", StringComparison.Ordinal))) +"\r\n";
        Reject(() => NativeAotSocketGuest.ReadService(Encoding.ASCII.GetBytes(noWinner)));
    }

    private static string[] RandomRows()
    {
        List<string> rows = [
            "M11_012_SEED source=firmware-rng acquisitions=2 entropyBytes=32 nonceBytes=16 domain=getrandom state=ready firmwareCallbacks=none",
            "M07_033_SEED source=firmware-rng acquisitions=1 secretBytes=32 domain=tcp-isn state=ready firmwareCallbacks=none"];
        ulong ordinal = 0, requests = 0, bytes = 0;
        void Add(ulong address, ulong requested, ulong flags, long value)
        {
            ordinal++; if (value >0) { requests++; bytes +=(ulong)value; }
            ulong bits = unchecked((ulong)value);
            rows.Add($"M11_012_RANDOM requested={requested} flags={flags} valueBits={bits} requests={requests} generatedBytes={bytes}");
            rows.Add($"M16_008_CALL identity={Worker} nr=318 a0={address} a1={requested} a2={flags} a3=0 a4=0 a5=0" +
                $" rip=4198402 rsp=1879048184 flags=514 fs=0 gs=0 clock={ordinal} valueBits={bits} action=1 status=1 ordinal={ordinal}" +
                Suffix.Replace("returnValueBits=1", "returnValueBits=" +bits, StringComparison.Ordinal));
        }
        Add(4096, 0, 1, 0); Add(4096, 1, 8, -22); Add(0, 32, 1, -14);
        Add(4096, 16384, 1, 8192); Add(4096, 16384, 1, 8192);
        for (int i = 0; i <31; i++) Add(4096, 32, 1, 32);
        rows.Add($"M11_012_END requests={requests} generatedBytes={bytes} generator=zero seed=zero firmwareCallbacks=none");
        return rows.ToArray();
    }

    private static void Reject(Action action)
    {
        bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
