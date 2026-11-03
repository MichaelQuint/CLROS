using System.Buffers.Binary;
using OS.Tooling;

internal static partial class NativeAotServiceWireRangeTests
{
    private static Vector Packets(bool wrap = false, bool changedRetry = false, bool clientReset = false)
    {
        var original = NativeAotSocketIntegrationTests.Packets();
        var frames = original.Frames.Take(8).Select(frame => frame.ToArray()).ToList();
        const ushort port = 43000;
        uint client = wrap ? uint.MaxValue - 2 : 1001, server = wrap ? uint.MaxValue - 3 : 2001;
        void Add(bool outgoing, uint sequence, uint ack, byte flags, ReadOnlySpan<byte> payload = default) => frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing, port, sequence, ack, flags, payload));
        Add(false, unchecked(client - 1), 0, 2);
        Add(true, unchecked(server - 1), client, 0x12);
        Add(false, client, server, 0x10);
        Add(false, client, server, 0x18, "ab"u8);
        Add(false, unchecked(client + 4), server, 0x18, "ef"u8);
        Add(false, client, server, 0x18, "abcdef"u8);
        Add(true, server, unchecked(client + 6), 0x10);
        Add(true, unchecked(server + 1), unchecked(client + 6), 0x18, "YZ"u8);
        Add(true, server, unchecked(client + 6), 0x18, "XYZ"u8);
        Add(true, server, unchecked(client + 6), 0x18, changedRetry ? "XYx"u8 : "XYZ"u8);
        Add(true, unchecked(server + 2), unchecked(client + 6), 0x18, "Z123"u8);
        client = unchecked(client + 6);
        server = unchecked(server + 6);
        Add(false, client, server, 0x10);
        if (clientReset)
            Add(false, client, server, 0x14);
        else
        {
            Add(true, server, client, 0x11);
            server++;
            Add(false, client, server, 0x10);
            Add(false, client, server, 0x11);
            client++;
            Add(true, server, client, 0x10);
        }

        return new(frames, original.Anchor, original.Arp, port);
    }

    private static byte[] Capture(Vector vector)
    {
        byte[] raw = NativeAotSocketIntegrationTests.Capture(vector.Frames);
        int offset = 24, record = 0;
        while (offset < raw.Length)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(offset), (uint)(101 + record / 8));
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(offset + 4), (uint)(record % 8 * 99));
            offset += 16 + checked((int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(offset + 8)));
            record++;
        }

        return raw;
    }

    private static IReadOnlyList<QemuTranscripts.ServiceWireByteRange> Read(Vector vector, byte[]? raw = null, bool lifecycle = false) => QemuTranscripts.InspectNativeAotServiceWireRangesForTests(raw ?? Capture(vector), vector.Anchor, vector.Arp, [vector.Port], lifecycle);
    [HostTest("service wire ranges bind unique overlapping and out-of-order bytes to first original record and PCAP units")]
    private static void FirstAppearances()
    {
        foreach (bool wrap in new[]
        {
            false,
            true
        }

        )
        {
            var vector = Packets(wrap);
            var ranges = Read(vector);
            HostCheck.Equal(6, ranges.Count);
            (bool Outgoing, int Offset, int Count, int Record)[] expected = [(false, 0, 2, 12), (false, 4, 2, 13), (false, 2, 2, 14), (true, 1, 2, 16), (true, 0, 1, 17), (true, 3, 3, 19)];
            for (int i = 0; i < expected.Length; i++)
            {
                var actual = ranges[i];
                var wanted = expected[i];
                HostCheck.True(actual.RemotePort == vector.Port && actual.ServerToClient == wanted.Outgoing && actual.StreamOffset == wanted.Offset && actual.ByteCount == wanted.Count && actual.Record == wanted.Record);
                uint seconds = (uint)(101 + (wanted.Record - 1) / 8), microseconds = (uint)((wanted.Record - 1) % 8 * 99);
                HostCheck.True(actual.Seconds == seconds && actual.Microseconds == microseconds && actual.TimestampMicroseconds == (ulong)seconds * 1_000_000 + microseconds);
            }
        }
    }

    [HostTest("service wire ranges return immutable copied values without claiming close or exchange authority")]
    private static void CopiesAndExistingPolicy()
    {
        var vector = Packets(clientReset: true);
        byte[] raw = Capture(vector), before = raw.ToArray();
        Reject(() => Read(vector, raw), "service_wire_established");
        var ranges = Read(vector, raw, lifecycle: true);
        HostCheck.Equal(6, ranges.Count);
        HostCheck.True(raw.AsSpan().SequenceEqual(before));
        Array.Clear(raw);
        vector.Frames.Clear();
        HostCheck.Equal(12, ranges[0].Record);
        HostCheck.Equal(43000U, ranges[0].RemotePort);
        bool immutable = false;
        try
        {
            ((IList<QemuTranscripts.ServiceWireByteRange>)ranges)[0] = default;
        }
        catch (NotSupportedException)
        {
            immutable = true;
        }

        HostCheck.True(immutable);
    }

    [HostTest("service wire range observation preserves changed retransmission and original timestamp rejection")]
    private static void Substitutions()
    {
        Reject(() => Read(Packets(changedRetry: true)), "service_wire_retransmission");
        var vector = Packets();
        byte[] raw = Capture(vector);
        int offset = 24;
        for (int record = 1; record < 16; record++)
            offset += 16 + checked((int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(offset + 8)));
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(offset), 100);
        Reject(() => Read(vector, raw), "service_wire_clock");
        raw = Capture(vector);
        BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(28), 1_000_000);
        bool refused = false;
        try
        {
            _ = Read(vector, raw);
        }
        catch (InvalidDataException)
        {
            refused = true;
        }

        HostCheck.True(refused);
    }

    private static void Reject(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (ToolFailure error)
        {
            HostCheck.Equal(reason, error.Reason);
            return;
        }

        throw new InvalidOperationException("expected rejection " + reason);
    }
}
