using System.Buffers.Binary;
using OS.Tooling;

internal static partial class SocketContainmentPacketTests
{
    private const ulong Peer = 0x020000000002;
    internal static (byte[] Capture, uint Anchor, QemuArpReceiveReceipt Arp) ReferenceCapture(int startupRetries = 0)
    {
        var fixture = Create(startupRetries: startupRetries);
        return (Capture(fixture.Records), fixture.Anchor, fixture.Arp);
    }

    [HostTest("containment PCAP bounds unanswered startup SYNs and requires one unambiguous completed handshake")]
    private static void StartupHandshake()
    {
        foreach (int retries in new[]
        {
            0,
            12,
            59
        }

        )
        {
            var f = Create(startupRetries: retries);
            var value = Inspect(f, Capture(f.Records));
            HostCheck.Equal(retries, value.UnansweredStartupSyns);
            HostCheck.Equal(72UL + (ulong)retries, value.Totals.PumpReceived);
            HostCheck.Equal(1UL, value.Totals.PumpIgnored);
        }

        Reject(Create(startupRetries: 60));
        var compressed = Create(startupRetries: 2);
        int firstSyn = compressed.Records.FindIndex(row => IsTcp(row, false, 41000) && row.Frame[47] == 2);
        compressed.Records[firstSyn + 1] = compressed.Records[firstSyn + 1] with
        {
            Time = compressed.Records[firstSyn].Time + 1
        };
        Reject(compressed);
        var duplicate = Create();
        int synAck = duplicate.Records.FindIndex(row => IsTcp(row, true, 41000) && row.Frame[47] == 0x12);
        duplicate.Records.Insert(synAck, duplicate.Records[synAck]);
        Reject(duplicate);
        var late = Create();
        int syn = late.Records.FindIndex(row => IsTcp(row, false, 41000) && row.Frame[47] == 2);
        var first = late.Records[syn];
        late.Records.Insert(syn + 2, first with { Time = late.Records[syn + 1].Time });
        Reject(late);
        var other = Create();
        syn = other.Records.FindIndex(row => IsTcp(row, false, 41001) && row.Frame[47] == 2);
        other.Records.Insert(syn, other.Records[syn]);
        Reject(other);
        var missing = Create();
        int ack = missing.Records.FindIndex(row => IsTcp(row, false, 41000) && row.Frame[47] == 0x10);
        missing.Records.RemoveAt(ack);
        Reject(missing);
        var reset = Create();
        synAck = reset.Records.FindIndex(row => IsTcp(row, true, 41000) && row.Frame[47] == 0x12);
        Change(reset, synAck, bytes => bytes[47] = 0x14);
        Reject(reset);
    }

    [HostTest("containment PCAP independently proves retries timed healthy traffic VLAN and exact resets")]
    private static void Complete()
    {
        foreach (var(fin, acknowledged, late)in new[]
        {
            (false, false, false),
            (true, true, false),
            (true, false, false),
            (true, false, true)
        }

        )
        {
            var fixture = Create(fin, acknowledged, late);
            byte[] raw = Capture(fixture.Records), original = (byte[])raw.Clone();
            var value = Inspect(fixture, raw);
            HostCheck.True(raw.SequenceEqual(original));
            HostCheck.Equal(3, value.Totals.Connections);
            HostCheck.Equal(33, value.Totals.Received);
            HostCheck.Equal(1054, value.Totals.Sent);
            HostCheck.Equal(1024, value.ExpiredBytesObserved);
            HostCheck.Equal(8, value.HeadTransmissions);
            HostCheck.Equal(2, value.Totals.Resets);
            HostCheck.Equal(fin ? 2 : 0, value.Totals.ServerFins);
            HostCheck.Equal(1UL, value.Totals.PumpIgnored);
            HostCheck.True(value.HealthyElapsedMicroseconds >= 70_000_000);
            HostCheck.True(value.LastHeadMicroseconds - value.FirstHeadMicroseconds >= 30_000_000);
            bool oldRejected = false;
            try
            {
                _ = QemuTranscripts.InspectSocketPacketsForTests(raw, fixture.Anchor, fixture.Arp, [41000, 41001, 41002]);
            }
            catch (ToolFailure)
            {
                oldRejected = true;
            }

            HostCheck.True(oldRejected);
        }
    }

    [HostTest("containment PCAP rejects ACK of deliberately withheld data and changed checksummed stream bytes")]
    private static void Streams()
    {
        var f = Create();
        int head = f.Records.FindIndex(row => IsTcp(row, true, 41000, data: true));
        Change(f, head, bytes => bytes[54] ^= 1);
        Reject(f);
        f = Create();
        int role = f.Records.FindIndex(row => IsTcp(row, false, 41000, data: true));
        Change(f, role, bytes => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(42), 902));
        Reject(f);
        f = Create();
        int reply = f.Records.FindIndex(row => IsTcp(row, true, 41001, data: true));
        Change(f, reply, bytes => bytes[54] ^= 1);
        Reject(f);
        f = Create();
        int request = f.Records.FindLastIndex(row => IsTcp(row, false, 41002, data: true));
        Change(f, request, bytes => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(38), 30017));
        Reject(f);
        f = Create();
        f.Records[head].Frame[54] ^= 1;
        Reject(f);
    }

    [HostTest("containment PCAP rejects missing changed duplicated or misplaced VLAN and traffic after early reset")]
    private static void DropAndRetirement()
    {
        var f = Create();
        int vlan = f.Records.FindIndex(row => row.Frame.Length == 1518);
        f.Records.RemoveAt(vlan);
        Reject(f);
        f = Create();
        f.Records[vlan].Frame[^1] = 1;
        Reject(f);
        f = Create();
        f.Records.Insert(vlan, f.Records[vlan]);
        Reject(f);
        f = Create();
        Wire moved = f.Records[vlan];
        f.Records.RemoveAt(vlan);
        f.Records.Insert(14, moved with { Time = f.Records[14].Time });
        Reject(f);
        f = Create();
        int reset = f.Records.FindIndex(row => IsTcp(row, false, 41001) && row.Frame[47] == 0x14);
        Wire early = f.Records[reset];
        f.Records.RemoveAt(reset);
        f.Records.Insert(vlan, early with { Time = f.Records[vlan].Time });
        Reject(f);
        f = Create();
        f.Records.RemoveAt(f.Records.Count - 1);
        Reject(f);
        f = Create();
        Change(f, reset, bytes => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(42), 1915));
        Reject(f);
    }

    [HostTest("containment PCAP rejects compressed healthy duration and insufficient retry count or spacing")]
    private static void Timing()
    {
        var f = Create();
        f = f with
        {
            Records = f.Records.Select(row => row with { Time = row.Time / 2 }).ToList()
        };
        Reject(f);
        f = Create();
        int seen = 0;
        f.Records.RemoveAll(row => IsTcp(row, true, 41000, data: true) && ++seen > 5);
        Reject(f);
        f = Create();
        var heads = f.Records.Where(row => IsTcp(row, true, 41000, data: true)).ToList();
        f.Records.RemoveAll(row => IsTcp(row, true, 41000, data: true));
        int at = f.Records.FindIndex(row => IsTcp(row, false, 41001, data: true) && row.Frame[54] == 0);
        f.Records.InsertRange(at, heads.Select(row => row with { Time = f.Records[at].Time }));
        Reject(f);
        f = Create();
        f.Records[20] = f.Records[20] with
        {
            Time = 0
        };
        Reject(f);
    }

    [HostTest("containment PCAP rejects truncated records foreign tuples and unsupported Ethernet content")]
    private static void Envelope()
    {
        var f = Create();
        byte[] raw = Capture(f.Records);
        foreach (int length in new[]
        {
            0,
            23,
            40,
            raw.Length - 1
        }

        )
            Reject(f, raw[..length]);
        int packet = f.Records.FindIndex(row => IsTcp(row, true, 41001, data: true));
        Change(f, packet, bytes => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(36), 41003));
        Reject(f);
        f = Create();
        f.Records[packet].Frame[12] = 0x88;
        Reject(f);
        f = Create();
        f.Records[packet].Frame[6] = 3;
        Reject(f);
        f = Create();
        f.Records.RemoveAt(1);
        Reject(f);
    }

    private static QemuTranscripts.SocketContainmentPacketInspection Inspect(Fixture f, byte[] bytes) => QemuTranscripts.InspectSocketContainmentPacketsForTests(bytes, f.Anchor, f.Arp);
    private static void Reject(Fixture f, byte[]? bytes = null)
    {
        bool rejected = false;
        try
        {
            _ = Inspect(f, bytes ?? Capture(f.Records));
        }
        catch (Exception error)when (error is ToolFailure or InvalidDataException)
        {
            rejected = true;
        }

        HostCheck.True(rejected);
    }

    private static bool IsTcp(Wire row, bool outgoing, ushort port, bool data = false) => row.Frame.Length >= 54 && row.Frame[12] == 8 && row.Frame[13] == 0 && row.Frame[23] == 6 && row.Frame[11] == (outgoing ? 1 : 2) && BinaryPrimitives.ReadUInt16BigEndian(row.Frame.AsSpan(outgoing ? 36 : 34)) == port && (!data || row.Frame.Length > 54);
    private static void Change(Fixture f, int at, Action<byte[]> mutation)
    {
        byte[] bytes = (byte[])f.Records[at].Frame.Clone();
        mutation(bytes);
        Seal(bytes);
        f.Records[at] = f.Records[at] with
        {
            Frame = bytes
        };
    }

    // Independent byte vectors: do not invoke the active peer or kernel codec.
    private static Fixture Create(bool fins = false, bool acknowledged = true, bool late = false, int startupRetries = 0)
    {
        List<Wire> records = [];
        ulong now = 1_000_000;
        byte[] anchor = new byte[60], request = new byte[60], reply = new byte[60];
        Convert.FromHexString("02000000000202000000000188B5").CopyTo(anchor, 0);
        "CLROS-M07"u8.CopyTo(anchor.AsSpan(14));
        Convert.FromHexString("FFFFFFFFFFFF020000000001080600010800060400010200000000010A00020F0000000000000A000202").CopyTo(request, 0);
        Convert.FromHexString("020000000001020000000002080600010800060400020200000000020A0002020200000000010A00020F").CopyTo(reply, 0);
        foreach (byte[] frame in new[]
        {
            anchor,
            request,
            reply,
            request,
            reply
        }

        )
            records.Add(new(now++, frame));
        uint[] client = [10001, 20001, 30001], server = [901, 1901, 0xfffffffc];
        void Add(bool outgoing, int role, uint sequence, uint ack, byte flags, byte[]? payload = null) => records.Add(new(now++, Tcp(outgoing, (ushort)(41000 + role), sequence, ack, flags, payload ?? [])));
        for (int role = 0; role < 3; role++)
        {
            if (role == 0)
                for (int retry = 0; retry < startupRetries; retry++)
                {
                    Add(false, role, client[role] - 1, 0, 2);
                    now += 500_000;
                }

            Add(false, role, client[role] - 1, 0, 2);
            Add(true, role, server[role] - 1, client[role], 0x12);
            Add(false, role, client[role], server[role], 0x10);
            Add(false, role, client[role], server[role], 0x18, [(byte)role]);
            client[role]++;
            Add(true, role, server[role], client[role], 0x10);
        }

        for (int round = 0; round < 15; round++)
        {
            now = 10_000_000 + (ulong)startupRetries * 500_000 + (ulong)round * 5_000_001;
            if (round <= 7)
                Add(true, 0, server[0], client[0], 0x18, Enumerable.Range(0, 1024).Select(i => (byte)(i % 251)).ToArray());
            if (round == 7)
            {
                byte[] vlan = new byte[1518];
                Convert.FromHexString("020000000001020000000002810000010800").CopyTo(vlan, 0);
                records.Add(new(now++, vlan));
            }

            for (int role = 1; role < 3; role++)
            {
                Add(false, role, client[role], server[role], 0x18, [(byte)round]);
                client[role]++;
            }

            for (int role = 1; role < 3; role++)
            {
                Add(true, role, server[role], client[role], 0x18, [(byte)(round ^ 0x80)]);
                server[role]++;
                Add(false, role, client[role], server[role], 0x10);
            }
        }

        if (fins && !late)
            for (int role = 1; role < 3; role++)
            {
                Add(true, role, server[role], client[role], 0x11);
                if (acknowledged)
                {
                    server[role]++;
                    Add(false, role, client[role], server[role], 0x10);
                }
            }

        for (int role = 1; role < 3; role++)
            Add(false, role, client[role], server[role], 0x14);
        if (fins && late)
            for (int role = 1; role < 3; role++)
                Add(true, role, server[role], client[role], 0x11);
        return new(records, OS.Std.Hash.Crc32.Compute(anchor), new(OS.Std.Hash.Crc32.Compute(reply), 60, OS.Std.Hash.Crc32.Compute(request), Peer));
    }

    private static byte[] Capture(IEnumerable<Wire> rows)
    {
        using MemoryStream bytes = new();
        bytes.Write(Convert.FromHexString("D4C3B2A10200040000000000000000000000010001000000"));
        using BinaryWriter writer = new(bytes);
        foreach (var row in rows)
        {
            writer.Write((uint)(row.Time / 1_000_000));
            writer.Write((uint)(row.Time % 1_000_000));
            writer.Write(row.Frame.Length);
            writer.Write(row.Frame.Length);
            writer.Write(row.Frame);
        }

        return bytes.ToArray();
    }

    private static byte[] Tcp(bool outgoing, ushort port, uint sequence, uint ack, byte flags, byte[] payload)
    {
        byte[] bytes = new byte[54 + payload.Length];
        Convert.FromHexString(outgoing ? "0200000000020200000000010800" : "0200000000010200000000020800").CopyTo(bytes, 0);
        Span<byte> ip = bytes.AsSpan(14), tcp = bytes.AsSpan(34);
        ip[0] = 0x45;
        ip[8] = 64;
        ip[9] = 6;
        BinaryPrimitives.WriteUInt16BigEndian(ip[2..], (ushort)(40 + payload.Length));
        BinaryPrimitives.WriteUInt32BigEndian(ip[12..], outgoing ? 0x0a00020fU : 0x0a000202U);
        BinaryPrimitives.WriteUInt32BigEndian(ip[16..], outgoing ? 0x0a000202U : 0x0a00020fU);
        BinaryPrimitives.WriteUInt16BigEndian(tcp, outgoing ? (ushort)8080 : port);
        BinaryPrimitives.WriteUInt16BigEndian(tcp[2..], outgoing ? port : (ushort)8080);
        BinaryPrimitives.WriteUInt32BigEndian(tcp[4..], sequence);
        BinaryPrimitives.WriteUInt32BigEndian(tcp[8..], ack);
        tcp[12] = 0x50;
        tcp[13] = flags;
        tcp[14] = tcp[15] = 255;
        payload.CopyTo(tcp[20..]);
        Seal(bytes);
        return bytes;
    }

    private static void Seal(byte[] bytes)
    {
        Span<byte> ip = bytes.AsSpan(14), tcp = bytes.AsSpan(34);
        ip[10] = ip[11] = tcp[16] = tcp[17] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(ip[10..], Checksum(ip[..20]));
        byte[] pseudo = new byte[12 + tcp.Length];
        ip.Slice(12, 8).CopyTo(pseudo);
        pseudo[9] = 6;
        BinaryPrimitives.WriteUInt16BigEndian(pseudo.AsSpan(10), (ushort)tcp.Length);
        tcp.CopyTo(pseudo.AsSpan(12));
        BinaryPrimitives.WriteUInt16BigEndian(tcp[16..], Checksum(pseudo));
    }

    private static ushort Checksum(ReadOnlySpan<byte> bytes)
    {
        uint sum = 0;
        for (int i = 0; i < bytes.Length; i += 2)
            sum += (uint)(bytes[i] << 8) + (i + 1 < bytes.Length ? bytes[i + 1] : 0U);
        while (sum > 65535)
            sum = (sum & 65535) + (sum >> 16);
        return (ushort)~sum;
    }
}
