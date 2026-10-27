using System.Globalization;
using System.Reflection;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;
using Q = OS.Tooling.QemuTranscripts;

// Independently authored copied rows, not app/ELF execution or real scheduler,
// mappings, fd, device, syscall-copy, native, guest or persistence evidence.
internal static class NativeAotLogUploadProtocolTests
{
    private const ulong Main = 4294967297, Peer = 4294967298;
    private const string Cleanup = " userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
    private delegate Q.NativeAotLogUploadProtocolReport Core(string[] rows, string phase, string? hash,
        NativeAotLogUploadOutput.Capture? output, NativeAotLogUploadProgram? program);
    private static readonly Core Inspect = typeof(Q).GetMethod("InspectNativeAotLogUploadProtocolCore", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Core>();

    [HostTest("BCL protocol copied source5 turns retain immutable original census and explicit authority limits")]
    private static void Census()
    {
        string[] rows = Simple(), original = rows.ToArray(); var r = Read(rows);
        HostCheck.True(r.Phase == "write" && r.PackageSha256 is null && r.OriginalRowCount == rows.Length && r.FirstRecordIndex == 1 &&
            r.LastRecordIndex == rows.Length - 2 && r.Transfers == 4 && r.Drains == 2 && r.Reaped == 1 && r.WritableTurns.Count == 2);
        var first = r.WritableTurns[0]; var resumed = r.WritableTurns[1];
        HostCheck.True(first.CompleteRecordIndex is null && first.ResumeRecordIndex is null && first.DrainRecordIndex.HasValue &&
            resumed.CompleteRecordIndex == At(rows, "M16_027_COMPLETE ") && resumed.ResumeRecordIndex == At(rows, "M16_008_RESUME ") &&
            !resumed.PrepareOnly && r.CheckedSource5RelativeFifoValues && !r.CheckedFullSchedulerValues && !r.CheckedPhaseOutputValues);
        HostCheck.True(r.ProtocolRows.Select(x => x.RecordIndex).Concat(r.ResidualRows.Select(x => x.RecordIndex)).Order()
            .SequenceEqual(Enumerable.Range(0, rows.Length)) && rows.SequenceEqual(original));
        HostCheck.True(!r.GrantsOriginalInstructionAuthority && !r.GrantsSchedulerAuthority && !r.GrantsProcessAuthority &&
            !r.SyscallInputQualified && !r.NativeOrGuestQualified && !r.RebootOrPhysicalPersistenceQualified && !r.WholeCaptureQualified);
        rows[At(rows, "M16_027_COMPLETE ")] = "changed";
        HostCheck.True(r.ProtocolRows.Any(x => x.Text.StartsWith("M16_027_COMPLETE ", StringComparison.Ordinal)));
        bool denied = false;
        try { ((IList<Q.NativeAotLogUploadProtocolTurn>)r.WritableTurns).Clear(); } catch (NotSupportedException) { denied = true; }
        HostCheck.True(denied);
    }

    [HostTest("BCL protocol exact copied noIRQ policy uses payload bytes16workers32fds and preserves phase boundary")]
    private static void Policy()
    {
        HostCheck.True(Read(Simple(), "verify").Phase == "verify");
        foreach (string from in new[] { "descriptors=32", "workers=16", "timer=idle-only", "packageBytes=1356944" })
        {
            string to = from switch { "descriptors=32" => "descriptors=16", "workers=16" => "workers=2",
                "timer=idle-only" => "timer=user-irq-and-idle", _ => "packageBytes=1357289" };
            Reject(Simple().Select(x => x.Replace(from, to, StringComparison.Ordinal)).ToArray());
        }
        Reject(Simple(), phase: "Write"); Reject(Simple(), phase: "database");
        Reject(Simple().Prepend("TEST_START m16-009").ToArray(), "leaf");
        Reject(Simple().Where(x => x != "TEST_PASS m16-009").ToArray(), "leaf");
        foreach (string field in new[] { "flags", "returnFlags" }) Reject(Change(Simple(), "M16_008_CALL ", field, 0x202), "flags");
        Reject(Insert(Simple(), "M16_027_WAKE ", "M16_009_IRQ identity=4294967297"), "user_irq_or_tick_unsupported");
        Reject(Insert(Simple(), "M16_027_WAKE ", "M16_008_TICK epoch=2 nanos=101 ready=0"), "user_irq_or_tick_unsupported");
    }

    [HostTest("BCL protocol completed wait requires exact matching RESUME before another call drain or supervisor")]
    private static void Resume()
    {
        string[] rows = Simple();
        Reject(Change(rows, "M16_008_RESUME ", "identity", Peer), "resume");
        Reject(Change(rows, "M16_008_RESUME ", "valueBits", 20), "resume");
        Reject(Change(rows, "M16_008_RESUME ", "status", 2), "resume");
        Reject(rows.Where(x => !x.StartsWith("M16_008_RESUME ", StringComparison.Ordinal)).ToArray(), "call_order");
        Reject(Insert(rows, "M16_008_CALL identity=4294967297 nr=231", ResumeRow()), "resume_unmatched");
        Reject(Insert(rows, "M16_008_CALL identity=4294967297 nr=231", Drain(2, 102)), "drain");
        // Copied errors and shorts stay facts; this scanner cannot claim that
        // the application accepted them or that a user copy took place.
        foreach (ulong value in new[] { 0UL, 7UL, unchecked((ulong)-5L), unchecked((ulong)-4L) })
        {
            rows = Change(Simple(), "M16_027_COMPLETE ", "valueBits", value);
            rows = Change(rows, "M16_008_RESUME ", "valueBits", value);
            HostCheck.True(Read(rows).WritableTurns.Count == 2 && !Read(rows).SyscallInputQualified);
        }
    }

    [HostTest("BCL protocol source5 repark preserves request and jumping sequence with no CPL3 drain or replay")]
    private static void Repark()
    {
        string[] rows = WithRepark(); var r = Read(rows);
        HostCheck.True(r.WritableTurns.Count == 3 && r.Transfers == 6 && r.Drains == 2 &&
            r.WritableTurns[1].PrepareOnly && r.WritableTurns[1].DrainRecordIndex is null && r.WritableTurns[1].ResumeRecordIndex is null &&
            r.Waits.Operations.Single().Attempts[0].Repark!["sequence"] == 7);
        Reject(Insert(rows, "M16_008_SUPERVISOR identity=4294967297 action=3 transfers=4", Drain(2, 102)), "drain");
        Reject(Insert(rows, "M16_008_SUPERVISOR identity=4294967297 action=3 transfers=4", ResumeRow()), "resume");
        var replay = Insert(rows, "M16_008_SUPERVISOR identity=4294967297 action=3 transfers=4", Call(ordinal: 2, clock: 102));
        replay = Change(replay, "M16_008_CALL identity=4294967297 nr=231", "ordinal", 3);
        Reject(replay, "call_while_waiting", wait: true);
        Reject(Change(rows, "M16_027_REPARK ", "request", 2), "repark_identity", wait: true);
        Reject(Change(rows, "M16_027_REPARK ", "sequence", 1), "repark_identity", wait: true);
        Reject(Change(rows, "M16_008_SUPERVISOR identity=4294967297 action=3 transfers=4", "action", 1), "supervisor");
    }

    [HostTest("BCL protocol enforces only observed source5 relative FIFO while non5 ready remains explicitly unjoined")]
    private static void RelativeFifo()
    {
        string[] rows = TwoWaits(); var r = Read(rows);
        HostCheck.True(r.Waits.Operations.Count == 2 && r.WritableTurns.Count == 4 && r.Reaped == 2 &&
            !r.CheckedFullSchedulerValues && r.ResidualRows.Any(x => x.Text.StartsWith("M16_006_POLICY", StringComparison.Ordinal)));
        int first = At(rows, "M16_027_WAKE identity=4294967297"), second = At(rows, "M16_027_WAKE identity=4294967298");
        // Change the order of ready wakes without changing any individual
        // wait envelope, proving this check is independent of that parser.
        var reordered = rows.ToArray(); (reordered[first], reordered[second]) = (reordered[second], reordered[first]);
        Reject(reordered, "source5_fifo");
    }

    [HostTest("BCL protocol typed zeroTCP polls check copied current identity idle and source5 ready lower bound")]
    private static void Polls()
    {
        string[] rows = Simple();
        Reject(Change(rows, "M16_012_POLL identity=0 clock=101 ready=1", "ready", 0), "poll");
        Reject(Change(rows, "M16_012_POLL identity=0 clock=101 ready=0", "identity", Main), "poll");
        Reject(Change(rows, "M16_012_POLL identity=0 clock=101 ready=0", "clock", 99), "clock");
        Reject(Change(rows, "M16_012_POLL identity=0 clock=101 ready=0", "idle", 2), "poll");
        rows = Insert(rows, "M16_008_CALL identity=4294967297 nr=231", "M16_012_POLL identity=4294967297 clock=102 ready=0 idle=1");
        Reject(rows, "poll");
        // Unknown non-source5 ready threads are not invented/validated here.
        rows = Change(Simple(), "M16_012_POLL identity=0 clock=101 ready=1", "ready", 8);
        HostCheck.True(!Read(rows).CheckedFullSchedulerValues);
    }

    [HostTest("BCL protocol binds actual preCALL selected polls without inventing a supervisor zero identity")]
    private static void SelectedPolls()
    {
        // Actual native syscall entry polls before its corresponding CALL.
        string[] rows = Insert(Simple(), "M16_008_CALL ", "M16_012_POLL identity=4294967297 clock=100 ready=0 idle=0");
        var r = Read(rows);
        HostCheck.True(r.WritableTurns[0].FirstRecordIndex == At(rows, "M16_012_POLL identity=4294967297") && !r.CheckedFullSchedulerValues);
        Reject(Change(rows, "M16_012_POLL identity=4294967297", "identity", Peer), "turn_selection");
        Reject(Change(rows, "M16_012_POLL identity=4294967297", "idle", 1), "poll");
        Reject(Insert(rows, "M16_008_CALL ", "M16_012_POLL identity=0 clock=100 ready=0 idle=0"), "poll");
        // A genuinely ready source5 selection can also be observed before
        // Prepare's COMPLETE; the queue still supplies its exact sequence.
        rows = Insert(Simple(), "M16_027_COMPLETE ", "M16_012_POLL identity=4294967297 clock=101 ready=0 idle=0");
        HostCheck.True(Read(rows).WritableTurns[1].FirstRecordIndex == At(rows, "M16_012_POLL identity=4294967297"));
        Reject(Insert(Simple(), "M16_008_BUDGET_END ", "M16_012_POLL identity=4294967297 clock=104 ready=0 idle=0"), "poll");
    }

    [HostTest("BCL protocol indexed media and readonly wake residuals never change table clock or gain joins")]
    private static void Residuals()
    {
        string[] rows = Insert(Simple(), "M16_027_WAKE ", "M16_027_IO synthetic=unjoined clock=0");
        rows = Insert(rows, "M16_027_WAKE ", "M16_027_STATE synthetic=unjoined clock=18446744073709551615");
        rows = Insert(rows, "M16_027_WAKE ", Wake(Peer, source: 4, prefix: 20000));
        var r = Read(rows);
        HostCheck.True(r.Waits.UnjoinedSource4Wakes.Count == 1 && r.ResidualRows.Count(x => x.Text.StartsWith("M16_027", StringComparison.Ordinal)) == 3);
        foreach (string tag in new[] { "M16_027_TIMING", "M16_027_UNKNOWN", "M16_027_COMPLETE_EXTRA" })
            Reject(Insert(Simple(), "M16_027_WAKE ", tag + " copied=unjoined"), "unknown_writable_record", wait: true);
    }

    [HostTest("BCL protocol exit0 keeps epoch versus userIRQ distinction and requires real copied reap closure")]
    private static void ExitClosure()
    {
        string[] rows = Simple();
        Reject(Change(rows, "M16_008_CALL identity=4294967297 nr=231", "a0", 42), "exit0");
        Reject(Change(rows, "M16_008_OWNER_END ", "exit", 42), "owner_end");
        Reject(Change(rows, "M16_008_OWNER_END ", "irqs", 0), "owner_end");
        Reject(Change(rows, "M16_008_OWNER_END ", "reaped", 2), "owner_end");
        Reject(Change(rows, "M16_008_REAP ", "next", Main), "reap");
        Reject(rows.Where(x => !x.StartsWith("M16_008_REAP ", StringComparison.Ordinal)).ToArray(), "budget_end");
        Reject(rows.Where(x => !x.StartsWith("M16_008_BUDGET_END ", StringComparison.Ordinal)).ToArray(), "owner_end");
        Reject(Change(rows, "M16_008_BUDGET_END ", "nanos", 300000000101), "budget_end");
        Reject(Change(rows, "M16_008_SUPERVISOR identity=4294967297 action=2", "transfers", 6), "supervisor");
        HostCheck.True(Read(rows).Drains == 2); // No IRQ rows, OWNER_END irqs=2 epochs.
    }

    [HostTest("BCL protocol bounded malformed inputs and public unprepared gate reject without execution")]
    private static void Admission()
    {
        Reject(null!); Reject([]); Reject(["not-a-leaf"]);
        Reject(Enumerable.Repeat("OUTER", Q.MaximumNativeAotLogUploadWaitRows + 1).ToArray());
        Reject(Simple().Append(new string('x', 2049)).ToArray(), "input", wait: true);
        Reject(Simple().Append("non-ascii=é").ToArray(), "input", wait: true);
        Reject(Change(Simple(), "M16_008_CALL ", "identity", 4294967313));
        Reject(Simple().Select(x => x.StartsWith("M16_012_POLL ", StringComparison.Ordinal) ? x + " injected=0" : x).ToArray());
        bool rejected = false;
        try { _ = Q.InspectNativeAotLogUploadProtocolValues([1], [1], null!); }
        catch (ToolFailure x) { rejected = x.Message == "log_upload_protocol_program"; }
        HostCheck.True(rejected);
    }

    private static Q.NativeAotLogUploadProtocolReport Read(string[] rows, string phase = "write") => Inspect(rows, phase, null, null, null);
    private static string[] Header() => ["OUTER_BEFORE retained=1", "TEST_START m16-009", "M16_006_POLICY synthetic=unjoined",
        "M16_007_POLICY descriptors=32 pipes=2 capacity=4096 scratch=8192 packageBytes=1356944 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler",
        "M16_011_BOUND synthetic=unjoined", "M16_005_ARMED synthetic=unjoined", "TEST_START m16.027",
        $"M16_008_BOUND root=4096 pool=8192 workers=16 code={LinuxThreadProgram.Bytes} metadata={LinuxThreadProgram.MetadataPages * 4096UL} userImage=728 timer=idle-only clock=hpet-nanoseconds reuse=off-stack",
        "M16_008_BUDGET started=100 epochs=8192 steps=16384 nanos=300000000000"];
    private static string[] Tail(ulong transfers = 4, ulong epochs = 2, ulong reaped = 1) => ["M16_008_BUDGET_END nanos=110",
        $"M16_008_OWNER_END reaped={reaped} transfers={transfers} irqs={epochs} exit=0" + Cleanup,
        "M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", "CLROS_M16_009_READY", "TEST_PASS m16-009", "OUTER_AFTER retained=1"];
    private static string[] Simple() => Header().Concat(new[] { Call(), Begin(), Drain(1, 100), Supervisor(3, 2, 0),
        "M16_012_POLL identity=0 clock=101 ready=0 idle=0", Wake(), "M16_012_POLL identity=0 clock=101 ready=1 idle=1", Complete(), ResumeRow(),
        ExitCall(2), Drain(2, 103), ExitRow(), Supervisor(2, 4, 0), Reap() }).Concat(Tail()).ToArray();
    private static string[] WithRepark() => Header().Concat(new[] { Call(), Begin(), Drain(1, 100), Supervisor(3, 2, 0), Wake(),
        Complete(action: 3, value: 0), "M16_027_REPARK identity=4294967297 previous=1 sequence=7 request=1 clock=102", Supervisor(3, 4, 0),
        Wake(sequence: 7, clock: 102), Complete(sequence: 7), ResumeRow(), ExitCall(2), Drain(2, 103), ExitRow(), Supervisor(2, 6, 0), Reap() }).Concat(Tail(6)).ToArray();
    private static string[] TwoWaits() => Header().Concat(new[] { Call(), Begin(), Drain(1, 100), Supervisor(3, 2, 1, active: 2),
        Call(Peer), Begin(Peer, owner: 16000), Drain(2, 100, Peer), Supervisor(3, 4, 0, Peer, 2), Wake(), Wake(Peer, owner: 16000),
        Complete(), ResumeRow(), Call(Main, 2, nr: 24, source: 0, action: 6), Drain(3, 103), Supervisor(6, 6, 1, active: 2),
        Complete(Peer, owner: 16000, clock: 104), ResumeRow(Peer), Call(Peer, 2, nr: 60, source: 0, action: 4, status: 2, clock: 105), Drain(4, 105, Peer),
        ExitRow(Peer), Supervisor(4, 8, 1, Peer, 2), Reap(Peer), Call(Main, 3, 231, 0, 2, 2, 107), Drain(5, 107), ExitRow(), Supervisor(2, 10, 0), Reap() }).Concat(Tail(10, 5, 2)).ToArray();
    private static string Call(ulong identity = Main, ulong ordinal = 1, ulong nr = 17, uint source = 5, ulong action = 3, ulong status = 9, ulong? clock = null) =>
        FormattableString.Invariant($"M16_008_CALL identity={identity} nr={nr} a0=0 a1=6000000 a2=19 a3=11 a4=0 a5=0 rip=4231031 rsp=8000000 flags=2 fs=7000000 gs=0 clock={clock ?? (source == 5 ? 100UL : 103UL)} valueBits=0 action={action} status={status} ordinal={ordinal} waitSource={source} waitDeadline=0 waitSequence={(source == 0 ? 0 : 1)} signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4231031 returnRsp=8000000 returnFlags=2 returnFs=7000000 returnGs=0 returnValueBits=0");
    private static string ExitCall(ulong ordinal) => Call(Main, ordinal, 231, 0, 2, 2);
    private static string Begin(ulong identity = Main, ulong owner = 8000) =>
        FormattableString.Invariant($"M16_027_BEGIN identity={identity} ordinal=1 sequence=1 requestOwner={owner} requestEpoch=12 request=1 nr=17 rip=4231031 outputPrefix=19 fdOwner=9000 fd=5 fdGeneration=7 description=2 bindingGeneration=9 reservedFd=0 reservedDescription=0 reservationGeneration=0 providerOwner=10000 providerEpoch=12 providerRequest=77 operation=2 phase=1 clock=100");
    private static string Wake(ulong identity = Main, ulong sequence = 1, ulong owner = 8000, uint source = 5, ulong prefix = 19, ulong clock = 101) =>
        FormattableString.Invariant($"M16_027_WAKE identity={identity} sequence={sequence} source={source} requestOwner={owner} requestEpoch=12 request=1 outputPrefix={prefix} completion=1 state=1 clock={clock}");
    private static string Complete(ulong identity = Main, ulong sequence = 1, ulong owner = 8000, ulong action = 1, ulong value = 19, ulong clock = 102) =>
        FormattableString.Invariant($"M16_027_COMPLETE identity={identity} sequence={sequence} requestOwner={owner} requestEpoch=12 request=1 nr=17 rip=4231031 outputBytes=19 completion=1 action={action} valueBits={value} clock={clock}");
    private static string ResumeRow(ulong identity = Main) => $"M16_008_RESUME identity={identity} valueBits=19 status=1";
    private static string Drain(ulong epoch, ulong clock, ulong identity = Main) => $"M16_009_DRAIN identity={identity} epoch={epoch} nanos={clock}";
    private static string Supervisor(ulong action, ulong transfers, ulong ready, ulong identity = Main, ulong active = 1) =>
        $"M16_008_SUPERVISOR identity={identity} action={action} transfers={transfers} ready={ready} active={active}";
    private static string ExitRow(ulong identity = Main) => $"M16_008_EXIT identity={identity} clearTidResult=0";
    private static string Reap(ulong identity = Main) => $"M16_008_REAP old={identity} next={identity + (1UL << 32)} context=zero stack=zero slot=zero";
    private static int At(string[] rows, string prefix) => Array.FindIndex(rows, x => x.StartsWith(prefix, StringComparison.Ordinal));
    private static string[] Insert(string[] rows, string before, string added)
    { int i = At(rows, before); HostCheck.True(i >= 0); return rows.Take(i).Concat([added]).Concat(rows.Skip(i)).ToArray(); }
    private static string[] Change(string[] rows, string prefix, string field, ulong value)
    { var copy = rows.ToArray(); int i = At(copy, prefix); HostCheck.True(i >= 0); copy[i] = string.Join(' ', copy[i].Split(' ').Select(x => x.StartsWith(field + "=", StringComparison.Ordinal) ? field + "=" + value.ToString(CultureInfo.InvariantCulture) : x)); return copy; }
    private static void Reject(string[] rows, string reason = "", bool wait = false, string phase = "write")
    {
        try { _ = Read(rows, phase); }
        catch (ToolFailure x) { HostCheck.True(reason.Length == 0 || x.Message == (wait ? "log_upload_wait_" : "log_upload_protocol_") + reason, "unexpected rejection: " + x.Message); return; }
        HostCheck.True(false, "malformed copied protocol accepted: " + reason);
    }
}
