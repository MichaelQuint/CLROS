#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using System;
using OS.Net;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxSocketServices
{
    /// <summary>Initially zero, private socket metadata. One open description
    /// owns a socket, including all dup fds. Accepted TCP pointers remain borrowed
    /// through ReapClosed, even after the last fd closes. Never copy a live slot.</summary>

    public struct Endpoint
    {
        internal uint Phase, Listener, Backlog, ReadShutdown, ErrorConsumed;
        internal uint ReuseAddress, NoDelay, SendBytes, ReceiveBytes;
        internal Ipv4Address Address;
        internal ushort Port;
        internal ulong Order;
        internal TcpReceiveWindow.Owner* Receiver;
        internal TcpSendWindow.Owner* Sender;
#if !NOSTDLIB_EFI || M16_027_PROBE
        internal void* SharedContext;
        internal uint SharedContextBytes;
#endif
    }
}
#endif
