using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling;
using OS.Tools.QemuRunner;

// Real loopback HTTP/stream controls, with independently supplied diagnostic
// bodies. They establish no native signal, guest flow, process or syscall proof.
internal static partial class NativeAotWebSignalRaceTransportTests
{
    internal sealed class Connection(int port)
    {
        internal readonly int Port = port;
        internal readonly List<byte[]> Requests = [], Responses = [];
    }
}
