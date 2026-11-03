using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Net;

namespace OS.Tools.QemuRunner;
public sealed partial class ServiceSocketControls
{
    private sealed class Flow(int port, uint sequence, string name)
    {
        internal readonly int Port = port;
        internal readonly uint Initial = sequence;
        internal readonly string Name = name;
        internal uint Next = unchecked(sequence + 1), Server;
        internal int Received, Writes;
        internal readonly byte[] Response = new byte[8192];
        internal DateTimeOffset Started;
    }
}
