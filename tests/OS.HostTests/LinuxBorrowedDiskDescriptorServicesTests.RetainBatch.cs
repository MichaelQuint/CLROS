using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;
using OS.Pal;
using OS.Kernel.Threading;
using Memory = OS.Kernel.Compatibility.LinuxX64.LinuxMemoryServices;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;
using OS.Net;
using B = OS.Storage.WritableBlockSession;
using C = OS.Storage.ContinuingFat32Mutation;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using P = OS.Storage.WritableBlockReadPort;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using Sockets = OS.Kernel.Compatibility.LinuxX64.LinuxSocketServices;
using Epoll = OS.Kernel.Compatibility.LinuxX64.LinuxEpollServices;
using Network = OS.Kernel.Compatibility.LinuxX64.LinuxSocketNetwork;
using Tx = OS.Net.TcpSendWindow;
using Rx = OS.Net.TcpReceiveWindow;
using Wire = TcpReceiveWindowTests;

// Real queued fake media, with separate C-writer and paired-global controls.
// Every positive pair uses one ORIGINAL B/C. Thread controls use copied host
// contexts and actual scheduler contracts; none establishes native execution.
internal static unsafe partial class LinuxBorrowedDiskDescriptorServicesTests
{
    private delegate bool RetainBatch(F.State* state, ReadOnlySpan<ulong> ranges);
}
