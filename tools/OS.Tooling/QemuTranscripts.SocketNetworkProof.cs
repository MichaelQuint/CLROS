using System.Buffers.Binary;
using System.Text;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed record SocketNetworkProof(string[] Nic, QemuArpReceiveReceipt Arp, Dictionary<string, ulong> Peer, Dictionary<string, ulong> Wire, Dictionary<string, ulong> End, Dictionary<string, ulong> Stop, List<SocketLifetime> Lifetimes, SocketContainmentTransport? Containment = null);
}
