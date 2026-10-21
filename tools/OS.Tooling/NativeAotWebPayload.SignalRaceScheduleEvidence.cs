using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    /// <summary>Copied journal inventory only. IDs, descriptors and record counts
    /// confer no live process, syscall, scheduler, native-execution or guest authority.</summary>
    public readonly record struct SignalRaceScheduleSummary(int Tracer, int Process, int Waiter, int Actor,
        int Epoll, int Read, int Write, int Records);

    /// <summary>Validate borrowed native receipt payloads without I/O. Historical
    /// v5/v6 require the original v1 protocol; controlled v7 requires v2 selected
    /// and attempt evidence plus schedule-v1. Event-owned v8 requires selected/
    /// attempts-v3 and schedule-v2 with exact untimed waits. Throws for mixed,
    /// missing or inconsistent evidence. File/source hashes and original process,
    /// native instruction and guest execution authority remain the owner's job.</summary>
    public static QemuTranscripts.WebSignalRaceSelection ValidateSignalRaceQualificationEvidence(string nativeSchema,
        string selectedJson, string? attemptsJson, string? scheduleJournal, string endpoint, string expectedHealthBody)
    {
        ScheduleNeed(nativeSchema is "clros.nativeaot.web.v5" or "clros.nativeaot.web.v6" or "clros.nativeaot.web.v7" or "clros.nativeaot.web.v8", "native_schema");
        bool eventOwned = nativeSchema == "clros.nativeaot.web.v8", controlled = eventOwned || nativeSchema == "clros.nativeaot.web.v7";
        var selection = ValidateSignalRaceEvidence(selectedJson, endpoint, expectedHealthBody);
        using var selected = JsonDocument.Parse(selectedJson);
        ScheduleNeed(selected.RootElement.GetProperty("schema").GetString() ==
            (eventOwned ? "clros.web.signalrace.v3" : controlled ? "clros.web.signalrace.v2" : "clros.web.signalrace.v1"), "protocol_pairing");
        if (controlled)
        {
            ScheduleNeed(scheduleJournal is not null, "required");
            _ = InspectSignalRaceScheduleCore(scheduleJournal!, selection, eventOwned);
        }
        else ScheduleNeed(scheduleJournal is null, "historical_journal");
        if (nativeSchema == "clros.nativeaot.web.v5") ScheduleNeed(attemptsJson is null, "historical_attempts");
        else
        {
            ScheduleNeed(attemptsJson is not null, "attempts_required");
            // Every selected version requires its exact attempt companion,
            // retaining all original response and completion-order joins.
            ScheduleNeed(InspectSignalRaceAttempts(attemptsJson!, selectedJson, endpoint, expectedHealthBody).SemanticComplete,
                "attempts_incomplete");
        }
        return selection;
    }

    /// <summary>Inspect bounded, borrowed JSONL values. Requires the precise
    /// controlled experiment order, original-role/generation consistency, one
    /// CPU, unchanged epoll/pipe and actual later sleeping-state observations,
    /// one start intent, tracer restoration and confirmed terminal task exits.
    /// Performs no I/O or scheduling. A successful parse establishes copied
    /// consistency, not a merged kernel chronology, atomic TID ownership or guest
    /// proof. This historical entrypoint requires schedule-v1/timed2500 only.</summary>
    public static SignalRaceScheduleSummary InspectSignalRaceScheduleEvidence(string journal,
        QemuTranscripts.WebSignalRaceSelection selection)=>InspectSignalRaceScheduleCore(journal,selection,eventOwned:false);

    /// <summary>Inspect only the explicit event-owned schedule-v2 copied
    /// contract. Require the pinned wrapper's raw all-ones timeout argument in
    /// initial/release wait snapshots, preserving the original text, and the
    /// same bounded role, ownership, ordering, restoration and terminal guards
    /// as schedule-v1. Starts no process or native operation; copied consistency
    /// grants no original native wait, process cleanup or guest authority.</summary>
    public static SignalRaceScheduleSummary InspectEventOwnedSignalRaceScheduleEvidence(string journal,
        QemuTranscripts.WebSignalRaceSelection selection)=>InspectSignalRaceScheduleCore(journal,selection,eventOwned:true);

    private static SignalRaceScheduleSummary InspectSignalRaceScheduleCore(string journal,
        QemuTranscripts.WebSignalRaceSelection selection,bool eventOwned)
    {
        ScheduleNeed(selection is not null && selection.Pid > 0 && selection.WaiterTid > 0 && selection.ActorTid > 0,
            "selection");
        ScheduleNeed(journal is { Length: > 0 and <= 1048576 } && Encoding.UTF8.GetByteCount(journal) <= 1048576 &&
            journal.EndsWith('\n') && !journal.Contains('\r'), "size_or_framing");
        string[] lines = journal[..^1].Split('\n');
        ScheduleNeed(lines.Length is 12 or 13, "record_count");
        List<JsonElement> rows = []; long previous = -1, frequency = 0;
        foreach (string line in lines)
        {
            ScheduleNeed(line.Length > 0 && Encoding.UTF8.GetByteCount(line) <= 65536, "record_size");
            using var document = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 12 });
            var row = document.RootElement;
            ChurnKeys(row, "schema", "operation", "observation", "utc", "monotonic", "frequency");
            ScheduleNeed(ChurnString(row.GetProperty("schema"), 64) ==
                (eventOwned ? "clros.web.signalrace.schedule.v2" : "clros.web.signalrace.schedule.v1"), "schema");
            ScheduleNeed(DateTimeOffset.TryParse(ChurnString(row.GetProperty("utc"), 64), CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _), "utc");
            ScheduleNeed(row.GetProperty("monotonic").TryGetInt64(out long tick) && tick >= 0 && tick >= previous &&
                row.GetProperty("frequency").TryGetInt64(out long hz) && hz > 0 && (frequency == 0 || frequency == hz), "clock");
            previous = tick; frequency = row.GetProperty("frequency").GetInt64(); rows.Add(row.Clone());
        }
        int cursor = 0;
        JsonElement Next(string operation)
        {
            ScheduleNeed(cursor < rows.Count && ChurnString(rows[cursor].GetProperty("operation"), 32) == operation, "order_" + operation);
            return rows[cursor++].GetProperty("observation");
        }
        List<ScheduleTask> tasks = [];
        for (int role = 0; role < 4; role++)
        {
            var row = Next("preflight");
            ChurnKeys(row, "Pid", "Tid", "Initial", "taskStatus", "online", "affinity", "policy", "priority");
            int pid = ChurnInt(row.GetProperty("Pid")), tid = ChurnInt(row.GetProperty("Tid"));
            int tracer = role == 0 ? tid : tasks[0].Tid;
            int expected = role switch { 0 => tracer, 1 => selection!.Pid, 2 => selection!.WaiterTid, _ => selection!.ActorTid };
            ScheduleNeed(pid > 0 && tid > 0 && tid == expected && pid == (role == 0 ? tracer : selection!.Pid) &&
                tasks.All(t => t.Tid != tid), "roles");
            ScheduleStat stat = ReadScheduleStat(row.GetProperty("Initial"));
            ScheduleNeed(stat.Tid == tid && stat.Group == tracer && stat.Session == tracer &&
                (role == 0 || stat.Parent == tracer && stat.Start >= tasks[0].Initial.Start) &&
                (role < 2 || stat.Start >= tasks[1].Initial.Start), "generation_session");
            string status = ChurnString(row.GetProperty("taskStatus"), 65536);
            ScheduleNeed(ProcUnsigned(status, "Tgid", 10) == (ulong)pid && ProcUnsigned(status, "Pid", 10) == (ulong)tid &&
                (role == 0 || ProcUnsigned(status, "TracerPid", 10) == (ulong)tracer), "proc_roles");
            string online = ChurnString(row.GetProperty("online"), 8);
            ScheduleNeed(int.TryParse(online, NumberStyles.None, CultureInfo.InvariantCulture, out int cpu) &&
                cpu is >= 0 and < 32768 && (role == 0 || tasks[0].Cpu == cpu), "one_cpu");
            string affinity = ChurnString(row.GetProperty("affinity"), 8192); CheckScheduleAffinity(affinity, cpu);
            ScheduleNeed(ChurnInt(row.GetProperty("policy")) == 0 && ChurnInt(row.GetProperty("priority")) == 0, "initial_policy");
            tasks.Add(new(pid, tid, stat, cpu, affinity));
        }
        var initial = Next("initial-wait"); ChurnKeys(initial, "waiter", "pipe");
        ScheduleNeed(ChurnInt(initial.GetProperty("waiter")) == tasks[2].Tid, "waiter");
        SchedulePipe pipe = ReadSchedulePipe(initial.GetProperty("pipe"), tasks[2], eventOwned);
        CheckSchedulePolicy(Next("tracer-raised"), tasks[0], 1, 2);
        CheckSchedulePolicy(Next("actor-raised"), tasks[3], 1, 1);
        for (int recheck = 0; recheck < 2; recheck++)
        {
            var row = Next("release-preflight"); ChurnKeys(row, "pipe", "policies");
            ScheduleNeed(ReadSchedulePipe(row.GetProperty("pipe"), tasks[2], eventOwned) == pipe, "pipe_drift");
            var policies = row.GetProperty("policies");
            ScheduleNeed(policies.ValueKind == JsonValueKind.Array && policies.GetArrayLength() == 4, "policies");
            for (int role = 0; role < 4; role++)
                CheckSchedulePolicy(policies[role], tasks[role], role is 0 or 3 ? 1 : 0, role == 0 ? 2 : role == 3 ? 1 : 0);
        }
        var start = Next("start-post-intent"); ChurnKeys(start, "actor", "Start");
        ScheduleNeed(ChurnInt(start.GetProperty("actor")) == tasks[3].Tid && ScheduleU64(start.GetProperty("Start")) == tasks[3].Initial.Start, "start_actor");
        CheckSchedulePolicy(Next("tracer-restored"), tasks[0], 0, 0);
        if (rows.Count == 13)
        {
            var retired = Next("actor-retired"); ChurnKeys(retired, "Tid", "Start", "nativeExit", "reapClaim");
            ScheduleNeed(ChurnInt(retired.GetProperty("Tid")) == tasks[3].Tid &&
                ScheduleU64(retired.GetProperty("Start")) == tasks[3].Initial.Start &&
                RaceBoolean(retired.GetProperty("nativeExit")) && !RaceBoolean(retired.GetProperty("reapClaim")), "actor_exit");
        }
        var stop = Next("owned-session-stopped"); ChurnKeys(stop, "Id", "nativeExit", "reapClaim", "tasks");
        ScheduleNeed(ChurnInt(stop.GetProperty("Id")) == tasks[0].Tid && RaceBoolean(stop.GetProperty("nativeExit")) &&
            !RaceBoolean(stop.GetProperty("reapClaim")), "session_stop");
        var stopped = stop.GetProperty("tasks");
        ScheduleNeed(stopped.ValueKind == JsonValueKind.Array && stopped.GetArrayLength() == 4, "stopped_tasks");
        for (int role = 0; role < 4; role++)
        {
            var row = stopped[role]; ChurnKeys(row, "Pid", "Tid", "Start", "nativeExit");
            ScheduleNeed(ChurnInt(row.GetProperty("Pid")) == tasks[role].Pid && ChurnInt(row.GetProperty("Tid")) == tasks[role].Tid &&
                ScheduleU64(row.GetProperty("Start")) == tasks[role].Initial.Start && RaceBoolean(row.GetProperty("nativeExit")), "task_exit");
        }
        ScheduleNeed(cursor == rows.Count, "trailing_record");
        return new(tasks[0].Tid, tasks[1].Tid, tasks[2].Tid, tasks[3].Tid, pipe.Epoll, pipe.Read, pipe.Write, rows.Count);
    }

    private sealed record ScheduleStat(int Tid, int Parent, int Group, int Session, ulong Start, string State);
    private sealed record ScheduleTask(int Pid, int Tid, ScheduleStat Initial, int Cpu, string Affinity);
    private sealed record SchedulePipe(int Epoll, int Read, int Write, string Inode, string EpollInfo,
        string ReadInfo, string WriteInfo, string Syscall, string WaitChannel);

    private static ScheduleStat ReadScheduleStat(JsonElement row)
    {
        ChurnKeys(row, "Tid", "Parent", "Group", "Session", "Start", "State");
        var value = new ScheduleStat(ChurnInt(row.GetProperty("Tid")), ChurnInt(row.GetProperty("Parent")),
            ChurnInt(row.GetProperty("Group")), ChurnInt(row.GetProperty("Session")), ScheduleU64(row.GetProperty("Start")),
            ChurnString(row.GetProperty("State"), 1));
        ScheduleNeed(value.Tid > 0 && value.Parent >= 0 && value.Group > 0 && value.Session > 0 && value.Start > 0 &&
            value.State.Length == 1 && "RSDTtKWPI".Contains(value.State, StringComparison.Ordinal), "stat");
        return value;
    }
    private static void CheckSchedulePolicy(JsonElement row, ScheduleTask task, int policy, int priority)
    {
        ChurnKeys(row, "Tid", "Start", "Policy", "Priority", "Affinity");
        ScheduleNeed(ChurnInt(row.GetProperty("Tid")) == task.Tid && ScheduleU64(row.GetProperty("Start")) == task.Initial.Start &&
            ChurnInt(row.GetProperty("Policy")) == policy && ChurnInt(row.GetProperty("Priority")) == priority &&
            ChurnString(row.GetProperty("Affinity"), 8192) == task.Affinity, "policy_readback");
    }
    private static void CheckScheduleAffinity(string text, int cpu)
    {
        ScheduleNeed(text.Length == 8192 && text.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F'), "affinity_encoding");
        byte[] bytes = Convert.FromHexString(text);
        ScheduleNeed(bytes[cpu / 8] == (byte)(1 << (cpu % 8)) && bytes.Where((_, i) => i != cpu / 8).All(b => b == 0), "affinity_mask");
    }
    private static SchedulePipe ReadSchedulePipe(JsonElement row, ScheduleTask waiter,bool eventOwned)
    {
        ChurnKeys(row, "Epoll", "Read", "Write", "Inode", "EpollInfo", "ReadInfo", "WriteInfo", "Syscall", "WaitChannel", "WaiterState");
        int epoll = ChurnInt(row.GetProperty("Epoll")), read = ChurnInt(row.GetProperty("Read")), write = ChurnInt(row.GetProperty("Write"));
        ScheduleNeed(epoll >= 0 && read >= 0 && write >= 0 && epoll != read && epoll != write && read != write, "descriptors");
        ScheduleStat state = ReadScheduleStat(row.GetProperty("WaiterState"));
        ScheduleNeed(state == (waiter.Initial with { State = "S" }), "blocked_state");
        string syscall = ChurnString(row.GetProperty("Syscall"), 1024), channel = ChurnString(row.GetProperty("WaitChannel"), 64);
        string[] args = syscall.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        ScheduleNeed(args.Length == 9 && args[0] == "232" && ScheduleHex(args[1]) == (ulong)epoll &&
            ScheduleHex(args[2]) > 0 && ScheduleHex(args[3]) == 1 && ScheduleHex(args[4]) == (eventOwned ? ulong.MaxValue : 2500UL) &&
            ScheduleHex(args[7]) > 0 && ScheduleHex(args[8]) > 0 && channel is "ep_poll" or "do_epoll_wait", "epoll_wait");
        _ = ScheduleHex(args[5]); _ = ScheduleHex(args[6]);
        string inode = ChurnString(row.GetProperty("Inode"), 64);
        Match pipe = Regex.Match(inode, @"^pipe:\[(\d+)\]$", RegexOptions.CultureInvariant);
        ScheduleNeed(pipe.Success && ulong.TryParse(pipe.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _), "inode");
        ulong number = ulong.Parse(pipe.Groups[1].Value, CultureInfo.InvariantCulture);
        string epollInfo = ChurnString(row.GetProperty("EpollInfo"), 65536), readInfo = ChurnString(row.GetProperty("ReadInfo"), 65536),
            writeInfo = ChurnString(row.GetProperty("WriteInfo"), 65536);
        MatchCollection registered = Regex.Matches(epollInfo, @"(?m)^tfd:\s*(\d+)\s+events:\s*([0-9a-fA-F]+)\s+data:\s*([0-9a-fA-F]+)\s", RegexOptions.CultureInvariant);
        ScheduleNeed(registered.Count == 1 && registered[0].Groups[1].Value == read.ToString(CultureInfo.InvariantCulture) &&
            ScheduleHex(registered[0].Groups[2].Value) == 0x19 && ScheduleHex(registered[0].Groups[3].Value) == 0x50524f4245 &&
            ProcUnsigned(readInfo, "flags", 8) == 0x80800 && ProcUnsigned(writeInfo, "flags", 8) == 0x80801 && number > 0 &&
            ProcUnsigned(readInfo, "ino", 10) == number && ProcUnsigned(writeInfo, "ino", 10) == number, "pipe_binding");
        return new(epoll, read, write, inode, epollInfo, readInfo, writeInfo, syscall, channel);
    }
    private static ulong ProcUnsigned(string text, string key, int radix)
    {
        MatchCollection values = Regex.Matches(text, @"(?m)^" + Regex.Escape(key) + @":\s*([0-9]+)[ \t]*$", RegexOptions.CultureInvariant);
        ScheduleNeed(values.Count == 1 && values[0].Groups[1].Length is > 0 and <= 20 &&
            values[0].Groups[1].Value.All(c => c >= '0' && c < '0' + radix), "proc_field");
        string value = values[0].Groups[1].Value;
        if (radix == 10) { ScheduleNeed(ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong number), "proc_integer"); return number; }
        return Convert.ToUInt64(value, 8);
    }
    private static ulong ScheduleHex(string value)
    {
        if (value.StartsWith("0x", StringComparison.Ordinal)) value = value[2..];
        ScheduleNeed(value.Length is > 0 and <= 16 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'), "hex");
        return Convert.ToUInt64(value, 16);
    }
    private static ulong ScheduleU64(JsonElement value)
    {
        ScheduleNeed(value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out _), "integer");
        ulong number = value.GetUInt64();
        ScheduleNeed(value.GetRawText() == number.ToString(CultureInfo.InvariantCulture), "integer"); return number;
    }
    private static void ScheduleNeed(bool condition, string reason)
    { if (!condition) throw new InvalidDataException("web_signalrace_schedule_" + reason); }
}
