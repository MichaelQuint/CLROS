using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Tooling;

internal static class NativeAotStaticFileReplayTests
{
    private const ulong Ready = 1000, Requested = Ready + LinuxStaticFileObservation.RequestDelayNanoseconds, Exited = Requested + 100;
    // Independent finite policy row; this synthetic ELF is not the pinned app.
    private const int StagedElfBytes = 8_641_616;
    private const string Entry = "CLROS_STATIC_FILES_APP_ENTRY v1\n", End = "CLROS_STATIC_FILES_APP_END v1\n";

    // These rows are independent synthetic calls and owned scheduler transitions.
    // They do not construct a package/source/native/packet or guest authority.
    private static NativeAotThreadingTranscriptTests.Vector Vector(bool deadline = false, bool masked = false,
        bool ready = false, bool capacity = false, bool ioStates = false, bool idlePrefix = false)
    {
        var v = new NativeAotThreadingTranscriptTests.Vector(false).Start();
        for (int i = 0; i < v.Rows.Count; i++)
            v.Rows[i] = v.Rows[i]
            .Replace("entropyBytes=32", "entropyBytes=4096", StringComparison.Ordinal)
            .Replace("descriptors=16", "descriptors=32", StringComparison.Ordinal)
            .Replace("packageBytes=1320176", "packageBytes=" + StagedElfBytes, StringComparison.Ordinal)
            .Replace("workers=4", "workers=16", StringComparison.Ordinal)
            .Replace("epochs=8192 steps=16384", "epochs=1000000 steps=4000000", StringComparison.Ordinal);
        v.Rows.Insert(3, "M16_011_BOUND connections=16 rx=8192 tx=32768 epoll=4 watches=64 nic=live");
        int budget = v.Rows.FindIndex(row => row.StartsWith("M16_008_BUDGET ", StringComparison.Ordinal));
        v.Rows.Insert(budget + 1, "TEST_START m10.017");
        v.Call(13, 0, [15, 0x500400, 0, 8]);
        Write(v, 2, Entry);
        if (capacity)
        {
            for (int fd = 5; fd < 32; fd++)
                File(v, 2, fd, (uint)fd, 1, 5, 261, 1, 0, 0, "/app/wwwroot/one.bin", [0x500200, 0x80100, 0]);
            Path(v, 2, -24, "/app/wwwroot/one.bin", 0, uint.MaxValue, 0, 0, 0, [0x500200, 0x80100, 0]);
            for (int fd = 5; fd < 32; fd++)
                File(v, 3, 0, (uint)fd, 1, 5, 261, 1, 0, 0, args: [(uint)fd]);
        }
        else
        {
            File(v, 2, 5, 5, 1, 5, 261, 1, 0, 0, "/app/wwwroot/one.bin", [0x500200, 0x80100, 0]);
            File(v, 5, 0, 5, 1, 5, 261, 1, 0, 0, args: [5, 0x500800]);
            File(v, 17, 1, 5, 1, 5, 261, 1, 0, 0, args: [5, 0x500800, 1, 0]);
            File(v, 0, 1, 5, 1, 5, 261, 1, 0, 1, args: [5, 0x500800, 1]);
            File(v, 0, 0, 5, 1, 5, 261, 1, 1, 1, args: [5, 0x500800, 1]);
            File(v, 8, 0, 5, 1, 5, 261, 1, 1, 0, args: [5, ulong.MaxValue, 1]);
            File(v, 72, 0, 5, 1, 5, 261, 1, 0, 0, args: [5, 2, 1]);
            File(v, 73, -38, 5, 1, 5, 261, 1, 0, 0, args: [5, 5]);
            File(v, 221, -38, 5, 1, 5, 261, 1, 0, 0, args: [5, 0, 0, 2]);
            File(v, 3, 0, 5, 1, 5, 261, 1, 0, 0, args: [5]);
        }
        Path(v, 4, 0, "/app/wwwroot/one.bin", 1, 5, 261, 1, 0x8124, [0x500200, 0x500800]);
        Path(v, 6, 0, "/app/wwwroot", 3, uint.MaxValue, 0x100000000UL + 256 + 11, 0, 0x416d, [0x500200, 0x500800]);
        Path(v, 6, 0, "/proc/self/exe", 4, uint.MaxValue, 68,
            (ulong)("/app/apps/" + NativeAotStaticFilePublisher.Application).Length, 0xa1ff, [0x500200, 0x500800]);
        Path(v, 89, 1, "/proc/self", 4, uint.MaxValue, 0, 1, 0, [0x500200, 0x500800, 8]);
        Path(v, 89, 8, "/proc/self/exe", 4, uint.MaxValue, 0, 8, 0, [0x500200, 0x500800, 8]);
        Path(v, 4, -2, "/app/wwwroot/missing.bin", 0, uint.MaxValue, 0, 0, 0, [0x500200, 0x500800]);
        v.Call(291, 5, [0x80000]);
        v.Call(41, 6, [2, 0x80001, 6]);
        v.Call(49, 0, [6, 0x500200, 16]);
        v.Rows.Add($"M10_017_LISTENING main={v.Main.Id} writer={v.Current.Id} fd=6 nanos={Ready} http=external cleanup=pending");
        v.Call(50, 0, [6, 512]);
        v.Call(233, 0, [5, 1, 6, 0x500300]);
        var first = v.Clone(2, 2);
        var second = v.Clone(3, 3);
        v.Irq(10, 3, 3);
        v.Select(first);
        v.Wait(0x500208);
        v.Drain(20);
        v.Supervisor(3, 2, 3);
        v.Select(second);
        v.Wait(0x50020c);
        v.Drain(30);
        v.Supervisor(3, 1, 3);
        v.Select(v.Main);
        for (int i = 0; i < 3; i++)
        {
            v.Call(288, 7, [6, 0x500700, 0x500780, 0x80000]);
            v.Call(45, 1, [7, 0x500800, 1, 0, 0, 0]);
            v.Call(44, 1, [7, 0x500000, 1, 0, 0, 0]);
            if (ioStates)
            {
                v.Call(45, i == 1 ? -104 : 0, [7, 0x500800, 1, 0, 0, 0]);
                if (i != 1)
                    v.Call(48, 0, [7, 2]);
            }
            v.Call(3, 0, [7]);
        }
        if (masked)
        {
            v.Main.Mask = 1UL << 14;
            v.Call(14, 0, [2, 0x500400, 0, 8]);
        }
        if (ready)
            v.Irq(40, 1, 3);
        else
        {
            v.Wait(0x500204, deadline ? 100_000_000_000UL : 0);
            v.Drain(40);
            v.Supervisor(3, 0, 3);
        }
        ulong tableClock = ready || masked ? v.Clock : Requested;
        if (idlePrefix)
        {
            v.Rows.Add("M16_012_POLL identity=0 clock=40 ready=0 idle=0");
            v.Rows.Add($"M16_012_POLL identity=0 clock={Requested - 1} ready=0 idle=1");
        }
        v.Rows.Add($"M10_017_TERMINATE main={v.Main.Id} signal=15 origin=128 nanos={Requested} tableClock={tableClock} owner=supervisor completion=pending");
        if (ready || masked)
            v.Rows.Add($"M16_012_POLL identity=0 clock={Requested} ready=1 idle=0");
        v.Clock = Requested;
        long interrupted = ready ? 0 : deadline ? -110 : -4;
        if (!ready)
            v.Resume(interrupted);
        if (masked)
        {
            v.Main.Mask = 0;
            v.Call(14, 0, [2, 0x500400, 0, 8]);
        }
        ulong savedRsp = v.Main.Rsp, fp = (savedRsp - 128 - 512) & ~63UL, frame = ((fp - 440) & ~15UL) - 8;
        v.Main.Frame = frame;
        v.Main.Depth = 1;
        v.Main.Mask = 1UL << 14;
        v.Main.Rsp = frame - 64;
        v.Call(186, 1);
        v.Call(1, 1, [4, 0x500400, 1]);
        v.Main.Rsp = frame + 8;
        v.Main.Frame = v.Main.Depth = v.Main.Mask = 0;
        v.Call(15, interrupted, action: 5, returnedRsp: savedRsp);
        v.Main.Rsp = savedRsp;
        v.Call(3, 0, [6]);
        Write(v, 1, "info: synthetic static fixture\n");
        Write(v, 2, End);
        v.Rows.Add($"M10_017_APP_EXIT main={v.Main.Id} nanos={Exited} exit=0 cleanup=pending");
        v.Call(231, 0, [0], 2);
        v.Drain(Requested);
        v.Exit(2, 2, 3);
        v.Select(first);
        v.Exit(4, 1, 2);
        v.Select(second);
        v.Exit(4, 0, 1);
        for (int fd = 0; fd <= 5; fd++)
            v.Rows.Add($"M16_011_CLOSE fd={fd} valueBits=0");
        v.Clock = Exited;
        v.End(3);
        int owner = v.Rows.FindIndex(row => row.StartsWith("M16_008_OWNER_END ", StringComparison.Ordinal));
        v.Rows.Insert(owner + 1, $"M10_017_OWNER_END main={v.Main.Id} ready={Ready} requested={Requested} exited={Exited} reaped=3 app=exit0 process=drained timer=restored descriptors=closed mappings=restored pool=scrubbed nic=pending");
        return v;
    }

    private static void Write(NativeAotThreadingTranscriptTests.Vector v, uint channel, string value)
    {
        int length = Encoding.UTF8.GetByteCount(value);
        v.Rows.Add($"M16_005_WRITE fd={channel} bytes={length} data={value}");
        v.Call(1, length, [channel, 0x500400, (uint)length]);
    }
    private static void Path(NativeAotThreadingTranscriptTests.Vector v, ulong nr, long returned, string path,
        ulong kind, ulong view, ulong inode, ulong bytes, ulong mode, ulong[] args)
    {
        v.Rows.Add($"M10_017_PATH identity={v.Current.Id} ordinal={v.Current.Ordinal + 1} nr={nr} pathHex={Convert.ToHexString(Encoding.ASCII.GetBytes(path))} valueBits={unchecked((ulong)returned)} kind={kind} view={view} inode={inode} bytes={bytes} mode={mode}");
        v.Call(nr, returned, args);
    }
    private static void File(NativeAotThreadingTranscriptTests.Vector v, ulong nr, long returned, uint fd, uint generation,
        ulong view, ulong inode, ulong bytes, ulong position, ulong after, string? path = null, ulong[]? args = null)
    {
        const ulong kind = 1;
        v.Rows.Add($"M10_017_FILE identity={v.Current.Id} ordinal={v.Current.Ordinal + 1} nr={nr} fd={fd} generation={generation} kind={kind} view={view} inode={inode} bytes={bytes} position={position} afterPosition={after}");
        if (path is not null)
            Path(v, nr, returned, path, kind, view, inode, bytes, 0x8124, args!);
        else
            v.Call(nr, returned, args);
    }
    private static QemuTranscripts.StaticFileReplayReport Inspect(IEnumerable<string> rows) =>
        QemuTranscripts.InspectNativeAotStaticFileProtocolForTests(NativeAotThreadingTranscriptTests.Elf(),
            Encoding.UTF8.GetBytes(string.Join("\r\n", rows) + "\r\n"), address => address == 0x400202,
            address => address == 0x400202, 8192, 12288);
    private static void Reject(IEnumerable<string> rows, string? reason = null)
    {
        string? actual = null;
        try
        {
            Inspect(rows);
        }
        catch (ToolFailure failure) { actual = failure.Reason; }
        HostCheck.True(actual is not null && (reason is null || actual.Contains(reason, StringComparison.Ordinal)), "static replay accepted mutation or wrong failure: " + actual);
    }
    private static string[] Change(IEnumerable<string> rows, string selector, string field, ulong value)
    {
        string[] result = rows.ToArray();
        int at = Array.FindIndex(result, row => row.Contains(selector, StringComparison.Ordinal));
        HostCheck.True(at >= 0, "static replay mutation selector");
        result[at] = Regex.Replace(result[at], @"\b" + field + "=[0-9]+", field + "=" + value.ToString(CultureInfo.InvariantCulture));
        return result;
    }

    [HostTest("static replay joins actual owner termination handler sigreturn exit and three complete reaps")]
    private static void Complete()
    {
        foreach (var vector in new[] { Vector(), Vector(ready: true), Vector(deadline: true), Vector(deadline: true, masked: true), Vector(idlePrefix: true) })
        {
            var report = Inspect(vector.Rows);
            HostCheck.Equal(3, report.Protocol.Reaped);
            HostCheck.Equal(1, report.Signal15Returns);
            HostCheck.Equal(Ready, report.ReadyNanoseconds);
            HostCheck.Equal(Requested, report.RequestedNanoseconds);
            HostCheck.Equal(Exited, report.ExitedNanoseconds);
            HostCheck.Equal(7, report.PathObservations);
            HostCheck.Equal(10, report.FileObservations);
            HostCheck.Equal(3, report.Connections.Count);
            for (int i = 0; i < report.Connections.Count; i++)
            {
                var connection = report.Connections[i];
                HostCheck.Equal(i, connection.Connection);
                HostCheck.Equal(1UL, connection.Sent);
                HostCheck.Equal(1UL, connection.Received);
                HostCheck.Equal(0, connection.Reset);
                HostCheck.Equal(0, connection.Eof);
                HostCheck.True(!connection.WriteClosed && !connection.ReadClosed);
            }
            HostCheck.True(report.Connections is not QemuTranscripts.StaticFileSocketIo[]);
            bool denied = false;
            try
            {
                ((IList<QemuTranscripts.StaticFileSocketIo>)report.Connections)[0] = default;
            }
            catch (NotSupportedException) { denied = true; }
            HostCheck.True(denied, "copied socket facts must be read only");
        }
        var states = Inspect(Vector(ioStates: true).Rows).Connections;
        HostCheck.Equal(new QemuTranscripts.StaticFileSocketIo(0, 1, 1, 0, 1, true, true), states[0]);
        HostCheck.Equal(new QemuTranscripts.StaticFileSocketIo(1, 1, 1, 1, 0, false, false), states[1]);
        HostCheck.Equal(new QemuTranscripts.StaticFileSocketIo(2, 1, 1, 0, 1, true, true), states[2]);
    }
    [HostTest("static replay admits the explicit32 descriptor bound and proves capacity plus descriptor generation reuse")]
    private static void Capacity()
    {
        var vector = Vector(capacity: true);
        var report = Inspect(vector.Rows);
        HostCheck.Equal(54, report.FileObservations);
        HostCheck.Equal(34, report.PathObservations);
        Reject(vector.Rows.Select(row => row.Replace("descriptors=32", "descriptors=16", StringComparison.Ordinal)), "threading_order");
        Reject(Change(vector.Rows, "nr=2 fd=31 ", "fd", 32), "static_replay_file_binding");
        Reject(Change(vector.Rows, "nr=3 fd=31 ", "generation", 2), "static_replay_file_generation");
    }
    [HostTest("static replay rejects missing duplicate forged and stale supervisor termination")]
    private static void TerminationOwner()
    {
        var rows = Vector().Rows;
        Reject(rows.Where(row => !row.StartsWith("M10_017_TERMINATE ", StringComparison.Ordinal)));
        foreach ((string field, ulong value) in new[] { ("main", 0x200000001UL), ("signal", 12UL), ("origin", 0UL), ("nanos", Requested - 1), ("tableClock", Requested - 1) })
            Reject(Change(rows, "M10_017_TERMINATE ", field, value), "static_replay_termination");
        var duplicate = rows.ToList();
        int at = duplicate.FindIndex(row => row.StartsWith("M10_017_TERMINATE ", StringComparison.Ordinal));
        duplicate.Insert(at + 1, duplicate[at]);
        Reject(duplicate);
        Reject(rows.Where(row => !row.Contains(" nr=13 a0=15 ", StringComparison.Ordinal)));
        var idle = Vector(idlePrefix: true).Rows;
        Reject(idle.Where(row => !row.StartsWith("M10_017_TERMINATE ", StringComparison.Ordinal)), "socket_no_unowned_idle");
        Reject(Change(idle, "M10_017_TERMINATE ", "main", 0x200000001), "static_replay_termination");
        Reject(Change(idle, "M16_012_POLL identity=0 clock=40 ", "ready", 1), "socket_poll_ready");
    }
    [HostTest("static replay retains deadline winner and blocked signal until actual mask transition")]
    private static void CompletionWinner()
    {
        var rows = Vector(deadline: true).Rows;
        Reject(Change(rows, "M16_008_RESUME ", "valueBits", unchecked((ulong)-4L)), "threading_resume");
        Reject(Change(rows, "waitDeadline=100000000000", "waitDeadline", Requested + 1), "threading_resume");
        var masked = Vector(deadline: true, masked: true).Rows;
        Reject(masked.Where(row => !(row.Contains(" nr=14 ", StringComparison.Ordinal) && row.Contains("blockedSignals=0 ", StringComparison.Ordinal))));
        Reject(Change(rows, " nr=186 ", "blockedSignals", 0), "static_signal15_handler_mask");
        Reject(Change(rows, " nr=15 ", "returnRsp", 0x500800), "threading_sigreturn_context");
    }
    [HostTest("static replay rejects early end wrong stderr writer unclosed listener and grace expiration")]
    private static void Lifecycle()
    {
        var rows = Vector().Rows;
        Reject(Change(rows, "M10_017_LISTENING ", "writer", 0x100000002), "static_replay_listen_call");
        Reject(Change(rows, "M10_017_APP_EXIT ", "main", 0x100000002), "static_replay_actual_group_exit");
        Reject(Change(rows, "M10_017_APP_EXIT ", "nanos", Requested + LinuxStaticFileObservation.GraceNanoseconds), "static_replay_actual_group_exit");
        Reject(rows.Where(row => !row.Contains(" nr=3 a0=6 ", StringComparison.Ordinal)));
        Reject(rows.Where(row => !row.StartsWith("M10_017_OWNER_END ", StringComparison.Ordinal)));
        Reject(Change(rows, "M10_017_OWNER_END ", "reaped", 2), "static_replay_owner_accounting");
        Reject(rows.Select(row => row.Replace(End, "CLROS_STATIC_FILES_APP_END v0\n", StringComparison.Ordinal)), "static_replay_output_contract");
    }
    [HostTest("static replay authenticates every file call and its package path rather than inferring unknown user pointers")]
    private static void FileBinding()
    {
        var rows = Vector().Rows;
        Reject(rows.Where(row => !row.StartsWith("M10_017_FILE ", StringComparison.Ordinal)), "static_replay_file_binding");
        Reject(rows.Where(row => !row.StartsWith("M10_017_PATH ", StringComparison.Ordinal)), "static_replay_path_binding");
        Reject(Change(rows, "M10_017_FILE ", "identity", 0x100000002), "static_replay_file_binding");
        Reject(Change(rows, "M10_017_FILE ", "generation", 2), "static_replay_file_generation");
        Reject(Change(rows, "M10_017_PATH ", "view", 0), "static_replay_path_metadata");
        Reject(Change(rows, "M10_017_FILE ", "view", 0), "static_replay_open_view_join");
        Reject(Change(rows, "M10_017_PATH ", "valueBits", 6), "static_replay_path_binding");
        Reject(Change(rows, "M10_017_PATH ", "ordinal", 1), "static_replay_path_binding");
    }
    [HostTest("static replay independently models file EOF seek positioned reads and exact committed cursor")]
    private static void FileCursor()
    {
        var rows = Vector().Rows;
        Reject(Change(rows, "nr=17 fd=5 ", "afterPosition", 1), "static_replay_file_cursor_commit");
        Reject(Change(rows, "nr=0 fd=5 ", "afterPosition", 0), "static_replay_file_cursor_commit");
        Reject(Change(rows, " nr=17 a0=5 ", "a1", 0x800000000000), "static_replay_read_result");
        Reject(Change(rows, "nr=8 fd=5 ", "position", 0), "static_replay_file_generation");
        Reject(Change(rows, "nr=5 fd=5 ", "bytes", 2), "static_replay_file_generation");
    }
    [HostTest("static replay rejects partial tails forged instruction boundaries omitted cleanup and fabricated file paths")]
    private static void CompleteInputs()
    {
        var rows = Vector().Rows;
        Reject(rows.Take(rows.Count - 1));
        Reject(rows.Concat(["unowned"]));
        Reject(Change(rows, "M16_008_REAP ", "next", 0x300000001), "threading_reap_generation");
        Reject(Change(rows, "M10_017_PATH ", "mode", 0), "static_replay_path_metadata");
        var changed = rows.ToArray();
        int at = Array.FindIndex(changed, row => row.StartsWith("M10_017_PATH ", StringComparison.Ordinal));
        changed[at] = Regex.Replace(changed[at], @"pathHex=[0-9A-F]+", "pathHex=" + Convert.ToHexString("/app/../wwwroot/one.bin"u8));
        Reject(changed, "static_replay_path_result");
    }
}
