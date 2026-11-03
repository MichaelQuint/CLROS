using System.Buffers.Binary;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Inspect a borrowed bounded classic PCAP against independent
    /// parent NIC/ARP values and the exact containment peer contract. Require
    /// checked streams, withheld role-0 data ACKs and timed head retransmissions,
    /// fifteen healthy rounds over at least70 seconds, one exact1518-byte VLAN
    /// before round7, continued traffic and final exact resets. Caller bytes are
    /// not retained. Firmware, syscall, scheduler and package authority require
    /// separate joins; generated packet vectors alone grant none.</summary>

    public static SocketContainmentPacketInspection InspectSocketContainmentPacketsForTests(byte[] capture, uint anchor, QemuArpReceiveReceipt arp) => ReadSocketContainmentPackets(capture, anchor, arp);
    internal static SocketContainmentPacketInspection ReadSocketContainmentPackets(byte[] bytes, uint anchor, QemuArpReceiveReceipt arp)
    {
        Need(arp.PeerMac == 0x020000000002, "socket_containment_wire_peer");
        _ = QemuEthernetCapture.Inspect(bytes, anchor);
        int offset = 24, prefixStage = 0, prefixEnd = 0, records = 0, resolutionSent = 0, heads = 0;
        ulong clock = 0, peerReceived = 0, pumpReceived = 0, arpReceived = 0, arpCopied = 0, tcpCopied = 0, ignored = 0;
        ulong firstHead = 0, lastHead = 0, vlan = 0;
        bool resolved = false;
        List<ContainmentWireFlow> flows = [];
        while (offset < bytes.Length)
        {
            ulong now = (ulong)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) * 1_000_000 + BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4));
            Need(now >= clock && ++records <= 8192, "socket_containment_wire_clock");
            clock = now;
            int size = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 8)));
            offset += 16;
            var frame = bytes.AsSpan(offset, size);
            offset += size;
            if (prefixStage < 3)
            {
                if (prefixStage == 0)
                {
                    if (frame.Length == 60 && frame.Slice(14, 9).SequenceEqual("CLROS-M07"u8) && OS.Std.Hash.Crc32.Compute(frame) == anchor)
                        prefixStage = 1;
                    continue;
                }

                Need(OS.Std.Hash.Crc32.Compute(frame) == (prefixStage == 1 ? arp.RequestCrc32 : arp.ReceiveCrc32) && frame.Length == (prefixStage == 1 ? 60 : arp.ReceiveBytes), "socket_wire_parent_arp");
                prefixStage++;
                prefixEnd = offset;
                continue;
            }

            Need(frame.Length >= 14, "socket_wire_ethernet");
            if (SocketIgnoredRouterAdvertisement(frame))
            {
                if (resolved)
                {
                    pumpReceived++;
                    ignored++;
                }
                else
                    peerReceived++;
                continue;
            }

            bool outgoing = MatchNetworkMac(frame[6..], 0x020000000001), incoming = MatchNetworkMac(frame[6..], arp.PeerMac);
            Need(outgoing != incoming && (outgoing ? MatchNetworkMac(frame, arp.PeerMac) || frame[..6].IndexOfAnyExcept((byte)255) < 0 : MatchNetworkMac(frame, 0x020000000001) || frame[..6].IndexOfAnyExcept((byte)255) < 0), "socket_wire_mac");
            ushort type = BinaryPrimitives.ReadUInt16BigEndian(frame[12..]);
            if (!resolved)
            {
                Need(type == 0x0806, "socket_wire_resolution_type");
                CheckSocketArp(frame, outgoing, arp.PeerMac, out ushort operation);
                if (outgoing)
                    Need(operation == 1 && ++resolutionSent == 1, "socket_wire_resolution_request");
                else
                {
                    peerReceived++;
                    if (operation == 2)
                    {
                        Need(resolutionSent == 1, "socket_wire_resolution_order");
                        resolved = true;
                    }
                }

                continue;
            }

            if (incoming)
                pumpReceived++;
            if (type == 0x0806)
            {
                CheckSocketArp(frame, outgoing, arp.PeerMac, out _);
                if (outgoing)
                    arpCopied++;
                else
                    arpReceived++;
                continue;
            }

            if (type == 0x8100)
            {
                Need(incoming && vlan == 0 && frame.Length == 1518 && frame[..18].SequenceEqual(Convert.FromHexString("020000000001020000000002810000010800")) && frame[18..].IndexOfAnyExcept((byte)0) < 0 && flows.Count == 3 && flows.Skip(1).All(flow => flow.Received == 8 && flow.SentThrough == 7 && !flow.Reset && !flow.Fin), "socket_containment_wire_vlan");
                vlan = clock;
                ignored++;
                continue;
            }

            if (!ReadSocketTcpPacket(frame, outgoing, arp.PeerMac, out var packet))
            {
                ignored++;
                continue;
            }

            if (outgoing)
                tcpCopied++;
            Need(packet.Remote is >= 41000 and <= 41002, "socket_containment_wire_tuple");
            int role = (int)packet.Remote - 41000;
            uint sequence = packet.Sequence, ack = packet.Acknowledgement;
            byte flags = packet.Flags;
            var payload = packet.Payload;
            if (incoming && flags == 2)
            {
                Need(payload.Length == 0 && ack == 0 && sequence == 10000 + role * 10000 && role <= flows.Count && (role == 0 || flows[role - 1].Received != 0), "socket_containment_wire_syn");
                if (role == flows.Count)
                    flows.Add(new(role));
                var pending = flows[role];
                Need(!pending.SynAck && !pending.Reset && ++pending.SynRequests <= (role == 0 ? 60 : 1), "socket_containment_wire_syn_retry");
                if (pending.SynRequests == 1)
                    pending.FirstSyn = clock;
                else
                    Need(clock - pending.LastSyn >= 500_000 && clock - pending.FirstSyn < 30_000_000, "socket_containment_wire_syn_retry_timing");
                pending.LastSyn = clock;
                continue;
            }

            Need(role < flows.Count, "socket_containment_wire_unowned");
            var flow = flows[role];
            if (outgoing && flags == 0x12)
            {
                Need(!flow.SynAck && !flow.Reset && payload.Length == 0 && ack == flow.Client, "socket_wire_syn_ack");
                flow.SynAck = true;
                flow.Server = unchecked(sequence + 1);
                continue;
            }

            if (incoming && !flow.HandshakeAck)
            {
                Need(flow.SynAck && flags == 0x10 && payload.Length == 0 && sequence == flow.Client && ack == flow.Server, "socket_containment_wire_handshake_ack");
                flow.HandshakeAck = true;
                continue;
            }

            Need(flow.HandshakeAck, "socket_containment_wire_handshake_ack");
            // Final close can race the peer's explicit abort: PCAP arrival of
            // RST does not mean the guest has consumed that RX buffer yet.
            if (outgoing && flags == 0x11)
            {
                Need(flow.SynAck && role != 0 && payload.Length == 0 && sequence == unchecked(flow.Server + 15) && ack == flow.Client + 16 && flow.SentThrough == 15 && flow.Received == 16, "socket_containment_wire_fin");
                flow.Fin = true;
                continue;
            }

            Need(flow.SynAck && !flow.Reset && flags is 0x10 or 0x18 or 0x14, "socket_containment_wire_established");
            uint relative = unchecked(sequence - (outgoing ? flow.Server : flow.Client));
            uint acknowledged = unchecked(ack - (outgoing ? flow.Client : flow.Server));
            Need(relative <= (uint)(outgoing ? flow.SentThrough + (flow.Fin ? 1 : 0) : flow.Received) && acknowledged <= (uint)(outgoing ? flow.Received : flow.SentThrough + (flow.Fin ? 1 : 0)), "socket_wire_acknowledgement");
            if (incoming && role == 0)
                Need(acknowledged == 0, "socket_containment_wire_withheld_ack");
            if ((flags & 4) != 0)
            {
                Need(incoming && role != 0 && payload.Length == 0 && flags == 0x14 && relative == 16 && acknowledged >= 15 && acknowledged <= 15 + (flow.Fin ? 1U : 0U) && flows.Count == 3 && flows.Skip(1).All(value => value.SentThrough == 15 && value.Received == 16), "socket_containment_wire_reset");
                flow.Reset = true;
                continue;
            }

            if (payload.Length == 0)
                continue;
            Need(flags == 0x18 && !flow.Fin, "socket_containment_wire_data_flags");
            if (incoming)
            {
                Need(payload.Length == 1 && relative == flow.Received && (relative == 0 ? payload[0] == role : role != 0 && relative <= 15 && payload[0] == relative - 1 && (relative <= 7 || vlan != 0) && flows.Skip(1).All(value => value.SentThrough >= relative - 1)), "socket_containment_wire_request");
                flow.Received++;
                continue;
            }

            Need(relative + payload.Length <= flow.Sent.Length, "socket_wire_payload_extent");
            for (int i = 0; i < payload.Length; i++)
            {
                int at = checked((int)relative + i);
                Need(payload[i] == (role == 0 ? (byte)(at % 251) : (byte)(at ^ 0x80)), "socket_wire_literal_payload");
                flow.Sent[at] = true;
            }

            if (role == 0 && relative == 0)
            {
                if (heads++ == 0)
                    firstHead = clock;
                lastHead = clock;
            }

            if (role != 0)
            {
                Need(payload.Length == 1 && relative < flow.Received - 1, "socket_containment_wire_reply");
                if (relative == flow.SentThrough)
                {
                    if (flow.SentThrough == 0)
                        flow.FirstReply = clock;
                    flow.LastReply = clock;
                }
            }

            while (flow.SentThrough < flow.Sent.Length && flow.Sent[flow.SentThrough])
                flow.SentThrough++;
        }

        Need(prefixStage == 3, "socket_wire_parent_prefix");
        _ = QemuEthernetCapture.InspectArp(bytes.AsSpan(0, prefixEnd), arp);
        Need(resolved && flows.Count == 3 && vlan != 0 && heads >= 6 && lastHead - firstHead >= 30_000_000 && flows[0].Received == 1 && flows[0].SentThrough > 0 && !flows[0].Reset && !flows[0].Fin && flows.Skip(1).All(flow => flow.Received == 16 && flow.SentThrough == 15 && flow.Reset && flow.LastReply - flow.FirstReply >= 70_000_000), "socket_containment_wire_complete");
        int expired = flows[0].Sent.Count(value => value);
        return new(new(3, 33, expired + 30, 2, 0, flows.Count(flow => flow.Fin), peerReceived, pumpReceived, arpReceived, arpCopied, tcpCopied, ignored), expired, heads, firstHead, lastHead, flows.Skip(1).Min(flow => flow.LastReply - flow.FirstReply), vlan, flows[0].SynRequests - 1);
    }
}
