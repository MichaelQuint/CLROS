using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Net;

namespace OS.Tools.QemuRunner;

public sealed partial class ServiceSocketControls
{
    private sealed record Seen(long Ordinal, uint Sequence, uint Ack, byte Flags, byte[] Payload);
}
