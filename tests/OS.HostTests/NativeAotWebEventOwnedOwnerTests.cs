using OS.Tooling;
using OS.Tools.QemuRunner;
using System.Text.Json;
using Transport = NativeAotWebSignalRaceTransportTests;

// Copied serial predicates and owned local failure controls only. No successful
// private Completion, QEMU, original native or guest evidence is fabricated.
internal static class NativeAotWebEventOwnedOwnerTests
{
    private const string GetTid = "M16_008_CALL identity=4294967303 nr=186 a0=0 a1=0 a2=0 a3=0 a4=0 a5=0 rip=4194818 rsp=4296794048 flags=514 fs=4296796160 gs=0 clock=20 valueBits=7 action=1 status=1 ordinal=1 waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=4296794048 returnFlags=514 returnFs=4296796160 returnGs=0 returnValueBits=7\r\n";
    private const string Wait = "M16_008_CALL identity=4294967303 nr=232 a0=10 a1=5244928 a2=1 a3=18446744073709551615 a4=0 a5=0 rip=4194818 rsp=4296794048 flags=514 fs=4296796160 gs=0 clock=20 valueBits=0 action=3 status=9 ordinal=2 waitSource=3 waitDeadline=0 waitSequence=1 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4194818 returnRsp=4296794048 returnFlags=514 returnFs=4296796160 returnGs=0 returnValueBits=0\r\n";
    private const string Begin = "TEST_START m10.004\r\n";

    [HostTest("web event-owned provisional serial predicate requires exact original untimed waiter fields")]
    private static void InitialWait()
    {
        bool Pending(string text, bool eventOwned = true, int waiter = 7) => NativeAotWebRacePeer.InspectInitialWaitForTests(text, waiter, eventOwned);
        HostCheck.True(Pending(Begin + GetTid + Wait) && !Pending(Begin + GetTid + Wait, false));
        string timed = Wait.Replace("a3=18446744073709551615", "a3=1", StringComparison.Ordinal).Replace("waitDeadline=0", "waitDeadline=2500000000", StringComparison.Ordinal);
        HostCheck.True(Pending(Begin + GetTid + timed, false) && !Pending(Begin + GetTid + timed));
        foreach (var change in new[]{("nr=232","nr=281"),("a2=1 ","a2=2 "),("a3=18446744073709551615","a3=4294967295"),
            ("status=9","status=1"),("action=3","action=1"),("valueBits=0 ","valueBits=1 "),("waitSource=3","waitSource=0"),
            ("waitDeadline=0","waitDeadline=1"),("waitSequence=1","waitSequence=0"),("ordinal=2","ordinal=0")})
            HostCheck.True(!Pending(Begin + GetTid + Wait.Replace(change.Item1, change.Item2, StringComparison.Ordinal)));
        HostCheck.True(!Pending(Begin + Wait) && !Pending(Begin + GetTid + Wait, waiter: 8) && !Pending(Begin + GetTid + Wait[..^2]));
        HostCheck.True(!Pending(Begin + GetTid + Wait + Wait.Replace("ordinal=2", "ordinal=3", StringComparison.Ordinal)) &&
            !Pending(Begin + GetTid + Wait + "M16_008_RESUME identity=4294967303 valueBits=1 status=1\r\n"));
    }

    [HostTest("web recovery serial hint waits for the second selected pending epoll call in either explicit profile")]
    private static void RecoveryWait()
    {
        foreach (bool eventOwned in new[] { false, true })
        {
            var (initial, recovery) = RecoveryWaitRows(eventOwned);
            string serial = RecoverySerial(initial, recovery);
            HostCheck.True(NativeAotWebRacePeer.InspectRecoveryWaitForTests(serial, 7, eventOwned));
            HostCheck.True(!NativeAotWebRacePeer.InspectRecoveryWaitForTests(Begin + GetTid + initial, 7, eventOwned) &&
                !NativeAotWebRacePeer.InspectRecoveryWaitForTests(Begin + GetTid + recovery, 7, eventOwned) &&
                !NativeAotWebRacePeer.InspectRecoveryWaitForTests(serial, 7, !eventOwned));
            string other = GetTid.Replace("identity=4294967303", "identity=4294967304", StringComparison.Ordinal)
                .Replace("valueBits=7 ", "valueBits=8 ", StringComparison.Ordinal).Replace("returnValueBits=7", "returnValueBits=8", StringComparison.Ordinal);
            string before = other.Replace("clock=20 ", "clock=30 ", StringComparison.Ordinal),
                after = other.Replace("clock=20 ", "clock=60 ", StringComparison.Ordinal).Replace("ordinal=1 ", "ordinal=2 ", StringComparison.Ordinal);
            HostCheck.True(NativeAotWebRacePeer.InspectRecoveryWaitForTests(RecoverySerial(initial, before + recovery) + after, 7, eventOwned));
        }
        var legacy = RecoveryWaitRows(false);
        var current = RecoveryWaitRows(true);
        HostCheck.True(NativeAotWebRacePeer.InspectRecoveryWaitForTests(RecoverySerial(legacy.Initial, legacy.Recovery), 7) &&
            !NativeAotWebRacePeer.InspectRecoveryWaitForTests(RecoverySerial(current.Initial, current.Recovery), 7));
    }

    [HostTest("web recovery serial hint refuses completed repeated foreign malformed or profile-mismatched waits")]
    private static void RecoveryWaitRefusals()
    {
        foreach (bool eventOwned in new[] { false, true })
        {
            var (initial, recovery) = RecoveryWaitRows(eventOwned);
            string serial = RecoverySerial(initial, recovery);
            bool Pending(string text, int tid = 7) => NativeAotWebRacePeer.InspectRecoveryWaitForTests(text, tid, eventOwned);
            HostCheck.True(!Pending(serial, 8) && !Pending(serial, 0) && !Pending(serial, -1) && !Pending(Begin + initial + recovery));
            HostCheck.True(!Pending(RecoverySerial(initial, recovery[..^2])) &&
                !Pending(RecoverySerial(initial, recovery.Replace("a0=10 ", "a0=11 ", StringComparison.Ordinal))) &&
                !Pending(RecoverySerial(initial, recovery.Replace("identity=4294967303", "identity=4294967304", StringComparison.Ordinal))));
            foreach (string tail in new[]{
                "M16_008_RESUME identity=4294967303 valueBits=1 status=1\r\n",
                "M16_008_EXIT identity=4294967303 code=0\r\n",
                "M16_008_REAP old=4294967303 next=8589934599\r\n",
                GetTid.Replace("ordinal=1 ","ordinal=9 ",StringComparison.Ordinal),
                recovery.Replace("valueBits=0 ","valueBits=malformed ",StringComparison.Ordinal),
                recovery,
                recovery.Replace("ordinal=8 ","ordinal=9 ",StringComparison.Ordinal).Replace("waitSequence=2 ","waitSequence=3 ",StringComparison.Ordinal)})
                HostCheck.True(!Pending(serial + tail));
            foreach (var change in new[]{("nr=232 ","nr=281 "),("valueBits=0 ","valueBits=1 "),
                ("action=3 ","action=1 "),("status=9 ","status=1 "),("waitSource=3 ","waitSource=0 ")})
            {
                HostCheck.True(!Pending(RecoverySerial(initial.Replace(change.Item1, change.Item2, StringComparison.Ordinal), recovery)));
                HostCheck.True(!Pending(RecoverySerial(initial, recovery.Replace(change.Item1, change.Item2, StringComparison.Ordinal))));
            }
            HostCheck.True(!Pending(RecoverySerial(initial.Replace("ordinal=2 ", "ordinal=0 ", StringComparison.Ordinal), recovery)) &&
                !Pending(RecoverySerial(initial, recovery.Replace("ordinal=8 ", "ordinal=0 ", StringComparison.Ordinal))) &&
                !Pending(RecoverySerial(initial.Replace("waitSequence=1 ", "waitSequence=0 ", StringComparison.Ordinal), recovery)) &&
                !Pending(RecoverySerial(initial, recovery.Replace("waitSequence=2 ", "waitSequence=0 ", StringComparison.Ordinal))) &&
                !Pending(RecoverySerial(initial, recovery.Replace(" waitSequence=2 ", " ", StringComparison.Ordinal))));
            string initialDeadline = eventOwned ? "waitDeadline=0 " : "waitDeadline=2500000000 ",
                recoveryDeadline = eventOwned ? "waitDeadline=0 " : "waitDeadline=5000000000 ", badDeadline = eventOwned ? "waitDeadline=1 " : "waitDeadline=0 ";
            HostCheck.True(!Pending(RecoverySerial(initial.Replace(initialDeadline, badDeadline, StringComparison.Ordinal), recovery)) &&
                !Pending(RecoverySerial(initial, recovery.Replace(recoveryDeadline, badDeadline, StringComparison.Ordinal))));
            if (eventOwned)
                foreach (var change in new[]{("a2=1 ","a2=2 "),("a3=18446744073709551615 ","a3=0 "),
                    ("a3=18446744073709551615 ","a3=2500 "),("a3=18446744073709551615 ","a3=4294967295 "),
                    ("a3=18446744073709551615 ","a3=18446744073709551614 ")})
                {
                    HostCheck.True(!Pending(RecoverySerial(initial.Replace(change.Item1, change.Item2, StringComparison.Ordinal), recovery)));
                    HostCheck.True(!Pending(RecoverySerial(initial, recovery.Replace(change.Item1, change.Item2, StringComparison.Ordinal))));
                }
        }
    }

    private static (string Initial, string Recovery) RecoveryWaitRows(bool eventOwned)
    {
        string initial = eventOwned ? Wait : Wait.Replace("a3=18446744073709551615 ", "a3=2500 ", StringComparison.Ordinal)
            .Replace("waitDeadline=0 ", "waitDeadline=2500000000 ", StringComparison.Ordinal);
        string recovery = initial.Replace("ordinal=2 ", "ordinal=8 ", StringComparison.Ordinal)
            .Replace("waitSequence=1 ", "waitSequence=2 ", StringComparison.Ordinal).Replace("clock=20 ", "clock=50 ", StringComparison.Ordinal);
        if (!eventOwned)
            recovery = recovery.Replace("waitDeadline=2500000000 ", "waitDeadline=5000000000 ", StringComparison.Ordinal);
        return (initial, recovery);
    }
    private static string RecoverySerial(string initial, string recovery) => Begin + GetTid + initial +
        "M16_008_RESUME identity=4294967303 valueBits=18446744073709551612 status=1\r\n" + recovery;

    [HostTest("web event-owned owner retains actual early child logs and refuses canceled entry without launching")]
    private static async Task EarlyRunner()
    {
        string root = HostToolProcess.FindRepositoryRoot(), path = Transport.Output();
        ProcessResult? child = null;
        Exception error = await Refused(() => NativeAotWebRacePeer.RunEventOwnedAsync(root, 20080, "health", path, path + ".summary", async (name, token) =>
        {
            WebRaceRendezvous.ValidateEventOwnedPipeName(name);
            return child = await ProcessRunner.RunAsync(new("dotnet", [HostTools.ManagedTool(root, "OS.QemuRunner"), "--help"], root)
            {
                Timeout = TimeSpan.FromSeconds(10)
            }, token);
        }));
        HostCheck.True(error is ToolFailure { Reason: "web_race_owner_runner_before_ready" } && child is { Succeeded: true, TerminationConfirmed: true });
        HostCheck.Equal(child!.StandardOutput, File.ReadAllText(path + ".runner.log"));
        Closed(path, onlyLogs: true);
        string canceledPath = Transport.Output();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        int starts = 0;
        error = await Refused(() => NativeAotWebRacePeer.RunEventOwnedAsync(root, 20080, "health", canceledPath, canceledPath + ".summary", (_, _) =>
        {
            starts++;
            throw new InvalidOperationException("must not start");
        }, canceled.Token));
        HostCheck.True(error is OperationCanceledException && starts == 0 && !File.Exists(canceledPath + ".runner.log"));
    }

    [HostTest("web event-owned owner joins truncated actual HTTP capture and preserves partial v3 companions")]
    private static Task FailedHttp() => FailedCapture(false);

    [HostTest("web event-owned owner joins external cancellation during an actual pending response")]
    private static Task CancelledHttp() => FailedCapture(true);

    [HostTest("web event-owned owner holds real v3 reports through DONE then refuses deliberately failed runner")]
    private static Task ReportsBeforeDone() => Transport.OwnedReportsBeforeDoneCore(true);

    private static async Task FailedCapture(bool cancel)
    {
        string root = HostToolProcess.FindRepositoryRoot(), path = Transport.Output();
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(limit.Token);
        await using var peer = new Transport.Peer(partial: !cancel, eventOwned: true, stall: cancel);
        bool joined = false, gateRefused = false;
        Task<NativeAotWebRacePeer.Completion>? owner = null;
        try
        {
            owner = NativeAotWebRacePeer.RunEventOwnedAsync(root, peer.Port, "OS_MINIMAL_API_OK", path, path + ".summary", async (name, token) =>
            {
                var gate = WebRaceRendezvous.WaitEventOwnedInRunnerAsync(name, peer.Port, Environment.ProcessId, TimeSpan.FromSeconds(10), () => TimeSpan.FromSeconds(165), cancellation: token);
                try
                {
                    _ = await gate;
                    throw new InvalidOperationException("failed capture must not send DONE");
                }
                catch (Exception error) when (error is IOException or InvalidDataException or OperationCanceledException) { gateRefused = true; }
                finally { try { _ = await gate; } catch (Exception error) when (error is IOException or InvalidDataException or OperationCanceledException) { } joined = true; }
                return new(1, "synthetic-event-runner-failure", "", false, false, token.IsCancellationRequested, TimeSpan.Zero);
            }, caller.Token);
            await peer.RequestObserved.WaitAsync(limit.Token);
            if (cancel)
                caller.Cancel();
            Exception error = await Refused(() => owner);
            HostCheck.True(cancel ? ContainsCancellation(error) : error is HttpRequestException or IOException or InvalidDataException or AggregateException);
            HostCheck.True(joined && gateRefused && owner.IsCompleted && !File.Exists(path + ".summary"));
            Closed(path);
            HostCheck.Equal("synthetic-event-runner-failure", File.ReadAllText(path + ".runner.log"));
            using var selected = JsonDocument.Parse(File.ReadAllText(path));
            using var attempts = JsonDocument.Parse(File.ReadAllText(path + ".attempts.json"));
            HostCheck.Equal("clros.web.signalrace.v3", selected.RootElement.GetProperty("schema").GetString());
            HostCheck.Equal("clros.web.signalrace.attempts.v3", attempts.RootElement.GetProperty("schema").GetString());
            HostCheck.True(selected.RootElement.GetProperty("samples").GetArrayLength() == 0 && !attempts.RootElement.GetProperty("semanticComplete").GetBoolean());
            var wire = NativeAotWebPayload.InspectSignalRaceWire(File.ReadAllBytes(path + ".wire.json"));
            HostCheck.True(wire.Purpose == "signalrace-partial" && wire.Connections.Count > 0 && wire.Connections.All(c => c.DisposedTicks > 0));
            NativeAotWebPayload.ValidateSignalRaceTransport(File.ReadAllText(path + ".attempts.json"), File.ReadAllText(path), File.ReadAllBytes(path + ".wire.json"), peer.Endpoint, "OS_MINIMAL_API_OK");
        }
        finally { caller.Cancel(); if (owner is not null) try { _ = await owner.WaitAsync(TimeSpan.FromSeconds(10)); } catch when (owner.IsCompleted) { } }
    }
    private static bool ContainsCancellation(Exception error) => error is OperationCanceledException || error is AggregateException aggregate && aggregate.InnerExceptions.Any(ContainsCancellation);
    private static void Closed(string path, bool onlyLogs = false)
    {
        foreach (string suffix in onlyLogs ? new[] { ".runner.log", ".runner.stderr.log" } : new[] { "", ".attempts.json", ".wire.json", ".runner.log", ".runner.stderr.log" })
        {
            using var closed = new FileStream(path + suffix, FileMode.Open, FileAccess.Read, FileShare.None);
        }
    }
    private static async Task<Exception> Refused(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception error) when (error is ToolFailure or IOException or HttpRequestException or InvalidDataException or ArgumentException or OperationCanceledException or AggregateException) { return error; }
        throw new InvalidOperationException("Expected owner refusal.");
    }
}
