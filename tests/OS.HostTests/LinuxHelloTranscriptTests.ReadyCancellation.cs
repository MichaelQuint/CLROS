using System.Globalization;
using System.Text.RegularExpressions;
using OS.Tooling;

internal static partial class LinuxHelloTranscriptTests
{
    [HostTest("nativeaot.hello exit group cancels an unentered ready signal child before the blocked finalizer")]
    private static void ReadySignalChildCancellation()
    {
        foreach (bool wakeBeforeWait in new[] { false, true })
        {
            string[] rows = ReadyCancellation(wakeBeforeWait);
            var report = Inspect(rows);
            HostCheck.Equal(1, report.Irqs);
            HostCheck.Equal(2, report.Drains);
            HostCheck.Equal(10, report.Transfers);
            HostCheck.True(!rows.Any(r => r.StartsWith("M16_008_CALL identity=4294967299 ", StringComparison.Ordinal)));
        }
    }

    [HostTest("nativeaot.hello ready cancellation rejects reordered children invented execution lost exit and stale reaps")]
    private static void ReadyCancellationRejections()
    {
        string[] rows = ReadyCancellation();
        int at = Array.FindIndex(rows, r => r.StartsWith("M16_008_EXIT identity=4294967299 ", StringComparison.Ordinal));
        HostCheck.True(at > 0);
        Reject(rows.Take(at).Concat(rows.Skip(at + 3).Take(3)).Concat(rows.Skip(at).Take(3)).Concat(rows.Skip(at + 6)).ToArray());
        foreach (string invented in new[]
        {
            "M16_008_RESUME identity=4294967299 valueBits=0 status=1",
            "M16_009_DRAIN identity=4294967299 epoch=4 nanos=3",
            "M16_009_IRQ identity=4294967299 epoch=4 nanos=3 rip=4194818 rsp=4296540040 flags=514 fs=4296540424 gs=0 valueBits=0"
        })
            Reject(rows.Take(at).Append(invented).Concat(rows.Skip(at)).ToArray());
        Reject(rows.Where((_, index) => index != at).ToArray());
        Reject(Change(rows, "M16_008_REAP old=4294967299 ", "next", 8589934596));
        Reject(Change(rows, "M16_008_SUPERVISOR identity=4294967299 ", "ready", 0));

        // A signal child which really entered and set its mask cannot be
        // relabelled unentered to hide a missing pipe wait.
        var started = Build(uncontended: true, earlyWrites: 3).ToList();
        int read = started.FindIndex(r => r.StartsWith("M16_008_CALL identity=4294967299 nr=0 ", StringComparison.Ordinal));
        HostCheck.True(read > 0);
        string call = started[read];
        string Value(string key) => Regex.Match(call, @"\b" + key + @"=([0-9]+)").Groups[1].Value;
        started.RemoveAt(read);
        started[read] = $"M16_009_IRQ identity=4294967299 epoch=4 nanos=6 rip={Value("rip")} rsp={Value("rsp")} flags={Value("flags")} fs={Value("fs")} gs=0 valueBits={unchecked((ulong)-38L)}";
        started[read + 1] = "M16_008_SUPERVISOR identity=4294967299 action=1 transfers=8 ready=2 active=3";
        Reject(started.ToArray(), reason: "hello_runtime_children_and_startup");
    }

    private static string[] ReadyCancellation(bool wakeBeforeWait = false)
    {
        // Reuse the independent ABI/memory/startup vector, then explicitly
        // describe the distinct five-dispatch cancellation history. No signal
        // child user entry, mask, syscall, timer, IRQ or drain is invented.
        string[] original = Build(wakeBeforeWait: wakeBeforeWait, uncontended: !wakeBeforeWait, earlyWrites: 3)
            .Select(r => Regex.Replace(r, @" ordinal=[0-9]+\b", "")).ToArray();
        int at = Array.FindIndex(original, r => r.StartsWith("M16_009_IRQ identity=4294967297 epoch=3 ", StringComparison.Ordinal));
        HostCheck.True(at > 0);
        List<string> rows = original.Take(at).ToList();
        string lastCall = rows.Last(r => r.StartsWith("M16_008_CALL ", StringComparison.Ordinal));
        ulong clock = ulong.Parse(Regex.Match(lastCall, @"\bclock=([0-9]+)").Groups[1].Value, CultureInfo.InvariantCulture);
        string exit = original.Single(r => r.StartsWith("M16_008_CALL identity=4294967297 nr=231 ", StringComparison.Ordinal));
        rows.Add(Regex.Replace(exit, @"\bclock=[0-9]+", "clock=" + clock.ToString(CultureInfo.InvariantCulture)));
        rows.AddRange([
            $"M16_009_DRAIN identity=4294967297 epoch=3 nanos={clock}",
            "M16_008_EXIT identity=4294967297 clearTidResult=0",
            "M16_008_SUPERVISOR identity=4294967297 action=2 transfers=6 ready=2 active=3",
            "M16_008_REAP old=4294967297 next=8589934593 context=zero stack=zero slot=zero",
            "M16_008_EXIT identity=4294967299 clearTidResult=0",
            "M16_008_SUPERVISOR identity=4294967299 action=4 transfers=8 ready=1 active=2",
            "M16_008_REAP old=4294967299 next=8589934595 context=zero stack=zero slot=zero",
            "M16_008_EXIT identity=4294967298 clearTidResult=0",
            "M16_008_SUPERVISOR identity=4294967298 action=4 transfers=10 ready=0 active=1",
            "M16_008_REAP old=4294967298 next=8589934594 context=zero stack=zero slot=zero",
            $"M16_008_BUDGET_END nanos={clock}",
            "M16_008_OWNER_END reaped=3 transfers=10 irqs=3 exit=0 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained",
            "M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true",
            "CLROS_M16_009_READY", "TEST_PASS m16-009"
        ]);
        return LinuxCallOrdinalTests.Annotate(rows);
    }
}
