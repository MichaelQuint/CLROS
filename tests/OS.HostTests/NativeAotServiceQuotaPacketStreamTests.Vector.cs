using OS.Tooling;

internal static partial class NativeAotServiceQuotaPacketStreamTests
{
    private sealed record Vector(List<byte[]> Frames, uint Anchor, QemuArpReceiveReceipt Arp, ushort Port);
}
