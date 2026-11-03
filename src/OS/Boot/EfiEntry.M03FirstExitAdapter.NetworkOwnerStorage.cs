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

#if CLROS_LINUX_SOCKETS

#endif

#if CLROS_LINUX_SOCKETS

#else

#endif

#if M10_004_PROBE && !M16_014_PROBE && !M10_017_PROBE && !M10_018_PROBE

#else
#if M16_014_PROBE

#endif

#if M16_027_PROBE

#elif M16_025_PROBE

#endif
#if M10_017_PROBE || M10_018_PROBE

#endif

#if CLROS_LINUX_SOCKETS

#if M16_012_PROBE

#endif
#if M16_013_PROBE

#endif
#if M07_027_PROBE && M07_028_PROBE

#endif
#endif

#endif

#if M10_004_PROBE && !M16_014_PROBE && !M10_017_PROBE && !M10_018_PROBE

#else
#if M16_027_PROBE

#elif M16_025_PROBE

#endif

#if M16_014_PROBE

#endif

#endif

#if CLROS_LINUX_SOCKETS

#else

#endif

    }
}
#endif
