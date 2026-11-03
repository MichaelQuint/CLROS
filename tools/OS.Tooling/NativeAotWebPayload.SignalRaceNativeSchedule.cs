using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace OS.Tooling;
// Controlled Linux-native experiment only; the selected v2/v3 controller owns each hook.
// No guest code uses this scheduling helper.
public static partial class NativeAotWebPayload
{
    /// <summary>Private Linux experiment owner borrowed from RunHttpAsync's
    /// actual strace Process. HTTP identities are untrusted selectors until
    /// joined to that child/session, executable and retained proc/pidfd handles.
    /// Owns only its journal and task handles, never the Process or app fds.
    /// The caller must serialize hooks, restore the tracer, stop/join the owned
    /// session in finally, then ConfirmStopped before disposal. No synthesized
    /// syscall result, kernel chronology or guest qualification is produced.</summary>

    private sealed partial class SignalRaceNativeSchedule : IDisposable
    {
        private const ulong ProbeTag = 0x50524f4245;
        private readonly Process process;
        private readonly string executable;
        private readonly FileStream journal;
        private readonly bool eventOwned;
        private NativeTask? tracer, app, waiter, actor;
        private PipeView? pipe;
        private bool prepared, releaseIssued, tracerChanged, actorChanged, stopped;
        private int records;
        internal SignalRaceNativeSchedule(Process ownedTracer, string expectedExecutable, string outputDirectory) : this(ownedTracer, expectedExecutable, outputDirectory, eventOwned: false)
        {
        }

        internal SignalRaceNativeSchedule(Process ownedTracer, string expectedExecutable, string outputDirectory, bool eventOwned)
        {
            Require(OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64, "signalrace_schedule_platform");
            process = ownedTracer;
            executable = Path.GetFullPath(expectedExecutable);
            this.eventOwned = eventOwned;
            string directory = Path.GetFullPath(outputDirectory);
            string output = Path.Combine(directory, "web-signalrace-schedule.jsonl");
            AuthorityFiles.RequirePlainPath(directory, directory, "signalrace_schedule_path");
            journal = new(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            try
            {
                AuthorityFiles.RequirePlainPath(directory, output, "signalrace_schedule_path");
            }
            catch
            {
                journal.Dispose();
                throw;
            }
        }

        // Called once after observing phase2, before starting churn. No native
        // mutation occurs until all identities, original waits and policies pass.
        internal async Task PrepareAsync(ulong[] status, CancellationToken cancellation)
        {
            Require(!prepared && tracer is null && status.Length == 6 && status[0] == 2 && status[1] is> 0 and <= int.MaxValue && status[2] is> 0 and <= int.MaxValue && status[3] is> 0 and <= int.MaxValue && status[2] != status[3] && status[4] == 0 && status[5] == 0, "signalrace_schedule_phase");
            // Actual ReadRaceStatus layout: phase,pid,waiter,actor,callback,recoveryRequested.
            // There is no public overload that treats supplied PIDs as ownership.
            cancellation.ThrowIfCancellationRequested();
            Require(!process.HasExited, "signalrace_schedule_tracer_exit");
            tracer = OpenTask(process.Id, process.Id);
            Require(tracer.Initial.Group == process.Id && tracer.Initial.Session == process.Id, "signalrace_schedule_session");
            string children = ReadBounded($"/proc/{process.Id}/task/{process.Id}/children");
            string[] childIds = children.Split((char[]? )null, StringSplitOptions.RemoveEmptyEntries);
            Require(childIds.Length == 1 && childIds[0] == status[1].ToString(CultureInfo.InvariantCulture), "signalrace_schedule_child");
            app = OpenTask((int)status[1], (int)status[1]);
            Require(app.Initial.Parent == process.Id && File.ResolveLinkTarget($"/proc/{app.Tid}/exe", true)?.FullName == executable, "signalrace_schedule_executable");
            waiter = OpenTask(app.Tid, (int)status[2]);
            actor = OpenTask(app.Tid, (int)status[3]);
            string online = ReadBounded("/sys/devices/system/cpu/online").Trim();
            Require(int.TryParse(online, NumberStyles.None, CultureInfo.InvariantCulture, out int cpu) && cpu is >= 0 and < 32768, "signalrace_schedule_one_cpu");
            foreach (NativeTask task in new[]
            {
                tracer!,
                app!,
                waiter!,
                actor!
            }

            )
            {
                Require(task.Initial.Group == process.Id && task.Initial.Session == process.Id, "signalrace_schedule_task_session");
                string taskStatus = ReadBounded($"/proc/{task.Pid}/task/{task.Tid}/status");
                Require(StatusInt(taskStatus, "Tgid") == task.Pid && (task == tracer || StatusInt(taskStatus, "TracerPid") == process.Id), "signalrace_schedule_task_owner");
                task.EnsureLive();
                int policy = Policy(task.Tid), priority = Priority(task.Tid);
                Require(policy == 0 && priority == 0, "signalrace_schedule_initial_policy");
                byte[] affinity = Affinity(task.Tid);
                Require(affinity[cpu / 8] == (byte)(1 << (cpu % 8)) && affinity.Where((_, index) => index != cpu / 8).All(value => value == 0), "signalrace_schedule_affinity");
                task.Cpu = cpu;
                Log("preflight", new { task.Pid, task.Tid, task.Initial, taskStatus, online, affinity = Convert.ToHexString(affinity), policy, priority });
            }

            var waitBudget = Stopwatch.StartNew();
            while (!IsSleepingEpoll())
            {
                Require(waitBudget.Elapsed < TimeSpan.FromMilliseconds(500), "signalrace_schedule_wait_not_blocked");
                await Task.Delay(5, cancellation);
            }

            pipe = ReadPipe();
            Log("initial-wait", new { waiter = waiter.Tid, pipe });
            cancellation.ThrowIfCancellationRequested();
            var tracerPolicy = Set(tracer, 1, 2);
            tracerChanged = true;
            Log("tracer-raised", tracerPolicy);
            cancellation.ThrowIfCancellationRequested();
            var actorPolicy = Set(actor, 1, 1);
            actorChanged = true;
            Log("actor-raised", actorPolicy);
            prepared = true;
            RecheckBeforeStart(cancellation);
        }

        // Run immediately before submitting the single start POST, after churn
        // is observed pending. The marker means intent to send, not guest receipt.
        internal void RecheckBeforeStart(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Require(prepared && !releaseIssued && !stopped, "signalrace_schedule_not_prepared");
            Require(!process.HasExited, "signalrace_schedule_tracer_exit");
            foreach (NativeTask task in new[]
            {
                tracer!,
                app!,
                waiter!,
                actor!
            }

            )
                task.EnsureLive();
            PolicyView[] policies = [ObservePolicy(tracer!), ObservePolicy(app!), ObservePolicy(waiter!), ObservePolicy(actor!)];
            Require(policies[0].Policy == 1 && policies[0].Priority == 2 && policies[3].Policy == 1 && policies[3].Priority == 1 && policies[1].Policy == 0 && policies[1].Priority == 0 && policies[2].Policy == 0 && policies[2].Priority == 0, "signalrace_schedule_policy_drift");
            foreach (NativeTask task in new[]
            {
                tracer!,
                app!,
                waiter!,
                actor!
            }

            )
                CheckAffinity(task);
            Require(IsSleepingEpoll(), "signalrace_schedule_wait_drift");
            PipeView observedPipe = ReadPipe();
            Require(observedPipe == pipe, "signalrace_schedule_wait_drift");
            Log("release-preflight", new { pipe = observedPipe, policies });
        }

        internal void MarkStartIssued()
        {
            Require(prepared && !releaseIssued, "signalrace_schedule_duplicate_start");
            releaseIssued = true;
            Log("start-post-intent", new { actor = actor!.Tid, actor.Initial.Start });
        }

        // Called at observed phase4, and again in RunHttpAsync.finally. Restore
        // only the still-owned tracer. The actor may already have exited, so
        // never target its recycled numeric TID for a post-release restoration.
        internal void RestoreTracer()
        {
            if (!tracerChanged)
                return;
            if (tracer!.Exited())
            {
                tracerChanged = false;
                Log("tracer-retired", new { tracer.Tid });
                return;
            }

            var restored = Set(tracer, 0, 0);
            tracerChanged = false;
            Log("tracer-restored", restored);
            if (waiter is not null && !waiter.Exited())
            {
                waiter.EnsureLive();
                Require(Policy(waiter.Tid) == 0 && Priority(waiter.Tid) == 0, "signalrace_schedule_waiter_policy");
                CheckAffinity(waiter);
            }

            if (actorChanged && actor!.Exited())
            {
                actorChanged = false;
                Log("actor-retired", new { actor.Tid, actor.Initial.Start, nativeExit = true, reapClaim = false });
            }
        }

        // Call only after root's existing StopSessionAsync confirms actual group
        // death. pidfd readiness names these exact old tasks, including a thread
        // whose numeric TID has subsequently been reused. No restoration is
        // claimed for settings released by task death. This does not claim reap.
        internal void ConfirmStopped()
        {
            Require(process.HasExited, "signalrace_schedule_session_live");
            foreach (NativeTask? task in new[]
            {
                tracer,
                app,
                waiter,
                actor
            }

            )
                if (task is not null)
                    Require(task.Exited(), "signalrace_schedule_original_task_live");
            stopped = true;
            tracerChanged = actorChanged = false;
            Log("owned-session-stopped", new { process.Id, nativeExit = true, reapClaim = false, tasks = new[] { tracer, app, waiter, actor }.OfType<NativeTask>().Select(t => new { t.Pid, t.Tid, t.Initial.Start, nativeExit = true }).ToArray() });
        }

        public void Dispose()
        {
            // The caller's finally is responsible for StopSession/ConfirmStopped
            // even if an earlier restoration or journal operation throws.
            actor?.Dispose();
            waiter?.Dispose();
            app?.Dispose();
            tracer?.Dispose();
            journal.Dispose();
        }

        private static NativeTask OpenTask(int pid, int tid)
        {
            // Disable FileStream buffering: seeking an old buffered proc file
            // could otherwise replay cached bytes instead of reading live state.
            var stat = new FileStream($"/proc/{pid}/task/{tid}/stat", FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1, FileOptions.None);
            SafeFileHandle? fd = null;
            try
            {
                using var reader = new StreamReader(stat, Encoding.UTF8, false, 1024, leaveOpen: true);
                string raw = reader.ReadToEnd();
                Require(raw.Length <= 65536, "signalrace_schedule_stat_size");
                Stat original = ParseStat(raw);
                Require(original.Tid == tid, "signalrace_schedule_tid");
                // Linux x64 pidfd_open, PIDFD_THREAD (O_EXCL): Linux >=6.9.
                // Unsupported kernels fail before any scheduling mutation.
                nint opened = NativePidfd(434, tid, 128);
                NativeCheck(opened >= 0 && opened <= int.MaxValue, "pidfd_open_thread");
                fd = new SafeFileHandle(opened, ownsHandle: true);
                var result = new NativeTask(pid, tid, stat, fd, original);
                result.EnsureLive();
                return result;
            }
            catch
            {
                fd?.Dispose();
                stat.Dispose();
                throw;
            }
        }

        private bool IsSleepingEpoll()
        {
            waiter!.EnsureLive();
            string current = ReadBounded($"/proc/{waiter.Pid}/task/{waiter.Tid}/syscall").Trim();
            if (waiter.Read().State != "S" || !current.StartsWith("232 ", StringComparison.Ordinal))
                return false;
            string channel = ReadBounded($"/proc/{waiter.Pid}/task/{waiter.Tid}/wchan").Trim();
            return channel is "ep_poll" or "do_epoll_wait";
        }

        private PipeView ReadPipe()
        {
            string prefix = $"/proc/{app!.Tid}";
            string syscall = ReadBounded($"{prefix}/task/{waiter!.Tid}/syscall").Trim();
            string channel = ReadBounded($"{prefix}/task/{waiter.Tid}/wchan").Trim();
            string[] args = syscall.Split((char[]? )null, StringSplitOptions.RemoveEmptyEntries);
            Stat waiterState = waiter.Read();
            // Preserve the pinned wrapper's raw register witness. All-ones is
            // a new profile expectation pending original native qualification,
            // not a universal Linux int(-1) encoding; never mask another value.
            Require(args.Length == 9 && args[0] == "232" && Hex(args[1]) <= int.MaxValue && Hex(args[3]) == 1 && Hex(args[4]) == (eventOwned ? ulong.MaxValue : 2500UL) && waiterState.State == "S" && channel is "ep_poll" or "do_epoll_wait", "signalrace_schedule_epoll_wait");
            int epoll = (int)Hex(args[1]);
            Require(new FileInfo($"{prefix}/fd/{epoll}").LinkTarget == "anon_inode:[eventpoll]", "signalrace_schedule_epoll_fd");
            string info = ReadBounded($"{prefix}/fdinfo/{epoll}");
            var registrations = Regex.Matches(info, @"(?m)^tfd:\s*(\d+)\s+events:\s*([0-9a-fA-F]+)\s+data:\s*([0-9a-fA-F]+)\s", RegexOptions.CultureInvariant);
            Require(registrations.Count == 1, "signalrace_schedule_epoll_registration");
            var entry = registrations[0];
            int read = int.Parse(entry.Groups[1].Value, CultureInfo.InvariantCulture);
            Require(Convert.ToUInt64(entry.Groups[2].Value, 16) == 0x19 && Convert.ToUInt64(entry.Groups[3].Value, 16) == ProbeTag, "signalrace_schedule_epoll_tag");
            string? inode = new FileInfo($"{prefix}/fd/{read}").LinkTarget;
            Require(inode is not null && Regex.IsMatch(inode, @"^pipe:\[\d+\]$", RegexOptions.CultureInvariant), "signalrace_schedule_pipe_inode");
            string readInfo = ReadBounded($"{prefix}/fdinfo/{read}");
            Require(PipeFlags(readInfo) == 0x80800, "signalrace_schedule_read_flags");
            string[] files = Directory.GetFileSystemEntries(prefix + "/fd");
            Require(files.Length <= 128, "signalrace_schedule_fd_bound");
            var matches = new List<(int Fd, string Info)>();
            foreach (string file in files)
            {
                if (new FileInfo(file).LinkTarget != inode)
                    continue;
                int number = int.Parse(Path.GetFileName(file), CultureInfo.InvariantCulture);
                if (number == read)
                    continue;
                string candidate = ReadBounded($"{prefix}/fdinfo/{number}");
                if (PipeFlags(candidate) == 0x80801)
                    matches.Add((number, candidate));
            }

            Require(matches.Count == 1, "signalrace_schedule_write_end");
            app.EnsureLive();
            waiter.EnsureLive();
            return new(epoll, read, matches[0].Fd, inode, info, readInfo, matches[0].Info, syscall, channel, waiterState);
        }

        private PolicyView Set(NativeTask task, int policy, int priority)
        {
            task.EnsureLive();
            NativeCheck(NativeSetScheduler(144, task.Tid, policy, ref priority) == 0, "sched_setscheduler");
            // Publish mutation ownership before any fallible readback/journaling.
            if (task == tracer)
                tracerChanged = policy != 0;
            if (task == actor)
                actorChanged = policy != 0;
            task.EnsureLive();
            var observed = ObservePolicy(task);
            Require(observed.Policy == policy && observed.Priority == priority, "signalrace_schedule_readback");
            CheckAffinity(task);
            return observed;
        }

        private static PolicyView ObservePolicy(NativeTask task) => new(task.Tid, task.Read().Start, Policy(task.Tid), Priority(task.Tid), Convert.ToHexString(Affinity(task.Tid)));
        private static void CheckAffinity(NativeTask task)
        {
            byte[] mask = Affinity(task.Tid);
            Require(mask[task.Cpu / 8] == (byte)(1 << (task.Cpu % 8)) && mask.Where((_, i) => i != task.Cpu / 8).All(b => b == 0), "signalrace_schedule_affinity_drift");
        }

        private static int Policy(int tid)
        {
            nint result = NativeGetScheduler(145, tid);
            NativeCheck(result >= 0, "sched_getscheduler");
            return checked((int)result);
        }

        private static int Priority(int tid)
        {
            NativeCheck(NativeGetParam(143, tid, out int result) == 0, "sched_getparam");
            return result;
        }

        private static byte[] Affinity(int tid)
        {
            byte[] mask = new byte[4096];
            NativeCheck(NativeGetAffinity(tid, (nuint)mask.Length, mask) == 0, "sched_getaffinity");
            return mask;
        }

        private static ulong Hex(string value) => Convert.ToUInt64(value.StartsWith("0x", StringComparison.Ordinal) ? value[2..] : value, 16);
        private static uint PipeFlags(string info)
        {
            var match = Regex.Match(info, @"(?m)^flags:\s*([0-7]+)\s*$", RegexOptions.CultureInvariant);
            Require(match.Success, "signalrace_schedule_fd_flags");
            return Convert.ToUInt32(match.Groups[1].Value, 8);
        }

        private static int StatusInt(string text, string name)
        {
            var value = Regex.Match(text, @"(?m)^" + Regex.Escape(name) + @":\s*(\d+)\s*$", RegexOptions.CultureInvariant);
            Require(value.Success, "signalrace_schedule_proc_status");
            return int.Parse(value.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static Stat ParseStat(string text)
        {
            int first = text.IndexOf('('), last = text.LastIndexOf(')');
            Require(first > 1 && last > first, "signalrace_schedule_proc_stat");
            string[] fields = text[(last + 1)..].Split((char[]? )null, StringSplitOptions.RemoveEmptyEntries);
            Require(fields.Length >= 20, "signalrace_schedule_proc_stat");
            int Number(int index) => int.Parse(fields[index], CultureInfo.InvariantCulture);
            return new(int.Parse(text[..first].Trim(), CultureInfo.InvariantCulture), Number(1), Number(2), Number(3), ulong.Parse(fields[19], CultureInfo.InvariantCulture), fields[0]);
        }

        private static string ReadBounded(string path)
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            byte[] bytes = new byte[65537];
            int total = 0, count;
            while (total < bytes.Length && (count = file.Read(bytes.AsSpan(total))) != 0)
                total += count;
            Require(total < bytes.Length, "signalrace_schedule_proc_size");
            return new UTF8Encoding(false, true).GetString(bytes, 0, total);
        }

        private void Log(string operation, object observation)
        {
            Require(++records <= 64, "signalrace_schedule_journal_bound");
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(new { schema = eventOwned ? "clros.web.signalrace.schedule.v2" : "clros.web.signalrace.schedule.v1", operation, observation, utc = DateTimeOffset.UtcNow, monotonic = Stopwatch.GetTimestamp(), frequency = Stopwatch.Frequency });
            Require(bytes.Length <= 65536 && journal.Length + bytes.Length < 1048576, "signalrace_schedule_journal_size");
            journal.Write(bytes);
            journal.WriteByte((byte)'\n');
            journal.Flush();
        }

        private static void NativeCheck(bool success, string operation)
        {
            if (!success)
                throw new IOException($"signalrace_schedule_{operation} errno={Marshal.GetLastPInvokeError()}");
        }

        [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
        private static extern nint NativePidfd(nint number, nint tid, nint flags);
        [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
        private static extern int NativePoll(ref PollFd descriptor, nuint count, int timeout);
        // musl's three POSIX scheduler exports are ENOSYS stubs. This owner is
        // Linux x64 only; its kernel sched_param is exactly one int priority.
        // libc syscall retains -1/errno semantics for the existing NativeCheck.
        [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
        private static extern nint NativeSetScheduler(nint number, nint tid, nint policy, ref int priority);
        [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
        private static extern nint NativeGetScheduler(nint number, nint tid);
        [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
        private static extern nint NativeGetParam(nint number, nint tid, out int priority);
        [DllImport("libc", EntryPoint = "sched_getaffinity", SetLastError = true)]
        private static extern int NativeGetAffinity(int tid, nuint bytes, [Out] byte[] mask);
    }
}
