#if M16_025_PROBE
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Boot;

// One retained BSP-owned diagnostic disk lifetime. Real device, mapping,
// native stack and guest admission are independently required by the runner.
public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        // Post-EBS, current original supervisor root, NIC already initialized,
        // no Linux kernel table, user worker or mutation guard exists yet.
        private static bool RunNativeDiskBootstrap(Storage* state)
        {
            fixed (LinuxSocketStorage* network = &s_linuxSocketStorage)
            {
                if (state->CooperativePhase != 0 || state->LinuxDescriptorPhase != 4 || state->LinuxSyscallPhase != 4 ||
                    state->LinuxMemory.HasMutationGuard || network->Boot != state || network->Phase != 2 ||
                    network->Device == null || !network->Device->Running) return RejectNativeDiskBootstrap(state, 1);
                if (!StartNativeDisk(state)) return RejectNativeDiskBootstrap(state, 2);
                // This outer boot driver owns the bounded cooperative lifetime;
                // each turn services NIC RX/TX/timers then advances one sector,
                // parser or mount step. Never spin inside Submit/Poll/Copy.
                for (uint turn = 0; turn < 3_000_000; turn++)
                {
                    if (!PumpLinuxSocketFrames(network)) return RejectNativeDiskBootstrap(state, 3);
                    var progress = NativeDiskBootstrapStep(state); // Fresh HPET +30s absolute deadline inside.
                    if (progress == NativeDiskProgress.Rejected) return RejectNativeDiskBootstrap(state, 4);
                    if (progress == NativeDiskProgress.Ready)
                        return AttachNativeDiskDescriptors(state) || RejectNativeDiskBootstrap(state, 5);
                }
                return RejectNativeDiskBootstrap(state, 6); // Iteration ceiling is never readiness.
            }
        }

        // Failure-only, bounded POD facts on the original supervisor root.
        // Never call a device, retry, allocate, or infer cleanup from this row.
        private static bool WriteNativeDiskRejection(Storage* state, uint stage, NativeDiskOwner* o) =>
            state != null && o != null &&
            LinuxText(state, "M16_023_REJECTED stage=") && LinuxNumber(state, stage) &&
            LinuxText(state, " phase=") && LinuxNumber(state, o->Phase) &&
            LinuxText(state, " blockFailure=") && LinuxNumber(state, (uint)o->Device.LastFailure) &&
            LinuxText(state, " fsFailure=") && LinuxNumber(state, (uint)o->Mount.LastFailure) &&
            LinuxText(state, " submitted=") && LinuxNumber(state, o->Device.Submitted) &&
            LinuxText(state, " completed=") && LinuxNumber(state, o->Device.Completed) &&
            LinuxText(state, " copied=") && LinuxNumber(state, o->Device.Copied) &&
            LinuxText(state, " ioActive=") && LinuxNumber(state, o->IoActive ? 1UL : 0UL) &&
            LinuxText(state, " arraySector=") && LinuxNumber(state, o->ArraySector) &&
            LinuxText(state, " header=") && LinuxNumber(state, (uint)o->Header.Status) &&
            LinuxText(state, " partition=") && LinuxNumber(state, (uint)o->Partition.Status) && LinuxText(state, "\r\n");

        private static bool RejectNativeDiskBootstrap(Storage* state, uint stage)
        {
            fixed (NativeDiskOwner* o = &s_nativeDisk) _ = WriteNativeDiskRejection(state, stage, o);
            return false;
        }

        // One bounded reset/retirement attempt after actual exit/reaps and timer
        // restoration, before Process.Release detaches the mutation/descriptor
        // guards. Failed/uncertain stop leaves all owners mapped and retained.
        private static bool RetireNativeDiskAttachment(Storage* state, LinuxNativeControl* c)
        {
            if (state == null || c != null && (c->State != state || c->Process.ActiveThreads != 0 ||
                c->Threads.Current.IsValid || c->Threads.HandoffPending) ||
                !FlushNativeDiskEvidence(state) || !RetireNativeDisk(state)) return false;
            // Mount/device ownership starts before Linux metadata publication.
            // A failed prepare may have no control or attached process at all.
            if (state->LinuxDescriptors.ThreadOwner == 0) return true;
            if (c == null || state->LinuxDescriptors.ThreadOwner != (ulong)(&c->Process)) return false;
            // Retired FS receipts authorize local scrub only; these turns issue
            // no live reads. Eight records, at most one selected per turn.
            for (uint i = 0; i < LinuxDescriptorServices.DiskSlots; i++)
                if (LinuxDescriptorServices.PumpDiskOne(&state->LinuxDescriptors, (ulong)(&c->Process)).Progress ==
                    LinuxDescriptorServices.DiskProgress.Rejected) return false;
            return LinuxDescriptorServices.CanDetachThreads(&state->LinuxDescriptors, (ulong)(&c->Process));
        }
    }
}
#endif
