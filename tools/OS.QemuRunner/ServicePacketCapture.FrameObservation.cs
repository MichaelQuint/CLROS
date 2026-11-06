using System.Buffers.Binary;

namespace OS.Tools.QemuRunner;

public sealed partial class ServicePacketCapture
{
    /// <summary>One complete original Ethernet record. Ordinals start at one;
    /// timestamps are microseconds since the PCAP epoch. The caller exclusively
    /// owns Bytes and the returned observation array.</summary>

    public readonly record struct FrameObservation(long Ordinal, ulong Microseconds, byte[] Bytes);
}
