using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static partial class NativeAotServiceLifecyclePacketTests
{
    private sealed record Fixture(List<byte[]> Frames, uint Anchor, QemuArpReceiveReceipt Arp, uint[] Ports, string Report);
}
