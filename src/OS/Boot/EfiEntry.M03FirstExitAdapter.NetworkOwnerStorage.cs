#if M07_026_PROBE
using OS.Drivers;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Platform;
using OS.Net;
using NetDevice = OS.Drivers.ContinuingVirtioNetDevice;

#if !M07_ARP_RECEIVE_PROBE || !M16_003_PROBE
#error Continuing networking requires the original ARP and service-loop parents.
#endif

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
        private struct NetworkOwnerStorage
        {
            internal NetDevice.State Device;
            internal DmaBufferPool Pool;
            internal fixed ulong Metadata[256];
        }






    }
}
#endif
