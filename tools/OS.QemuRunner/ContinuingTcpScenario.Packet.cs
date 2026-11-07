using System.Buffers.Binary;
using System.Text;

namespace OS.Tools.QemuRunner;

public static partial class ContinuingTcpScenario
{
    public readonly record struct Packet(bool FromGuest, ushort Port, uint Sequence, uint Ack, byte Flags, ushort Window = 6, string Payload = "", bool Mss = false, bool BadChecksum = false, string Tag = "", int DelayMilliseconds = 0, ushort MaximumSegmentSize = 4);
}
