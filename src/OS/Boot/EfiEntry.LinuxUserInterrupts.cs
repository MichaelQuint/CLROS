#if M16_009_PROBE
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using NativeProgram = OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram;
using NativeSwitch = OS.Kernel.Threading.ThreadContextSwitch;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // One BSP owns this borrow from the prepared worker through the actual
        // restored-kernel CALL capture. Neither syscalls nor faults enable IF
        // in C#. Only checked user IRETQ and the explicit drain checkpoint do.
        private static bool LinuxNativeArmUserTimer(LinuxNativeControl* c, uint index)
        {
            if (!LinuxNativeUserWorker(c, index) || c->UserTimer != 0 || c->UserAcknowledged != 0 || c->UserDrain != 0 ||
                c->UserPreempted != 0 || c->Timer.IsPrepared || c->Epoch >= c->Budget.MaximumEpochs ||
                !c->TimerDeadlines.IsEmpty || c->TimerDeadlines.ObservedTicks != c->Epoch ||
                !LinuxNativeTimerInstalled(c) || !LinuxNativeObserve(c) || !LinuxNativeVerify(c) ||
                !LinuxNativeUserGates(c, false) || !CooperativeAuditPool(c->State, true)) return false;
            var worker = c->Plan.Bindings[index].Context;
            if (c->TimerDeadlines.Schedule(c->Key, new(worker.Identity), new(worker.CurrentIdentity), c->Epoch, 1) != DeadlineQueue.Status.Ok)
                return false;
            c->UserTimer = index + 1; // Retain partial preparation until verified restoration.
            if (c->Timer.Prepare(c->Key, c->State->ApicTopology.LocalApicAddress, 2000000, 8,
                ApicAccess(c->State), 2, c->Epoch) != SchedulerTimer.Status.Ok) return false;
            *(ulong*)(c->State->Arena.Descriptors->Tss + 60) = c->State->CooperativePool + OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram.IrqTopPage * Page;
            return LinuxNativeTimerInstalled(c) && c->Timer.Arm(c->Key, 2, true) == SchedulerTimer.Status.Ok;
        }

        private static bool LinuxNativeUserWorker(LinuxNativeControl* c, uint index) =>
            c != null && LinuxNativeControlOwner(c) == c && c->User.UserInterrupts &&
            c->State->LinuxSyscallPlan.UserInterrupts && c->Installed == 1 && index < c->Capacity &&
            c->Selected == index + 1 && c->Threads.Current.Value == c->Plan.Bindings[index].Context.Identity &&
            !c->Threads.HandoffPending;

        // Validate both the retained private IDT and its actual active envelope.
        // The gateway changes RSP0/IDTR; the supervisor timer validator cannot
        // stand in for those observations. Every unborrowed byte stays original.
        private static bool LinuxNativeUserGates(LinuxNativeControl* c, bool active)
        {
            Storage* state = c->State;
            byte* data = (byte*)state->LinuxSyscallData, idt = data + X64LinuxUserTransition.IdtOffset;
            var plan = state->LinuxSyscallPlan;
            if (!plan.UserInterrupts || plan.UserSpuriousEntry != c->Handler.CodeAddress + X64TimerInterrupt.SpuriousOffset ||
                !LinuxSupervisorLeaf(state, plan.UserFaultEntry, true, false) ||
                !LinuxSupervisorLeaf(state, plan.UserFaultEntry + X64LinuxUserTransition.UserInterruptOffset, true, false) ||
                !LinuxSupervisorLeaf(state, plan.UserSpuriousEntry, true, false) ||
                !LinuxNativeTimerGateMatches(idt + 0xe1 * 16, plan.UserFaultEntry + X64LinuxUserTransition.UserInterruptOffset) ||
                !LinuxNativeTimerGateMatches(idt + 0xff * 16, plan.UserSpuriousEntry) ||
                !LinuxNativeUserGate(idt + 13 * 16, plan.CodeAddress + X64LinuxUserTransition.GpOffset) ||
                !LinuxNativeUserGate(idt + 14 * 16, plan.CodeAddress + X64LinuxUserTransition.PfOffset)) return false;
            for (uint i = 0; i < 4096; i++)
                if (!(i >= 13 * 16 && i < 15 * 16) && !(i >= 0xe1 * 16 && i < 0xe2 * 16) &&
                    i < 0xff * 16 && idt[i] != state->Arena.Descriptors->FinalIdt[i]) return false;
            if (!active) return true;
            if (*(ulong*)(state->Arena.Descriptors->Tss + 4) != state->LinuxSyscallData + X64LinuxUserTransition.DataBytes ||
                *(ulong*)(state->Arena.Descriptors->Tss + 60) != state->CooperativePool + OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram.IrqTopPage * Page) return false;
            for (uint i = 0; i < 4096; i++)
                if (!(i >= 4 && i < 12) && !(i >= 60 && i < 68) &&
                    state->Arena.Descriptors->Tss[i] != (i < 104 ? c->OriginalTss[i] : 0)) return false;
            ulong* observed = stackalloc ulong[(int)X64LinuxUserTransition.ObservationWords];
            ((delegate* unmanaged<ulong*, void>)(state->LinuxSyscallCode + X64LinuxUserTransition.ObserveOffset))(observed);
            return (observed[3] & 0x602) == 2 && observed[4] == 8 && observed[5] == 0 && observed[6] == 24 &&
                observed[17] == *(ulong*)(data + 0x98) && observed[18] == *(ulong*)(data + 0xa0) &&
                observed[19] == *(ulong*)(data + 0xb8) && observed[20] == *(ulong*)(data + 0xc0);
        }

        private static bool LinuxNativeUserGate(byte* gate, ulong address) =>
            *(ushort*)gate == (ushort)address && *(ushort*)(gate + 2) == 8 && gate[4] == 0 && gate[5] == 0x8e &&
            *(ushort*)(gate + 6) == (ushort)(address >> 16) && *(uint*)(gate + 8) == (uint)(address >> 32) && *(uint*)(gate + 12) == 0;

        // The common full capture has already SWAPGS'd, saved every GPR/FX/base
        // and adopted the original kernel root. This path stages a yield, never
        // fabricates a syscall, handler, CALL context or scheduling completion.
        private static byte LinuxNativeUserInterrupt(Storage* state, byte* data)
        {
            LinuxNativeControl* c = LinuxNativeOwner(state);
            if (c == null || data != (byte*)state->LinuxSyscallData || c->Selected == 0) return 0;
            uint index = c->Selected - 1;
            if (!LinuxNativeUserWorker(c, index) || c->Entered[index] != 1 || c->UserTimer != index + 1 ||
                c->UserAcknowledged != 0 || c->UserDrain != 0 || c->UserPreempted != 0)
                return LinuxNativeUserInterruptRejected(state, 1);
            if (*(ulong*)(data + X64LinuxUserTransition.CaptureStackOffset) !=
                state->CooperativePool + NativeProgram.IrqTopPage * Page - 56)
                return LinuxNativeUserInterruptRejected(state, 2);
            if (!LinuxNativeUserGates(c, true)) return LinuxNativeUserInterruptRejected(state, 3);
            if (!LinuxNativeVerify(c)) return LinuxNativeUserInterruptRejected(state, 4);
            if (!CooperativeAuditPool(state, true)) return LinuxNativeUserInterruptRejected(state, 8);
            if (!c->Process.CurrentImage(c->Key, 2, out var image)) return LinuxNativeUserInterruptRejected(state, 5);
            if (!X64LinuxUserTransition.CaptureInterruptUserImage(data, X64LinuxUserTransition.DataBytes, image,
                X64LinuxUserTransition.UserImageBytes, c->Plan.Owner.ObservedMxcsrMask, c->User))
                return LinuxNativeUserInterruptRejected(state, 6);
            ulong* cpu = stackalloc ulong[(int)NativeSwitch.ObservationWords];
            ((delegate* unmanaged<ulong*, void>)(state->Memory.Code + FirstExitCodeBundle.CooperativeObserveOffset))(cpu);
            if (!LinuxNativeAcknowledgeUserTimer(c, index, cpu[3], image->Frame.Rflags, false))
                return LinuxNativeUserInterruptRejected(state, 7);
            c->UserPreempted = index + 1; c->Actions[index] = (uint)LinuxSyscallAction.Return;
            return 2; // Original gateway restoration, then actual kernel CALL capture and Yield.
        }

        // Preserve the admission failure itself in serial evidence. This path
        // never changes captured state, acknowledges an IRQ or authorizes reuse.
        private static byte LinuxNativeUserInterruptRejected(Storage* state, uint stage)
        {
            _ = LinuxText(state, "M16_009_IRQ_REJECT stage=") && LinuxNumber(state, stage) && LinuxText(state, "\r\n");
            if (stage == 6)
            {
                byte* data = (byte*)state->LinuxSyscallData;
                var frame = *(LinuxSyscallFrame*)(data + X64LinuxUserTransition.FrameOffset);
                LinuxNativeControl* c = LinuxNativeOwner(state);
                ulong* values = stackalloc ulong[18] { *(ulong*)(data + X64LinuxUserTransition.PhaseOffset),
                    *(ulong*)(data + X64LinuxUserTransition.OutcomeOffset), *(ulong*)(data + X64LinuxUserTransition.VectorOffset),
                    *(ulong*)(data + X64LinuxUserTransition.ErrorOffset), *(ushort*)(data + X64LinuxUserTransition.FaultSelectorsOffset),
                    *(ushort*)(data + X64LinuxUserTransition.FaultSelectorsOffset + 2), frame.Number, frame.Rip, frame.Rsp, frame.Rflags,
                    frame.FsBase, frame.GsBase, *(ulong*)(data + X64LinuxUserTransition.ValueOffset),
                    *(uint*)(data + X64LinuxUserTransition.UserFxOffset + 24), *(uint*)(data + X64LinuxUserTransition.UserFxOffset + 28),
                    c == null ? 0 : c->Plan.Owner.ObservedMxcsrMask, c != null && LinuxSyscallGateway.ValidateReturn(frame, c->User) ? 1UL : 0UL,
                    c != null && c->User.UserInterrupts ? 1UL : 0UL };
                for (uint i = 0; i < 18; i++)
                    _ = LinuxText(state, "M16_009_CAPTURE index=") && LinuxNumber(state, i) &&
                        LinuxText(state, " value=") && LinuxNumber(state, values[i]) && LinuxText(state, "\r\n");
            }
            _ = LinuxText(state, "TEST_FAIL m16-009 user-irq-admission\r\n");
            return 0;
        }

        private static bool LinuxNativeAcknowledgeUserTimer(LinuxNativeControl* c, uint index,
            ulong entryFlags, ulong interruptedFlags, bool drain)
        {
            if (!LinuxNativeUserWorker(c, index) || c->UserTimer != index + 1 || c->UserAcknowledged != 0 ||
                c->Epoch >= c->Budget.MaximumEpochs || c->Timer.Ticks != c->Epoch ||
                c->TimerDeadlines.ObservedTicks != c->Epoch || c->TimerDeadlines.Count != 1) return false;
            var tick = c->Timer.HandleInterrupt(c->Key, entryFlags, interruptedFlags,
                ref c->TimerDeadlines, c->Key, SchedulerTimer.Phase.Safe);
            if (!tick.Succeeded || tick.Next != SchedulerTimer.Action.WakeReady || tick.Tick != c->Epoch + 1 ||
                tick.Tick > c->Budget.MaximumEpochs || tick.Due.Identity.Value != c->Threads.Current.Value || tick.Due.Deadline != tick.Tick) return false;
            DeadlineQueue.Entry due = default;
            var drained = c->TimerDeadlines.Drain(c->Key, tick.Tick, &due, (uint)sizeof(DeadlineQueue.Entry), 1);
            if (!drained.Succeeded || drained.Count != 1 || due.Identity.Value != tick.Due.Identity.Value || due.Deadline != tick.Tick) return false;
            c->Epoch = tick.Tick; c->UserAcknowledged = 1;
            if (!(c->Stopping != 0 || c->Process.AdvanceFutexClock(c->Key, 2)) || !(
                LinuxText(c->State, drain ? "M16_009_DRAIN identity=" : "M16_009_IRQ identity=") && LinuxNumber(c->State, due.Identity.Value) &&
                LinuxText(c->State, " epoch=") && LinuxNumber(c->State, tick.Tick) &&
                LinuxText(c->State, " nanos=") && LinuxNumber(c->State, c->Threads.ObservedTicks))) return false;
            if (!drain && (!c->Process.CurrentImage(c->Key, 2, out var image) ||
                !(LinuxText(c->State, " rip=") && LinuxNumber(c->State, image->Frame.Rip) &&
                LinuxText(c->State, " rsp=") && LinuxNumber(c->State, image->Frame.Rsp) &&
                LinuxText(c->State, " flags=") && LinuxNumber(c->State, image->Frame.Rflags) &&
                LinuxText(c->State, " fs=") && LinuxNumber(c->State, image->Frame.FsBase) &&
                LinuxText(c->State, " gs=") && LinuxNumber(c->State, image->Frame.GsBase) &&
                LinuxText(c->State, " valueBits=") && LinuxNumber(c->State, unchecked((ulong)image->Value))))) return false;
            return LinuxText(c->State, "\r\n");
        }

        // Called on the owned native switch stack after gateway restoration,
        // before Park/Exit/Yield. Leave a pending E1 deliverable and acknowledge
        // it at this exact STI/HLT/CLI checkpoint; Restore never discards IRR.
        private static bool LinuxNativeStopUserTimer(LinuxNativeControl* c, uint index)
        {
            if (!LinuxNativeUserWorker(c, index) || c->UserDrain != 0 || !LinuxNativeTimerInstalled(c)) return false;
            if (c->UserTimer == 0)
                return c->UserAcknowledged == 0 && c->UserPreempted == 0 && !c->Timer.IsPrepared && c->TimerDeadlines.IsEmpty;
            if (c->UserTimer != index + 1 || c->UserAcknowledged > 1) return false;
            if (c->Timer.IsArmed)
            {
                if (c->UserAcknowledged != 0) return false;
                c->UserDrain = 1;
                bool returned = ((delegate* unmanaged<uint>)(c->Plan.Owner.CodeStart + NativeProgram.Idle))() == 1;
                c->UserDrain = 0;
                if (!returned || c->UserAcknowledged != 1 || c->Timer.IsArmed) return false;
            }
            if (c->Timer.IsPrepared && c->Timer.Restore(c->Key, 2) != SchedulerTimer.Status.Ok) return false;
            *(ulong*)(c->State->Arena.Descriptors->Tss + 60) = c->OriginalIst;
            if (c->TimerDeadlines.HasPending)
            {
                var next = c->TimerDeadlines.Peek(c->Key);
                if (c->UserAcknowledged != 0 || c->TimerDeadlines.Count != 1 || !next.Succeeded ||
                    next.Value.Identity.Value != c->Threads.Current.Value || next.Value.Deadline != c->Epoch + 1 ||
                    c->TimerDeadlines.Cancel(c->Key, next.Value.Identity) != DeadlineQueue.Status.Ok) return false;
            }
            if (!c->TimerDeadlines.IsEmpty || c->TimerDeadlines.ObservedTicks != c->Epoch || !LinuxNativeTimerInstalled(c)) return false;
            c->UserTimer = c->UserAcknowledged = 0;
            return true;
        }

        private static byte LinuxNativeDrainUserInterrupt(LinuxNativeControl* c, X64TimerInterrupt.Frame* frame)
        {
            if (c->Selected == 0) return 0;
            uint index = c->Selected - 1;
            var owner = c->Plan.Owner;
            if (!LinuxNativeUserWorker(c, index) || c->UserDrain != 1 || c->UserTimer != index + 1 || frame == null ||
                !LinuxNativeTimerInstalled(c) || *(ulong*)(c->State->Arena.Descriptors->Tss + 60) != c->State->CooperativePool + OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram.IrqTopPage * Page ||
                (ulong)frame != c->State->CooperativePool + OS.Kernel.Compatibility.LinuxX64.LinuxThreadProgram.IrqTopPage * Page - X64TimerInterrupt.FrameBytes || frame->Complete != 1 ||
                frame->Rip != owner.CodeStart + NativeProgram.Idle + 2 || frame->Cs != 8 || frame->Ss != 16 ||
                (frame->EntryFlags & 0x602) != 2 || (frame->Rflags & 0x602) != 0x202 ||
                (frame->Rflags & ~(NativeSwitch.AdmittedFlags | 0x10200UL)) != 0 ||
                frame->Rsp < owner.SwitchStackGuardStart + Page || frame->Rsp > owner.SwitchStackGuardStart + owner.SwitchStackBytes - Page - 32)
                return 0;
            ulong* cpu = stackalloc ulong[(int)NativeSwitch.ObservationWords];
            ((delegate* unmanaged<ulong*, void>)(c->State->Memory.Code + FirstExitCodeBundle.CooperativeObserveOffset))(cpu);
            if (cpu[1] != owner.Root || cpu[4] != 8 || cpu[5] != 0 || cpu[6] != 24 || (cpu[3] & 0x602) != 2 ||
                cpu[14] != owner.KernelGs || cpu[15] != 0 || cpu[16] != 0 || !LinuxNativeVerify(c) || !CooperativeAuditPool(c->State, true, ownedIst: true)) return 0;
            return LinuxNativeAcknowledgeUserTimer(c, index, frame->EntryFlags, frame->Rflags, true) ? (byte)1 : (byte)0;
        }
    }
}
#endif
