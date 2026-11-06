using System.Buffers.Binary;
using OS.Tooling;

internal static partial class SocketContainmentPacketTests
{
    private sealed record Fixture(List<Wire> Records, uint Anchor, QemuArpReceiveReceipt Arp);
}
