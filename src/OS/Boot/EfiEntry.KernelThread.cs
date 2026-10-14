#if M09_006_PROBE || M09_009_PROBE || M09_010_PROBE
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;
using OS.Pal;
using Lifecycle = OS.Kernel.Threading.KernelThreadTable;
using WorkerSlot = OS.Kernel.Threading.KernelThreadSlot;
using WorkerWait = OS.Kernel.Threading.KernelThreadWait;
using WorkerEntry = OS.Kernel.Threading.KernelThreadEntry;
using WorkerProgram = OS.Kernel.Threading.KernelThreadProgram;
using Cooperative = OS.Kernel.Threading.ThreadContextSwitch;
using SavedContext = OS.Kernel.Threading.X64ThreadContext;
using TimerIrq = OS.Kernel.Threading.X64TimerInterrupt;
using TickTimer = OS.Kernel.Threading.SchedulerTimer;
using Deadlines = OS.Kernel.Threading.DeadlineQueue;

namespace OS.Boot;

#pragma warning disable 649 // Adjacent caller-owned buffers are written through their first element pointers.

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Caller-owned metadata resides in the retained context arena. Only the
        // single BSP/IF-clear owner accesses it; no managed object is published.
        private struct LifecycleControl
        {
            internal Storage* State;
            internal WorkerProgram.Plan Plan;
            internal Lifecycle Threads;
            internal WorkerSlot SlotA, SlotB;
            internal OSHostHandle ReadyA, ReadyB;
            internal Deadlines.Entry DeadlineA, DeadlineB;
            internal OSHostHandle A, B;
            internal TickTimer Timer;
            internal TimerIrq.Plan Handler;
            internal ulong Key, OriginalIst, Epoch, Transfers, WakeIntervals, Reuse;
            internal uint Installed, Failed, Selected, JoinObservations, Cancellations, Reaped;
            internal fixed ulong Cpu[19];
            internal fixed ulong OriginalIdtr[2], NewIdtr[2], ObservedIdtr[2];
            internal fixed byte OriginalTss[104];
#if M09_009_PROBE || M09_010_PROBE
            internal ThreadWaitControl* Wait;
#endif
        }

        private static bool RunKernelThreads(void* context)
        {
            Storage* state = (Storage*)context;
            if (!CooperativeAuthority(state) || state->ApicPhase != 2 || state->CooperativePhase != 0 || !LifecycleText(state, 0)) return false;
            ((delegate* unmanaged<void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeSegmentsOffset))();
            if (!CooperativeAuditPool(state, false) || !CooperativeAuditOriginalMappings(state)) return false;
            ulong pool = state->CooperativePool;
            BootPageFreeListNode node = new(0, 0, pool, pool + 32 * Page, 32, 32 * Page, -1);
            BootPageAllocatorState allocator = default;
            if (!BootPageAllocatorStateMaterializer.Materialize(&node, 1, 1, 1, 0, Page, 32, 32 * Page, &allocator, 1).Succeeded) return false;
            BootPageAllocatorState initial = allocator; BootPageFreeListNode initialNode = node;
#if M09_009_PROBE || M09_010_PROBE
            const uint leases = 24;
#else
            const uint leases = 23;
#endif
            BootPageAllocatorOnePageResult* receipts = stackalloc BootPageAllocatorOnePageResult[(int)leases];
            ulong* originals = stackalloc ulong[32]; for (uint i = 0; i < 32; i++) originals[i] = 0;
            uint borrowed = 0; bool addresses = true;
            for (; borrowed < leases; borrowed++)
            {
                var receipt = BootPageAllocatorMutator.AllocateOnePage(allocator, &node, 1, 1, null, 0, 0, 1, Page);
                if (!receipt.Succeeded) break;
                allocator = receipt.AfterState; receipts[borrowed] = receipt;
                addresses &= receipt.PhysicalStart.TryGetRawValueForHardwareRegisterBoundary(out ulong address) == AddressValueStatus.Success &&
                    address == pool + borrowed * Page && receipt.PageCount == 1 && receipt.ByteLength == Page;
            }
            LifecycleControl* c = null;
            bool prepared = borrowed == leases && addresses && LifecyclePrepare(state, out c);
            bool captured = false, published = false, executed = false, hardware = true;
            if (prepared)
            {
                published = CooperativeSetPoolMappings(state, originals, true, out captured) && CooperativeAuditPool(state, true);
                if (published)
                {
                    c->Plan.Owner.GuardsVerified = true; c->Plan.Owner.MappingsVerified = true; LifecycleSync(c);
                    state->CooperativePhase = 2;
                    ulong prior = ((delegate* unmanaged<ulong>)(pool + WorkerProgram.IrqSaveDisable))();
#if M09_009_PROBE || M09_010_PROBE
                    executed = (prior & 0x202) == 2 && LifecycleBind(c) && ThreadWaitBind(c) && LifecycleInstall(c) &&
                        ThreadWaitRun(c) && ThreadWaitReap(c, originals, true) && ThreadWaitRun(c) && ThreadWaitReap(c, originals, false);
#else
                    executed = (prior & 0x202) == 2 && LifecycleBind(c) && LifecycleInstall(c) && LifecycleRunInitial(c) &&
                        LifecycleReuse(c, originals, 1) && LifecycleReuse(c, originals, 2) && LifecycleFinish(c, originals);
#endif
                    // Finish revokes the pool code; IF was admitted clear and every
                    // idle return executes CLI. Fresh retained CPU observation below
                    // proves the same owner state without executing revoked bytes.
                    if (!LifecycleObserve(c) || (c->Plan.Owner.Flags & 0x202) != (prior & 0x202)) executed = false;
                }
            }
            if (c != null) hardware = LifecycleRestoreHardware(c);
            // A failed transfer/admission may leave logical execution or native
            // references unresolved. Keep their backing, even after timer restore.
            bool empty = c == null || !c->Threads.Current.IsValid && !c->Threads.HandoffPending && c->Reaped == 4;
            if (!hardware || published && !empty)
            { _ = LifecycleText(state, 12, c == null ? 0 : c->Epoch, hardware ? 1UL : 0, empty ? 1UL : 0, borrowed); return false; }
            bool mappings = captured ? CooperativeSetPoolMappings(state, originals, false, out _) && CooperativeAuditPool(state, false)
                : CooperativeAuditPool(state, false);
            bool released = mappings;
            if (mappings)
            {
                for (ulong i = 0; i < borrowed * Page; i++) ((byte*)pool)[i] = 0;
                for (ulong i = 0; i < borrowed * Page; i++) released &= ((byte*)pool)[i] == 0;
                for (uint i = borrowed; i > 0; i--)
                {
                    var free = BootPageAllocatorMutator.FreeOnePage(allocator, &node, 1, 1, null, 0, 0, receipts[i - 1]);
                    if (!free.Succeeded) { released = false; break; } allocator = free.AfterState;
                }
            }
            released &= CooperativeAllocatorEqual(initial, allocator, initialNode, node) && CooperativeAuditOriginalMappings(state) &&
                FirstExitOwnedArena.ValidateCanaries(state->Arena);
            bool success = prepared && published && executed && released;
            state->CooperativePhase = success ? 4U : 5U;
            if (!success) return LifecycleText(state, 12, prepared ? 1UL : 0, published ? 1UL : 0, executed ? 1UL : 0, released ? 1UL : 0) && false;
            return LifecycleText(state, 9, leases, 32, 4, 1) && LifecycleText(state, 10) && LifecycleText(state, 11);
        }

        private static bool LifecyclePrepare(Storage* state, out LifecycleControl* c)
        {
            ulong pool = state->CooperativePool, arena = pool + 2 * Page;
            c = (LifecycleControl*)(arena + Page);
            if (sizeof(LifecycleControl) > 0xe00) return false;
            *c = default; c->State = state; c->Key = pool ^ 0x4d30393030364f57UL;
            c->Plan.Owner = new() { Initialized = true, Key = c->Key, CurrentKey = c->Key,
                Root = (ulong)state->Memory.Entries, ContextArena = arena, ContextArenaBytes = 2 * Page,
                CodeStart = pool, CodeBytes = WorkerProgram.Bytes, SwitchStackGuardStart = pool + 4 * Page, SwitchStackBytes = 10 * Page };
            if (!LifecycleObserve(c)) return false;
            byte* rawFx = (byte*)arena + 0xe00;
            ((delegate* unmanaged<byte*, void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeFxOffset))(rawFx);
            c->Plan.Owner.ObservedMxcsrMask = *(uint*)(rawFx + 28);
            uint mask = c->Plan.Owner.ObservedMxcsrMask;
            SavedContext* supervisor = (SavedContext*)arena, a = (SavedContext*)(arena + 0x300), b = (SavedContext*)(arena + 0x600);
            if (SavedContext.Initialize(supervisor, 688, pool + WorkerProgram.DriverResume, (state->Arena.StackTop - 64) & ~15UL, mask) != SavedContext.Status.Ok ||
                SavedContext.Initialize(a, 688, pool + WorkerProgram.EntryA, pool + 16 * Page - 40, mask, 2, arena + 0x1f00, arena + 0x1f08) != SavedContext.Status.Ok ||
                SavedContext.Initialize(b, 688, pool + WorkerProgram.EntryB, pool + 19 * Page - 40, mask, 2, arena + 0x1f10, arena + 0x1f18) != SavedContext.Status.Ok) return false;
            LifecycleSeed(a, 1); LifecycleSeed(b, 2);
            byte* helper = (byte*)arena + 0xc00;
            for (uint i = 0; i < 512; i++) helper[i] = 0;
            *(ushort*)helper = 0x027f; *(uint*)(helper + 24) = 0x1f80; *(uint*)(helper + 28) = mask;
            c->Plan.Supervisor = new() { Identity = 0x1ffffffff, CurrentIdentity = 0x1ffffffff, Context = supervisor, ContextBytes = 688,
                StackGuardStart = (ulong)state->Arena.Storage + 21 * Page, StackBytes = 66 * Page,
                ResumeRip = pool + WorkerProgram.DriverResume, Kind = Cooperative.ResumeKind.CapturedCall };
            c->Plan.A = new() { Identity = 0x100000001, CurrentIdentity = 0x100000001, Context = a, ContextBytes = 688,
                StackGuardStart = pool + 14 * Page, StackBytes = 3 * Page, ResumeRip = pool + WorkerProgram.EntryA, Kind = Cooperative.ResumeKind.Entry };
            c->Plan.B = new() { Identity = 0x100000002, CurrentIdentity = 0x100000002, Context = b, ContextBytes = 688,
                StackGuardStart = pool + 17 * Page, StackBytes = 3 * Page, ResumeRip = pool + WorkerProgram.EntryB, Kind = Cooperative.ResumeKind.Entry };
            c->Plan.Witness = (SavedContext*)(arena + 0x900);
            c->Plan.Stub = new() { AdmissionCallback = (ulong)(delegate* unmanaged<void*, SavedContext*, SavedContext*, byte>)&LifecycleTransfer,
                CallbackContext = (ulong)c, CallbackBytes = (uint)sizeof(LifecycleControl), HelperFx = (ulong)helper,
                JumpSlot = arena + 0x1e00, RejectRip = pool + WorkerProgram.Recovery };
            c->Plan.First = LifecycleEntry(c, c->Plan.A, WorkerProgram.EntryA, WorkerProgram.WorkerA, WorkerProgram.FromA);
            c->Plan.Second = LifecycleEntry(c, c->Plan.B, WorkerProgram.EntryB, WorkerProgram.WorkerB, WorkerProgram.FromB);
            c->Plan.RecoverCallback = (ulong)(delegate* unmanaged<void*, SavedContext*, byte>)&LifecycleRecover;
            c->Plan.TerminalCallback = (ulong)(delegate* unmanaged<void*, void>)&LifecycleTerminal;
            c->Handler = new() { CodeAddress = pool + WorkerProgram.TimerHandler, Callback = (ulong)(delegate* unmanaged<void*, TimerIrq.Frame*, byte>)&LifecycleInterrupt,
                CallbackContext = (ulong)c, CallbackBytes = (uint)sizeof(LifecycleControl), HelperFx = (ulong)helper };
#if M09_009_PROBE || M09_010_PROBE
            return ThreadWaitPrepare(c);
#else
            if (!LifecycleImageCallback(c, c->Plan.Stub.AdmissionCallback) || !LifecycleImageCallback(c, c->Plan.First.Start) ||
                !LifecycleImageCallback(c, c->Plan.RecoverCallback) || !LifecycleImageCallback(c, c->Plan.TerminalCallback) || !LifecycleImageCallback(c, c->Handler.Callback) ||
                !WorkerProgram.Emit((byte*)pool, WorkerProgram.Bytes, c->Plan) || !TimerIrq.Emit((byte*)c->Handler.CodeAddress, 512, c->Handler)) return false;
            LifecycleSaveCode(c);
            return LifecycleVerifyCode(c) && LifecycleText(state, 1, pool, 23, pool + 13 * Page, c->Key);
#endif
        }

        private static WorkerEntry.Plan LifecycleEntry(LifecycleControl* c, Cooperative.Descriptor d, uint offset, uint body, uint back) =>
            new() { Owner = c->Plan.Owner, Initial = d, CodeOffset = offset, Entry = c->Plan.Owner.CodeStart + body,
                Argument = (ulong)c, Start = (ulong)(delegate* unmanaged<void*, ulong, byte>)&LifecycleStart,
                Control = (ulong)c, ControlBytes = (uint)sizeof(LifecycleControl), ExitStub = c->Plan.Owner.CodeStart + back,
                RejectRip = c->Plan.Stub.RejectRip };
        private static void LifecycleSync(LifecycleControl* c)
        { c->Plan.First.Owner = c->Plan.Second.Owner = c->Plan.Owner; c->Plan.First.Initial = c->Plan.A; c->Plan.Second.Initial = c->Plan.B; }
        private static bool LifecycleObserve(LifecycleControl* c)
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
            LifecycleSync(c);
            return Cooperative.AdmitPlan(*o) == Cooperative.Status.Ok;
        }
        private static bool LifecycleImageCallback(LifecycleControl* c, ulong address)
        {
            var range = c->State->Memory.Capture.Range;
            return range.IsValid && range.ImageBase.TryGetRawValueForUefiBoundary(out ulong image) == AddressValueStatus.Success &&
                address >= image && address - image < range.ImageSize;
        }
        private static void LifecycleSeed(SavedContext* context, uint worker)
        {
            context->Rbx = CooperativeGpr(worker, 3); context->Rbp = CooperativeGpr(worker, 5);
            context->Rsi = CooperativeGpr(worker, 6); context->Rdi = CooperativeGpr(worker, 7);
            context->R12 = CooperativeGpr(worker, 12); context->R13 = CooperativeGpr(worker, 13);
            context->R14 = CooperativeGpr(worker, 14); context->R15 = CooperativeGpr(worker, 15);
            for (uint register = 6; register < 16; register++) for (uint part = 0; part < 16; part++)
                context->FxSave[160 + register * 16 + part] = (byte)(0x10 + worker * 0x20 + register * 7 + part * 3);
        }
        private static void LifecycleSaveCode(LifecycleControl* c)
        { for (uint i = 0; i < WorkerProgram.Bytes; i++) ((byte*)(c->State->CooperativePool + 21 * Page))[i] = ((byte*)c->Plan.Owner.CodeStart)[i]; }
        private static bool LifecycleVerifyCode(LifecycleControl* c)
        {
#if M09_009_PROBE || M09_010_PROBE
            bool program = OS.Kernel.Threading.ThreadWaitProgram.Verify((byte*)c->Plan.Owner.CodeStart, WorkerProgram.Bytes, c->Plan, Probes.EnableM09010Probe);
#else
            bool program = WorkerProgram.Verify((byte*)c->Plan.Owner.CodeStart, WorkerProgram.Bytes, c->Plan);
#endif
            if (!program || !TimerIrq.Verify((byte*)c->Handler.CodeAddress, 512, c->Handler)) return false;
            for (uint i = 0; i < WorkerProgram.Bytes; i++) if (((byte*)(c->State->CooperativePool + 21 * Page))[i] != ((byte*)c->Plan.Owner.CodeStart)[i]) return false;
            return true;
        }

        private static bool LifecycleBind(LifecycleControl* c)
        {
            if (!LifecycleObserve(c) || !CooperativeAuditPool(c->State, true) ||
                c->Threads.Initialize(c->Key, 2, c->Plan.Owner, c->Plan.Supervisor, &c->SlotA, (uint)sizeof(WorkerSlot) * 2, 2,
                    &c->ReadyA, 16, 2, &c->DeadlineA, (uint)sizeof(Deadlines.Entry) * 2, 2) != Lifecycle.Status.Ok) return false;
            var a = c->Threads.Create(c->Key, 2, c->Plan.A.Context, 688, c->Plan.A.StackGuardStart, 3 * Page,
                c->Plan.A.ResumeRip, c->Plan.First.Entry, (ulong)c);
            var b = c->Threads.Create(c->Key, 2, c->Plan.B.Context, 688, c->Plan.B.StackGuardStart, 3 * Page,
                c->Plan.B.ResumeRip, c->Plan.Second.Entry, (ulong)c);
            if (!a.Succeeded || !b.Succeeded || a.Identity.Value != c->Plan.A.Identity || b.Identity.Value != c->Plan.B.Identity) return false;
            c->A = a.Identity; c->B = b.Identity;
            ulong before = LifecycleDigest(c);
            var full = c->Threads.Create(c->Key, 2, c->Plan.A.Context, 688, c->Plan.A.StackGuardStart, 3 * Page,
                c->Plan.A.ResumeRip, c->Plan.First.Entry, (ulong)c);
            return full.Code == Lifecycle.Status.Full && before == LifecycleDigest(c) &&
                LifecycleText(c->State, 7, 0, (ulong)full.Code, before, c->Threads.ReadyCount);
        }
        private static ulong LifecycleDigest(LifecycleControl* c)
        {
            byte* start = (byte*)&c->Threads, end = (byte*)(&c->DeadlineB + 1);
            ulong value = 14695981039346656037UL;
            for (byte* p = start; p < end; p++) value = unchecked((value ^ *p) * 1099511628211UL);
            return value;
        }
        private static ulong LifecycleRsp(LifecycleControl* c) =>
            ((delegate* unmanaged<ulong>)(c->Plan.Owner.CodeStart + WorkerProgram.StackPointer))();

        private static bool LifecycleRunInitial(LifecycleControl* c)
        {
            for (uint step = 0; step < 32; step++)
            {
                var a = c->Threads.Inspect(c->Key, 2, c->A); var b = c->Threads.Inspect(c->Key, 2, c->B);
                if (!a.Succeeded || !b.Succeeded) return false;
                if (a.State == Lifecycle.State.Exited && b.State == Lifecycle.State.Exited)
                    return a.ExitValue == 17 && b.ExitValue == 23 && a.JoinedExitValue == 23 && c->JoinObservations == 1 &&
                        c->Epoch == 5 && c->WakeIntervals == 4;
                if (c->Threads.ReadyCount != 0) { if (!LifecycleDispatch(c)) return false; }
                else if (c->Threads.DeadlineCount == 0 || !LifecycleWaitOne(c, false)) return false;
            }
            return false;
        }
        private static bool LifecycleDispatch(LifecycleControl* c)
        {
            if (!LifecycleObserve(c) || !CooperativeAuditPool(c->State, true) || !LifecycleVerifyCode(c) ||
                c->Threads.Flush(c->Key, 2) != Lifecycle.Status.Ok) return false;
            var next = c->Threads.Dispatch(c->Key, 2);
            if (!next.Succeeded) return false;
            c->Selected = next.Identity.Value == c->A.Value ? 1U : next.Identity.Value == c->B.Value ? 2U : 0;
            if (c->Selected == 0 || next.State != Lifecycle.State.Running) return false;
            uint offset = c->Selected == 1 ? WorkerProgram.DriverA : WorkerProgram.DriverB;
            bool returned = ((delegate* unmanaged<uint>)(c->Plan.Owner.CodeStart + offset))() == 1;
            if (!returned || c->Failed != 0 || !CooperativeEqualContexts(c->Plan.Supervisor.Context, c->Plan.Witness) ||
                !LifecycleObserve(c) || !CooperativeAuditPool(c->State, true) || !LifecycleVerifyCode(c)) return false;
            ulong rsp = LifecycleRsp(c);
            if (c->Threads.ConfirmSupervisor(c->Key, 2, rsp) != Lifecycle.Status.Ok) return false;
            c->Selected = 0;
            return !c->Threads.Current.IsValid && !c->Threads.HandoffPending;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LifecycleStart(void* context, ulong identity)
        {
            LifecycleControl* c = (LifecycleControl*)context;
            if (c == null || c->Selected == 0 || c->Threads.Current.Value != identity ||
                identity != (c->Selected == 1 ? c->A.Value : c->B.Value)) return 0;
            var entry = c->Threads.Inspect(c->Key, 2, new(identity));
            return !entry.Succeeded || entry.State != Lifecycle.State.Running ? (byte)0 : entry.CancellationRequested ? (byte)2 : (byte)1;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LifecycleTransfer(void* context, SavedContext* source, SavedContext* destination)
        {
            LifecycleControl* c = (LifecycleControl*)context;
            if (c == null || c->Selected == 0 || !LifecycleObserve(c) || !CooperativeAuditPool(c->State, true)) return 0;
            ulong pool = c->Plan.Owner.CodeStart;
            bool first = c->Selected == 1;
            Cooperative.Descriptor worker = first ? c->Plan.A : c->Plan.B;
            OSHostHandle identity = first ? c->A : c->B;
            if (source == c->Plan.Supervisor.Context && destination == worker.Context)
            {
                c->Plan.Supervisor.ResumeRip = pool + (first ? WorkerProgram.DriverA : WorkerProgram.DriverB) + WorkerProgram.DriverResume;
                LifecycleSync(c);
                var running = c->Threads.Inspect(c->Key, 2, identity);
                if (!running.Succeeded || running.State != Lifecycle.State.Running || c->Threads.Current.Value != identity.Value ||
                    !LifecycleVerifyCode(c) || Cooperative.Admit(c->Plan.Owner, c->Plan.Supervisor, worker) != Cooperative.Status.Ok) return 0;
                return LifecycleTransferRow(c, 0x1ffffffff, identity.Value, source->Rip);
            }
            if (source != worker.Context || destination != c->Plan.Supervisor.Context || c->Threads.Current.Value != identity.Value) return 0;
            worker.Kind = Cooperative.ResumeKind.CapturedCall; worker.ResumeRip = source->Rip;
            ulong relative = source->Rip - pool;
            uint body = first ? c->Reuse == 0 ? WorkerProgram.WorkerA : WorkerProgram.ReusedWorker : WorkerProgram.WorkerB;
            uint entry = first ? WorkerProgram.EntryA : WorkerProgram.EntryB;
            bool exit = relative == entry + WorkerEntry.ExitResumeOffset || !first && relative == body + WorkerProgram.JoinExitResume;
            bool yield = c->Reuse == 0 || !first ? relative == body + WorkerProgram.Yield1Resume || relative == body + WorkerProgram.Yield2Resume : false;
            bool sleep = c->Reuse == 0 || !first ? relative == body + WorkerProgram.Sleep1Resume || relative == body + WorkerProgram.Sleep2Resume : relative == body + WorkerProgram.ReusedSleepResume;
            bool join = first && (c->Reuse == 0 ? relative == body + WorkerProgram.JoinExitResume : relative == body + WorkerProgram.ReusedJoin1Resume || relative == body + WorkerProgram.ReusedJoin2Resume);
            if ((!exit && !yield && !sleep && !join) || !LifecycleSentinels(c, source, first ? (uint)(c->Reuse == 0 ? 1 : c->Reuse + 2) : 2,
                    worker.StackGuardStart, relative == entry + WorkerEntry.ExitResumeOffset)) return 0;
            if (first) c->Plan.A = worker; else c->Plan.B = worker;
            LifecycleSync(c);
            if (!LifecycleVerifyCode(c) || Cooperative.Admit(c->Plan.Owner, worker, c->Plan.Supervisor) != Cooperative.Status.Ok) return 0;
            Lifecycle.Result result;
            if (exit)
            {
                int value = unchecked((int)source->Rdx);
                if (first && c->Reuse == 0)
                {
                    var joined = c->Threads.Inspect(c->Key, 2, identity);
                    if (value != 17 || joined.Completion != Lifecycle.Completion.Joined || joined.JoinedExitValue != 23) return 0;
                    c->JoinObservations++;
                    if (!LifecycleText(c->State, 4, identity.Value, c->B.Value, 23, 1)) return 0;
                }
                else if (value != (first ? -1 : 23)) return 0;
                result = c->Threads.Exit(c->Key, 2, identity, worker, value);
            }
            else if (yield) result = c->Threads.Yield(c->Key, 2, identity, worker);
            else if (sleep)
            {
                ulong delay = c->Reuse != 0 && first ? 1UL : !first && relative == body + WorkerProgram.Sleep1Resume ? 3UL : 2UL;
                if (source->Rdx != delay) return 0;
                result = c->Threads.Sleep(c->Key, 2, identity, worker, c->Epoch, delay);
                if (!result.Succeeded || !LifecycleText(c->State, 3, identity.Value, result.Wait.Sequence, c->Epoch + delay, (ulong)result.State)) return 0;
            }
            else
            {
                result = c->Threads.BeginWait(c->Key, 2, identity, Lifecycle.WaitKind.Join, c->Epoch, target: c->B);
                if (!result.Succeeded) return 0;
                if (result.State == Lifecycle.State.Parking) result = c->Threads.Park(c->Key, 2, result.Wait, worker);
                else
                {
                    if (result.Completion != Lifecycle.Completion.Joined || result.JoinedExitValue != 23) return 0;
                    c->JoinObservations++;
                    if (!LifecycleText(c->State, 4, identity.Value, c->B.Value, 23, c->JoinObservations)) return 0;
                    result = c->Threads.Yield(c->Key, 2, identity, worker);
                }
            }
            if (!result.Succeeded || !c->Threads.HandoffPending) return 0;
            return LifecycleTransferRow(c, identity.Value, 0x1ffffffff, source->Rip);
        }
        private static byte LifecycleTransferRow(LifecycleControl* c, ulong from, ulong to, ulong rip)
        {
            if (!LifecycleText(c->State, 2, c->Transfers, from, to, rip)) return 0;
            c->Transfers++; return 1;
        }
        private static bool LifecycleSentinels(LifecycleControl* c, SavedContext* x, uint worker, ulong guard, bool returned)
        {
            uint pattern = worker;
            ulong arena = c->Plan.Owner.ContextArena;
            if (x->Rsp != guard + 2 * Page - (returned ? 80UL : 192UL) ||
                x->Rbx != CooperativeGpr(pattern, 3) || x->Rbp != CooperativeGpr(pattern, 5) || x->Rsi != CooperativeGpr(pattern, 6) || x->Rdi != CooperativeGpr(pattern, 7) ||
                x->R12 != CooperativeGpr(pattern, 12) || x->R13 != CooperativeGpr(pattern, 13) || x->R14 != CooperativeGpr(pattern, 14) || x->R15 != CooperativeGpr(pattern, 15) ||
                x->FsBase != arena + (worker == 2 ? 0x1f10UL : 0x1f00UL) || x->UserGsBase != arena + (worker == 2 ? 0x1f18UL : 0x1f08UL) ||
                (x->Rflags & ~Cooperative.AdmittedFlags) != 0 || (x->Rflags & 0x600) != 0 ||
                *(ushort*)x->FxSave != 0x027f || x->FxSave[4] != 0 || *(uint*)(x->FxSave + 24) != 0x1f80) return false;
            for (uint register = 6; register < 16; register++) for (uint part = 0; part < 16; part++)
                if (x->FxSave[160 + register * 16 + part] != (byte)(0x10 + pattern * 0x20 + register * 7 + part * 3)) return false;
            return true;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LifecycleRecover(void* context, SavedContext* supervisor)
        {
            LifecycleControl* c = (LifecycleControl*)context;
            if (c == null || supervisor != c->Plan.Supervisor.Context || c->Selected == 0 || !LifecycleObserve(c) ||
                Cooperative.AdmitResume(c->Plan.Owner, c->Plan.Supervisor) != Cooperative.Status.Ok) return 0;
            c->Failed = 1; return 1;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static void LifecycleTerminal(void* context)
        {
            LifecycleControl* c = (LifecycleControl*)context;
            if (c != null && c->State != null) _ = LifecycleText(c->State, 12, c->Transfers, c->Epoch, c->Selected, c->Failed);
            while (true) { }
        }

        private static bool LifecycleInstall(LifecycleControl* c)
        {
            Storage* state = c->State; ulong pool = state->CooperativePool;
            if (c->Installed != 0 || !LifecycleObserve(c) || !CooperativeAuditPool(state, true)) return false;
            byte* code = (byte*)c->Handler.CodeAddress, idt = (byte*)(pool + 20 * Page);
            ((delegate* unmanaged<void*, void>)(code + TimerIrq.SaveIdtrOffset))(c->OriginalIdtr);
            if (*(ushort*)c->OriginalIdtr != 4095 || *(ulong*)((byte*)c->OriginalIdtr + 2) != (ulong)state->Arena.Descriptors->FinalIdt) return false;
            c->OriginalIst = *(ulong*)(state->Arena.Descriptors->Tss + 60);
            if (c->OriginalIst != 0) return false;
            for (uint i = 0; i < 104; i++) c->OriginalTss[i] = state->Arena.Descriptors->Tss[i];
            for (uint i = 0; i < 4096; i++) idt[i] = state->Arena.Descriptors->FinalIdt[i];
            TimerGate(idt + 0xe1 * 16, c->Handler.CodeAddress, 4);
            TimerGate(idt + 0xff * 16, c->Handler.CodeAddress + TimerIrq.SpuriousOffset, 4);
            *(ushort*)c->NewIdtr = 4095; *(ulong*)((byte*)c->NewIdtr + 2) = (ulong)idt;
            c->Installed = 1;
            ((delegate* unmanaged<void*, void>)(code + TimerIrq.LoadIdtrOffset))(c->NewIdtr);
            return LifecycleInstalled(c);
        }
        private static bool LifecycleInstalled(LifecycleControl* c)
        {
            if (c->Installed != 1 || c->OriginalIst != 0) return false;
            byte* tss = c->State->Arena.Descriptors->Tss;
            ulong top = *(ulong*)(tss + 60), expected = c->State->CooperativePool + 13 * Page;
            if (top != 0 && top != expected) return false;
            for (uint i = 0; i < 4096; i++) if (!(i >= 60 && i < 68) && tss[i] != (i < 104 ? c->OriginalTss[i] : 0)) return false;
            byte* idt = (byte*)(c->State->CooperativePool + 20 * Page);
            if (!TimerGateMatches(idt + 0xe1 * 16, c->Handler.CodeAddress) || !TimerGateMatches(idt + 0xff * 16, c->Handler.CodeAddress + TimerIrq.SpuriousOffset)) return false;
            for (uint i = 0; i < 4096; i++)
                if (!(i >= 0xe1 * 16 && i < 0xe2 * 16) && i < 0xff * 16 && idt[i] != c->State->Arena.Descriptors->FinalIdt[i]) return false;
            ((delegate* unmanaged<void*, void>)(c->Handler.CodeAddress + TimerIrq.SaveIdtrOffset))(c->ObservedIdtr);
            for (uint i = 0; i < 16; i++) if (((byte*)c->ObservedIdtr)[i] != ((byte*)c->NewIdtr)[i]) return false;
            return true;
        }
        private static bool LifecycleArm(LifecycleControl* c)
        {
            if (c->Timer.IsPrepared || c->Threads.Current.IsValid || c->Threads.HandoffPending || !LifecycleInstalled(c) ||
                !LifecycleObserve(c) || !LifecycleVerifyCode(c) || !CooperativeAuditPool(c->State, true) || c->Threads.ObservedTicks != c->Epoch ||
                c->Timer.Prepare(c->Key, c->State->ApicTopology.LocalApicAddress, 2000000, 8, ApicAccess(c->State), 2, c->Epoch) != TickTimer.Status.Ok) return false;
            *(ulong*)(c->State->Arena.Descriptors->Tss + 60) = c->State->CooperativePool + 13 * Page;
            return LifecycleInstalled(c) && c->Timer.Arm(c->Key, 2, true) == TickTimer.Status.Ok;
        }
        private static bool LifecycleWaitOne(LifecycleControl* c, bool alreadyArmed)
        {
            if ((!alreadyArmed && !LifecycleArm(c)) || !c->Timer.IsArmed || !LifecycleInstalled(c)) return false;
            ulong previous = c->Epoch;
            bool returned = ((delegate* unmanaged<uint>)(c->Plan.Owner.CodeStart + WorkerProgram.Idle))() == 1;
            // CLI is the first instruction after the interrupt-enabled HLT.
            // Save the epoch before Restore clears it. No IRQ frame is retained.
            ulong tick = c->Timer.Ticks;
            bool restored = c->Timer.Restore(c->Key, 2) == TickTimer.Status.Ok;
            if (!restored) return false;
            *(ulong*)(c->State->Arena.Descriptors->Tss + 60) = c->OriginalIst;
            return returned && c->Failed == 0 && tick == previous + 1 && c->Epoch == tick && LifecycleInstalled(c) &&
                LifecycleObserve(c) && CooperativeAuditPool(c->State, true);
        }
        private static bool LifecycleRestoreHardware(LifecycleControl* c)
        {
            if (c->Timer.IsPrepared && c->Timer.Restore(c->Key, 2) != TickTimer.Status.Ok) return false;
            if (c->Installed == 1)
            {
                byte* code = (byte*)c->Handler.CodeAddress;
                ((delegate* unmanaged<void*, void>)(code + TimerIrq.LoadIdtrOffset))(c->OriginalIdtr);
                ((delegate* unmanaged<void*, void>)(code + TimerIrq.SaveIdtrOffset))(c->ObservedIdtr);
                for (uint i = 0; i < 16; i++) if (((byte*)c->ObservedIdtr)[i] != ((byte*)c->OriginalIdtr)[i]) return false;
                *(ulong*)(c->State->Arena.Descriptors->Tss + 60) = c->OriginalIst;
                if (*(ulong*)(c->State->Arena.Descriptors->Tss + 60) != c->OriginalIst) return false;
                c->Installed = 0;
            }
            return true;
        }
        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LifecycleInterrupt(void* context, TimerIrq.Frame* frame)
        {
            LifecycleControl* c = (LifecycleControl*)context;
            if (c == null || frame == null || !LifecycleInstalled(c) || c->Threads.Current.IsValid || c->Threads.HandoffPending ||
                (ulong)frame != c->State->CooperativePool + 13 * Page - TimerIrq.FrameBytes || frame->Complete != 1 ||
                frame->Rip != c->Plan.Owner.CodeStart + WorkerProgram.Idle + 2 || frame->Cs != 8 || frame->Ss != 16 ||
                (frame->EntryFlags & 0x202) != 2 || (frame->Rflags & 0x602) != 0x202 ||
                (frame->Rflags & ~(Cooperative.AdmittedFlags | 0x10200UL)) != 0 ||
                !FirstExitOwnedArena.InMainStack(c->State->Arena, frame->Rsp, 32)) return 0;
            ulong* cpu = c->Cpu;
            ((delegate* unmanaged<ulong*, void>)(c->State->Memory.Code + FirstExitCodeBundle.CooperativeObserveOffset))(cpu);
            if (cpu[1] != c->Plan.Owner.Root || cpu[4] != 8 || cpu[5] != 0 || cpu[6] != 24 ||
                (cpu[3] & 0x602) != 2 || cpu[14] != c->Plan.Owner.KernelGs || cpu[15] != 0 || cpu[16] != 0) return 0;
            var status = c->Threads.HandleIdleTimer(c->Key, frame->EntryFlags, ref c->Timer, c->Key, frame->Rflags, out var tick);
            if (status != Lifecycle.Status.Ok || !tick.Succeeded || tick.Tick != c->Epoch + 1 || tick.Tick > 7) return 0;
            c->Epoch = tick.Tick;
            if (tick.Next == TickTimer.Action.WakeReady) c->WakeIntervals++;
            // Return the actual interrupted idle frame unchanged. Arbitrary
            // worker frames are never converted into cooperative continuations.
            return LifecycleText(c->State, 8, tick.Tick, (ulong)tick.Next, c->Threads.ReadyCount, tick.Due.Identity.Value) ? (byte)1 : (byte)0;
        }

        private static bool LifecycleReuse(LifecycleControl* c, ulong* originals, ulong cycle)
        {
            if (!LifecycleVerifyCode(c) || !LifecycleRestoreHardware(c)) return false;
            ulong rsp = LifecycleRsp(c); OSHostHandle old = c->A;
            if (c->Threads.Pin(c->Key, 2, old, true) != Lifecycle.Status.Ok ||
                c->Threads.Reap(c->Key, 2, old, rsp).Code != Lifecycle.Status.Pinned ||
                c->Threads.Pin(c->Key, 2, old, false) != Lifecycle.Status.Ok ||
                !CooperativeSetPoolMappings(c->State, originals, false, out _) || !CooperativeAuditPool(c->State, false)) return false;
            // IF is clear, timer/IDT/IST are restored, pool code is NX. Revoke the
            // old native binding before scrub; the table remains the sole owner.
            c->Plan.A = default; c->Plan.First.Initial = default;
            var reaped = c->Threads.Reap(c->Key, 2, old, rsp);
            if (!reaped.Succeeded || !LifecycleScrubbed(reaped.Context)) return false;
            c->Reaped++;
            if (!LifecycleText(c->State, 6, old.Value, c->Reaped, reaped.Context.ContextBytes, Page)) return false;
            c->Reuse = cycle;
            c->Plan.Owner.GuardsVerified = false; c->Plan.Owner.MappingsVerified = false;
            var d = reaped.Context; d.Identity += 1UL << 32; d.CurrentIdentity = d.Identity; d.Kind = Cooperative.ResumeKind.Entry;
            d.ResumeRip = c->Plan.Owner.CodeStart + WorkerProgram.EntryA;
            ulong arena = c->Plan.Owner.ContextArena;
            if (SavedContext.Initialize(d.Context, 688, d.ResumeRip, d.StackGuardStart + 2 * Page - 40,
                c->Plan.Owner.ObservedMxcsrMask, 2, arena + 0x1f00, arena + 0x1f08) != SavedContext.Status.Ok) return false;
            LifecycleSeed(d.Context, (uint)(cycle + 2)); c->Plan.A = d;
            c->Plan.First = LifecycleEntry(c, d, WorkerProgram.EntryA, WorkerProgram.ReusedWorker, WorkerProgram.FromA); LifecycleSync(c);
            if (!WorkerEntry.Emit((byte*)c->Plan.Owner.CodeStart + WorkerProgram.EntryA, 256, c->Plan.First)) return false;
            LifecycleSaveCode(c);
            if (!CooperativeSetPoolMappings(c->State, originals, true, out _) || !CooperativeAuditPool(c->State, true)) return false;
            c->Plan.Owner.GuardsVerified = true; c->Plan.Owner.MappingsVerified = true; LifecycleSync(c);
            ulong before = LifecycleDigest(c);
            var rejected = c->Threads.Create(c->Key, 2, null, 688, d.StackGuardStart, d.StackBytes, d.ResumeRip, c->Plan.First.Entry, (ulong)c);
            if (rejected.Code != Lifecycle.Status.InvalidContext || before != LifecycleDigest(c) ||
                !LifecycleText(c->State, 7, cycle, (ulong)rejected.Code, before, c->Threads.ReadyCount)) return false;
            var created = c->Threads.Create(c->Key, 2, d.Context, 688, d.StackGuardStart, d.StackBytes, d.ResumeRip, c->Plan.First.Entry, (ulong)c);
            if (!created.Succeeded || created.Identity.Value != d.Identity || c->Threads.Inspect(c->Key, 2, old).Code != Lifecycle.Status.StaleIdentity) return false;
            c->A = created.Identity;
            if (!LifecycleText(c->State, 5, old.Value, c->A.Value, (ulong)Lifecycle.Status.StaleIdentity, cycle) || !LifecycleInstall(c)) return false;
            // Two completed joins observe B's retained status, then one sleep.
            for (uint i = 0; i < 3; i++) if (!LifecycleDispatch(c)) return false;
            var waiting = c->Threads.Inspect(c->Key, 2, c->A);
            if (waiting.State != Lifecycle.State.Waiting || c->Threads.ReadyCount != 0 || c->Threads.DeadlineCount != 1) return false;
            if (cycle == 1)
            {
                if (!LifecycleArm(c)) return false;
                var cancelled = c->Threads.Cancel(c->Key, 2, c->A);
                if (!cancelled.Succeeded || cancelled.Completion != Lifecycle.Completion.Cancelled ||
                    !LifecycleText(c->State, 13, c->A.Value, waiting.Wait.Sequence, (ulong)cancelled.Completion, 1) || !LifecycleWaitOne(c, true)) return false;
            }
            else
            {
                if (!LifecycleWaitOne(c, false)) return false;
                var cancelled = c->Threads.Cancel(c->Key, 2, c->A);
                if (!cancelled.Succeeded || cancelled.Completion != Lifecycle.Completion.Deadline ||
                    !LifecycleText(c->State, 13, c->A.Value, waiting.Wait.Sequence, (ulong)cancelled.Completion, 2)) return false;
            }
            c->Cancellations++;
            var winner = c->Threads.Inspect(c->Key, 2, c->A);
            if (!winner.CancellationRequested || winner.Completion != (cycle == 1 ? Lifecycle.Completion.Cancelled : Lifecycle.Completion.Deadline) ||
                c->Threads.Wake(c->Key, 2, waiting.Wait).Code != Lifecycle.Status.AlreadyCompleted || !LifecycleDispatch(c)) return false;
            var exited = c->Threads.Inspect(c->Key, 2, c->A);
            return exited.State == Lifecycle.State.Exited && exited.ExitValue == -1 && c->Epoch == 5 + cycle;
        }
        private static bool LifecycleScrubbed(Cooperative.Descriptor d)
        {
            for (uint i = 0; i < d.ContextBytes; i++) if (((byte*)d.Context)[i] != 0) return false;
            for (ulong i = Page; i < d.StackBytes - Page; i++) if (((byte*)d.StackGuardStart)[i] != 0) return false;
            return true;
        }
        private static bool LifecycleFinish(LifecycleControl* c, ulong* originals)
        {
            if (c->Epoch != 7 || c->Cancellations != 2 || c->JoinObservations != 5 || c->Threads.ReadyCount != 0 || c->Threads.DeadlineCount != 0 ||
                !LifecycleVerifyCode(c) || !LifecycleRestoreHardware(c)) return false;
            ulong rsp = LifecycleRsp(c);
            if (!CooperativeSetPoolMappings(c->State, originals, false, out _) || !CooperativeAuditPool(c->State, false)) return false;
            OSHostHandle a = c->A, b = c->B;
            c->Plan.A = c->Plan.B = default; c->Plan.First.Initial = c->Plan.Second.Initial = default;
            var ra = c->Threads.Reap(c->Key, 2, a, rsp); var rb = c->Threads.Reap(c->Key, 2, b, rsp);
            if (!ra.Succeeded || !rb.Succeeded || !LifecycleScrubbed(ra.Context) || !LifecycleScrubbed(rb.Context)) return false;
            c->Reaped += 2;
            return LifecycleText(c->State, 6, a.Value, 3, ra.Context.ContextBytes, Page) && LifecycleText(c->State, 6, b.Value, 4, rb.Context.ContextBytes, Page) &&
                c->Threads.Release(c->Key, 2, rsp) == Lifecycle.Status.Ok && LifecycleText(c->State, 14, c->Epoch, c->Transfers, c->JoinObservations, c->WakeIntervals);
        }

        private static bool LifecycleText(Storage* state, uint kind, ulong a = 0, ulong b = 0, ulong c = 0, ulong d = 0)
        {
#if M09_009_PROBE || M09_010_PROBE
            return ThreadWaitText(state, kind, a, b, c, d);
#else
            string marker = kind switch { 0 => "TEST_START m09.006", 1 => "M09_006_PREPARED", 2 => "M09_006_TRANSFER", 3 => "M09_006_WAIT",
                4 => "M09_006_JOIN", 5 => "M09_006_REUSE", 6 => "M09_006_REAP", 7 => "M09_006_ROLLBACK", 8 => "M09_006_TICK",
                9 => "M09_006_RESTORED", 10 => "TEST_PASS m09.006", 11 => "CLROS_M09_006_READY", 12 => "TEST_FAIL m09.006 rejected",
                13 => "M09_006_CANCEL", 14 => "M09_006_COMPLETE", _ => "TEST_FAIL m09.006 marker" };
            byte* text = stackalloc byte[128]; uint length = 0;
            for (int i = 0; i < marker.Length; i++) text[length++] = (byte)marker[i];
            if (kind != 0 && kind != 10 && kind != 11)
            {
                ulong* values = stackalloc ulong[4] { a, b, c, d };
                for (uint i = 0; i < 4; i++)
                {
                    text[length++] = (byte)' '; text[length++] = (byte)'0'; text[length++] = (byte)'x';
                    for (int shift = 60; shift >= 0; shift -= 4)
                    { uint n = (uint)((values[i] >> shift) & 15); text[length++] = (byte)(n < 10 ? '0' + n : 'A' + n - 10); }
                }
            }
            text[length++] = 13; text[length++] = 10;
            return WriteOwnedBytes(state, text, length);
#endif
        }
    }
}
#endif
