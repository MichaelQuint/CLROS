using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Net;
using Network = OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Epoll = OS.Kernel.Compatibility.LinuxX64.LinuxEpollServices;
using Table = OS.Net.TcpConnectionTable;
using Wire = TcpReceiveWindowTests;
using Rx = OS.Net.TcpReceiveWindow;

internal static unsafe partial class LinuxSocketNetworkTests
{
    private delegate bool ContextProbe(Network.State* state);
}
