using System.Globalization;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        private readonly string app;
        private readonly string[] rows;
        private readonly Func<ulong, bool> instruction, syscall;
        private readonly HashSet<ulong> exitTails;
        private readonly IReadOnlyList<int> writeLengths;
        private readonly Dictionary<int, LinuxCallWaitObservation> waits;
        private readonly Dictionary<int, LinuxCallReturnObservation> returns;
        private readonly HelloMemory memory;
        private readonly ulong userRoot, kernelRoot;
        private readonly int capacity, descriptorCapacity;
        private readonly bool staticFiles, diskStaticFull;
        private readonly bool broader, socket, web, service, containment, vectors, webLogQuota, webRaceTransport;
        private readonly NativeAotSocketContainmentGuest.Capture? containmentCapture;
        private readonly uint webSettingsBytes;
        private readonly int webSettingsFd;
        private readonly (ulong Identity, ulong Ordinal) webSettingsCall;
        private readonly IReadOnlyList<uint> writeChannels;
        private readonly uint entropyLimit, memoryPoolPages;
        private readonly RuntimePhases? phases;
        private LinuxThreadExecutionBudget budget;
        private readonly RuntimeThread?[] slots;
        private readonly ulong[] generations;
        private readonly Queue<RuntimeThread> ready = [];
        private readonly List<RuntimeThread> history = [];
        private int position, active = 1, nextTid = 2, clones, reaped, calls, writes, epochs, irqs, drains, ticks, transfers, normalExits, signalReturns, peak = 1, stdoutBytes;
        private ulong clock, waitOrder, addressOrder, currentBreak = 0x400000000;
        private bool stopping, heapIntact = true, barrierRegistered;
        private bool signalPipeCreated, stackProbe;
        private ulong entropyBytes;
        private readonly HashSet<int> files = [];
        private readonly HashSet<ulong> actions = [];
        private int pipeRead, pipeWrite, pipeBytes;
        private static readonly string[] Fields = "identity nr a0 a1 a2 a3 a4 a5 rip rsp flags fs gs clock valueBits action status".Split(' ').Select(k => k + ":dec").ToArray();
        internal ThreadingProtocol(string app, string[] rows, byte[] elf, Func<ulong, bool> instruction, Func<ulong, bool> syscall, IReadOnlyList<int> lengths, ulong userRoot, ulong kernelRoot, string? stdout = null, ulong hpetFrequencyHz = 0, NativeAotSocketGuest.Capture? socketCapture = null, uint webSettingsBytes = 0, int webSettingsFd = -1, (ulong Identity, ulong Ordinal) webSettingsCall = default, NativeAotSocketContainmentGuest.Capture? containmentCapture = null, bool webLogQuota = false, WebControlInspection? webControls = null, bool configuredServiceMemory = false, bool serviceQuotaFailure = false, bool webRaceTransport = false, WebRootDiskInventory.Inspection? diskInventory = null, bool diskStaticFull = false, WebRootDiskInventory.VerifiedTruncated? diskTruncated = null)
        {
            broader = app is "NativeAotSuspensionStress" or "NativeAotThreadPoolStress" or "NativeAotRuntimeMatrix" or "NativeAotRuntimeSmoke";
            containment = app == NativeAotSocketContainmentPublisher.Application;
            vectors = app == NativeAotSocketVectorsPublisher.Application;
            service = app == NativeAotServicePackage.Application;
            staticFiles = app == NativeAotStaticFilePublisher.Application;
            descriptorCapacity = staticFiles ? 32 : 16;
            Need(diskInventory is null || staticFiles, "disk_static_replay_application");
            Need(!diskStaticFull || diskInventory is not null && staticFiles && !service, "disk_static_full_replay_mode");
            this.diskStaticFull = diskStaticFull;
            this.diskInventory = diskInventory;
            Need(diskTruncated is null || diskStaticFull && ReferenceEquals(diskInventory, diskTruncated.Baseline), "disk_static_truncated_scope");
            this.diskTruncated = diskTruncated;
            descriptorGenerations = new uint[descriptorCapacity];
            Need(!serviceQuotaFailure || service && configuredServiceMemory, "threading_service_quota_mode");
            this.serviceQuotaFailure = serviceQuotaFailure;
            quotaImageBytes = elf.Length;
            Need(!configuredServiceMemory || service, "threading_configured_memory");
            memoryPoolPages = configuredServiceMemory ? OS.Manifest.LinuxAppConfiguration.ConfiguredServicePoolPages : LinuxMemoryServices.ApplicationPoolPages;
            web = staticFiles || service || app == "MinimalApiSmoke";
            socket = web || containment || vectors || app is "NativeAotSocketSmoke" or "NativeAotSocketSmokeAsync";
            Need(!webLogQuota || web && !service, "web_quota_application");
            this.webLogQuota = webLogQuota;
            Need(!webRaceTransport || web && !service && !webLogQuota && webControls is not null, "web_race_transport_application");
            this.webRaceTransport = webRaceTransport;
            this.webSettingsBytes = webSettingsBytes;
            this.webSettingsFd = webSettingsFd;
            this.webSettingsCall = webSettingsCall;
            Need(!web || staticFiles || webSettingsBytes is > 0 and <= 65536 && webSettingsFd is >= 3 and < 16 && webSettingsCall.Identity != 0 && webSettingsCall.Ordinal != 0, "web_replay_configuration");
            Need(broader || socket || app is "NativeAotSuspensionSmoke" or "NativeAotThreadPoolSmoke", "threading_application");
            Need(rows is not null && rows.Length >= 25 && rows.Length <= (diskStaticFull ? DiskStaticCapturePolicy.ProtocolRows : service ? 1_000_000 : 100000) && instruction is not null && syscall is not null && lengths is not null && lengths.Count is > 0 and <= 4096 && lengths.All(n => n > 0 && n <= (web ? 8192 : 4096)) && lengths.Sum() <= (diskStaticFull ? DiskStaticCapturePolicy.CombinedOutputBytes : web ? 65536 : vectors ? 32768 : 4096), "threading_protocol_input");
            this.app = app;
            this.instruction = instruction!;
            this.syscall = syscall!;
            writeLengths = lengths!.ToArray();
            exitTails = FindExitTails(elf, this.instruction, this.syscall);
            var diskCalls = diskInventory is null ? default : ReadLinuxDiskCallObservations(rows!, diskStaticFull);
            waits = (diskInventory is not null ? diskCalls.Waits : service ? ReadLinuxServiceCallWaits(rows!) : ReadLinuxCallWaits(rows!, allowEpoll: socket)).ToDictionary(x => x.RecordIndex);
            returns = (diskInventory is not null ? diskCalls.Returns : service ? ReadLinuxServiceCallReturns(rows!) : ReadLinuxCallReturns(rows!, allowEpoll: socket)).ToDictionary(x => x.RecordIndex);
            if (socket)
            {
                var messages = NativeAotSocketMessages.Read(rows!, returns);
                socketMessageInputs = messages.ToDictionary(x => x.RecordIndex);
                socketMessageCalls = messages.ToDictionary(x => x.CallRecordIndex);
            }

            this.rows = diskInventory is not null ? ValidateLinuxDiskCallOrdinals(rows!, diskStaticFull) : service ? ValidateLinuxServiceCallOrdinals(rows!) : ValidateLinuxCallOrdinals(rows!, allowEpoll: socket);
            this.userRoot = userRoot;
            this.kernelRoot = kernelRoot;
            Need(webControls is null || web && !service, "web_controls_application");
            this.webControls = webControls;
            if (webControls is not null)
                webLifetimes = SocketLifetimes(this.rows, true, webLogQuota ? 7 : 8, webControls.Sockets, webRaceTransport);
            memory = new(elf, memoryPoolPages);
            capacity = broader || socket ? 16 : app == "NativeAotSuspensionSmoke" ? 4 : 8;
            entropyLimit = service ? 0U : web ? 4096U : broader || socket ? 256U : 32U;
            Need(containment == (containmentCapture is not null) && (socket && !containment) == (socketCapture is not null), "socket_protocol_capture");
            this.containmentCapture = containmentCapture;
            socketPolicy = vectors && NativeAotSocketVectorsOutput.Read(socketCapture!.Output.Stdout).Policy;
            if (socketPolicy)
                _ = VectorSocketPolicyCalls(rows!);
            if (socket)
                socketObservations = (containment ? containmentCapture!.Scheduling : socketCapture!).Readiness.ToDictionary(x => x.RecordIndex);
            writeChannels = web ? socketCapture!.Output.WriteChannels : Enumerable.Repeat(1U, writeLengths.Count).ToArray();
            if (broader)
            {
                Need(stdout is not null, "runtime_stdout_missing");
                phases = new RuntimePhases(app, stdout!, lengths!, hpetFrequencyHz);
            }
            else
                Need(stdout is null, "threading_unexpected_stdout");
            slots = new RuntimeThread?[capacity];
            generations = Enumerable.Repeat(1UL, capacity).ToArray();
            var main = new RuntimeThread(0x100000001, 1, 0x70000000 - 1572864, 0x70000000, 0, Entry(elf));
            slots[0] = main;
            history.Add(main);
            ready.Enqueue(main);
            if (staticFiles)
                StaticInitialize(socketCapture!.Output);
        }

        private static ulong Entry(byte[] elf) => System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(elf.AsSpan(24, 8));
        private static ulong Bits(long value) => unchecked((ulong)value);
        private bool webReturningToSupervisor;
        private string Take(string expected = "record")
        {
            if (web && !service && !staticFiles && position == rows.Length)
                throw new WebBoundary(expected);
            Need(position < rows.Length, "threading_truncated");
            return rows[position++];
        }

        private bool Starts(string prefix) => position < rows.Length && rows[position].StartsWith(prefix, StringComparison.Ordinal);
        private void Exact(string expected) => Need(Take(expected) == expected, "threading_order_" + expected);
        private Dictionary<string, ulong> Row(string prefix, params string[] fields) => ReadRecord(Take(prefix), prefix, fields);
        internal ThreadingProtocolReport Validate()
        {
            Exact("TEST_START m16-009");
            LinuxMemoryPolicy(Take(), entropyLimit, memoryPoolPages, continuingService: service);
            int bytes = staticFiles ? NativeAotStaticFileProgram.ElfBytes : service ? (serviceQuotaFailure ? quotaImageBytes : NativeAotServiceProgram.ElfBytes) : web ? NativeAotWebProgram.ElfBytes : containment ? NativeAotSocketContainmentPackage.ElfBytes : vectors ? NativeAotSocketVectorsPackage.ElfBytes : socket ? 2117424 : broader ? NativeAotRuntimePackage.Specification(app).Bytes : app == "NativeAotSuspensionSmoke" ? 1320176 : 1577712;
            Exact("M16_007_POLICY descriptors=" + descriptorCapacity + " pipes=2 capacity=4096 scratch=8192 packageBytes=" + bytes + " package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler");
            if (socket)
                Exact("M16_011_BOUND connections=" + (web && !service ? 16 : 8) + " rx=8192 tx=32768 epoll=4 watches=64 nic=live");
            var armed = LinuxSyscallArm(Take());
            const string policy = " timer=user-irq-and-idle clock=hpet-nanoseconds reuse=off-stack";
            if (diskInventory is not null)
                DiskStaticBegin();
            string line = Take();
            Need(line.EndsWith(policy, StringComparison.Ordinal), "threading_bound_policy");
            var bound = ReadRecord(line[..^policy.Length], "M16_008_BOUND", "root:dec", "pool:dec", "workers:dec", "code:dec", "metadata:dec", "userImage:dec");
            uint poolPages = web ? WebThreadPoolPages : LinuxThreadProgram.SignalPoolPages;
            Need(armed["root"] == userRoot && bound["root"] == kernelRoot && kernelRoot is >= 4096 and <= 0xfffff000 && kernelRoot % 4096 == 0 && bound["pool"] >= 4096 && bound["pool"] <= 0x100000000UL - poolPages * 4096UL && bound["pool"] % 4096 == 0 && bound["workers"] == (ulong)capacity && bound["code"] == LinuxThreadProgram.Bytes && bound["metadata"] == LinuxThreadProgram.MetadataPages * 4096UL && bound["userImage"] == 728, "threading_parent_geometry");
            DistinctExtents([(userRoot, 4096), (kernelRoot, 4096), (bound["pool"], poolPages * 4096UL)], "threading_roots_pool");
            if (service)
                ServiceBegin();
            else
            {
                var b = Row("M16_008_BUDGET", "started:dec", "epochs:dec", "steps:dec", "nanos:dec");
                Need(budget.Begin((uint)capacity, b["started"], application: true, continuingWeb: web, diskStaticFull: diskStaticFull) && b["epochs"] == budget.MaximumEpochs && b["steps"] == budget.MaximumSteps && b["nanos"] == budget.ElapsedLimitNanoseconds, "runtime_budget_policy");
            }

            if (staticFiles)
                Exact("TEST_START m10." + (diskInventory is null ? "017" : "018"));
            if (diskStaticFull)
                Exact("M10_018_POLICY profile=DiskStaticFullV1 version=" + DiskStaticExecutionPolicy.Version + " calls=" + DiskStaticExecutionPolicy.MaximumCalls + " requestNanos=" + DiskStaticExecutionPolicy.RequestDelayNanoseconds + " graceNanos=" + DiskStaticExecutionPolicy.GraceNanoseconds + " runtimeNanos=" + DiskStaticExecutionPolicy.RuntimeNanoseconds + " renewal=none");
            while (active != 0)
            {
                if (staticFiles)
                    StaticSupervisor();
                if (service && (!serviceQuotaFailure || !quotaAborted))
                    ServiceProgress();
                SocketObservations(supervisor: true, allowDiskPollBatch: true);
                // Idle polling may stop at the next owned static request row.
                // Consume that real supervisor boundary before judging an empty
                // FIFO, then replay its actual post-request socket/clock polls.
                if (staticFiles && Starts(StaticTag + "_TERMINATE "))
                {
                    StaticSupervisor();
                    SocketObservations(supervisor: true, allowDiskPollBatch: true);
                }

                if (web && !service && !staticFiles && position == rows.Length)
                    throw new WebBoundary("dispatch_or_idle");
                Need((uint)(transfers / 2 + ticks + socketIdlePolls) < budget.MaximumSteps, "threading_transfer_bound");
                if (ready.Count == 0)
                {
                    if (socket)
                        throw new ToolFailure("socket_no_unowned_idle");
                    Need(!stopping && history.Any(t => !t.Reaped && t.WaitSource == 1 && t.Completion is null && t.Deadline != 0), "threading_no_unowned_idle");
                    var tick = Row("M16_008_TICK", "epoch:dec", "nanos:dec", "ready:dec");
                    Epoch(tick);
                    ticks++;
                    Need(tick["ready"] == (ulong)ready.Count, "threading_tick_ready");
                    continue;
                }

                RuntimeThread t = ready.Dequeue();
                Need(!t.Reaped, "threading_selected_generation");
                socketSelected = t;
                bool exiting = stopping || t.Exiting, ending = false, prepareBlocked = false;
                ulong action = exiting ? 4UL : 1UL;
                if (!exiting)
                {
                    if (t.WaitSource == 3 && SocketPrepare(t))
                    {
                        ending = prepareBlocked = true;
                        action = 3;
                    }

                    if (t.WaitSource == 4 && DiskStaticPrepare(t))
                    {
                        ending = prepareBlocked = true;
                        action = 3;
                    }

                    if (t.WaitSource != 0 && !prepareBlocked)
                    {
                        Need(t.Completion.HasValue, "threading_unwon_resume");
                        long completion = t.Completion.GetValueOrDefault();
                        var resume = Row("M16_008_RESUME", "identity:dec", "valueBits:dec", "status:dec");
                        Need(resume["identity"] == t.Identity && resume["valueBits"] == Bits(completion) && resume["status"] == 1, "threading_resume");
                        if (t.WaitSource == 1 && completion == 0)
                            phases?.FutexResumed(t.Identity, t.LastSequence, clock);
                        if (t.WaitSource == 2 && completion > 0)
                        {
                            Need(pipeBytes >= completion, "threading_pipe_completion_bytes");
                            pipeBytes -= (int)completion;
                        }

                        if (t.WaitSource == 1 && completion == -4 && t.WaitCall is { Timeout: not 0 } replay)
                            t.TimedRestart = replay;
                        t.WaitSource = 0;
                        t.WaitAddress = t.Deadline = 0;
                        t.Completion = null;
                        t.Epoll = null;
                        t.EpollMaximum = t.EpollWinner = 0;
                        t.EpollObserved = t.EpollEligible = false;
                        t.Disk = null;
                    }

                    if (!prepareBlocked)
                        ExpectDelivery(t);
                    bool exitTailContinued = false;
                    while (!prepareBlocked)
                    {
                        SocketObservations(allowDiskPollBatch: true);
                        SocketMessageInput(t);
                        if (staticFiles)
                            StaticBeforeCall(t);
                        if (t.TailRip != 0 && Starts("M16_009_IRQ "))
                        {
                            Need(!exitTailContinued, "threading_exit_tail_irq_repeat");
                            var tail = Row("M16_009_IRQ", "identity:dec", "epoch:dec", "nanos:dec", "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "valueBits:dec");
                            Need(tail["identity"] == t.Identity && instruction(tail["rip"]), "threading_exit_tail_irq_binding");
                            Envelope(t, tail["rip"], tail["rsp"], tail["flags"], tail["fs"], tail["gs"], 0);
                            Need(tail["valueBits"] == (tail["rip"] == t.TailRip + 8 ? 60UL : 0), "threading_exit_tail_rax");
                            Epoch(tail);
                            irqs++;
                            t.LastUserIrq = (ulong)epochs;
                            t.SavedRsp = tail["rsp"];
                            t.SavedRip = tail["rip"];
                            exitTailContinued = true;
                            continue; // Actual direct IRQ continuation; no FIFO enqueue/transfer.
                        }

                        if (!Starts("M16_008_CALL ") && !Starts("M16_005_WRITE"))
                            break;
                        bool output = Starts("M16_005_WRITE");
                        if (output)
                        {
                            Need(writes < writeLengths.Count, "threading_extra_write");
                            string prefix = "M16_005_WRITE fd=" + writeChannels[writes].ToString(CultureInfo.InvariantCulture) + " bytes=" + writeLengths[writes].ToString(CultureInfo.InvariantCulture) + " data=";
                            Need(Take().StartsWith(prefix, StringComparison.Ordinal), "threading_write_envelope");
                        }

                        int index = position;
                        var r = Row("M16_008_CALL", Fields);
                        Need(r["identity"] == t.Identity && syscall(r["rip"]) && output == ((r["nr"] == 1 || web && r["nr"] == 20) && (web ? r["a0"] is 1 or 2 && unchecked((long)r["valueBits"]) > 0 : r["a0"] == 1)), "threading_call_binding");
                        SignalObservation(t, r, returns[index]);
                        Need((r["flags"] & 0x400) == 0, "threading_syscall_df");
                        if (t.TailRip != 0)
                        {
                            Envelope(t, r["rip"], r["rsp"], r["flags"], r["fs"], r["gs"], r["nr"]);
                            Need(r["nr"] == 60, "threading_exit_tail_syscall");
                        }

                        Call(t, r, waits[index], output);
                        DiskStaticSocketQueued(r, index);
                        if (staticFiles)
                            StaticAfterCall(t, r, waits[index], output);
                        ObserveWebSignalRaceCall(t, r, waits[index]);
                        if (serviceQuotaFailure)
                            QuotaSocketObservation(r, returns[index]);
                        Envelope(t, r["rip"], r["rsp"], r["flags"], r["fs"], r["gs"], r["nr"]);
                        t.SavedRsp = returns[index].Rsp;
                        t.SavedRip = returns[index].Rip;
                        if (Starts("M16_008_SYSCALL_TIME "))
                        {
                            var timing = LinuxSyscallTiming(Take(), t.Identity, waits[index].Ordinal, r["nr"]);
                            if (r["nr"] == 228 && r["a0"] == 1 && r["valueBits"] == 0)
                                phases?.ClockRead(timing);
                        }

                        ExpectDelivery(t);
                        SocketObservations(allowDiskPollBatch: true);
                        calls++;
                        ending = r["action"] is 2 or 3 or 4 or 6;
                        exiting = r["action"] is 2 or 4;
                        action = r["action"];
                        if (serviceQuotaFailure && Starts("M16_016_QUOTA_ABORT "))
                        {
                            QuotaAbort(t, r, waits[index]);
                            ending = exiting = true;
                            action = 4;
                        }

                        if (ending)
                            break;
                    }

                    if (prepareBlocked)
                    {
                        t.Parking = false;
                        if (t.Completion.HasValue)
                            Enqueue(t);
                    }
                    else if (ending)
                    {
                        if (exitTailContinued)
                            Need(exiting && action == 4 && t.NormalExit, "threading_exit_tail_irq_exit");
                        else
                        {
                            if (Starts("M16_009_CANCEL "))
                            {
                                Need(diskStaticFull, "threading_timer_cancel_profile");
                                var cancelled = Row("M16_009_CANCEL", "identity:dec", "epoch:dec", "nanos:dec");
                                Need(cancelled["identity"] == t.Identity && cancelled["epoch"] == (ulong)epochs &&
                                    cancelled["nanos"] >= budget.StartedNanoseconds && (!stopping || cancelled["nanos"] == clock),
                                    "threading_timer_cancel_binding");
                                Clock(cancelled["nanos"]);
                            }
                            else
                            {
                                var drain = Row("M16_009_DRAIN", "identity:dec", "epoch:dec", "nanos:dec");
                                Need(drain["identity"] == t.Identity && (!stopping || drain["nanos"] == clock), "threading_drain_binding");
                                Epoch(drain);
                                drains++;
                            }
                            if (action == 6 || !exiting && t.Completion.HasValue)
                                Enqueue(t);
                            t.Parking = false;
                        }
                    }
                    else
                    {
                        var irq = Row("M16_009_IRQ", "identity:dec", "epoch:dec", "nanos:dec", "rip:dec", "rsp:dec", "flags:dec", "fs:dec", "gs:dec", "valueBits:dec");
                        Need(irq["identity"] == t.Identity && instruction(irq["rip"]), "threading_irq_binding");
                        Envelope(t, irq["rip"], irq["rsp"], irq["flags"], irq["fs"], irq["gs"], 0);
                        if (t.TailRip != 0)
                            Need(irq["valueBits"] == (irq["rip"] == t.TailRip + 8 ? 60UL : 0), "threading_exit_tail_rax");
                        Epoch(irq);
                        irqs++;
                        t.LastUserIrq = (ulong)epochs;
                        t.SavedRsp = irq["rsp"];
                        t.SavedRip = irq["rip"];
                        Enqueue(t);
                        action = 1;
                    }
                }

                if (exiting)
                {
                    var exit = Row("M16_008_EXIT", "identity:dec", "clearTidResult:dec");
                    ulong clear = t.ClearTid == 0 || memory.Covered(t.ClearTid, 4, 3) ? 0 : Bits(-14);
                    Need(exit["identity"] == t.Identity && exit["clearTidResult"] == clear, "threading_clear_tid");
                    if (t.ClearTid != 0)
                        Wake(t.ClearTid, 0, 1, uint.MaxValue);
                }

                webReturningToSupervisor = web;
                transfers += 2;
                var supervisor = Row("M16_008_SUPERVISOR", "identity:dec", "action:dec", "transfers:dec", "ready:dec", "active:dec");
                Need(supervisor["identity"] == t.Identity && supervisor["action"] == action && supervisor["transfers"] == (ulong)transfers && supervisor["ready"] == (ulong)ready.Count && supervisor["active"] == (ulong)active, "threading_supervisor");
                webReturningToSupervisor = false;
                if (Starts("M16_008_TIMING "))
                {
                    Need(action == 1 && !ending && !exiting, "threading_timing_irq_only");
                    LinuxTiming(Take(), t.Identity, (ulong)epochs);
                }

                if (exiting)
                {
                    const string scrub = " context=zero stack=zero slot=zero";
                    string reap = Take();
                    Need(reap.EndsWith(scrub, StringComparison.Ordinal), "threading_reap_scrub");
                    var re = ReadRecord(reap[..^scrub.Length], "M16_008_REAP", "old:dec", "next:dec");
                    Need(re["old"] == t.Identity && re["next"] == t.Identity + (1UL << 32), "threading_reap_generation");
                    int slot = (int)(uint)t.Identity - 1;
                    generations[slot]++;
                    slots[slot] = null;
                    t.Reaped = true;
                    t.WaitSource = 0;
                    t.Completion = null;
                    t.Disk = null;
                    active--;
                    reaped++;
                    if (service)
                        Need(budget.ForgetWorker((uint)slot), "service_reaped_budget_slot");
                    if (t.NormalExit)
                        phases?.Reaped();
                }

                socketSelected = null;
            }

            Need(!web || service || staticFiles, "web_replay_unexpected_termination");
            Need(stopping && reaped == clones + 1 && writes == writeLengths.Count && irqs > 0 && history.All(t => t.Reaped), "threading_complete_lifetimes");
            if (broader)
            {
                Need(app == "NativeAotRuntimeSmoke" ? peak >= 5 && normalExits >= 1 && clones >= 5 : app == "NativeAotSuspensionStress" ? peak >= 8 && normalExits >= 15 && clones >= 17 : app == "NativeAotRuntimeMatrix" ? peak >= 9 && normalExits >= 6 : peak >= 5 && clones >= 4, "runtime_workload_lifetimes");
                phases!.Finish();
            }
            else if (socket)
            {
                if (serviceQuotaFailure)
                    QuotaCloseDescriptors();
                else
                    SocketFinish();
            }
            else
                Need(app == "NativeAotSuspensionSmoke" ? clones >= 3 && normalExits >= 1 && signalReturns >= 2 && history.Any(t => t.NormalExit && t.Returned >= 2) : clones >= 5, "threading_workload_lifetimes");
            if (serviceQuotaFailure)
                QuotaMemoryEnd();
            var finished = Row("M16_008_BUDGET_END", "nanos:dec");
            // Quota cancellation freezes admission before the final syscall;
            // QuotaFinish binds the fresh post-reap clock to both observations.
            Need((serviceQuotaFailure ? finished["nanos"] >= budget.StartedNanoseconds : finished["nanos"] >= clock) && (!broader || finished["nanos"] >= phases!.LatestClock && phases.EarliestClock >= budget.StartedNanoseconds) && (service ? budget.CancellationRequested && finished["nanos"] <= long.MaxValue : budget.Observe(finished["nanos"])), "runtime_budget_complete");
            if (service)
                serviceBudgetFinished = finished["nanos"];
            // Group cancellation retires pending/active signal state during
            // actual off-stack reap. It does not fabricate a user sigreturn.
            Need(history.All(t => t.GroupRetired || t.Delivered == t.Returned && t.Requests == 0 && !t.PendingUsr2 && !t.PendingTermination && t.DeliverySignal == 0), "threading_unreturned_signal");
            Need(entropyBytes > 0 && barrierRegistered && signalPipeCreated && stackProbe && files.Count == 0 && new ulong[] { 2, 3, 8, 11, 13, 18, 35 }.All(actions.Contains), "threading_runtime_startup");
            const string cleanup = " userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained";
            string owner = Take();
            Need(owner.EndsWith(cleanup, StringComparison.Ordinal), "threading_owner_cleanup");
            var end = ReadRecord(owner[..^cleanup.Length], "M16_008_OWNER_END", "reaped:dec", "transfers:dec", "irqs:dec", "exit:dec");
            Need(end["reaped"] == (ulong)reaped && end["transfers"] == (ulong)transfers && end["irqs"] == (ulong)epochs && end["exit"] == (serviceQuotaFailure ? 99UL : 0UL), "threading_owner_accounting");
            if (serviceQuotaFailure)
                QuotaFinish();
            else
            {
                if (service)
                    ServiceFinish();
                if (staticFiles)
                    StaticFinish(finished["nanos"], reaped);
                Exact("M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true");
                Exact("CLROS_M16_009_READY");
                Exact("TEST_PASS m16-009");
            }

            Need(position == rows.Length, "threading_extra_record");
            return new(calls, clones, reaped, irqs, drains, ticks, transfers, signalReturns);
        }

        private void Envelope(RuntimeThread t, ulong rip, ulong rsp, ulong flags, ulong fs, ulong gs, ulong number)
        {
            if (t.TailRip != 0)
            {
                ExitEnvelope(t, rip, rsp, flags, fs, gs, number);
                return;
            }

            Need(rsp >= t.Bottom + 8 && rsp <= t.Top && memory.Covered(rsp - 8, 8, 3) && fs == t.Fs && gs == t.Gs && LinuxSyscallGateway.ValidateUserFlags(flags, true), "threading_user_envelope");
            if (fs != 0)
                Need(memory.Covered(fs, 8, 1), "threading_live_tls");
        }

        private void Enqueue(RuntimeThread t)
        {
            Need(!ready.Contains(t) && !t.Reaped, "threading_duplicate_ready");
            ready.Enqueue(t);
        }

        private static uint PendingSignal(RuntimeThread t) => t.PendingUsr2 && (t.Mask & (1UL << 11)) == 0 ? 12U : t.PendingTermination && (t.Mask & (1UL << 14)) == 0 ? 15U : t.Requests > 0 && (t.Mask & (1UL << 34)) == 0 ? 35U : 0;
        private void ExpectDelivery(RuntimeThread t)
        {
            // Native PrepareUserReturn runs at dispatch and after every returning
            // call. Selection consumes one pending entry now; keep its number,
            // proof and interrupted RSP across IRQs before the first handler CALL.
            // Standard12 coalesces; the independent realtime35 queue has four slots.
            if (t.Exiting || t.WaitSource != 0 || t.DeliverySignal != 0)
                return;
            uint signal = PendingSignal(t);
            if (signal == 0)
                return;
            t.DeliverySignal = signal;
            t.DeliveryRsp = t.SavedRsp;
            if (signal == 12)
                t.PendingUsr2 = false;
            else if (signal == 15)
                t.PendingTermination = false;
            else
            {
                t.Requests--;
                Need(phases is null || t.SignalProofs.Count != 0, "runtime_signal_request_evidence");
                t.DeliveryProof = phases is null ? null : t.SignalProofs.Dequeue();
            }
        }

        // Publishing a new wait rechecks the real pending queue. An already-won
        // readiness completion stays intact until Prepare actually reparks it.
        private void InterruptPendingWait(RuntimeThread t)
        {
            if (t.Exiting || t.WaitSource == 0 || t.Completion.HasValue || PendingSignal(t) == 0)
                return;
            if (t.WaitSource == 3)
                t.EpollWinner = 5;
            if (t.WaitSource == 4)
            {
                Need(t.Disk is not null, "disk_static_interrupt_owner");
                t.Disk!.Winner = 5;
            }

            Complete(t, -4);
        }

        private void Complete(RuntimeThread t, long result, List<RuntimeThread>? deferred = null)
        {
            if (t.Completion.HasValue)
                return;
            t.Completion = result;
            if (!t.Parking)
            {
                if (deferred is null)
                    Enqueue(t);
                else
                    deferred.Add(t);
            }
        }

        private void Clock(ulong now)
        {
            if (now < clock || now > long.MaxValue)
                throw new ToolFailure("threading_clock", $"position={position} previous={clock} now={now} calls={calls} row={rows[Math.Min(position, rows.Length - 1)]}");
            clock = now;
            if (service)
                ServiceClock(now, ref budget);
            else if (now != 0)
                Need(budget.Observe(now), "runtime_budget_clock");
            foreach (var t in history.Where(t => !t.Reaped && t.WaitSource is 1 or 3 && t.Completion is null && t.Deadline != 0 && t.Deadline <= now).OrderBy(t => t.Deadline).ThenBy(t => t.WaitOrder))
            {
                if (t.WaitSource == 3)
                {
                    t.EpollWinner = 2;
                    Complete(t, long.MinValue);
                }
                else
                {
                    phases?.Deadline(t.Deadline, now);
                    Complete(t, -110);
                }
            }
        }

        private void Epoch(Dictionary<string, ulong> row)
        {
            Need(++epochs <= budget.MaximumEpochs && row["epoch"] == (ulong)epochs && row["nanos"] >= budget.StartedNanoseconds, "threading_epoch");
            Clock(row["nanos"]);
        }

        private int Wake(ulong address, uint mode, uint count, uint bits, bool explicitWake = false)
        {
            var selected = history.Where(t => !t.Reaped && t.WaitSource == 1 && t.Completion is null && t.WaitAddress == address && t.WaitMode == mode && (t.WaitBits & bits) != 0).OrderBy(t => t.AddressOrder).Take((int)Math.Min(count, int.MaxValue)).ToArray();
            foreach (var t in selected)
            {
                Complete(t, 0);
                if (explicitWake)
                    phases?.FutexWoken(t.Identity, t.LastSequence, clock);
            }

            return selected.Length;
        }

        private int Requeue(ulong source, ulong destination, uint mode, uint wake, uint move)
        {
            int count = Wake(source, mode, wake, uint.MaxValue);
            var selected = history.Where(t => !t.Reaped && t.WaitSource == 1 && t.Completion is null && t.WaitAddress == source && t.WaitMode == mode).OrderBy(t => t.AddressOrder).Take((int)Math.Min(move, int.MaxValue)).ToArray();
            if (source != destination)
                foreach (var t in selected)
                {
                    // Address FIFO changes; the scheduler's original deadline
                    // insertion order, identity, sequence and completion do not.
                    t.WaitAddress = destination;
                    t.AddressOrder = ++addressOrder;
                }

            return count + selected.Length;
        }
    }
}
