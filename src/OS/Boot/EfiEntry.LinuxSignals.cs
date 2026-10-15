#if M16_008_PROBE
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // This state is part of the same exclusive RW/NX metadata arena as
        // the real process/table. Attach before the first generation is
        // published; Release detaches only after all generations have reaped.
        private static bool LinuxNativeBindSignals(LinuxNativeControl* c)
        {
            if (c == null || c->Signals == null || c->SignalsInitialized != 0 || c->ProcessAttached != 1 ||
                !LinuxSignalServices.Initialize(c->Signals, (uint)sizeof(LinuxSignalServices.State), c->Key, 2,
                    new() { User = c->User, CopyToUser = &LinuxThreadCopyOutput, Capacity = c->Capacity,
                        FirmwareExited = true, SingleOwnerValidated = true })) return false;
            c->SignalsInitialized = 1;
            return c->Process.EnableSignals(c->Key, 2, c->Signals);
        }

        private static bool LinuxNativeReleaseSignals(LinuxNativeControl* c)
        {
            if (c == null || c->ProcessAttached != 0) return false;
            if (c->SignalsInitialized == 0) return true;
            if (!LinuxSignalServices.End(c->Signals, c->Key, 2)) return false;
            for (uint i = 0; i < (uint)sizeof(LinuxSignalServices.State); i++)
                if (((byte*)c->Signals)[i] != 0) return false;
            c->SignalsInitialized = 0; return true;
        }

        // Both the active syscall gateway and a rescheduled kernel worker use
        // this boundary after capturing/completing the actual operation. Signal
        // handling cannot increment gate counters or run inline in the kernel.
        // Fatal delivery has already staged owned shutdown; preserve its real
        // reason and let native restoration, captured exit and reap drain it.
        private static LinuxSyscallResult LinuxNativeUserReturn(LinuxNativeControl* c, uint index,
            out X64LinuxUserTransition.UserImage* image)
        {
            image = null;
            if (c == null || index >= c->Capacity || c->SignalsInitialized != 1 ||
                c->Selected != index + 1 || c->Threads.Current.Value != c->Plan.Bindings[index].Context.Identity)
                return new(LinuxSyscallAction.Rejected);
            var result = c->Process.PrepareUserReturn(c->Key, 2, out image);
            if (result.Action == LinuxSyscallAction.Terminate)
            {
                c->State->LinuxSyscallExitCode = result.ExitCode; c->Stopping = 1;
                c->Actions[index] = (uint)LinuxSyscallAction.Terminate;
            }
            return result;
        }

#if M16_009_PROBE
        // The separate native fault entry saves every user field, switches back
        // to the original kernel root and restores kernel FP before this call.
        // Its byte1 is resume authority, unlike the older terminal reporter.
        private static byte LinuxNativeUserFault(Storage* state, byte* data)
        {
            LinuxNativeControl* c = LinuxNativeOwner(state);
            if (c == null || data != (byte*)state->LinuxSyscallData || c->Selected == 0 || c->Selected > c->Capacity) return 0;
            uint index = c->Selected - 1;
            if (c->Entered[index] != 1 || c->Threads.Current.Value != c->Plan.Bindings[index].Context.Identity) return 0;
            if (c->UserFaults >= 64 || !c->Process.CurrentImage(c->Key, 2, out var image) ||
                !X64LinuxUserTransition.CaptureFaultUserImage(data, X64LinuxUserTransition.DataBytes, image,
                    X64LinuxUserTransition.UserImageBytes, c->Plan.Owner.ObservedMxcsrMask,c->User.UserInterrupts))
                return LinuxNativeAbort(c, index);
            uint trap = (uint)*(ulong*)(data + X64LinuxUserTransition.VectorOffset);
            ulong error = *(ulong*)(data + X64LinuxUserTransition.ErrorOffset);
            ulong address = trap == 14 ? *(ulong*)(data + X64LinuxUserTransition.FaultAddressOffset) : 0;
            ulong rip = image->Frame.Rip, rsp = image->Frame.Rsp;
            int code = 128;
            if (trap == 14 && !LinuxMemoryServices.TryPageFaultCode(&state->LinuxMemory, address, out code))
                return LinuxNativeAbort(c, index);
            c->UserFaults++;
            var delivery = c->Process.DeliverUserFault(c->Key, 2, trap, error, address, code);
            bool delivered = delivery == LinuxSignalServices.Delivery.Delivered &&
                X64LinuxUserTransition.RestoreActiveUserImage(data, X64LinuxUserTransition.DataBytes, image,
                    X64LinuxUserTransition.UserImageBytes, c->Plan.Owner.ObservedMxcsrMask, c->User);
            bool fatal = delivery is LinuxSignalServices.Delivery.Terminate or LinuxSignalServices.Delivery.BadFrame &&
                c->Process.Inspect(c->Key, 2, c->Threads.Current, out var view) && view.Exiting && view.ExitCode == 139;
            bool logged = LinuxText(state, "M16_009_FAULT identity=") && LinuxNumber(state, c->Threads.Current.Value) &&
                LinuxText(state, " vector=") && LinuxNumber(state, trap) && LinuxText(state, " error=") && LinuxNumber(state, error) &&
                LinuxText(state, " rip=") && LinuxNumber(state, rip) && LinuxText(state, " rsp=") && LinuxNumber(state, rsp) &&
                LinuxText(state, " address=") && LinuxNumber(state, address) && LinuxText(state, " code=") && LinuxNumber(state, (uint)code) &&
                LinuxText(state, " delivery=") && LinuxNumber(state, (uint)delivery) && LinuxText(state, "\r\n");
            if (!logged || !delivered && !fatal) return LinuxNativeAbort(c, index);
            if (fatal)
            {
                state->LinuxSyscallExitCode = 139; c->Stopping = 1;
                c->Actions[index] = (uint)LinuxSyscallAction.Terminate;
                return 2; // Original native cleanup, then actual captured exit/reap.
            }
            *(ulong*)(data + X64LinuxUserTransition.OutcomeOffset) = 0;
            return 1;
        }
#endif
    }
}
#endif
