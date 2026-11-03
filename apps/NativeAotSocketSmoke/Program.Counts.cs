using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

internal static partial class Program
{
    private struct Counts
    {
        internal int Received, Sent, AcceptBlocked, ReceiveBlocked, SendBlocked, SendPartial, Eof, Reset;
        internal void Add(Counts value)
        {
            Received += value.Received;
            Sent += value.Sent;
            AcceptBlocked += value.AcceptBlocked;
            ReceiveBlocked += value.ReceiveBlocked;
            SendBlocked += value.SendBlocked;
            SendPartial += value.SendPartial;
            Eof += value.Eof;
            Reset += value.Reset;
        }

        public override readonly string ToString() => $"received={Received} sent={Sent} accept_blocked={AcceptBlocked} receive_blocked={ReceiveBlocked} send_blocked={SendBlocked} send_partial={SendPartial} eof={Eof} reset={Reset}";
    }
}
