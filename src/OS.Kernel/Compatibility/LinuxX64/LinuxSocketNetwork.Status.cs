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
    public enum Status : byte
    {
        Success,
        Ignored,
        Capacity,
        InvalidStorage,
        InvalidState,
        TransportFailure
    }
}
#endif
