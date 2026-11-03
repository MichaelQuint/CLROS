using System.Buffers.Binary;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    // Borrow one already bounded Ethernet frame. Both socket cases retain their
    // own direction, ARP, stream and lifecycle assertions; this helper checks
    // only their shared fixed IPv4/TCP envelope. The caller consumes the
    // returned span synchronously without retaining it.
    private static bool ReadSocketTcpPacket(ReadOnlySpan<byte> frame, bool outgoing, ulong peer, out SocketTcpPacket packet)
    {
        packet = default;
        Need(frame.Length >= 34 && BinaryPrimitives.ReadUInt16BigEndian(frame[12..]) == 0x0800 && MatchNetworkMac(frame, outgoing ? peer : 0x020000000001), "socket_wire_ipv4");
        var ip = frame[14..];
        int ihl = (ip[0] & 15) * 4, total = BinaryPrimitives.ReadUInt16BigEndian(ip[2..]);
        Need((ip[0] >> 4) == 4 && ihl >= 20 && ihl <= 60 && total >= ihl && total <= 1500 && total <= ip.Length && (BinaryPrimitives.ReadUInt16BigEndian(ip[6..]) & 0x3fff) == 0 && ip[8] != 0 && NetworkChecksum(ip[..ihl]) && BinaryPrimitives.ReadUInt32BigEndian(ip[12..]) == (outgoing ? 0x0a00020fU : 0x0a000202U) && BinaryPrimitives.ReadUInt32BigEndian(ip[16..]) == (outgoing ? 0x0a000202U : 0x0a00020fU), "socket_wire_ip_envelope");
        if (ip[9] != 6)
        {
            Need(!outgoing && ip[9] == 17, "socket_wire_non_tcp");
            return false;
        }

        var tcp = ip.Slice(ihl, total - ihl);
        Need(tcp.Length >= 20, "socket_wire_tcp_extent");
        int header = (tcp[12] >> 4) * 4;
        Need(header >= 20 && header <= tcp.Length, "socket_wire_tcp_header");
        byte[] checksum = new byte[12 + tcp.Length];
        ip.Slice(12, 8).CopyTo(checksum);
        checksum[9] = 6;
        BinaryPrimitives.WriteUInt16BigEndian(checksum.AsSpan(10), checked((ushort)tcp.Length));
        tcp.CopyTo(checksum.AsSpan(12));
        Need(NetworkChecksum(checksum), "socket_wire_tcp_checksum");
        uint source = BinaryPrimitives.ReadUInt16BigEndian(tcp), destination = BinaryPrimitives.ReadUInt16BigEndian(tcp[2..]);
        Need((outgoing ? source : destination) == 8080, "socket_wire_port");
        byte flags = tcp[13];
        Need((flags & 0xc0) == 0 && (flags & 0x20) == 0, "socket_wire_tcp_flags");
        packet = new(outgoing ? destination : source, BinaryPrimitives.ReadUInt32BigEndian(tcp[4..]), BinaryPrimitives.ReadUInt32BigEndian(tcp[8..]), flags, tcp[header..]);
        return true;
    }
}
