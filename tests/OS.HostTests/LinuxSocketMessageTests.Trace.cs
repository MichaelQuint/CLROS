using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Net;
using Model = LinuxSocketServicesTests.Model;
using SocketCases = LinuxSocketServicesTests;
using Tx = OS.Net.TcpSendWindow;
using Wire = TcpReceiveWindowTests;

internal static unsafe partial class LinuxSocketMessageTests
{
    private struct Trace
    {
        internal uint Calls, Fd, Count, Total;
        internal ulong Number, Header, Flags, Table, FirstAddress, FirstBytes, SecondAddress, SecondBytes;
        internal bool Reject;
    }
}
