using System.Buffers.Binary;
using System.Diagnostics;

namespace OS.Tools.QemuRunner;

public sealed partial class SocketContainmentPeer
{
    private sealed class Connection(int role)
    {
        internal readonly ushort Port = (ushort)(41000 + role);
        internal readonly uint InitialNext = (uint)(10001 + role * 10000);
        internal uint LocalNext = (uint)(10001 + role * 10000), GuestNext, GuestStart;
        internal bool Responded, FinSeen;
        internal int LastRound = -1;
    }
}
