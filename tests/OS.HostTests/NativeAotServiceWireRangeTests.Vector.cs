using System.Buffers.Binary;
using OS.Tooling;

internal static partial class NativeAotServiceWireRangeTests
{
    private sealed record Vector(List<byte[]> Frames, uint Anchor, QemuArpReceiveReceipt Arp, uint Port);
}
