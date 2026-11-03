namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied whole-PCAP inventory. Ordinary retains its exact HTTP,
    /// VLAN, withheld-ACK and UTC checks; added streams retain original record
    /// and first-byte range indices. No host/NAT flow identity is inferred.</summary>

    public sealed record WebRacePacketInspection(WebPacketInspection Ordinary, ServiceQuotaPacketStreams Race, IReadOnlyList<int> OrdinaryRecords, int FirstRaceRecord);
}
