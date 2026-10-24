using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Complete finite scheduler replay facts only. Every count comes
    /// from original instruction-bound calls, real exits and generation reaps.
    /// This value grants no native/source, packet, runner or guest authority.</summary>
    public readonly record struct StaticFileReplayReport(ThreadingProtocolReport Protocol, ulong ReadyNanoseconds,
        ulong RequestedNanoseconds, ulong ExitedNanoseconds, int FileObservations, int PathObservations, int Signal15Returns,
        IReadOnlyList<StaticFileSocketIo> Connections);

    /// <summary>Copied syscall I/O counters from one original accepted open
    /// description, in actual accept order. Sent counts successfully queued
    /// application bytes; independent wire capture determines transmitted bytes.
    /// Closed state remains the observed shutdown state after descriptor close.</summary>
    public readonly record struct StaticFileSocketIo(int Connection, ulong Sent, ulong Received, int Reset, int Eof,
        bool WriteClosed, bool ReadClosed);

    /// <summary>Borrow one complete bounded raw inner M16-009 static-app tail,
    /// original ELF and instruction predicates. Replay the existing memory,
    /// syscall, FIFO, futex, pipe, epoll, signal-frame and cleanup model with
    /// explicit 32-descriptor staged admission. Mandatory path/file witnesses
    /// bind immutable package views rather than guessing user path bytes. The
    /// output decoder retains every channel/length; stderr entry/end belong to
    /// the original main generation. Missing witness, partial prefix, timeout,
    /// duplicate request or mismatched cleanup rejects. Caller values supply
    /// no prepared-program, source, native, packet, process or guest authority.</summary>
    public static StaticFileReplayReport InspectNativeAotStaticFileProtocolForTests(byte[] elf, ReadOnlySpan<byte> raw,
        Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot)
        => ReplayNativeAotStaticFileTail(elf, raw, instruction, syscallReturn, userRoot, kernelRoot);

    // The enclosing consumer alone supplies sealed package/program/source and
    // native authority. This common leaf returns only copied protocol facts.
    internal static StaticFileReplayReport ReplayNativeAotStaticFileTail(byte[] elf, ReadOnlySpan<byte> raw,
        Func<ulong, bool> instruction, Func<ulong, bool> syscallReturn, ulong userRoot, ulong kernelRoot)
    {
        var framed = NativeAotThreadingOutput.ReadWeb(raw);
        Need(framed.NormalizedTail.EndsWith("\r\n", StringComparison.Ordinal), "static_replay_complete_tail");
        var capture = NativeAotSocketGuest.ReadReadiness(framed);
        var replay = new ThreadingProtocol(NativeAotStaticFilePublisher.Application, Rows(framed), elf, instruction,
            syscallReturn, framed.WriteLengths, userRoot, kernelRoot, socketCapture: capture);
        var protocol = replay.Validate(); return replay.StaticReport(protocol);
    }

    private sealed partial class ThreadingProtocol
    {
        private LinuxStaticFileObservation staticObservation;
        private byte[] staticStdout = [], staticStderr = [];
        private int staticStdoutAt, staticStderrAt, staticSignalEntered, staticSignalReturned, staticFileCount, staticPathCount;
        private int staticListener = -1; private uint staticListenerGeneration;
        private Dictionary<string, ulong>? staticFileWitness, staticPathWitness;
        private string staticPathHex = "", staticLifecycle = "";
        private Dictionary<string, ulong>? staticLifecycleRow;
        private readonly Dictionary<int, StaticOpenFile> staticFilesByFd = [];
        private (string[] Paths, ulong[] Sizes) staticViews = ([], []);
        private sealed record StaticOpenFile(uint Generation, ulong Kind, ulong View, ulong Inode, ulong Bytes)
        { internal ulong Position; }
        private const string StaticEntry = "CLROS_STATIC_FILES_APP_ENTRY v1\n", StaticEnd = "CLROS_STATIC_FILES_APP_END v1\n";

        private void StaticInitialize(NativeAotThreadingOutput.Capture output)
        {
            Need(output.Stderr == StaticEntry + StaticEnd && staticObservation.Begin(0x100000001), "static_replay_output_contract");
            staticStdout = Encoding.UTF8.GetBytes(output.Stdout); staticStderr = Encoding.UTF8.GetBytes(output.Stderr);
            staticViews = diskInventory is null ? StaticViews() : (["/app/apps/" + NativeAotStaticFilePublisher.Application], [(ulong)NativeAotStaticFileProgram.ElfBytes]);
        }
        internal StaticFileReplayReport StaticReport(ThreadingProtocolReport protocol) => new(protocol,
            staticObservation.ReadyNanoseconds, staticObservation.RequestedNanoseconds, staticObservation.ExitedNanoseconds,
            staticFileCount, staticPathCount, staticSignalReturned, Array.AsReadOnly(socketDescriptions
                .Where(description => !description.Port && description.Phase == 3).OrderBy(description => description.Connection)
                .Select(description => new StaticFileSocketIo(description.Connection, description.Sent, description.Received,
                    description.Reset, description.Eof, description.WriteClosed, description.ReadClosed)).ToArray()));

        private void StaticSupervisor()
        {
            if (!Starts(StaticTag + "_TERMINATE ")) return;
            Need(socketSelected is null && !stopping && active > 0 && slots[0] is { Reaped: false, Exiting: false } target &&
                target.Identity == 0x100000001 && actions.Contains(15), "static_replay_termination_owner");
            const string suffix = " owner=supervisor completion=pending";
            string line = Take(); Need(line.EndsWith(suffix, StringComparison.Ordinal), "static_replay_termination_shape");
            var request = ReadRecord(line[..^suffix.Length], StaticTag + "_TERMINATE", "main:dec", "signal:dec", "origin:dec", "nanos:dec", "tableClock:dec");
            Need(request["main"] == 0x100000001 && request["signal"] == 15 && request["origin"] == 128 &&
                request["nanos"] >= clock && staticObservation.Poll(request["nanos"]) == LinuxStaticFileObservation.Progress.RequestTermination,
                "static_replay_termination_policy");
            var main = slots[0]!;
            bool samplesClock = (main.Mask & (1UL << 14)) == 0 &&
                (main.WaitSource == 1 || main.WaitSource == 3 && main.Deadline != 0);
            Need(samplesClock ? request["tableClock"] >= request["nanos"] : request["tableClock"] == clock,
                "static_replay_termination_table_clock");
            // The native API may sample/advance the table clock while resolving
            // an existing futex deadline. Resolve that winner before interruption.
            Clock(request["tableClock"]);
            Need(staticObservation.RecordRequest(request["main"], request["nanos"]), "static_replay_termination_once");
            main.PendingTermination = true; InterruptPendingWait(main);
        }

        private void StaticBeforeCall(RuntimeThread thread, bool deferred = false)
        {
            int count = 0;
            while (Starts(StaticTag + "_"))
            {
                Need(++count <= 4, "static_replay_metadata_bound");
                if (diskInventory is not null && Starts("M10_018_DISK_READY "))
                { Need(!deferred, "disk_static_readiness_context"); DiskStaticReady(); continue; }
                if (Starts(StaticTag + "_FILE "))
                {
                    Need(staticFileWitness is null, "static_replay_duplicate_file");
                    staticFileWitness = Row(StaticTag + "_FILE", "identity:dec", "ordinal:dec", "nr:dec", "fd:dec", "generation:dec",
                        "kind:dec", "view:dec", "inode:dec", "bytes:dec", "position:dec", "afterPosition:dec");
                    continue;
                }
                if (Starts(StaticTag + "_PATH "))
                {
                    Need(staticPathWitness is null, "static_replay_duplicate_path");
                    string line = Take(); string[] fields = line.Split(' ');
                    Need(fields.Length == 11 && fields[4].StartsWith("pathHex=", StringComparison.Ordinal), "static_replay_path_shape");
                    staticPathHex = fields[4][8..];
                    Need(staticPathHex.Length <= 510 && staticPathHex.Length % 2 == 0 && staticPathHex.All(char.IsAsciiHexDigit), "static_replay_path_bound");
                    staticPathWitness = ReadRecord(string.Join(' ', fields.Where((_, index) => index != 4)), StaticTag + "_PATH",
                        "identity:dec", "ordinal:dec", "nr:dec", "valueBits:dec", "kind:dec", "view:dec", "inode:dec", "bytes:dec", "mode:dec");
                    continue;
                }
                Need(!deferred && staticLifecycleRow is null, "static_replay_duplicate_lifecycle");
                if (Starts(StaticTag + "_LISTENING "))
                {
                    const string suffix = " http=external cleanup=pending"; string line = Take();
                    Need(line.EndsWith(suffix, StringComparison.Ordinal), "static_replay_listen_shape");
                    staticLifecycleRow = ReadRecord(line[..^suffix.Length], StaticTag + "_LISTENING", "main:dec", "writer:dec", "fd:dec", "nanos:dec");
                    staticLifecycle = "listen";
                }
                else if (Starts(StaticTag + "_APP_EXIT "))
                {
                    const string suffix = " exit=0 cleanup=pending"; string line = Take();
                    Need(line.EndsWith(suffix, StringComparison.Ordinal), "static_replay_exit_shape");
                    staticLifecycleRow = ReadRecord(line[..^suffix.Length], StaticTag + "_APP_EXIT", "main:dec", "nanos:dec");
                    staticLifecycle = "exit";
                }
                else throw new ToolFailure("static_replay_uninventoried_record");
            }
            if (count != 0) Need(Starts(deferred ? "M16_025_COMPLETE " : "M16_008_CALL "), "static_replay_metadata_adjacent_call");
        }

        private unsafe void StaticAfterCall(RuntimeThread thread, Dictionary<string, ulong> call, LinuxCallWaitObservation witness, bool output)
        {
            Need(staticFileWitness is null && staticPathWitness is null, "static_replay_orphan_metadata");
            ulong nr = call["nr"];
            if (output)
            {
                uint channel = (uint)call["a0"]; int count = checked((int)call["valueBits"]);
                byte[] bytes = channel == 1 ? staticStdout : staticStderr; int at = channel == 1 ? staticStdoutAt : staticStderrAt;
                Need(channel is 1 or 2 && count > 0 && count <= bytes.Length - at, "static_replay_write_source");
                fixed (byte* source = bytes) Need(staticObservation.ObserveOutput(thread.Identity, channel, source + at, (uint)count), "static_replay_write_generation");
                if (channel == 1) staticStdoutAt += count; else staticStderrAt += count;
            }
            if (nr == 49)
            {
                Need(call["action"] == 1 && call["valueBits"] == 0 && staticObservation.RecordBind(thread.Identity, (uint)call["a0"], true), "static_replay_bind");
                staticListener = checked((int)call["a0"]); staticListenerGeneration = descriptorGenerations[staticListener];
            }
            if (nr == 50)
            {
                var row = staticLifecycleRow;
                Need(staticLifecycle == "listen" && row is not null && row["main"] == 0x100000001 && row["writer"] == thread.Identity &&
                    row["fd"] == call["a0"] && row["nanos"] >= call["clock"] && call["action"] == 1 && call["valueBits"] == 0 &&
                    staticListener == (int)call["a0"] && descriptorGenerations[staticListener] == staticListenerGeneration &&
                    staticObservation.RecordListen(thread.Identity, (uint)call["a0"], true, row["nanos"]), "static_replay_listen_call");
                staticLifecycleRow = null; staticLifecycle = "";
            }
            if (nr == 3 && call["a0"] == (ulong)staticListener && descriptorGenerations[staticListener] == staticListenerGeneration)
                Need(call["action"] == 1 && call["valueBits"] == 0 && !socketDescriptors.ContainsKey(staticListener) &&
                    staticObservation.RecordClose((uint)staticListener, true), "static_replay_listener_close");
            if (nr == 231) { staticLifecycleRow = null; staticLifecycle = ""; }
            Need(staticLifecycleRow is null, "static_replay_lifecycle_call_mismatch");
        }

        private void StaticGroupExit(RuntimeThread thread, Dictionary<string, ulong> call)
        {
            if (diskInventory is not null) Need(history.All(t => t.Disk is null), "disk_static_pending_shutdown_unqualified");
            var row = staticLifecycleRow;
            Need(staticLifecycle == "exit" && row is not null && row["main"] == thread.Identity && thread.Identity == 0x100000001 &&
                row["nanos"] >= call["clock"] && call["a0"] == 0 && call["valueBits"] == 0 && call["action"] == 2 &&
                staticSignalEntered == 1 && staticSignalReturned == 1 && staticObservation.RecordExit(thread.Identity, 0, row["nanos"]),
                "static_replay_actual_group_exit");
        }

        private void StaticFinish(ulong finished, int actualReaped)
        {
            Need(staticObservation.GracefulExit && staticObservation.ExitedNanoseconds <= finished && staticSignalEntered == 1 &&
                staticSignalReturned == 1 && staticFilesByFd.Count == 0 && staticStdoutAt == staticStdout.Length && staticStderrAt == staticStderr.Length,
                "static_replay_complete_lifecycle");
            const string suffix = " app=exit0 process=drained timer=restored descriptors=closed mappings=restored pool=scrubbed nic=pending";
            string line = Take(); Need(line.EndsWith(suffix, StringComparison.Ordinal), "static_replay_cleanup_shape");
            var row = ReadRecord(line[..^suffix.Length], StaticTag + "_OWNER_END", "main:dec", "ready:dec", "requested:dec", "exited:dec", "reaped:dec");
            Need(row["main"] == 0x100000001 && row["ready"] == staticObservation.ReadyNanoseconds &&
                row["requested"] == staticObservation.RequestedNanoseconds && row["exited"] == staticObservation.ExitedNanoseconds &&
                row["reaped"] == (ulong)actualReaped, "static_replay_owner_accounting");
        }

        private bool StaticFileCall(Dictionary<string, ulong> call, LinuxCallWaitObservation witness)
        {
            ulong nr = call["nr"], fd = call["a0"], result = call["valueBits"];
            bool pathCall = nr is 2 or 257 or 4 or 6 or 89;
            var path = pathCall ? StaticPath(call, witness) : null;
            bool open = nr is 2 or 257;
            bool current = (nr is 0 or 3 or 5 or 8 or 17 or 72 or 73 or 221) &&
                fd < (ulong)descriptorCapacity && staticFilesByFd.ContainsKey((int)fd);
            if (!open && !current)
            {
                Need(staticFileWitness is null, "static_replay_unowned_file_witness");
                if (nr is 4 or 6 or 89) return true;
                if (nr == 79) { Need(call["a1"] >= 2 && result == 2 && memory.Covered(fd, 2, 3), "static_replay_cwd"); return true; }
                return false;
            }
            if (open && unchecked((long)result) < 0) { Need(staticFileWitness is null, "static_replay_failed_open_witness"); return true; }
            var record = staticFileWitness ?? throw new ToolFailure("static_replay_file_binding"); staticFileWitness = null;
            Need(record["identity"] == witness.Identity && record["ordinal"] == witness.Ordinal && record["nr"] == nr &&
                record["fd"] == (open ? result : fd) && record["generation"] is > 0 and <= uint.MaxValue && (record["kind"] is 1 or 2 || diskInventory is not null && record["kind"] == 5),
                "static_replay_file_binding");
            fd = record["fd"]; Need(fd >= 3 && fd < (ulong)descriptorCapacity, "static_replay_file_fd");
            if (open)
            {
                Need(path is not null && record["kind"] == path["kind"] && record["view"] == path["view"] &&
                    record["inode"] == path["inode"] && record["bytes"] == path["bytes"], "static_replay_open_view_join");
                Need(result == (ulong)LowestFd() && !staticFilesByFd.ContainsKey((int)fd), "static_replay_open_fd");
                files.Add((int)fd); InstallDescriptorGeneration((int)fd);
                Need(record["position"] == 0, "static_replay_open_position");
                staticFilesByFd.Add((int)fd, new((uint)record["generation"], record["kind"], record["view"], record["inode"], record["bytes"]));
            }
            var file = staticFilesByFd[(int)fd];
            Need(file.Generation == descriptorGenerations[(int)fd] && record["generation"] == file.Generation &&
                record["kind"] == file.Kind && record["view"] == file.View && record["inode"] == file.Inode && record["bytes"] == file.Bytes &&
                record["position"] == file.Position, "static_replay_file_generation");
            StaticView(file.Kind, file.View, file.Inode, file.Bytes);
            if (file.Kind == 5 && nr is 0 or 8 or 17)
                Need(diskCompleting is not null, "disk_static_missing_pending_file");
            ulong after = file.Position;
            if (nr is 0 or 17)
            {
                ulong wanted = call["a2"], offset = nr == 17 ? call["a3"] : file.Position;
                Need(wanted <= 65536 && offset <= long.MaxValue, "static_replay_read_arguments");
                ulong count = offset >= file.Bytes ? 0 : Math.Min(wanted, Math.Min(8192UL, file.Bytes - offset));
                long expected = (count == 0 || memory.Covered(call["a1"], count, 3)) && (diskCompleting is null || count <= diskCompleting.OutputPrefix) ? (long)count : -14;
                Need(result == Bits(expected), "static_replay_read_result"); if (nr == 0 && expected > 0) after += count;
                if (file.Kind == 5 && expected > 0)
                {
                    var pending = diskCompleting ?? throw new ToolFailure("disk_static_unparked_positive_read");
                    diskReadWindows.Add(new(witness.Identity,witness.Ordinal,nr,file.Generation,pending.Request,
                        pending.BeginIndex,position-1,file.Inode,offset,wanted,pending.OutputPrefix,(uint)expected));
                }
            }
            else if (nr == 5) Need(result == Bits(memory.Covered(call["a1"], 144, 3) ? 0 : -14), "static_replay_fstat");
            else if (nr == 8)
            {
                Need(call["a2"] <= 2, "static_replay_seek_whence");
                Int128 origin = call["a2"] == 0 ? 0 : call["a2"] == 1 ? file.Position : file.Bytes;
                Int128 next = origin + unchecked((long)call["a1"]); long expected = next < 0 ? -22 : next > long.MaxValue ? -75 : (long)next;
                Need(result == Bits(expected), "static_replay_seek_result"); if (expected >= 0) after = (ulong)expected;
            }
            else if (nr == 72) Need(call["a1"] == 2 && call["a2"] == 1 && result == 0, "static_replay_file_fcntl");
            else if (nr == 73) Need(call["a1"] == 5 && result == Bits(-38), "static_replay_flock_unsupported");
            else if (nr == 221) Need(call["a1"] == 0 && call["a2"] == 0 && call["a3"] == 2 && result == Bits(-38), "static_replay_fadvise_unsupported");
            else Need(open || nr == 3 && result == 0, "static_replay_file_operation");
            Need(record["afterPosition"] == after, "static_replay_file_cursor_commit"); file.Position = after;
            if (nr == 3) { Need(files.Remove((int)fd), "static_replay_file_close"); staticFilesByFd.Remove((int)fd); }
            staticFileCount++; return true;
        }

        private static (string[] Paths, ulong[] Sizes) StaticViews()
        {
            var assets = NativeAotStaticFilePackage.StagedAssets();
            return (["/app/apps/" + NativeAotStaticFilePublisher.Application, ..assets.Select(asset => "/app/" + asset.Path)],
                [(ulong)NativeAotStaticFileProgram.ElfBytes, ..assets.Select(asset => (ulong)asset.Bytes.Length)]);
        }
        private void StaticView(ulong kind, ulong view, ulong inode, ulong bytes)
        {
            if (kind == 5)
            {
                Need(diskInventory is not null && view == uint.MaxValue && diskInventory.Entries.Any(e =>
                    !e.IsDirectory && e.Inode == inode && e.Bytes == bytes), "disk_static_physical_file"); return;
            }
            var admitted = staticViews;
            Need(kind == 1 ? view < (ulong)admitted.Paths.Length && inode == 256 + view && bytes == admitted.Sizes[(int)view] :
                kind == 2 && view == uint.MaxValue && inode is 17 or 18 && bytes is > 0 and <= 8192, "static_replay_package_view");
        }
        private Dictionary<string, ulong> StaticPath(Dictionary<string, ulong> call, LinuxCallWaitObservation witness)
        {
            var row = staticPathWitness; staticPathWitness = null;
            Need(row is not null && row["identity"] == witness.Identity && row["ordinal"] == witness.Ordinal && row["nr"] == call["nr"] &&
                row["valueBits"] == call["valueBits"], "static_replay_path_binding");
            byte[] bytes = Convert.FromHexString(staticPathHex);
            ulong path = call[call["nr"] == 257 ? "a1" : "a0"];
            Need((diskCompleting is null ? memory.Covered(path, (ulong)bytes.Length + 1, 1) : (ulong)bytes.Length + 1 <= diskCompleting.PathPrefix) && bytes.All(value => value is >= 32 and <= 126), "static_replay_path_copy");
            string text = Encoding.ASCII.GetString(bytes); bool open = call["nr"] is 2 or 257;
            bool trailing = !open && text.EndsWith('/'); string relative = text.StartsWith('/') ? text[1..] : text;
            if (trailing && relative.Length > 0) relative = relative[..^1];
            bool valid = relative.Length <= 255 && !relative.Contains('\\') && !relative.Contains(':') &&
                relative.Split('/').All(segment => segment.Length > 0 && segment is not ("." or ".."));
            bool diskRoute = call["nr"] != 89 &&
                (relative == "app/wwwroot" || relative.StartsWith("app/wwwroot/",StringComparison.Ordinal));
            Need(diskCompleting is null || diskRoute, "disk_static_pending_path_route");
            if (diskInventory is not null && diskRoute)
                return DiskStaticPath(call,row!,text,relative,open,trailing);
            var views = staticViews; int view = Array.IndexOf(views.Paths, "/" + relative);
            long expected; ulong kind = 0, index = uint.MaxValue, inode = 0, size = 0, mode = 0;
            if (text.Length == 0) expected = -2;
            else if (!open && relative.Length == 0) { expected = 0; kind = 3; inode = 64; mode = 0x416d; }
            else if (!valid) expected = -13;
            else if (open)
            {
                ulong flags = call[call["nr"] == 257 ? "a2" : "a1"];
                Need(call["nr"] != 257 || call["a0"] == Bits(-100), "static_replay_openat_parent");
                if ((flags & (3UL | 0x40 | 0x200 | 0x400)) != 0) expected = -30;
                else if ((flags & ~0xa8900UL) != 0) expected = -22;
                else if (view >= 0) { expected = LowestFd(); if (expected < 0) expected = -24; else { kind = 1; index = (ulong)view; inode = 256 + index; size = views.Sizes[view]; mode = 0x8124; } }
                else if (relative is "proc/meminfo" or "proc/self/maps") { expected = LowestFd(); if (expected < 0) expected = -24; else { kind = 2; inode = relative == "proc/meminfo" ? 17UL : 18UL; size = row!["bytes"]; mode = 0x8124; } }
                else expected = -2;
            }
            else
            {
                bool link = (call["nr"] is 6 or 89) && !trailing;
                if (relative == "proc") { expected = 0; kind = 3; inode = 65; mode = 0x416d; }
                else if (relative is "proc/self" or "proc/1") { expected = 0; kind = link && relative == "proc/self" ? 4UL : 3UL; inode = kind == 4 ? 67UL : 66UL; size = kind == 4 ? 1UL : 0; mode = kind == 4 ? 0xa1ffUL : 0x416dUL; }
                else if (relative is "proc/self/exe" or "proc/1/exe") { expected = 0; kind = link ? 4UL : 1UL; inode = link ? 68UL : 256UL; size = link ? (ulong)views.Paths[0].Length : views.Sizes[0]; mode = link ? 0xa1ffUL : 0x8124UL; index = link ? uint.MaxValue : 0; }
                else if (relative is "proc/meminfo" or "proc/self/maps") { expected = 0; kind = 2; inode = relative == "proc/meminfo" ? 17UL : 18UL; mode = 0x8124; }
                else if (view >= 0) { expected = 0; kind = 1; index = (ulong)view; inode = 256 + index; size = views.Sizes[view]; mode = 0x8124; }
                else if (views.Paths.Any(value => relative.StartsWith(value[1..] + "/", StringComparison.Ordinal)) ||
                    new[] { "proc/self/exe", "proc/1/exe", "proc/meminfo", "proc/self/maps" }.Any(value => relative.StartsWith(value + "/", StringComparison.Ordinal))) expected = -20;
                else
                {
                    int parent = Array.FindIndex(views.Paths, value => value.StartsWith("/" + relative + "/", StringComparison.Ordinal));
                    expected = parent < 0 ? -2 : 0;
                    if (parent >= 0) { kind = 3; inode = 0x100000000UL + (ulong)parent * 256 + (ulong)relative.Length; mode = 0x416d; }
                }
                if (expected == 0 && trailing && kind is not 3) expected = -20;
                if (call["nr"] == 89)
                {
                    if (expected == 0 && (trailing || kind != 4)) expected = -22;
                    else if (expected == 0) expected = (long)Math.Min(size, call["a2"]);
                    Need(call["a2"] is > 0 and <= 4096, "static_replay_readlink_capacity");
                    if (expected > 0 && !memory.Covered(call["a1"], (ulong)expected, 3)) expected = -14;
                    if (expected >= 0) { kind = 4; inode = mode = 0; size = (ulong)expected; }
                }
                else if (expected == 0 && !memory.Covered(call["a1"], 144, 3)) expected = -14;
            }
            Need(call["valueBits"] == Bits(expected), "static_replay_path_result");
            if (expected < 0) { kind = inode = size = mode = 0; index = uint.MaxValue; }
            Need(row!["kind"] == kind && row["view"] == index && row["inode"] == inode && row["bytes"] == size && row["mode"] == mode,
                "static_replay_path_metadata");
            if (kind == 1) StaticView(kind, index, inode, size);
            staticPathCount++; return row!;
        }
    }
}
