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
    /// <summary>By-value control observation. Pending/Draining never mean EOF.
    /// A draining record with empty Worker is orphaned and cannot copy or wake;
    /// PumpDiskOne must still retain/drain it before final owner detachment.</summary>

    public readonly struct DiskResult
    {
        public readonly DiskProgress Progress;
        public readonly long Value;
        public readonly DiskToken Token;
        public readonly OSHostHandle Worker;
        public readonly Fs.Status Failure;
        /// <summary>One selected live pending request made an actual block
        /// submit/completion/copy transition. Only the serialized current owner
        /// may use this to consider another bounded step; it grants no copy.</summary>

        public readonly bool Advanced;
        internal DiskResult(DiskProgress progress, long value = 0, DiskToken token = default, OSHostHandle worker = default, Fs.Status failure = Fs.Status.Success, bool advanced = false)
        {
            Progress = progress;
            Value = value;
            Token = token;
            Worker = worker;
            Failure = failure;
            Advanced = advanced;
        }
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
