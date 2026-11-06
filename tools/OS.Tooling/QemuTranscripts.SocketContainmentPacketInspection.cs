using System.Buffers.Binary;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Value-only three-peer wire observations. PCAP times are host
    /// microseconds; they are not network-owner clocks or guest acceptance.
    /// ExpiredBytesObserved counts distinct bytes seen on wire, not app writes.
    /// UnansweredStartupSyns counts extra role0 SYNs before its sole SYN-ACK;
    /// it does not assign an individual kernel RX outcome.</summary>

    public readonly record struct SocketContainmentPacketInspection(SocketPacketInspection Totals, int ExpiredBytesObserved, int HeadTransmissions, ulong FirstHeadMicroseconds, ulong LastHeadMicroseconds, ulong HealthyElapsedMicroseconds, ulong VlanMicroseconds, int UnansweredStartupSyns);
}
