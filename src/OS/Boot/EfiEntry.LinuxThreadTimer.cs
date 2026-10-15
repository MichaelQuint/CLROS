#if M16_008_PROBE
using OS.Kernel.Threading;
using NativeProgram = OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram;
using NativeSwitch = OS.Kernel.Threading.ThreadContextSwitch;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Borrow only IST4 and two IDT gates. The timer, copied IDT and guarded
        // IRQ stack remain live until quiescence and original-descriptor readback.
        private static bool LinuxNativeInstallTimer(LinuxNativeControl* c)
        {
            if (c == null || LinuxNativeControlOwner(c) != c || c->Installed != 0 || c->Timer.IsPrepared ||
                c->TimerDeadlines.IsInitialized || c->Published != 1 || c->ProcessAttached != 1 || c->TableAttached != 1 ||
                c->Threads.Current.IsValid || c->Threads.HandoffPending || !LinuxNativeObserve(c) ||
                !LinuxNativeVerify(c) || !CooperativeAuditPool(c->State, true)) return false;
            Storage* state = c->State;
            byte* code = (byte*)c->Handler.CodeAddress, idt = (byte*)(state->CooperativePool + OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram.IdtPage * Page);
            c->OriginalIdtr[0] = c->OriginalIdtr[1] = 0;
            ((delegate* unmanaged<void*, void>)(code + X64TimerInterrupt.SaveIdtrOffset))(c->OriginalIdtr);
            if (*(ushort*)c->OriginalIdtr != 4095 ||
                *(ulong*)((byte*)c->OriginalIdtr + 2) != (ulong)state->Arena.Descriptors->FinalIdt) return false;
            c->OriginalIst = *(ulong*)(state->Arena.Descriptors->Tss + 60);
            if (c->OriginalIst != 0) return false;
            for (uint i = 0; i < 104; i++) c->OriginalTss[i] = state->Arena.Descriptors->Tss[i];
            for (uint i = 0; i < 4096; i++) idt[i] = state->Arena.Descriptors->FinalIdt[i];
            LinuxNativeTimerGate(idt + 0xe1 * 16, c->Handler.CodeAddress);
            LinuxNativeTimerGate(idt + 0xff * 16, c->Handler.CodeAddress + X64TimerInterrupt.SpuriousOffset);
            // This queue holds only the retained supervisor identity in an IRQ
            // epoch. The process/table deadlines use actual HPET nanoseconds.
            if (c->TimerDeadlines.Initialize(c->Key, &c->TimerDeadline, (uint)sizeof(DeadlineQueue.Entry), 1, c->Epoch) != DeadlineQueue.Status.Ok)
                return false;
            c->NewIdtr[0] = c->NewIdtr[1] = 0;
            *(ushort*)c->NewIdtr = 4095; *(ulong*)((byte*)c->NewIdtr + 2) = (ulong)idt;
            c->Installed = 1; // Retain the original before the first hardware mutation.
            ((delegate* unmanaged<void*, void>)(code + X64TimerInterrupt.LoadIdtrOffset))(c->NewIdtr);
            return LinuxNativeTimerInstalled(c);
        }

        private static bool LinuxNativeTimerInstalled(LinuxNativeControl* c)
        {
            if (c == null || LinuxNativeControlOwner(c) != c || c->Installed != 1 || c->OriginalIst != 0) return false;
            byte* tss = c->State->Arena.Descriptors->Tss;
            ulong top = *(ulong*)(tss + 60), expected = c->State->CooperativePool + OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram.IrqTopPage * Page;
            if (top != 0 && top != expected) return false;
            for (uint i = 0; i < 4096; i++)
                if (!(i >= 60 && i < 68) && tss[i] != (i < 104 ? c->OriginalTss[i] : 0)) return false;
            byte* idt = (byte*)(c->State->CooperativePool + OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram.IdtPage * Page);
            if (!LinuxNativeTimerGateMatches(idt + 0xe1 * 16, c->Handler.CodeAddress) ||
                !LinuxNativeTimerGateMatches(idt + 0xff * 16, c->Handler.CodeAddress + X64TimerInterrupt.SpuriousOffset)) return false;
            for (uint i = 0; i < 4096; i++)
                if (!(i >= 0xe1 * 16 && i < 0xe2 * 16) && i < 0xff * 16 && idt[i] != c->State->Arena.Descriptors->FinalIdt[i]) return false;
            c->ObservedIdtr[0] = c->ObservedIdtr[1] = 0;
            ((delegate* unmanaged<void*, void>)(c->Handler.CodeAddress + X64TimerInterrupt.SaveIdtrOffset))(c->ObservedIdtr);
            for (uint i = 0; i < 16; i++) if (((byte*)c->ObservedIdtr)[i] != ((byte*)c->NewIdtr)[i]) return false;
            return true;
        }

        // Wait only on the emitted supervisor STI/HLT/CLI path. APIC reload is a
        // bounded count, not a calibrated duration; no worker IRQ can be resumed.
        private static bool LinuxNativeWaitOne(LinuxNativeControl* c)
        {
            if (c == null || LinuxNativeControlOwner(c) != c || c->Failed != 0 || c->Epoch >= c->Budget.MaximumEpochs ||
                c->Timer.IsPrepared || c->Threads.Current.IsValid || c->Threads.HandoffPending ||
                !c->TimerDeadlines.IsEmpty || c->TimerDeadlines.ObservedTicks != c->Epoch ||
                !LinuxNativeTimerInstalled(c) || !LinuxNativeObserve(c) || !LinuxNativeVerify(c) ||
                !CooperativeAuditPool(c->State, true) ||
                c->TimerDeadlines.Schedule(c->Key, new(c->Plan.Supervisor.Identity), new(c->Plan.Supervisor.CurrentIdentity), c->Epoch, 1) != DeadlineQueue.Status.Ok ||
                c->Timer.Prepare(c->Key, c->State->ApicTopology.LocalApicAddress, 2000000, 8, ApicAccess(c->State), 2, c->Epoch) != SchedulerTimer.Status.Ok)
                return false;
            *(ulong*)(c->State->Arena.Descriptors->Tss + 60) = c->State->CooperativePool + OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram.IrqTopPage * Page;
            if (!LinuxNativeTimerInstalled(c) || c->Timer.Arm(c->Key, 2, true) != SchedulerTimer.Status.Ok) return false;
            ulong previous = c->Epoch;
            bool returned = ((delegate* unmanaged<uint>)(c->Plan.Owner.CodeStart + NativeProgram.Idle))() == 1;
            // CLI is the first instruction after HLT. Save the acknowledged epoch
            // before Restore clears it; never drop IST while APIC owns a borrow.
            ulong tick = c->Timer.Ticks;
            if (c->Timer.Restore(c->Key, 2) != SchedulerTimer.Status.Ok) return false;
            *(ulong*)(c->State->Arena.Descriptors->Tss + 60) = c->OriginalIst;
            return returned && c->Failed == 0 && tick == previous + 1 && c->Epoch == tick &&
                c->TimerDeadlines.IsEmpty && c->TimerDeadlines.ObservedTicks == tick && LinuxNativeTimerInstalled(c) &&
                LinuxNativeObserve(c) && LinuxNativeVerify(c) && CooperativeAuditPool(c->State, true);
        }

        private static bool LinuxNativeRestoreTimer(LinuxNativeControl* c)
        {
            if (c == null || LinuxNativeControlOwner(c) != c || c->Installed > 1) return false;
            if (c->Timer.IsPrepared && c->Timer.Restore(c->Key, 2) != SchedulerTimer.Status.Ok) return false;
            if (c->Installed == 1)
            {
                if (!LinuxNativeTimerInstalled(c)) return false;
                byte* code = (byte*)c->Handler.CodeAddress;
                ((delegate* unmanaged<void*, void>)(code + X64TimerInterrupt.LoadIdtrOffset))(c->OriginalIdtr);
                c->ObservedIdtr[0] = c->ObservedIdtr[1] = 0;
                ((delegate* unmanaged<void*, void>)(code + X64TimerInterrupt.SaveIdtrOffset))(c->ObservedIdtr);
                for (uint i = 0; i < 16; i++) if (((byte*)c->ObservedIdtr)[i] != ((byte*)c->OriginalIdtr)[i]) return false;
                *(ulong*)(c->State->Arena.Descriptors->Tss + 60) = c->OriginalIst;
                for (uint i = 0; i < 4096; i++)
                    if (c->State->Arena.Descriptors->Tss[i] != (i < 104 ? c->OriginalTss[i] : 0)) return false;
                c->Installed = 0;
            }
            // A failed preparation/arm may leave the dummy pending. Only after
            // hardware restoration may its retained supervisor reference drain.
            if (c->TimerDeadlines.HasPending)
            {
                var next = c->TimerDeadlines.Peek(c->Key);
                if (c->TimerDeadlines.Count != 1 || !next.Succeeded || next.Value.Identity.Value != c->Plan.Supervisor.Identity ||
                    c->Epoch == ulong.MaxValue || next.Value.Deadline != c->Epoch + 1 ||
                    c->TimerDeadlines.Cancel(c->Key, next.Value.Identity) != DeadlineQueue.Status.Ok) return false;
            }
            return !c->TimerDeadlines.IsInitialized || c->TimerDeadlines.Release(c->Key) == DeadlineQueue.Status.Ok;
        }

        [System.Runtime.InteropServices.UnmanagedCallersOnly]
        private static byte LinuxNativeInterrupt(void* context, X64TimerInterrupt.Frame* frame)
        {
            LinuxNativeControl* c = LinuxNativeControlOwner(context);
#if M16_009_PROBE
            if (c!=null && c->UserDrain!=0) return LinuxNativeDrainUserInterrupt(c,frame);
#endif
            if (c == null || frame == null || !LinuxNativeTimerInstalled(c) || c->Threads.Current.IsValid || c->Threads.HandoffPending ||
                c->Epoch >= c->Budget.MaximumEpochs || c->Timer.Ticks != c->Epoch || c->TimerDeadlines.ObservedTicks != c->Epoch ||
                c->TimerDeadlines.Count != 1 || *(ulong*)(c->State->Arena.Descriptors->Tss + 60) != c->State->CooperativePool + OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram.IrqTopPage * Page ||
                (ulong)frame != c->State->CooperativePool + OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram.IrqTopPage * Page - X64TimerInterrupt.FrameBytes || frame->Complete != 1 ||
                frame->Rip != c->Plan.Owner.CodeStart + NativeProgram.Idle + 2 || frame->Cs != 8 || frame->Ss != 16 ||
                (frame->EntryFlags & 0x202) != 2 || (frame->Rflags & 0x602) != 0x202 ||
                (frame->Rflags & ~(NativeSwitch.AdmittedFlags | 0x10200UL)) != 0 ||
                !FirstExitOwnedArena.InMainStack(c->State->Arena, frame->Rsp, 32)) return 0;
            ulong* cpu = c->Cpu;
            ((delegate* unmanaged<ulong*, void>)(c->State->Memory.Code + FirstExitCodeBundle.CooperativeObserveOffset))(cpu);
            if (cpu[1] != c->Plan.Owner.Root || cpu[4] != 8 || cpu[5] != 0 || cpu[6] != 24 ||
                (cpu[3] & 0x602) != 2 || cpu[14] != c->Plan.Owner.KernelGs || cpu[15] != 0 || cpu[16] != 0) return 0;
            var tick = c->Timer.HandleInterrupt(c->Key, frame->EntryFlags, frame->Rflags,
                ref c->TimerDeadlines, c->Key, SchedulerTimer.Phase.Safe);
            if (!tick.Succeeded || tick.Next != SchedulerTimer.Action.WakeReady || tick.Tick != c->Epoch + 1 ||
                tick.Tick > c->Budget.MaximumEpochs || tick.Due.Identity.Value != c->Plan.Supervisor.Identity || tick.Due.Deadline != tick.Tick) return 0;
            DeadlineQueue.Entry due = default;
            var drained = c->TimerDeadlines.Drain(c->Key, tick.Tick, &due, (uint)sizeof(DeadlineQueue.Entry), 1);
            if (!drained.Succeeded || drained.Count != 1 || due.Identity.Value != tick.Due.Identity.Value || due.Deadline != tick.Tick) return 0;
            c->Epoch = tick.Tick;
            // HPET is the sole process clock. Acknowledging an APIC interrupt
            // never injects its small epoch into Linux's nanosecond deadlines.
            return c->Process.AdvanceFutexClock(c->Key, 2) && LinuxText(c->State, "M16_008_TICK epoch=") && LinuxNumber(c->State, c->Epoch) &&
                LinuxText(c->State, " nanos=") && LinuxNumber(c->State, c->Threads.ObservedTicks) &&
                LinuxText(c->State, " ready=") && LinuxNumber(c->State, c->Threads.ReadyCount) && LinuxText(c->State, "\r\n") ? (byte)1 : (byte)0;
        }

        private static void LinuxNativeTimerGate(byte* gate, ulong address)
        {
            *(ushort*)gate = (ushort)address; *(ushort*)(gate + 2) = 8; gate[4] = 4; gate[5] = 0x8e;
            *(ushort*)(gate + 6) = (ushort)(address >> 16); *(uint*)(gate + 8) = (uint)(address >> 32); *(uint*)(gate + 12) = 0;
        }

        private static bool LinuxNativeTimerGateMatches(byte* gate, ulong address) => *(ushort*)gate == (ushort)address &&
            *(ushort*)(gate + 2) == 8 && gate[4] == 4 && gate[5] == 0x8e && *(ushort*)(gate + 6) == (ushort)(address >> 16) &&
            *(uint*)(gate + 8) == (uint)(address >> 32) && *(uint*)(gate + 12) == 0;
    }
}
#endif
