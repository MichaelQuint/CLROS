using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxContinuingWritableDescriptorServices
{
    /// <summary>Optional zeroed disjoint POD tables, borrowed until Detach.
    /// PrimaryPid is only a copied positive host contract value. No fork,
    /// scheduler identity, Linux fd namespace or native lock authority follows.</summary>

    public struct LockAdmission
    {
        public ProcessSlot* Processes;
        public RangeLock* Locks, Scratch;
        public uint ProcessCount, LockCount, ScratchCount, PrimaryPid;
    }
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
}
