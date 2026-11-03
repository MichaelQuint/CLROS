using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

internal static partial class Program
{
    private sealed class ContainmentPeer(Socket socket)
    {
        internal readonly Socket Socket = socket;
        internal int Role = -1, Round, Worker;
        internal long Started, Completed;
        internal bool ReplyPending;
    }
}
