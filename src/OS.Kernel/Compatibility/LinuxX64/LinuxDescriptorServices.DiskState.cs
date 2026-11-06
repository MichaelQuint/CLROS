#if !NOSTDLIB_EFI || M16_025_PROBE
using OS.Kernel.Memory;
using OS.Pal;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
#if !NOSTDLIB_EFI || M16_027_PROBE
using P = OS.Storage.WritableBlockReadPort;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;

#endif
namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>One exclusive, externally serialized attachment. Keep all
    /// metadata and the already mounted FS backing supervisor RW/NX and disjoint
    /// until DetachDisk succeeds. Structural audit deliberately survives device
    /// failure; it is not live I/O authority. Never copy a live instance.</summary>

    public struct DiskState
    {
        internal ulong Self, Epoch, NextGeneration;
        internal State* Owner;
        internal Fs.Mount* Mount;
        internal DiskBinding* Bindings;
        internal DiskRequest* Requests;
        internal uint NextPump;
    }
}
#endif
