#if !NOSTDLIB_EFI || M16_027_PROBE
using OS.Kernel.Memory;
using OS.Pal;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    /// <summary>Copied attachment/request generation, not a user fd, mapping
    /// pin or permission to copy/wake. The original serialized owner validates it.</summary>

    public readonly struct WritableDiskToken
    {
        internal readonly ulong Owner, Epoch, Generation;
        internal WritableDiskToken(WritableDiskState* s)
        {
            Owner = (ulong)s;
            Epoch = s->Epoch;
            Generation = s->Request.Generation;
        }

        public bool IsActive => Owner != 0 && Epoch != 0 && Generation != 0;
    }
}
#endif
