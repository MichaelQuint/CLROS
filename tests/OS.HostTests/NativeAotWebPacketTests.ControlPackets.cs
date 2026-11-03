using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static partial class NativeAotWebPacketTests
{
    internal sealed record ControlPackets(List<byte[]> Frames, uint Anchor, QemuArpReceiveReceipt Arp, uint[] Ports, QemuTranscripts.WebUtcObservation[] Windows, QemuTranscripts.WebControlInspection Controls, int VlanAt);
}
