using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using OS.Net;

namespace OS.Tools.QemuRunner;
public sealed partial class ServiceSocketControls
{
    public sealed record Report(string Schema, int PendingPort, uint FirstClientSequence, uint SecondClientSequence, long FirstSynAckOrdinal, long SecondSynAckOrdinal, long FirstProbeOrdinal, long SecondProbeOrdinal, HttpExchange Persist, HttpExchange Healthy, UnreadClose Unread, IReadOnlyList<PacketObservation> Observations);
}
