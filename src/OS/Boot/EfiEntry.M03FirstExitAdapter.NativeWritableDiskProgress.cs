#if M16_027_PROBE
#if !M16_025_PROBE || !M16_024_PROBE || !M16_023_PROBE
#error Writable disk admission requires the selected disk compile infrastructure.
#endif
using OS.Drivers;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Storage;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using K = OS.Storage.ContinuingFat32Bootstrap;
using P = OS.Storage.WritableBlockReadPort;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;

namespace OS.Boot;

// Default-off owner preparation only. Shared boot/profile/worker/runner hooks
// and original native/guest qualification are separate integration obligations.
public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {

        private enum NativeWritableDiskProgress : byte
        {
            Rejected, Pending, Ready
        }

    }
}
#endif
