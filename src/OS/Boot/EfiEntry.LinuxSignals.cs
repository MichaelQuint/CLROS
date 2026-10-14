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
    }
}
#endif
