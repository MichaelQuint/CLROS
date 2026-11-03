using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OS.Tools.QemuRunner;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed record ServiceControlPacket(long Ordinal, ulong Microseconds, byte[] Raw, bool Outgoing, uint Port, uint Sequence, uint Ack, byte Flags, ushort Window, byte[] Payload);
}
