using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Net;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Tx = OS.Net.TcpSendWindow;
using Rx = OS.Net.TcpReceiveWindow;
using Wire = TcpReceiveWindowTests;

internal static unsafe partial class LinuxSocketServicesTests
{
    private delegate bool RetainSocket(Fds.State* descriptors, ulong start, ulong bytes);
}
