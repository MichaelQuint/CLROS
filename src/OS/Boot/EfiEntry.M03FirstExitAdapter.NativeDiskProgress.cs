#if M16_025_PROBE
#if !M16_023_PROBE || !M16_024_PROBE
#error Live disk descriptors require their explicit continuing block and filesystem parents.
#endif
using OS.Drivers;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Storage;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;

namespace OS.Boot;

// One retained BSP-owned diagnostic disk lifetime. Real device, mapping,
// native stack and guest admission are independently required by the runner.
public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {


        private enum NativeDiskProgress : byte
        {
            Rejected, Pending, Ready
        }






    }
}
#endif
