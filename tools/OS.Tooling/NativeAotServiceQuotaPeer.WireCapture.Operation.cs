using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;
public static partial class NativeAotServiceQuotaPeer
{
    internal sealed partial class WireCapture
    {
        private sealed class Operation(int id, int connection, bool write, long offset, int requested, long started, string data, int exchange)
        {
            internal readonly int Id = id, Connection = connection, Requested = requested, ExchangeId = exchange;
            internal readonly bool Write = write;
            internal readonly long Offset = offset, Started = started;
            internal long Ended;
            internal int Transferred = -1;
            internal string Outcome = "pending", Data = data;
            internal WireOperation Copy() => new(Id, Connection, Write ? "write" : "read", Offset, Requested, Transferred, Started, Ended, Outcome, Data, ExchangeId);
        }
    }
}
