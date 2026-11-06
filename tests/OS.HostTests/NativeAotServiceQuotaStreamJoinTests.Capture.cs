using System.Text;
using OS.Tooling;

internal static partial class NativeAotServiceQuotaStreamJoinTests
{
    private sealed record Capture(byte[] Bytes, uint Anchor, QemuArpReceiveReceipt Arp);
}
