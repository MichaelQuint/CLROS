using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using OS.Net;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Epoll = OS.Kernel.Compatibility.LinuxX64.LinuxEpollServices;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Tx = OS.Net.TcpSendWindow;
using Wire = TcpReceiveWindowTests;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;

internal static unsafe partial class LinuxEpollServicesTests
{
    private struct RefreshClock
    {
        internal uint* Ready;
        internal KernelThreadTable* Table;
        internal OSHostHandle Parent;
        internal ulong Time;
        internal uint Calls;
        internal byte Result;
        internal bool Refreshed, Unwoken;
    }
}
