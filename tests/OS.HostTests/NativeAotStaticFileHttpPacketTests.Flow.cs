using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static partial class NativeAotStaticFileHttpPacketTests
{
    private sealed class Flow(ushort port, uint client, uint server)
    {
        internal readonly ushort Port = port;
        internal uint Client = client, Server = server;
    }
}
