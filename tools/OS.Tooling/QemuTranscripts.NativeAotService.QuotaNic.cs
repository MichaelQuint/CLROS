using System.Buffers.Binary;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Synchronously borrow <=4MiB original PCAP and bounded raw serial,
    /// beside the original parent/envelope reports. Require original parent ARP,
    /// exact raw peer/wire rows, zero capacity drops and all shared peer/ARP/TCP/
    /// accepted/ignored/quiet-SYN accounting. Reuse the positive counter helper;
    /// quota deliberately has no FIN/compact-expiry requirement. Return scalar
    /// copied counts; malformed, inconsistent or missing inputs throw. This is
    /// a value-only seam, not a substitute for the complete original quota parent,
    /// protocol/envelope, strict TCP/stream/HTTP, native and process consumers.
    /// Production must hold the same original PCAP/serial/program inputs across
    /// all joins. No synthetic shortened serial is admitted as a full capture.</summary>

    public static ServiceNicAccountingInspection InspectNativeAotServiceQuotaNicForTests(byte[] pcap, WebParentReport parent, ServiceEnvelopeInspection envelope, byte[] raw) => ReadServiceQuotaNic(pcap, parent, envelope, raw);
    private static ServiceNicAccountingInspection ReadServiceQuotaNic(byte[] pcap, WebParentReport parent, ServiceEnvelopeInspection envelope, byte[] raw)
    {
        if (pcap is null || raw is null)
            throw new ToolFailure("service_quota_nic_input");
        Need(raw.Length is > 0 and <= 128 * 1024 * 1024, "service_quota_nic_input");
        _ = QemuEthernetCapture.Inspect(pcap, parent.FrameAnchor);
        // Shared accounting is downstream of strict flow validation in actual
        // consumers. Guard its direct Ethernet-header reads in this value seam.
        int parentStage = 0;
        for (int offset = 24; offset < pcap.Length;)
        {
            int size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(pcap.AsSpan(offset + 8)));
            offset += 16;
            var frame = pcap.AsSpan(offset, size);
            offset += size;
            Need(size >= 14 && (BinaryPrimitives.ReadUInt16BigEndian(frame[12..]) != 0x0806 || size >= 22), "service_quota_nic_frame");
            if (parentStage == 0)
            {
                if (size == 60 && OS.Std.Hash.Crc32.Compute(frame) == parent.FrameAnchor)
                    parentStage = 1;
            }
            else if (parentStage < 3 && ++parentStage == 3)
                _ = QemuEthernetCapture.InspectArp(pcap.AsSpan(0, offset), parent.Arp);
        }

        Need(parentStage == 3, "service_quota_nic_parent_arp");
        Need(envelope.Capacity == 0 && (decimal)envelope.Received == (decimal)envelope.Arp + envelope.Accepted + envelope.Ignored, "service_quota_nic_envelope");
        string[] rows = ReadThreadingSerialLines(DecodeUtf8(raw));
        string One(string prefix)
        {
            var matches = rows.Where(row => row.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Need(matches.Length == 1, "service_quota_nic_row_once");
            return matches[0];
        }

        var peer = ReadRecord(One("M16_011_PEER "), "M16_011_PEER", "mac:dec", "sent:dec", "received:dec", "start:dec", "end:dec");
        Need(peer["mac"] == parent.Arp.PeerMac && peer["sent"] == 1 && peer["received"] > 0 && peer["start"] == parent.PeerStart && peer["end"] == parent.PeerEnd, "service_quota_nic_parent");
        var wire = ReadRecord(One("M16_011_WIRE "), "M16_011_WIRE", "received:dec", "arp:dec", "accepted:dec", "ignored:dec", "capacity:dec", "arpCopied:dec", "tcpCopied:dec");
        Need(wire["received"] == envelope.Received && wire["arp"] == envelope.Arp && wire["accepted"] == envelope.Accepted && wire["ignored"] == envelope.Ignored && wire["capacity"] == envelope.Capacity && wire["arpCopied"] == envelope.ArpCopied && wire["tcpCopied"] == envelope.TcpCopied, "service_quota_nic_wire");
        return ReadServiceWireTotals(pcap, parent, envelope, raw, out _);
    }
}
