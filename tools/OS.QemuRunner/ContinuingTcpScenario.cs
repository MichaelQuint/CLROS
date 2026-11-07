using System.Buffers.Binary;
using System.Text;

namespace OS.Tools.QemuRunner;
/// <summary>Finite M07-026 wire expectations, independent of kernel TCP codecs.
/// The active peer and separate PCAP reader use this literal fixture contract;
/// generated fixture bytes alone never prove guest execution. Fresh arrays are
/// caller-owned. No arbitrary protocol, routing or endpoint configuration is supported.</summary>

public static partial class ContinuingTcpScenario
{
    public static IReadOnlyList<Packet> Packets { get; } = Array.AsReadOnly<Packet>([new(false, 40000, 10000, 0, 2, Mss: true), new(true, 40000, 1000, 10001, 0x12, 8, Mss: true, Tag: "syn.first"), new(false, 40000, 10000, 0, 2, Mss: true), new(true, 40000, 1000, 10001, 0x12, 8, Mss: true), new(false, 40000, 10001, 1002, 0x10), new(true, 40000, 1000, 10001, 0x12, 8, Mss: true, Tag: "syn.retry"), new(false, 40000, 10001, 1001, 0x10), new(false, 40000, 10000, 0, 2, Mss: true), new(false, 40001, 20000, 0, 2, Mss: true), new(true, 40001, 2000, 20001, 0x12, 8, Mss: true), new(false, 40001, 20001, 2001, 0x10), new(false, 40001, 20001, 2001, 0x10), new(false, 40002, 50000, 0, 2, Mss: true), new(true, 40001, 2001, 20001, 0x11, 8), new(false, 40001, 20001, 2001, 0x11), new(true, 40001, 2002, 20002, 0x10, 8), new(false, 40001, 20002, 2002, 0x10), new(false, 40001, 20001, 2002, 0x11, Tag: "wait.fin"), new(true, 40001, 2002, 20002, 0x10, 8), new(false, 40000, 10005, 1001, 0x18, Payload: "ijkl"), new(true, 40000, 1001, 10001, 0x10, 8), new(false, 40000, 10001, 1001, 0x18, Payload: "abcdefgh"), new(true, 40000, 1001, 10009, 0x10, 0), new(true, 40000, 1001, 10009, 0x10, 3), new(false, 40000, 10005, 1001, 0x18, Payload: "efghij"), new(true, 40000, 1001, 10011, 0x10, 1), new(false, 40000, 10011, 1001, 0x18, Payload: "kl"), new(true, 40000, 1001, 10012, 0x10, 0), new(true, 40000, 1001, 10012, 0x10, 8), new(false, 40000, 10002, 1001, 0x18, Payload: "bcdefg"), new(true, 40000, 1001, 10012, 0x10, 8), new(false, 40000, 10014, 1001, 0x18, Payload: "no"), new(true, 40000, 1001, 10012, 0x10, 8), new(false, 40000, 10012, 1001, 0x19, Payload: "lmno"), new(true, 40000, 1001, 10017, 0x10, 4), new(true, 40000, 1001, 10017, 0x18, 4, "ABCD", Tag: "data.first"), new(false, 40000, 10017, 1001, 0x10), new(false, 40000, 10017, 1001, 0x10), new(false, 40000, 10017, 1001, 0x10, 0), new(true, 40000, 1001, 10017, 0x18, 4, "A", Tag: "data.retry"), new(false, 40000, 10017, 1003, 0x10), new(false, 40000, 10017, 1006, 0x10), new(false, 40000, 10017, 1002, 0x10), new(false, 40000, 10017, 1005, 0x10, 0), new(false, 40000, 10017, 1005, 0x10, 3), new(true, 40000, 1005, 10017, 0x18, 4, "EFG"), new(false, 40000, 10017, 1006, 0x10, 0), new(false, 40000, 10017, 1008, 0x10), new(true, 40000, 1008, 10017, 0x18, 4, "HIJK"), new(false, 40000, 10017, 1012, 0x10), new(true, 40000, 1012, 10017, 0x18, 4, "LMNO"), new(false, 40000, 10017, 1016, 0x10), new(true, 40000, 1016, 10017, 0x18, 4, "PQRS"), new(false, 40000, 10017, 1020, 0x10), new(true, 40000, 1020, 10017, 0x18, 4, "T"), new(false, 40000, 10017, 1021, 0x10), new(true, 40000, 1021, 10017, 0x11, 4), new(false, 40000, 10017, 1022, 0x10), new(false, 40000, 0xfffffffd, 0, 2, Mss: true), new(true, 40000, 0xfffffffc, 0xfffffffe, 0x12, 8, Mss: true), new(false, 40000, 0xfffffffe, 0xfffffffd, 0x10), new(false, 40000, 10016, 1022, 0x11), new(false, 40000, 0xfffffffe, 0xfffffffd, 0x18, Payload: "data"), new(true, 40000, 0xfffffffd, 2, 0x10, 4), new(true, 40000, 0xfffffffd, 2, 0x18, 4, "disc"), new(false, 40000, 2, 0, 4, BadChecksum: true), new(false, 40000, 40000, 0, 4), new(false, 40000, 3, 0, 4), new(true, 40000, 1, 2, 0x10, 4), new(false, 40000, 2, 0, 4), new(false, 40003, 40000, 0, 2, Mss: true), new(true, 40003, 4000, 40001, 0x12, 8, Mss: true), new(false, 40003, 40001, 4001, 0x10), new(true, 40003, 4001, 40001, 0x18, 8, "lost", Tag: "loss.first"), new(true, 40003, 4001, 40001, 0x18, 8, "lost", Tag: "loss.retry1"), new(true, 40003, 4001, 40001, 0x18, 8, "lost", Tag: "loss.retry2"), new(false, 40004, 50000, 0, 2, Mss: true, DelayMilliseconds: 4250), new(true, 40004, 5000, 50001, 0x12, 8, Mss: true, Tag: "pending.first"), new(true, 40004, 5000, 50001, 0x12, 8, Mss: true, Tag: "pending.retry1"), new(true, 40004, 5000, 50001, 0x12, 8, Mss: true, Tag: "pending.retry2")]);

    /// <summary>Serialize one literal packet without FCS, zero-padding short Ethernet frames.</summary>

    public static byte[] Write(Packet packet, ulong peerMac)
    {
        int tcpLength = 20 + (packet.Mss ? 4 : 0) + packet.Payload.Length, total = 20 + tcpLength;
        byte[] bytes = new byte[Math.Max(60, 14 + total)];
        Span<byte> frame = bytes;
        Mac(frame, packet.FromGuest ? peerMac : 0x020000000001);
        Mac(frame[6..], packet.FromGuest ? 0x020000000001 : peerMac);
        BinaryPrimitives.WriteUInt16BigEndian(frame[12..], 0x0800);
        frame[14] = 0x45;
        BinaryPrimitives.WriteUInt16BigEndian(frame[16..], (ushort)total);
        frame[22] = 64;
        frame[23] = 6;
        Ip(frame[26..], packet.FromGuest);
        Ip(frame[30..], !packet.FromGuest);
        BinaryPrimitives.WriteUInt16BigEndian(frame[24..], Checksum(frame.Slice(14, 20)));
        Span<byte> tcp = frame.Slice(34, tcpLength);
        BinaryPrimitives.WriteUInt16BigEndian(tcp, packet.FromGuest ? (ushort)8080 : packet.Port);
        BinaryPrimitives.WriteUInt16BigEndian(tcp[2..], packet.FromGuest ? packet.Port : (ushort)8080);
        BinaryPrimitives.WriteUInt32BigEndian(tcp[4..], packet.Sequence);
        BinaryPrimitives.WriteUInt32BigEndian(tcp[8..], packet.Ack);
        tcp[12] = packet.Mss ? (byte)0x60 : (byte)0x50;
        tcp[13] = packet.Flags;
        BinaryPrimitives.WriteUInt16BigEndian(tcp[14..], packet.Window);
        if (packet.Mss)
        {
            tcp[20] = 2;
            tcp[21] = 4;
            BinaryPrimitives.WriteUInt16BigEndian(tcp[22..], packet.MaximumSegmentSize);
        }

        Encoding.ASCII.GetBytes(packet.Payload, tcp[(packet.Mss ? 24 : 20)..]);
        ushort checksum = TcpChecksum(frame.Slice(26, 8), tcp);
        if (packet.BadChecksum)
            checksum ^= 0x0100;
        BinaryPrimitives.WriteUInt16BigEndian(tcp[16..], checksum);
        return bytes;
    }

    /// <summary>Require checksum/tuple/sequence/flags/window/MSS/payload, with no hidden options,
    /// fragmentation or nonzero Ethernet padding. IPv4 ID, DF and TTL are not fixture claims.</summary>

    public static void Validate(ReadOnlySpan<byte> frame, Packet expected, ulong peerMac)
    {
        byte[] golden = Write(expected, peerMac);
        int total = BinaryPrimitives.ReadUInt16BigEndian(golden.AsSpan(16));
        if (frame.Length < 14 + total || frame.Length > Math.Max(60, 14 + total) || !frame[..14].SequenceEqual(golden.AsSpan(0, 14)) || frame[14] != 0x45 || frame[23] != 6 || BinaryPrimitives.ReadUInt16BigEndian(frame[16..]) != total || (BinaryPrimitives.ReadUInt16BigEndian(frame[20..]) & 0xbfff) != 0 || Checksum(frame.Slice(14, 20)) != 0 || !frame.Slice(26, 8).SequenceEqual(golden.AsSpan(26, 8)))
            throw new InvalidDataException("TCP fixture IPv4/endpoint mismatch.");
        ReadOnlySpan<byte> tcp = frame.Slice(34, total - 20), wanted = golden.AsSpan(34, total - 20);
        if (!tcp[..16].SequenceEqual(wanted[..16]) || !tcp[18..].SequenceEqual(wanted[18..]) || (expected.BadChecksum ? !tcp.Slice(16, 2).SequenceEqual(wanted.Slice(16, 2)) : TcpChecksum(frame.Slice(26, 8), tcp) != 0))
            throw new InvalidDataException("TCP fixture sequence/control/checksum/payload mismatch.");
        foreach (byte padding in frame[(14 + total)..])
            if (padding != 0)
                throw new InvalidDataException("TCP fixture padding mismatch.");
    }

    private static void Ip(Span<byte> bytes, bool guest)
    {
        bytes[0] = 10;
        bytes[1] = 0;
        bytes[2] = 2;
        bytes[3] = guest ? (byte)15 : (byte)2;
    }

    private static void Mac(Span<byte> bytes, ulong address)
    {
        for (int i = 0; i < 6; i++)
            bytes[i] = (byte)(address >> (40 - i * 8));
    }

    private static ushort TcpChecksum(ReadOnlySpan<byte> addresses, ReadOnlySpan<byte> tcp)
    {
        byte[] pseudo = new byte[12 + tcp.Length];
        addresses.CopyTo(pseudo);
        pseudo[9] = 6;
        BinaryPrimitives.WriteUInt16BigEndian(pseudo.AsSpan(10), (ushort)tcp.Length);
        tcp.CopyTo(pseudo.AsSpan(12));
        return Checksum(pseudo);
    }

    private static ushort Checksum(ReadOnlySpan<byte> bytes)
    {
        uint sum = 0;
        for (int i = 0; i < bytes.Length; i += 2)
            sum += (uint)((bytes[i] << 8) | (i + 1 < bytes.Length ? bytes[i + 1] : 0));
        while (sum > 65535)
            sum = (sum & 65535) + (sum >> 16);
        return (ushort)~sum;
    }
}
