using System.Buffers.Binary;
using OS.Tooling;

internal static partial class SocketContainmentPacketTests
{
    private sealed record Wire(ulong Time, byte[] Frame);
}
