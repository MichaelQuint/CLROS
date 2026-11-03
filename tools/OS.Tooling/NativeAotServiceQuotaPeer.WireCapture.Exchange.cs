using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace OS.Tooling;
public static partial class NativeAotServiceQuotaPeer
{
    internal sealed partial class WireCapture
    {
        private sealed class Exchange(int id, string role, string method, string path, long started)
        {
            internal readonly int Id = id;
            internal readonly string Role = role, Method = method, Path = path;
            internal readonly long Started = started;
            internal long Ended;
            internal string Outcome = "pending", Body = "";
            internal int Status;
            internal readonly List<int> Connections = [];
            internal WireExchange Copy() => new(Id, Role, Method, Path, Started, Ended, Outcome, Status, Body, Array.AsReadOnly(Connections.ToArray()));
        }
    }
}
