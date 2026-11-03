using System.Buffers.Binary;
using OS.Tooling;

internal static partial class NativeAotWebRacePacketTests
{
    private sealed record Vector(NativeAotWebPacketTests.ControlPackets Original, List<byte[]> Frames, uint[] Ports, int OrdinaryCount);
}
