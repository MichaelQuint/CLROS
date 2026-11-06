using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Net;
using Rx = OS.Net.TcpReceiveWindow;
using Tcb = OS.Net.TcpConnectionTable;

internal static unsafe partial class TcpReceiveWindowTests
{
    private struct Hardware
    {
        public byte SendResult;
        public uint Calls;
        public fixed byte Last[54];
    }
}
