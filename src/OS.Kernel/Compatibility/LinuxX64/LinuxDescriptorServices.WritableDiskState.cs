#if !NOSTDLIB_EFI || M16_027_PROBE
using OS.Kernel.Memory;
using OS.Pal;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>One noncopyable, IRQ-off externally serialized attachment.
    /// Keep it, its description-sized binding table and the complete provider
    /// tree mapped supervisor RW/NX until Detach succeeds. Provider/engine/block
    /// teardown follows adapter detach. Dirty recovery prohibits successful End.
    /// A readonly attachment requires the exact same retained engine and fully
    /// disjoint private backing. Finite direct TCP requires its separately
    /// bounded shared context; the NIC network owner remains unsupported.</summary>

    public struct WritableDiskState
    {
        internal ulong Self, Epoch, NextGeneration;
        internal State* Owner;
        internal W.State* Provider;
        internal W.ProcessToken Process;
        internal W.FreeSearch Search;
        internal WritableDiskBinding* Bindings;
        internal uint BindingCount;
        internal WritableDiskRequest Request;
        public readonly bool RequestActive => Request.Phase != 0;
    }
}
#endif
