using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Tools.QemuRunner;

internal static partial class ServiceSocketControlTests
{
    private sealed class Flow(uint client, uint server, bool zero)
    {
        internal readonly uint Client = unchecked(client + 1), Server = unchecked(server + 1);
        internal readonly bool Zero = zero;
        internal readonly bool Unread = client == 0xfffffd00;
        internal uint Next = unchecked(client + 1);
        internal byte[] Response = [];
        internal int Probes;
    }
}
