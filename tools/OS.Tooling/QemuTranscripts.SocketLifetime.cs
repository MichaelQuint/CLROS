using System.Buffers.Binary;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed class SocketLifetime(Dictionary<string, ulong> first)
    {
        internal readonly Dictionary<string, ulong> First = first;
        internal ulong Retired, MaximumDeadline, Timeout;
        internal int Waits;
        internal bool Drain;
        internal uint RemotePort = checked((uint)first["remotePort"]);
    }
}
