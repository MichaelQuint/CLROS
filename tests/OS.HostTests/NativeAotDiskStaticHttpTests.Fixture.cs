using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static partial class NativeAotDiskStaticHttpTests
{
    private sealed record Fixture(byte[] Packets, uint Anchor, QemuArpReceiveReceipt Arp, JsonObject Receipt, Dictionary<string, byte[]> Bodies, WebRootDiskInventory.Inspection Inventory, QemuTranscripts.DiskStaticReplayReport Replay);
}
