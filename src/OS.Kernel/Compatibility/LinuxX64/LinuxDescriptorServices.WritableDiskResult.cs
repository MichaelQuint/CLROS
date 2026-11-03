#if !NOSTDLIB_EFI || M16_027_PROBE
using OS.Kernel.Memory;
using OS.Pal;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>Control observation. An empty Worker revokes all wake/copy
    /// authority while Draining/RecoveryRequired still retains original backing.
    /// Ready keeps the exact live worker for actual completion. No result grants
    /// mutation replay, user mapping or device durability authority.</summary>

    public readonly struct WritableDiskResult
    {
        public readonly WritableDiskProgress Progress;
        public readonly long Value;
        public readonly WritableDiskToken Token;
        public readonly OSHostHandle Worker;
        public readonly C.Status Failure;
        internal WritableDiskResult(WritableDiskProgress progress, long value = 0, WritableDiskToken token = default, OSHostHandle worker = default, C.Status failure = C.Status.Success)
        {
            Progress = progress;
            Value = value;
            Token = token;
            Worker = worker;
            Failure = failure;
        }
    }
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
#if !NOSTDLIB_EFI || M16_025_PROBE
#endif
#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
#endif
}
#endif
