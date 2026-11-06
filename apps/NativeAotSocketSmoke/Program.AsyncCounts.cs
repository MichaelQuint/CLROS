using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

internal static partial class Program
{
    private sealed class AsyncCounts
    {
        internal int Received, Sent, AcceptPending, ReceivePending, SendPending, PartialSends, Eof, Reset;
        internal void Add(AsyncCounts other)
        {
            Received += other.Received;
            Sent += other.Sent;
            AcceptPending += other.AcceptPending;
            ReceivePending += other.ReceivePending;
            SendPending += other.SendPending;
            PartialSends += other.PartialSends;
            Eof += other.Eof;
            Reset += other.Reset;
        }

        public override string ToString() => $"received={Received} sent={Sent} accept_pending={AcceptPending} receive_pending={ReceivePending} send_pending={SendPending} send_partial={PartialSends} eof={Eof} reset={Reset}";
    }
}
