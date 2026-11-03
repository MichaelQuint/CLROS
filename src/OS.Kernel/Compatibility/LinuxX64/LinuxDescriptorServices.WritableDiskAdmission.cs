#if !NOSTDLIB_EFI || M16_027_PROBE
using OS.Kernel.Memory;
using OS.Pal;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using C = OS.Storage.ContinuingFat32Mutation;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxDescriptorServices
{
    public struct WritableDiskAdmission
    {
        public W.State* Provider;
        public W.ProcessToken Process;
        /// <summary>Copied caller-selected free-cluster discovery budgets for
        /// growing writes. This is not a file-chain limit or reservation. The
        /// engine's independently admitted WorkBudget bounds chain validation;
        /// neither budget is enlarged or retried automatically by this adapter.</summary>

        public W.FreeSearch Search;
        public WritableDiskBinding* Bindings;
        public uint BindingCount;
        public ulong Epoch;
        public bool FirmwareExited, SingleOwnerValidated;
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
