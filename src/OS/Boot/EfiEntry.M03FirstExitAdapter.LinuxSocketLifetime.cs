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

        // Borrowed observations only. A release is reported after the original
        // handle becomes stale and its network slot disappears or is reused.
        private struct LinuxSocketLifetime
        {
            internal TcpConnectionTable.Handle Handle;
            internal TcpConnectionKey Key;
            internal ulong MaximumDeadline, Duration;
            internal uint Active, WaitObserved, DrainObserved, TimeoutObserved;
#if M16_014_PROBE
            internal uint LastCloseObserved, QuotaLeaseObserved;
            internal ulong AppSent, AppReceived;
#endif
        }

    }
}
#endif
