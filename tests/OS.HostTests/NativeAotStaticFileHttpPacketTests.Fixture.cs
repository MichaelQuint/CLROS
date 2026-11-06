using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static partial class NativeAotStaticFileHttpPacketTests
{
    private sealed record Fixture(List<byte[]> Frames, uint Anchor, QemuArpReceiveReceipt Arp, uint[] Ports, JsonObject Receipt, Dictionary<string, byte[]> Bodies, JsonObject Abort);
}
