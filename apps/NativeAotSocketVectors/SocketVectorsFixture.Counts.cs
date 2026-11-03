using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace OS.Apps.NativeAotSocketVectors;
public static partial class SocketVectorsFixture
{
    private sealed class Counts
    {
        internal int Received, Sent, AcceptBlocked, ReceiveCalls, ReceivePartial, ReceiveBlocked, SendCalls, SendPartial, SendBlocked, Reset, Pending, Aborted;
        internal void Add(Counts other)
        {
            Received += other.Received;
            Sent += other.Sent;
            Reset += other.Reset;
            Pending += other.Pending;
            Aborted += other.Aborted;
        }

        public override string ToString() => $"received={Received} sent={Sent} accept_blocked={AcceptBlocked} receive_calls={ReceiveCalls} receive_partial={ReceivePartial} receive_blocked={ReceiveBlocked} send_calls={SendCalls} send_partial={SendPartial} send_blocked={SendBlocked} reset={Reset} pending={Pending} aborted={Aborted}";
    }
}
