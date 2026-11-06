#if CLROS_LINUX_SOCKETS
using System;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Platform;
using OS.Net;
using NetDevice = OS.Drivers.ContinuingVirtioNetDevice;
using SocketNetwork = OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork;
using ThreadTable = OS.Kernel.Threading.KernelThreadTable;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {

#if M16_027_PROBE
        // The whole direct IO carrier is separate from transport metadata and
        // the user-copy carrier. Original Boot/NIC and all opaque referents
        // retain their independent native mappings and lifetime obligations.
        private struct LinuxSocketIoCarrier
        {
            internal ulong Self, NetworkArena;
            internal Storage* Boot;
            internal LinuxSocketStorage* Owner;
            internal NetDevice.State* Device;
        }

#endif

    }
}
#endif
