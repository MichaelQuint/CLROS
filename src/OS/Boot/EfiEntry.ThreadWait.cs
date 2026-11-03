#if M09_009_PROBE || M09_010_PROBE
using OS.Kernel.Boot;
using OS.Kernel.Diagnostics;
using OS.Pal;
using Waiter = OS.Kernel.Threading.WaitAddress;
using TlsOwner = OS.Kernel.Threading.ThreadLocalStorage;
using WaitProgram = OS.Kernel.Threading.ThreadWaitProgram;
using WorkerProgram = OS.Kernel.Threading.KernelThreadProgram;
using WorkerEntry = OS.Kernel.Threading.KernelThreadEntry;
using Lifecycle = OS.Kernel.Threading.KernelThreadTable;
using Cooperative = OS.Kernel.Threading.ThreadContextSwitch;
using SavedContext = OS.Kernel.Threading.X64ThreadContext;
using TimerIrq = OS.Kernel.Threading.X64TimerInterrupt;

namespace OS.Boot;

#pragma warning disable 649 // Adjacent caller-owned slots are reached through their first element.


public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {














        private static ulong ThreadWaitKey(LifecycleControl* c) => c->Key ^ 0x57414954;
        private static ulong ThreadTlsKey(LifecycleControl* c) => c->Key ^ 0x544c53;
        private static TlsOwner.Region ThreadTlsRegion(LifecycleControl* c, uint worker, bool gs)
        {
            ulong start = c->Plan.Owner.ContextArena + 0x1f00 + (worker - 1) * 128 + (gs ? 64UL : 0);
            return new(start, 64, start);
        }
        private static ulong ThreadTlsValue(LifecycleControl* c, uint worker, bool gs) =>
            (gs ? 0x4753000000000000UL : 0x4653000000000000UL) | ((c->Reuse + 1) << 32) | worker;

        private static bool ThreadWaitPrepare(LifecycleControl* c)
        {
            ulong pool = c->Plan.Owner.CodeStart;
            if (sizeof(ThreadWaitControl) > 0xa00) return false;
            c->Wait = (ThreadWaitControl*)(pool + 23 * Page); *c->Wait = default;
            c->Key = pool ^ (Probes.EnableM09010Probe ? 0x4d30393031304f57UL : 0x4d30393030394f57UL);
            c->Plan.Owner.Key = c->Plan.Owner.CurrentKey = c->Key;
            c->Plan.Stub.AdmissionCallback = (ulong)(delegate* unmanaged<void*, SavedContext*, SavedContext*, byte>)&ThreadWaitTransfer;
            for (uint worker = 1; worker <= 2; worker++)
            {
                SavedContext* x = worker == 1 ? c->Plan.A.Context : c->Plan.B.Context;
                x->FsBase = ThreadTlsRegion(c, worker, false).Base; x->UserGsBase = ThreadTlsRegion(c, worker, true).Base;
                ThreadTlsSeed(c, worker);
            }
            LifecycleSync(c);
            if (!LifecycleImageCallback(c, c->Plan.Stub.AdmissionCallback) || !LifecycleImageCallback(c, c->Plan.First.Start) ||
                !LifecycleImageCallback(c, c->Plan.RecoverCallback) || !LifecycleImageCallback(c, c->Plan.TerminalCallback) ||
                !LifecycleImageCallback(c, c->Handler.Callback) || !WaitProgram.Emit((byte*)pool, WaitProgram.Bytes, c->Plan, Probes.EnableM09010Probe) ||
                !TimerIrq.Emit((byte*)c->Handler.CodeAddress, 512, c->Handler)) return false;
            LifecycleSaveCode(c);
            return LifecycleVerifyCode(c) && LifecycleText(c->State, 1, pool, 24, pool + 13 * Page, c->Key);
        }
        private static void ThreadTlsSeed(LifecycleControl* c, uint worker)
        {
            for (uint segment = 0; segment < 2; segment++)
            {
                var region = ThreadTlsRegion(c, worker, segment != 0);
                for (ulong i = 0; i < region.Bytes; i++) ((byte*)region.Start)[i] = (byte)(0x30 + worker + c->Reuse * 8 + segment);
                *(ulong*)region.Base = ThreadTlsValue(c, worker, segment != 0);
            }
        }
        private static bool ThreadWaitBind(LifecycleControl* c)
        {
            ThreadWaitControl* w = c->Wait; ulong page = (ulong)w;
            if (w == null || !LifecycleObserve(c) ||
                w->Addresses.Initialize(ThreadWaitKey(c), 2, &c->Threads, c->Key, &w->AddressSlotA, (uint)sizeof(Waiter.Slot) * 2, 2,
                    &w->RegistrationA, (uint)sizeof(Waiter.Registration) * 2, 2) != Waiter.Status.Ok ||
                w->Tls.Initialize(ThreadTlsKey(c), c->Plan.Owner, &c->Threads, c->Key, &w->TlsSlotA, (uint)sizeof(TlsOwner.Slot) * 2, 2,
                    c->Plan.Owner.ContextArena + 0x1f00, 256) != TlsOwner.Status.Ok) return false;
            uint* a = (uint*)(page + 0xa00), b = (uint*)(page + 0xc00); *a = *b = 7;
            var ba = w->Addresses.Bind(ThreadWaitKey(c), 2, a, 1); var bb = w->Addresses.Bind(ThreadWaitKey(c), 2, b, 2);
            if (!ba.Succeeded || !bb.Succeeded || (((ulong)a >> 3) & 63) != (((ulong)b >> 3) & 63) || a == b) return false;
            w->AddressA = ba.Address; w->AddressB = bb.Address;
            return ThreadTlsBind(c) && w->Addresses.Bind(ThreadWaitKey(c), 2, (uint*)(page + 0xe00), 3).Code == Waiter.Status.Full;
        }
        private static bool ThreadTlsBind(LifecycleControl* c)
        {
            ThreadWaitControl* w = c->Wait;
            var a = w->Tls.Bind(ThreadTlsKey(c), c->Plan.Owner, c->A, ThreadTlsRegion(c, 1, false), ThreadTlsRegion(c, 1, true));
            var b = w->Tls.Bind(ThreadTlsKey(c), c->Plan.Owner, c->B, ThreadTlsRegion(c, 2, false), ThreadTlsRegion(c, 2, true));
            if (!a.Succeeded || !b.Succeeded) return false; w->TlsA = a.Identity; w->TlsB = b.Identity;
            return w->Tls.Admit(ThreadTlsKey(c), c->Plan.Owner, w->TlsA, w->TlsB) == TlsOwner.Status.Ok;
        }

        private static bool ThreadWaitWake(LifecycleControl* c, bool second, uint maximum, uint expected)
        {
            var address = second ? c->Wait->AddressB : c->Wait->AddressA;
            var result = c->Wait->Addresses.Wake(ThreadWaitKey(c), 2, address, maximum);
            return result.Succeeded && result.Count == expected &&
                LifecycleText(c->State, 15, address.Value, maximum, result.Count, c->Epoch);
        }
        private static bool ThreadWaitCompletion(LifecycleControl* c, bool first, Lifecycle.Completion completion, Lifecycle.State state)
        {
            var x = c->Threads.Inspect(c->Key, 2, first ? c->A : c->B);
            return x.Succeeded && x.Completion == completion && x.State == state &&
                LifecycleText(c->State, 4, x.Identity.Value, (ulong)x.State, (ulong)x.Completion, x.Wait.Sequence);
        }
        private static bool ThreadWaitRun(LifecycleControl* c)
        {
            // Each CALL continuation is real emitted worker code. IF is clear
            // across compare/enroll/park/transfer; only the supervisor wakes.
            if (!LifecycleDispatch(c) || !LifecycleDispatch(c) ||
                !ThreadWaitCompletion(c, true, Lifecycle.Completion.Woken, Lifecycle.State.Ready) ||
                !ThreadWaitCompletion(c, false, Lifecycle.Completion.None, Lifecycle.State.Waiting) ||
                !ThreadWaitWake(c, false, uint.MaxValue, 0) || !ThreadWaitWake(c, true, 1, 1)) return false;
            if (!LifecycleDispatch(c) || !LifecycleDispatch(c) || !ThreadWaitWake(c, false, 1, 1) ||
                !ThreadWaitCompletion(c, true, Lifecycle.Completion.Woken, Lifecycle.State.Ready) ||
                !ThreadWaitCompletion(c, false, Lifecycle.Completion.None, Lifecycle.State.Waiting) ||
                !ThreadWaitWake(c, false, uint.MaxValue, 1) || c->Threads.DeadlineCount != 0) return false;
            if (!LifecycleDispatch(c) || !LifecycleDispatch(c) || !ThreadWaitWake(c, false, 1, 1) ||
                !LifecycleWaitOne(c, false) || !ThreadWaitWake(c, true, uint.MaxValue, 0) ||
                !ThreadWaitCompletion(c, true, Lifecycle.Completion.Woken, Lifecycle.State.Ready) ||
                !ThreadWaitCompletion(c, false, Lifecycle.Completion.Deadline, Lifecycle.State.Ready)) return false;
            if (!LifecycleDispatch(c) || !LifecycleDispatch(c) || !LifecycleArm(c)) return false;
            var a = c->Threads.Cancel(c->Key, 2, c->A);
            if (!a.Succeeded || a.Completion != Lifecycle.Completion.Cancelled ||
                !LifecycleText(c->State, 13, c->A.Value, a.Wait.Sequence, (ulong)a.Completion, 1) || !LifecycleWaitOne(c, true)) return false;
            var b = c->Threads.Cancel(c->Key, 2, c->B);
            if (!b.Succeeded || b.Completion != Lifecycle.Completion.Deadline ||
                !LifecycleText(c->State, 13, c->B.Value, b.Wait.Sequence, (ulong)b.Completion, 2) ||
                !ThreadWaitWake(c, false, uint.MaxValue, 0) || !LifecycleDispatch(c) || !LifecycleDispatch(c)) return false;
            var ax = c->Threads.Inspect(c->Key, 2, c->A); var bx = c->Threads.Inspect(c->Key, 2, c->B);
            return ax.State == Lifecycle.State.Exited && bx.State == Lifecycle.State.Exited && ax.ExitValue == 17 && bx.ExitValue == 23 &&
                ax.CancellationRequested && bx.CancellationRequested && c->Wait->NextA == 4 && c->Wait->NextB == 4 &&
                c->Threads.ReadyCount == 0 && c->Threads.DeadlineCount == 0;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte ThreadWaitTransfer(void* context, SavedContext* source, SavedContext* destination)
        {
            LifecycleControl* c = (LifecycleControl*)context;
            if (c == null || c->Wait == null || c->Selected == 0 || !LifecycleObserve(c) ||
                !CooperativeAuditPool(c->State, true)) return 0;
            bool first = c->Selected == 1; var worker = first ? c->Plan.A : c->Plan.B; var identity = first ? c->A : c->B;
            var binding = first ? c->Wait->TlsA : c->Wait->TlsB; ulong pool = c->Plan.Owner.CodeStart;
            if (source == c->Plan.Supervisor.Context && destination == worker.Context)
            {
                c->Plan.Supervisor.ResumeRip = pool + (first ? WorkerProgram.DriverA : WorkerProgram.DriverB) + WorkerProgram.DriverResume;
                LifecycleSync(c);
                var running = c->Threads.Inspect(c->Key, 2, identity);
                if (!running.Succeeded || running.State != Lifecycle.State.Running || c->Threads.Current.Value != identity.Value ||
                    !c->Wait->Tls.Inspect(ThreadTlsKey(c), c->Plan.Owner, binding).Succeeded || !LifecycleVerifyCode(c) ||
                    Cooperative.Admit(c->Plan.Owner, c->Plan.Supervisor, worker) != Cooperative.Status.Ok) return 0;
                return LifecycleTransferRow(c, 0x1ffffffff, identity.Value, source->Rip);
            }
            if (source != worker.Context || destination != c->Plan.Supervisor.Context || c->Threads.Current.Value != identity.Value) return 0;
            uint next = first ? c->Wait->NextA : c->Wait->NextB, number = first ? 1U : 2U;
            uint entry = first ? WorkerProgram.EntryA : WorkerProgram.EntryB;
            uint body = first ? WorkerProgram.WorkerA : WorkerProgram.WorkerB;
            bool returned = source->Rip == pool + entry + WorkerEntry.ExitResumeOffset;
            if (returned ? next != 4 : next >= 4 || source->Rip != pool + body + WaitProgram.ActionResume + next * WaitProgram.ActionStride || source->Rdx != next) return 0;
            if (!ThreadWaitSentinels(c, source, number, worker.StackGuardStart, returned)) return 0;
            worker.Kind = Cooperative.ResumeKind.CapturedCall; worker.ResumeRip = source->Rip;
            if (first) c->Plan.A = worker; else c->Plan.B = worker; LifecycleSync(c);
            if (!LifecycleVerifyCode(c) || Cooperative.Admit(c->Plan.Owner, worker, c->Plan.Supervisor) != Cooperative.Status.Ok) return 0;
            Lifecycle.Result result;
            if (returned)
            {
                int value = first ? 17 : 23;
                if (source->Rdx != (ulong)value) return 0;
                result = c->Threads.Exit(c->Key, 2, identity, worker, value);
            }
            else
            {
                var address = (next == 0 || next == 2) && !first ? c->Wait->AddressB : c->Wait->AddressA;
                if (next == 0 && first)
                {
                    uint* word = (uint*)((ulong)c->Wait + 0xa00); *word = 8;
                    var changed = c->Wait->Addresses.Begin(ThreadWaitKey(c), 2, address, identity, 7, c->Epoch); *word = 7;
                    if (changed.Code != Waiter.Status.ValueChanged || !LifecycleText(c->State, 7, 1, (ulong)changed.Code, identity.Value, c->Reuse)) return 0;
                }
                var begin = c->Wait->Addresses.Begin(ThreadWaitKey(c), 2, address, identity, 7, c->Epoch, next != 0, next == 1 ? 2UL : 1UL);
                if (!begin.Succeeded) return 0;
                if (next == 0 && first && !ThreadWaitWake(c, false, 1, 1)) return 0;
                result = c->Threads.Park(c->Key, 2, begin.Wait, worker);
                if (!result.Succeeded || !LifecycleText(c->State, 3, identity.Value, begin.Wait.Sequence, address.Value, next)) return 0;
                // Wake-before-Park resumes in place. This fixture deliberately
                // returns to its supervisor after each action, so capture a real
                // ready/handoff transition rather than treating Running as parked.
                if (result.State == Lifecycle.State.Running) result = c->Threads.Yield(c->Key, 2, identity, worker);
                if (first) c->Wait->NextA++; else c->Wait->NextB++;
            }
            if (!result.Succeeded || !c->Threads.HandoffPending || !c->Wait->Tls.Inspect(ThreadTlsKey(c), c->Plan.Owner, binding).Succeeded ||
                !LifecycleText(c->State, 16, identity.Value, source->FsBase, source->UserGsBase, c->Plan.Owner.KernelGs)) return 0;
            return LifecycleTransferRow(c, identity.Value, 0x1ffffffff, source->Rip);
        }
        private static bool ThreadWaitSentinels(LifecycleControl* c, SavedContext* x, uint worker, ulong guard, bool returned)
        {
            ulong r12 = Probes.EnableM09010Probe && !returned ? ThreadTlsValue(c, worker, false) : CooperativeGpr(worker, 12);
            ulong r13 = Probes.EnableM09010Probe && !returned ? ThreadTlsValue(c, worker, true) : CooperativeGpr(worker, 13);
            if (x->Rsp != guard + 2 * Page - (returned ? 80UL : 192UL) || x->Rbx != CooperativeGpr(worker, 3) ||
                x->Rbp != CooperativeGpr(worker, 5) || x->Rsi != CooperativeGpr(worker, 6) || x->Rdi != CooperativeGpr(worker, 7) ||
                x->R12 != r12 || x->R13 != r13 || x->R14 != CooperativeGpr(worker, 14) || x->R15 != CooperativeGpr(worker, 15) ||
                x->FsBase != ThreadTlsRegion(c, worker, false).Base || x->UserGsBase != ThreadTlsRegion(c, worker, true).Base ||
                (x->Rflags & ~Cooperative.AdmittedFlags) != 0 || (x->Rflags & 0x600) != 0 ||
                *(ushort*)x->FxSave != 0x027f || x->FxSave[4] != 0 || *(uint*)(x->FxSave + 24) != 0x1f80) return false;
            for (uint register = 6; register < 16; register++) for (uint part = 0; part < 16; part++)
                if (x->FxSave[160 + register * 16 + part] != (byte)(0x10 + worker * 0x20 + register * 7 + part * 3)) return false;
            return *(ulong*)x->FsBase == ThreadTlsValue(c, worker, false) && *(ulong*)x->UserGsBase == ThreadTlsValue(c, worker, true);
        }

        private static bool ThreadWaitReap(LifecycleControl* c, ulong* originals, bool reuse)
        {
            if (!LifecycleVerifyCode(c) || !LifecycleRestoreHardware(c) || !LifecycleObserve(c)) return false;
            ulong rsp = LifecycleRsp(c); ThreadWaitControl* w = c->Wait;
            if (!CooperativeSetPoolMappings(c->State, originals, false, out _) || !CooperativeAuditPool(c->State, false)) return false;
            // No timer, active segment or executable pool reference remains.
            // Reap authenticates the actual table generation and scrubs both TLS
            // spans only after the supervisor/stack/pin guards succeed.
            var oldA = c->A; var oldB = c->B; var ta = w->TlsA; var tb = w->TlsB;
            c->Plan.A = c->Plan.B = default; c->Plan.First.Initial = c->Plan.Second.Initial = default;
            var a = w->Tls.Reap(ThreadTlsKey(c), c->Plan.Owner, ta, rsp, c->Cpu[12], c->Cpu[13]);
            var b = w->Tls.Reap(ThreadTlsKey(c), c->Plan.Owner, tb, rsp, c->Cpu[12], c->Cpu[13]);
            if (!a.Succeeded || !b.Succeeded || a.ReapedBytes != 128 || b.ReapedBytes != 128 || !LifecycleScrubbed(a.Descriptor) || !LifecycleScrubbed(b.Descriptor)) return false;
            for (uint i = 0; i < 256; i++) if (((byte*)(c->Plan.Owner.ContextArena + 0x1f00))[i] != 0) return false;
            c->Reaped += 2;
            if (!LifecycleText(c->State, 6, oldA.Value, c->Reaped - 1, 688, 128) || !LifecycleText(c->State, 6, oldB.Value, c->Reaped, 688, 128) ||
                !w->Addresses.Sweep(ThreadWaitKey(c), 2).Succeeded ||
                w->Tls.Inspect(ThreadTlsKey(c), c->Plan.Owner, ta).Code != TlsOwner.Status.StaleBinding) return false;
            var old = w->AddressA;
            if (w->Addresses.Unbind(ThreadWaitKey(c), 2, old) != Waiter.Status.Ok ||
                w->Addresses.Begin(ThreadWaitKey(c), 2, old, oldA, 7, c->Epoch).Code != Waiter.Status.StaleBinding) return false;
            if (!reuse)
                return w->Addresses.Unbind(ThreadWaitKey(c), 2, w->AddressB) == Waiter.Status.Ok &&
                    w->Addresses.Release(ThreadWaitKey(c), 2) == Waiter.Status.Ok && w->Tls.Release(ThreadTlsKey(c), c->Plan.Owner) == TlsOwner.Status.Ok &&
                    c->Threads.Release(c->Key, 2, rsp) == Lifecycle.Status.Ok && LifecycleText(c->State, 14, c->Epoch, c->Transfers, 4, c->WakeIntervals);
            var rebound = w->Addresses.Bind(ThreadWaitKey(c), 2, (uint*)((ulong)w + 0xa00), 3);
            if (!rebound.Succeeded || rebound.Address.Value == old.Value) return false; w->AddressA = rebound.Address;
            c->Reuse = 1; w->NextA = w->NextB = 0;
            c->Plan.Owner.GuardsVerified = c->Plan.Owner.MappingsVerified = false;
            c->Plan.A = a.Descriptor; c->Plan.B = b.Descriptor;
            for (uint worker = 1; worker <= 2; worker++)
            {
                var d = worker == 1 ? c->Plan.A : c->Plan.B;
                d.Identity += 1UL << 32; d.CurrentIdentity = d.Identity; d.Kind = Cooperative.ResumeKind.Entry;
                d.ResumeRip = c->Plan.Owner.CodeStart + (worker == 1 ? WorkerProgram.EntryA : WorkerProgram.EntryB);
                if (SavedContext.Initialize(d.Context, 688, d.ResumeRip, d.StackGuardStart + 2 * Page - 40,
                    c->Plan.Owner.ObservedMxcsrMask, 2, ThreadTlsRegion(c, worker, false).Base, ThreadTlsRegion(c, worker, true).Base) != SavedContext.Status.Ok) return false;
                LifecycleSeed(d.Context, worker); ThreadTlsSeed(c, worker);
                if (worker == 1) c->Plan.A = d; else c->Plan.B = d;
            }
            c->Plan.First = LifecycleEntry(c, c->Plan.A, WorkerProgram.EntryA, WorkerProgram.WorkerA, WorkerProgram.FromA);
            c->Plan.Second = LifecycleEntry(c, c->Plan.B, WorkerProgram.EntryB, WorkerProgram.WorkerB, WorkerProgram.FromB); LifecycleSync(c);
            if (!WaitProgram.Emit((byte*)c->Plan.Owner.CodeStart, WaitProgram.Bytes, c->Plan, Probes.EnableM09010Probe) ||
                !TimerIrq.Emit((byte*)c->Handler.CodeAddress, 512, c->Handler)) return false;
            LifecycleSaveCode(c);
            if (!CooperativeSetPoolMappings(c->State, originals, true, out _) || !CooperativeAuditPool(c->State, true)) return false;
            c->Plan.Owner.GuardsVerified = c->Plan.Owner.MappingsVerified = true; LifecycleSync(c);
            var ca = c->Threads.Create(c->Key, 2, c->Plan.A.Context, 688, c->Plan.A.StackGuardStart, 3 * Page, c->Plan.A.ResumeRip, c->Plan.First.Entry, (ulong)c);
            var cb = c->Threads.Create(c->Key, 2, c->Plan.B.Context, 688, c->Plan.B.StackGuardStart, 3 * Page, c->Plan.B.ResumeRip, c->Plan.Second.Entry, (ulong)c);
            if (!ca.Succeeded || !cb.Succeeded || ca.Identity.Value != c->Plan.A.Identity || cb.Identity.Value != c->Plan.B.Identity ||
                c->Threads.Inspect(c->Key, 2, oldA).Code != Lifecycle.Status.StaleIdentity || c->Threads.Inspect(c->Key, 2, oldB).Code != Lifecycle.Status.StaleIdentity) return false;
            c->A = ca.Identity; c->B = cb.Identity;
            return ThreadTlsBind(c) && LifecycleText(c->State, 5, oldA.Value, c->A.Value, old.Value, w->AddressA.Value) && LifecycleInstall(c);
        }

        private static bool ThreadWaitText(Storage* state, uint kind, ulong a, ulong b, ulong c, ulong d)
        {
            string suffix = kind switch { 1 => "PREPARED", 2 => "TRANSFER", 3 => "WAIT", 4 => "OBSERVE", 5 => "REUSE", 6 => "REAP", 7 => "REJECT",
                8 => "TICK", 9 => "RESTORED", 13 => "CANCEL", 14 => "COMPLETE", 15 => "WAKE", 16 => "TLS", _ => "INVALID" };
            string name = Probes.EnableM09010Probe ? "m09.010" : "m09.009";
            string prefix = Probes.EnableM09010Probe ? "M09_010_" : "M09_009_";
            string marker = kind == 0 ? "TEST_START " : kind == 10 ? "TEST_PASS " : kind == 12 ? "TEST_FAIL " : kind == 11 ? "CLROS_" : prefix;
            byte* text = stackalloc byte[128]; uint length = 0;
            for (int i = 0; i < marker.Length; i++) text[length++] = (byte)marker[i];
            string ending = kind is 0 or 10 or 12 ? name : kind == 11 ? Probes.EnableM09010Probe ? "M09_010_READY" : "M09_009_READY" : suffix;
            for (int i = 0; i < ending.Length; i++) text[length++] = (byte)ending[i];
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
            text[length++] = 13; text[length++] = 10; return WriteOwnedBytes(state, text, length);
        }
    }
}
#endif
