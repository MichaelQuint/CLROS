namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Borrow the original bounded whole PCAP and ordered distinct
    /// census of8 ordinary plus1..504 later ports. First run strict normal web
    /// controls over the SAME original bytes, explicitly delegating only declared
    /// added TCP after complete healthy After response+EOF. Then run bounded
    /// concurrent TCP reconstruction over the SAME bytes, delegating only the
    /// already-validated ordinary TCP/VLAN original record IDs. No PCAP record
    /// is filtered, copied into a synthetic capture, renumbered or retimestamped.
    /// Require exact delegation/TCP counts and no quiet unknown SYNs. Added
    /// streams retain holes/retries/partial suffixes and existing client-reset
    /// checks; zero-entity FIN is allowed only in this explicit added inventory.
    /// Ordinary and service public defaults remain unchanged. Return copied
    /// read-only records or throw on malformed/unknown/incomplete controls.
    /// This checks no race HTTP semantics/Date, host-byte assignment, syscall,
    /// file/process/native/source ownership or guest acceptance.</summary>

    public static WebRacePacketInspection InspectNativeAotWebRacePacketsForTests(byte[] bytes, uint anchor, QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports, int forward, string body, string configuration, IReadOnlyList<WebUtcObservation> utcWindows, WebControlInspection controls)
    {
        Need(ports is not null && ports.Count is >= 9 and <= 512 && ports.Distinct().Count() == ports.Count && ports.All(p => p is> 0 and <= 65535) && controls is not null && utcWindows is not null, "web_race_wire_inputs");
        uint[] all = ports!.ToArray();
        var partition = new WebRacePacketPartition(all[8..], controls!);
        var ordinary = ReadWebPackets(bytes, anchor, arp, all[..8], forward, body, configuration, false, utcWindows, controls, partition);
        Need(ordinary.ControlsChecked && ordinary.UtcChecked && partition.FirstRecord > 0 && partition.OrdinaryRecords.Count == ordinary.TcpRecords + 1, "web_race_wire_partition");
        var race = ReadQuotaPacketStreams(bytes, anchor, arp, all[8..], true, partition.OrdinaryRecords);
        Need(race.TcpRecords == partition.TcpRecords && race.QuietSyns == 0, "web_race_wire_partition");
        return new(ordinary, race, Array.AsReadOnly(partition.OrdinaryRecords.Order().ToArray()), partition.FirstRecord);
    }
}
