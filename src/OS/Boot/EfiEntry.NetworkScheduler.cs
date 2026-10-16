#if M07_026_PROBE
using OS.Kernel.Threading;
using OS.Pal;
using Cooperative = OS.Kernel.Threading.ThreadContextSwitch;
using SavedContext = OS.Kernel.Threading.X64ThreadContext;
using Program = OS.Kernel.Threading.KernelThreadProgram;
using ProgressProgram = OS.Kernel.Threading.KernelThreadProgressProgram;
using Lifecycle = OS.Kernel.Threading.KernelThreadTable;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Pool pages/context offsets and live admission are the accepted M09
        // layout. One persistent ready worker runs on its guarded stack; no
        // timer IRQ, arbitrary entry, firmware reclamation or SMP is supplied.
        private struct NetworkSchedulerControl
        {
            internal Storage* State;
            internal Program.Plan Plan;
            internal Lifecycle Threads;
            internal KernelThreadSlot Slot;
            internal OSHostHandle Ready, Worker;
            internal DeadlineQueue.Entry Deadline;
            internal ulong Key, Progress, PacketDispatches, TimeWaitDispatches, LastProgress, NextDue, MaximumGap;
            internal uint Stop, Bound, Selected, Failed, Transfers;
            internal fixed ulong Cpu[19];
        }

        private static bool RunNetworkScheduledService(Storage* state)
        {
            if (!CooperativeAuthority(state) || state->CooperativePhase != 0) return NetworkSchedulerFailure(state, 1);
            ((delegate* unmanaged<void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeSegmentsOffset))();
            if (!CooperativeAuditPool(state, false)) return NetworkSchedulerFailure(state, 2);
            if (!NetworkSchedulerPrepare(state, out var c)) return false;
            // The original PTE snapshot stays on this live supervisor frame for
            // the complete service. It never escapes into a returned stack.
            ulong* original = stackalloc ulong[(int)CooperativePoolPages];
            bool captured = false, success = false;
            if (CooperativeSetPoolMappings(state, original, true, out captured))
            {
                c->Plan.Owner.GuardsVerified = c->Plan.Owner.MappingsVerified = true;
                NetworkSchedulerSync(c);
                bool bound = NetworkSchedulerBind(c);
                if (!bound) _ = NetworkSchedulerFailure(state, 4);
                if (bound && NetworkSchedulerRow(state, 0, state->CooperativePool, 32, 1, 0))
                {
                    state->CooperativePhase = 2;
                    success = RunNetworkServiceCore(state);
                }
            }
            else _ = NetworkSchedulerFailure(state, 3);
            bool stopped = c->Bound == 0 || NetworkSchedulerStop(c);
            ulong progress = c->Progress, packets = c->PacketDispatches, waiting = c->TimeWaitDispatches;
            ulong gap = c->MaximumGap, transfers = c->Transfers;
            success &= stopped && c->Bound == 2 && packets >= 2 && waiting >= 2 && progress == packets + waiting &&
                gap > 0 && gap <= 1000000000 && transfers == 2 * (progress + 1);
            // A failed transfer retains its backing/mappings. Only the freshly
            // confirmed supervisor may restore guards/code and scrub the pool.
            if (!stopped || captured && !CooperativeSetPoolMappings(state, original, false, out _)) return false;
            if (!captured) return false;
            if (!CooperativeAuditPool(state, false)) return false;
            for (uint i = 0; i < CooperativePoolPages * Page; i++) ((byte*)state->CooperativePool)[i] = 0;
            for (uint i = 0; i < CooperativePoolPages * Page; i++) if (((byte*)state->CooperativePool)[i] != 0) return false;
            state->CooperativePhase = success ? 3U : 4U;
            return success && NetworkSchedulerRow(state, 1, progress, packets, waiting, gap) &&
                NetworkSchedulerRow(state, 2, transfers, 1, 0, 32);
        }

        private static bool NetworkSchedulerPrepare(Storage* state, out NetworkSchedulerControl* c)
        {
            ulong pool = state->CooperativePool, arena = pool + 2 * Page;
            c = (NetworkSchedulerControl*)(arena + Page);
            if (sizeof(NetworkSchedulerControl) > 0xe00) return NetworkSchedulerFailure(state, 10);
            *c = default; c->State = state; c->Key = pool ^ 0x4d30373032365343UL;
            c->Plan.Owner = new() { Initialized = true, Key = c->Key, CurrentKey = c->Key,
                Root = (ulong)state->Memory.Entries, ContextArena = arena, ContextArenaBytes = 2 * Page,
                CodeStart = pool, CodeBytes = Program.Bytes, SwitchStackGuardStart = pool + 4 * Page, SwitchStackBytes = 10 * Page };
            if (!NetworkSchedulerObserve(c)) return NetworkSchedulerFailure(state, 11);
            byte* rawFx = (byte*)arena + 0xe00;
            ((delegate* unmanaged<byte*, void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeFxOffset))(rawFx);
            c->Plan.Owner.ObservedMxcsrMask = *(uint*)(rawFx + 28);
            uint mask = c->Plan.Owner.ObservedMxcsrMask;
            SavedContext* supervisor = (SavedContext*)arena, a = (SavedContext*)(arena + 0x300), b = (SavedContext*)(arena + 0x600);
            if (SavedContext.Initialize(supervisor, SavedContext.Bytes, pool + Program.DriverResume, (state->Arena.StackTop - 64) & ~15UL, mask) != SavedContext.Status.Ok ||
                SavedContext.Initialize(a, SavedContext.Bytes, pool + Program.EntryA, pool + 16 * Page - 40, mask) != SavedContext.Status.Ok ||
                SavedContext.Initialize(b, SavedContext.Bytes, pool + Program.EntryB, pool + 19 * Page - 40, mask) != SavedContext.Status.Ok) return NetworkSchedulerFailure(state, 12);
            byte* helper = (byte*)arena + 0xc00;
            for (uint i = 0; i < 512; i++) helper[i] = 0;
            *(ushort*)helper = 0x027f; *(uint*)(helper + 24) = 0x1f80; *(uint*)(helper + 28) = mask;
            c->Plan.Supervisor = new() { Identity = 0x1ffffffff, CurrentIdentity = 0x1ffffffff,
                Context = supervisor, ContextBytes = SavedContext.Bytes,
                StackGuardStart = (ulong)state->Arena.Storage + 21 * Page, StackBytes = 66 * Page,
                ResumeRip = pool + Program.DriverResume, Kind = Cooperative.ResumeKind.CapturedCall };
            c->Plan.A = new() { Identity = 0x100000001, CurrentIdentity = 0x100000001, Context = a, ContextBytes = SavedContext.Bytes,
                StackGuardStart = pool + 14 * Page, StackBytes = 3 * Page, ResumeRip = pool + Program.EntryA, Kind = Cooperative.ResumeKind.Entry };
            c->Plan.B = new() { Identity = 0x100000002, CurrentIdentity = 0x100000002, Context = b, ContextBytes = SavedContext.Bytes,
                StackGuardStart = pool + 17 * Page, StackBytes = 3 * Page, ResumeRip = pool + Program.EntryB, Kind = Cooperative.ResumeKind.Entry };
            c->Plan.Witness = (SavedContext*)(arena + 0x900);
            c->Plan.Stub = new() { AdmissionCallback = (ulong)(delegate* unmanaged<void*, SavedContext*, SavedContext*, byte>)&NetworkSchedulerTransfer,
                CallbackContext = (ulong)c, CallbackBytes = (uint)sizeof(NetworkSchedulerControl), HelperFx = (ulong)helper,
                JumpSlot = arena + 0x1e00, RejectRip = pool + Program.Recovery };
            c->Plan.RecoverCallback = (ulong)(delegate* unmanaged<void*, SavedContext*, byte>)&NetworkSchedulerRecover;
            c->Plan.TerminalCallback = (ulong)(delegate* unmanaged<void*, void>)&NetworkSchedulerTerminal;
            c->Plan.First = NetworkSchedulerEntry(c, c->Plan.A, Program.EntryA, Program.WorkerA, Program.FromA);
            c->Plan.Second = NetworkSchedulerEntry(c, c->Plan.B, Program.EntryB, Program.WorkerB, Program.FromB);
            if (!NetworkSchedulerImage(c, c->Plan.Stub.AdmissionCallback) || !NetworkSchedulerImage(c, c->Plan.RecoverCallback) ||
                !NetworkSchedulerImage(c, c->Plan.TerminalCallback) || !NetworkSchedulerImage(c, c->Plan.First.Start)) return NetworkSchedulerFailure(state, 13);
            if (!ProgressProgram.Emit((byte*)pool, Program.Bytes, c->Plan, (ulong)&c->Progress, (ulong)&c->Stop)) return NetworkSchedulerFailure(state, 14);
            return NetworkSchedulerVerify(c) || NetworkSchedulerFailure(state, 15);
        }

        private static KernelThreadEntry.Plan NetworkSchedulerEntry(NetworkSchedulerControl* c, Cooperative.Descriptor descriptor,
            uint offset, uint body, uint back) => new() { Owner = c->Plan.Owner, Initial = descriptor, CodeOffset = offset,
                Entry = c->Plan.Owner.CodeStart + body, Argument = (ulong)c,
                Start = (ulong)(delegate* unmanaged<void*, ulong, byte>)&NetworkSchedulerStart,
                Control = (ulong)c, ControlBytes = (uint)sizeof(NetworkSchedulerControl),
                ExitStub = c->Plan.Owner.CodeStart + back, RejectRip = c->Plan.Stub.RejectRip };
        private static void NetworkSchedulerSync(NetworkSchedulerControl* c)
        { c->Plan.First.Owner = c->Plan.Second.Owner = c->Plan.Owner; c->Plan.First.Initial = c->Plan.A; c->Plan.Second.Initial = c->Plan.B; }
        private static bool NetworkSchedulerImage(NetworkSchedulerControl* c, ulong address)
        {
            var range = c->State->Memory.Capture.Range;
            return range.IsValid && range.ImageBase.TryGetRawValueForUefiBoundary(out ulong image) == AddressValueStatus.Success &&
                address >= image && address - image < range.ImageSize;
        }
        private static bool NetworkSchedulerVerify(NetworkSchedulerControl* c) =>
            ProgressProgram.Verify((byte*)c->Plan.Owner.CodeStart, Program.Bytes, c->Plan, (ulong)&c->Progress, (ulong)&c->Stop);
        private static bool NetworkSchedulerObserve(NetworkSchedulerControl* c)
        {
            if (c == null || !CooperativeAuthority(c->State)) return false;
            ulong* cpu = c->Cpu;
            ((delegate* unmanaged<ulong*, void>)(c->State->Memory.Code + FirstExitCodeBundle.CooperativeObserveOffset))(cpu);
            if (cpu[9] < 0x80000001 || !CooperativeSingleCpu(c->State, cpu)) return false;
            Cooperative.Owner* o = &c->Plan.Owner;
            o->CurrentRoot = cpu[1]; o->CurrentKernelGs = cpu[14]; o->Cr0 = cpu[0]; o->Cr4 = cpu[2]; o->Flags = cpu[3]; o->Efer = cpu[11];
            o->Cs = (ushort)cpu[4]; o->Ss = (ushort)cpu[5]; o->Tr = (ushort)cpu[6]; o->BasicEdx = (uint)cpu[7]; o->ExtendedEdx = (uint)cpu[10];
            o->Fs = (ushort)cpu[15]; o->Gs = (ushort)cpu[16]; o->CpuCount = 1;
            if (c->Transfers == 0) o->KernelGs = cpu[14];
            NetworkSchedulerSync(c);
            return Cooperative.AdmitPlan(*o) == Cooperative.Status.Ok;
        }
        private static bool NetworkSchedulerBind(NetworkSchedulerControl* c)
        {
            if (!NetworkSchedulerObserve(c) || !CooperativeAuditPool(c->State, true) || !NetworkSchedulerVerify(c) ||
                c->Threads.Initialize(c->Key, 2, c->Plan.Owner, c->Plan.Supervisor, &c->Slot, (uint)sizeof(KernelThreadSlot), 1,
                    &c->Ready, 8, 1, &c->Deadline, (uint)sizeof(DeadlineQueue.Entry), 1) != Lifecycle.Status.Ok) return false;
            var created = c->Threads.Create(c->Key, 2, c->Plan.A.Context, SavedContext.Bytes, c->Plan.A.StackGuardStart,
                c->Plan.A.StackBytes, c->Plan.A.ResumeRip, c->Plan.First.Entry, (ulong)c);
            if (!created.Succeeded || created.Identity.Value != c->Plan.A.Identity) return false;
            c->Worker = created.Identity; c->Bound = 1;
            if (!ReadNetworkTime(c->State, out c->LastProgress, preparing: true)) return false;
            return c->Threads.ReadyCount == 1 && !c->Threads.Current.IsValid && !c->Threads.HandoffPending;
        }

        private static bool NetworkSchedulerPoll(Storage* state, bool timeWait)
        {
            NetworkSchedulerControl* c = (NetworkSchedulerControl*)(state->CooperativePool + 3 * Page);
            if (c->State != state || c->Bound != 1 || c->Failed != 0 || !ReadNetworkTime(state, out ulong now) ||
                now < c->LastProgress || now > ~0UL - 100000000) return false;
            if (now < c->NextDue) return true;
            ulong previous = c->Progress;
            if (!NetworkSchedulerDispatch(c) || c->Progress != previous + 1 || !ReadNetworkTime(state, out now) ||
                now < c->LastProgress || now > ~0UL - 100000000) return false;
            if (c->LastProgress != 0)
            {
                ulong gap = now - c->LastProgress;
                if (gap > 1000000000) return false;
                if (gap > c->MaximumGap) c->MaximumGap = gap;
            }
            c->LastProgress = now; c->NextDue = now + 100000000;
            if (timeWait) c->TimeWaitDispatches++; else c->PacketDispatches++;
            return true;
        }
        private static bool NetworkSchedulerDispatch(NetworkSchedulerControl* c)
        {
            if (c->Selected != 0 || !NetworkSchedulerObserve(c) || !CooperativeAuditPool(c->State, true) || !NetworkSchedulerVerify(c) ||
                c->Threads.Flush(c->Key, 2) != Lifecycle.Status.Ok) return false;
            var next = c->Threads.Dispatch(c->Key, 2);
            if (!next.Succeeded || next.Identity.Value != c->Worker.Value || next.State != Lifecycle.State.Running) return false;
            c->Selected = 1;
            bool returned = ((delegate* unmanaged<uint>)(c->Plan.Owner.CodeStart + Program.DriverA))() == 1;
            if (!returned || c->Failed != 0 || !CooperativeEqualContexts(c->Plan.Supervisor.Context, c->Plan.Witness) ||
                !NetworkSchedulerObserve(c) || !CooperativeAuditPool(c->State, true) || !NetworkSchedulerVerify(c)) return false;
            ulong rsp = ((delegate* unmanaged<ulong>)(c->Plan.Owner.CodeStart + Program.StackPointer))();
            if (c->Threads.ConfirmSupervisor(c->Key, 2, rsp) != Lifecycle.Status.Ok) return false;
            c->Selected = 0;
            return !c->Threads.Current.IsValid && !c->Threads.HandoffPending;
        }
        private static bool NetworkSchedulerStop(NetworkSchedulerControl* c)
        {
            if (c->Failed != 0 || c->Bound != 1) return false;
            if (!ReadNetworkTime(c->State, out ulong now) || now < c->LastProgress || now - c->LastProgress > 1000000000) return false;
            if (now - c->LastProgress > c->MaximumGap) c->MaximumGap = now - c->LastProgress;
            c->Stop = 1;
            if (!NetworkSchedulerDispatch(c)) return false;
            var exited = c->Threads.Inspect(c->Key, 2, c->Worker);
            if (!exited.Succeeded || exited.State != Lifecycle.State.Exited || exited.ExitValue != 17) return false;
            ulong rsp = ((delegate* unmanaged<ulong>)(c->Plan.Owner.CodeStart + Program.StackPointer))();
            var reaped = c->Threads.Reap(c->Key, 2, c->Worker, rsp);
            if (!reaped.Succeeded || c->Threads.ReadyCount != 0 || c->Threads.DeadlineCount != 0 || c->Threads.Current.IsValid || c->Threads.HandoffPending) return false;
            c->Bound = 2;
            for (uint i = 0; i < SavedContext.Bytes; i++) if (((byte*)c->Plan.A.Context)[i] != 0) return false;
            for (uint i = 0; i < Page; i++) if (((byte*)(c->Plan.A.StackGuardStart + Page))[i] != 0) return false;
            return true;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte NetworkSchedulerStart(void* context, ulong identity)
        {
            NetworkSchedulerControl* c = (NetworkSchedulerControl*)context;
            if (c == null || c->Selected != 1 || c->Worker.Value != identity || c->Threads.Current.Value != identity) return 0;
            var running = c->Threads.Inspect(c->Key, 2, c->Worker);
            return running.Succeeded && running.State == Lifecycle.State.Running ? (byte)1 : (byte)0;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte NetworkSchedulerTransfer(void* context, SavedContext* source, SavedContext* destination)
        {
            NetworkSchedulerControl* c = (NetworkSchedulerControl*)context;
            if (c == null || c->Selected != 1 || c->Failed != 0 || !NetworkSchedulerObserve(c) || !CooperativeAuditPool(c->State, true)) return 0;
            ulong pool = c->Plan.Owner.CodeStart;
            if (source == c->Plan.Supervisor.Context && destination == c->Plan.A.Context)
            {
                c->Plan.Supervisor.ResumeRip = pool + Program.DriverResume; NetworkSchedulerSync(c);
                var running = c->Threads.Inspect(c->Key, 2, c->Worker);
                if (source->Rip != c->Plan.Supervisor.ResumeRip || !running.Succeeded || running.State != Lifecycle.State.Running ||
                    c->Threads.Current.Value != c->Worker.Value || !NetworkSchedulerVerify(c) ||
                    Cooperative.Admit(c->Plan.Owner, c->Plan.Supervisor, c->Plan.A) != Cooperative.Status.Ok) return 0;
            }
            else
            {
                if (source != c->Plan.A.Context || destination != c->Plan.Supervisor.Context || c->Threads.Current.Value != c->Worker.Value) return 0;
                var worker = c->Plan.A; worker.Kind = Cooperative.ResumeKind.CapturedCall; worker.ResumeRip = source->Rip;
                bool exit = source->Rip == pool + Program.EntryA + KernelThreadEntry.ExitResumeOffset;
                if (!exit && source->Rip != pool + Program.WorkerA + ProgressProgram.YieldResume ||
                    exit && (c->Stop != 1 || source->Rdx != 17) || !exit && (c->Stop != 0 || source->Rdx != 0)) return 0;
                c->Plan.A = worker; NetworkSchedulerSync(c);
                if (!NetworkSchedulerVerify(c) || Cooperative.Admit(c->Plan.Owner, worker, c->Plan.Supervisor) != Cooperative.Status.Ok) return 0;
                var result = exit ? c->Threads.Exit(c->Key, 2, c->Worker, worker, 17) : c->Threads.Yield(c->Key, 2, c->Worker, worker);
                if (!result.Succeeded || !c->Threads.HandoffPending) return 0;
            }
            c->Transfers++; return 1;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte NetworkSchedulerRecover(void* context, SavedContext* supervisor)
        {
            NetworkSchedulerControl* c = (NetworkSchedulerControl*)context;
            if (c == null) return 0;
            c->Failed = 1;
            return supervisor == c->Plan.Supervisor.Context && NetworkSchedulerObserve(c) && CooperativeAuditPool(c->State, true) &&
                Cooperative.AdmitResume(c->Plan.Owner, c->Plan.Supervisor) == Cooperative.Status.Ok ? (byte)1 : (byte)0;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static void NetworkSchedulerTerminal(void* context)
        {
            NetworkSchedulerControl* c = (NetworkSchedulerControl*)context;
            if (c != null && c->State != null) _ = LinuxText(c->State, "TEST_FAIL m07-026 scheduler terminal\r\n");
            while (true) { }
        }
        private static bool NetworkSchedulerRow(Storage* state, uint stage, ulong a, ulong b, ulong c, ulong d) =>
            LinuxText(state, "M07_026_SCHED stage=") && LinuxNumber(state, stage) && LinuxText(state, " a=") && LinuxNumber(state, a) &&
            LinuxText(state, " b=") && LinuxNumber(state, b) && LinuxText(state, " c=") && LinuxNumber(state, c) &&
            LinuxText(state, " d=") && LinuxNumber(state, d) && LinuxText(state, "\r\n");

        private static bool NetworkSchedulerFailure(Storage* state, uint stage)
        {
            _ = LinuxText(state, "TEST_FAIL m07-026 scheduler stage=") && LinuxNumber(state, stage) && LinuxText(state, "\r\n");
            return false;
        }
    }
}
#endif
