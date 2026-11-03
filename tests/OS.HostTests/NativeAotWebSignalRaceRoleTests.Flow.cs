using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;
using Q = OS.Tooling.QemuTranscripts;
using Peer = OS.Tooling.NativeAotServiceQuotaPeer;
using Values = NativeAotWebSignalRaceHostHttpTests.Values;

// Complete synthetic replay plus strict copied HTTP/stream values. No original
// process, native code, pointed-to bytes, actual GC or guest evidence is supplied.
internal static partial class NativeAotWebSignalRaceRoleTests
{
    private sealed class Flow(int connection, int descriptor, uint port, int slot, int generation)
    {
        internal readonly int Connection = connection, Descriptor = descriptor, Slot = slot, Generation = generation;
        internal readonly uint Port = port;
        internal string RequestBytes = "", ResponseBytes = "";
        internal readonly List<Q.ServiceQuotaPacketRange> Requests = [], Responses = [];
        internal Q.ServiceQuotaPacketStream Copy()
        {
            Q.ServiceQuotaPacketDirection Direction(bool response)
            {
                string bytes = response ? ResponseBytes : RequestBytes;
                return new(bytes.Length, bytes.Length, bytes.Length, Data(bytes), (response ? Responses : Requests).ToArray());
            }

            return new(Port, Direction(false), Direction(true), new(Connection, true, true, false, false, false, false, false, 0, 0, 0));
        }
    }
}
