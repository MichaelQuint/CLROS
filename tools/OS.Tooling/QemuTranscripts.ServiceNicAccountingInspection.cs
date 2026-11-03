using System.Buffers.Binary;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Copied original-record counts only. PeerReceived precedes service
    /// ARP resolution; all other traffic counters follow it. QuietSyns are still
    /// unanswered SYN records before FirstSynAckRecord (one-based PCAP index).
    /// No byte/HTTP, FIN/TIMEWAIT, ownership or cleanup verdict is implied.</summary>

    public readonly record struct ServiceNicAccountingInspection(ulong PeerReceived, ulong Received, ulong Arp, ulong Accepted, ulong Ignored, ulong ArpCopied, ulong TcpCopied, int QuietSyns, int FirstSynAckRecord);
}
