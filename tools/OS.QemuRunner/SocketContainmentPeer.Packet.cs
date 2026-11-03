using System.Buffers.Binary;
using System.Diagnostics;

namespace OS.Tools.QemuRunner;
public sealed partial class SocketContainmentPeer
{
    /// <summary>A validated guest TCP observation. Payload is a fresh caller-owned
    /// array; its presence is wire evidence only, not guest descriptor authority.</summary>

    public readonly record struct Packet(ushort Port, uint Sequence, uint Acknowledgement, byte Flags, ushort Window, byte[] Payload);
}
