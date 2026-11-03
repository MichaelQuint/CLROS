namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed class WebRacePacketPartition(uint[] ports, WebControlInspection controls)
    {
        internal readonly HashSet<uint> Ports = ports.ToHashSet();
        internal readonly HashSet<int> OrdinaryRecords = [];
        private readonly byte[] response = [..Convert.FromHexString(controls.Sockets.Before.ResponseHex), ..Convert.FromHexString(controls.Sockets.After.ResponseHex)];
        internal int FirstRecord, TcpRecords;
        internal void Record(int record, int ordinary, int vlan, WebWireFlow? healthy, bool incoming, byte flags)
        {
            if (FirstRecord == 0)
            {
                Need(ordinary == 8 && vlan == 1 && healthy is not null && healthy.ServerFin && incoming && flags == 2 && healthy.ClientSeen == healthy.Request.Length && healthy.ServerSeen == response.Length && healthy.ResponseExtent == response.Length && healthy.Response.AsSpan(0, response.Length).SequenceEqual(response), "web_race_wire_before_controls");
                FirstRecord = record;
            }

            TcpRecords++;
        }
    }
}
