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
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
    /// <summary>Stable pending storage independent of a thread Slot lifetime.
    /// No user address, resolved alias, caller pointer or Slot pointer is kept.
    /// Cancel revokes copy/wake first; original fd/description and FS tokens
    /// stay retained until actual cancellation drain or FS retirement.</summary>

    public struct DiskRequest
    {
        internal ulong Self, Generation, BindingGeneration, Offset, Number, OpenFlags;
        internal Fs.OperationToken Operation;
        internal Token OriginalFd;
        internal OSHostHandle Worker;
        internal long Error;
        internal uint Description, Binding, Requested, Whence;
        internal byte Phase, Kind;
        internal bool Positioned, Borrowed, RequireDirectory, Cancelling;
        internal Fs.Status Failure;
#if !NOSTDLIB_EFI || M16_027_PROBE
        // Only a Busy borrowed-backend open retains this bounded copied path.
        // It never retains/rereads an application pointer or enlarges025 layout.
        internal uint PathLength;
        internal fixed byte Path[(int)MaximumPath];
#endif
    }
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#else

#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#if !NOSTDLIB_EFI || M16_027_TIMING_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
}
#endif
