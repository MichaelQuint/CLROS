#if !NOSTDLIB_EFI || CLROS_LINUX_SOCKETS
using System;
using OS.Net;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Pending = OS.Net.TcpPassiveConnection;
using Rx = OS.Net.TcpReceiveWindow;
using Tx = OS.Net.TcpSendWindow;
using Waits = OS.Net.TcpTimeWaitTable;

namespace OS.Kernel.Compatibility.LinuxX64;

public static unsafe partial class LinuxSocketNetwork
{
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
    /// <summary>Initially zero, noncopyable connection metadata. The private
    /// one-entry handshake bridge is consumed only after its original table
    /// generation establishes. Socket transfer retains both stream windows until
    /// the adapter permits exact closed reap. Never independently release them.</summary>

    public struct Slot
    {
        internal Pending.State Handshake;
        internal Rx.Owner Receiver;
        internal Tx.Owner Sender;
        internal TcpListenerBacklogEntry Backlog;
        internal Fds.Token Listener;
        internal ulong ListenerInode;
        internal uint ListenerFd, ListenerDescription, Phase;
        internal ulong LeaseGeneration;
    }
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
#if !NOSTDLIB_EFI || M16_027_PROBE
#endif
}
#endif
