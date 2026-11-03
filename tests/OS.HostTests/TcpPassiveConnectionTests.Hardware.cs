using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Net;
using Passive = OS.Net.TcpPassiveConnection;
using Tcb = OS.Net.TcpConnectionTable;

internal static unsafe partial class TcpPassiveConnectionTests
{
    private struct Hardware
    {
        public ulong Now, ClockAfterSend;
        public uint Calls, Sends;
        public byte SendResult;
        public fixed byte Frame[1514];
    }
}
