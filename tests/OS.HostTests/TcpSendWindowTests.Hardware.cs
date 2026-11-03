using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Net;
using Rx = OS.Net.TcpReceiveWindow;
using Tx = OS.Net.TcpSendWindow;
using Tcb = OS.Net.TcpConnectionTable;
using Passive = OS.Net.TcpPassiveConnection;
using Wire = TcpReceiveWindowTests;

internal static unsafe partial class TcpSendWindowTests
{
    internal struct Hardware
    {
        public ulong Now, AdvanceOnSend;
        public uint Calls;
        public byte Result;
        public int Length;
        public fixed byte Last[1514];
    }
}
