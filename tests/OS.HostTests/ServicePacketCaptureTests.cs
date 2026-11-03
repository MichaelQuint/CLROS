using System.Buffers.Binary;
using OS.Tools.QemuRunner;

internal static partial class ServicePacketCaptureTests
{
    private static byte[] Header() => Convert.FromHexString("D4C3B2A10200040000000000000000000000010001000000");
    [HostTest("service packet capture waits for partial appends and returns owned globally ordered snapshots", HostLane.Fast, 0)]
    private static void Appends()
    {
        using var temp = new CaptureFile();
        using var capture = new ServicePacketCapture(temp.Path);
        byte[] header = Header(), first = Record(2, 3, 14, 0x11), second = Record(2, 3, 1514, 0x22);
        temp.Append(header.AsSpan(0, 7));
        HostCheck.Equal(0, capture.Poll().Length);
        temp.Append(header.AsSpan(7));
        HostCheck.Equal(0, capture.Poll().Length);
        temp.Append(first.AsSpan(0, 5));
        HostCheck.Equal(0, capture.Poll().Length);
        temp.Append(first.AsSpan(5, 15));
        HostCheck.Equal(0, capture.Poll().Length);
        temp.Append(first.AsSpan(20));
        var one = capture.Poll();
        HostCheck.Equal(1, one.Length);
        HostCheck.Equal(1L, one[0].Ordinal);
        HostCheck.Equal(2_000_003UL, one[0].Microseconds);
        HostCheck.True(one[0].Bytes.SequenceEqual(first.AsSpan(16).ToArray()));
        one[0].Bytes[0] = 0xff;
        HostCheck.Equal(0, capture.Poll().Length);
        temp.Append(second);
        var two = capture.Complete();
        HostCheck.Equal(1, two.Length);
        HostCheck.Equal(2L, two[0].Ordinal);
        HostCheck.True(two[0].Bytes.SequenceEqual(second.AsSpan(16).ToArray()));
        Reject(() => capture.Poll(), "service_capture_completed");
        Reject(() => capture.Complete(), "service_capture_completed");
    }

    [HostTest("service packet capture requires complete header and exact record EOF", HostLane.Fast, 1)]
    private static void Completion()
    {
        byte[] bytes = [.. Header(), .. Record(0, 0, 60, 1)];
        foreach (int length in new[]
        {
            0,
            1,
            23,
            25,
            39,
            40,
            53,
            bytes.Length - 1
        }

        )
        {
            using var temp = new CaptureFile();
            temp.Append(bytes.AsSpan(0, length));
            using var capture = new ServicePacketCapture(temp.Path);
            HostCheck.Equal(0, capture.Poll().Length);
            Reject(() => capture.Complete(), "service_capture_partial");
        }

        using var empty = new CaptureFile();
        empty.Append(Header());
        using var done = new ServicePacketCapture(empty.Path);
        HostCheck.Equal(0, done.Complete().Length);
    }

    [HostTest("service packet capture rejects changed headers truncation and recovery after failure", HostLane.Fast, 2)]
    private static void Mutation()
    {
        foreach (int field in new[]
        {
            0,
            4,
            6,
            8,
            12,
            16,
            20
        }

        )
        {
            using var temp = new CaptureFile();
            byte[] header = Header();
            temp.Append(header);
            using var capture = new ServicePacketCapture(temp.Path);
            _ = capture.Poll();
            temp.Rewrite(field, [(byte)(header[field] ^ 1)]);
            Reject(() => capture.Poll(), "service_capture_header_changed");
            temp.Rewrite(field, [header[field]]);
            Reject(() => capture.Poll(), "service_capture_failed");
        }

        foreach (int length in new[]
        {
            7,
            24,
            30,
            44,
            100
        }

        )
        {
            using var temp = new CaptureFile();
            temp.Append([.. Header(), .. Record(0, 0, 60, 1), 0]);
            using var capture = new ServicePacketCapture(temp.Path);
            _ = capture.Poll();
            temp.Truncate(length);
            Reject(() => capture.Poll(), "service_capture_truncated");
        }

        using var partial = new CaptureFile();
        partial.Append(Header().AsSpan(0, 10));
        using var shortCapture = new ServicePacketCapture(partial.Path);
        _ = shortCapture.Poll();
        partial.Truncate(9);
        Reject(() => shortCapture.Poll(), "service_capture_truncated");
    }

    [HostTest("service packet capture rejects invalid global record sizes and nonmonotonic timestamps", HostLane.Fast, 3)]
    private static void Malformed()
    {
        byte[] good = [.. Header(), .. Record(1, 999_999, 60, 1)];
        foreach (int field in new[]
        {
            0,
            4,
            6,
            8,
            12,
            16,
            20
        }

        )
        {
            byte[] changed = good.ToArray();
            changed[field] ^= 1;
            RejectBytes(changed, "service_capture_header");
        }

        foreach (var edit in new[]
        {
            (28, 1_000_000U),
            (32, 13U),
            (32, 1515U),
            (32, uint.MaxValue),
            (36, 59U)
        }

        )
        {
            byte[] changed = good.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(edit.Item1), edit.Item2);
            RejectBytes(changed, edit.Item1 == 28 ? "service_capture_timestamp" : "service_capture_record_extent");
        }

        RejectBytes([.. good, .. Record(1, 999_998, 14, 2)], "service_capture_timestamp");
        RejectBytes([.. good, .. Record(0, 999_999, 14, 2)], "service_capture_timestamp");
    }

    [HostTest("service packet capture bounds file records and disposes its exclusive reader", HostLane.Fast, 4)]
    private static void BoundsAndDisposal()
    {
        using var large = new CaptureFile();
        large.Truncate(4 * 1024 * 1024 + 1);
        using var capture = new ServicePacketCapture(large.Path);
        Reject(() => capture.Poll(), "service_capture_file_extent");
        using var many = new CaptureFile();
        many.Append(Header());
        byte[] record = Record(0, 0, 14, 1);
        for (int i = 0; i < 100_001; i++)
            many.Append(record, flush: false);
        many.Flush();
        using var records = new ServicePacketCapture(many.Path);
        Reject(() => records.Poll(), "service_capture_record_count");
        using var temp = new CaptureFile();
        var disposed = new ServicePacketCapture(temp.Path);
        disposed.Dispose();
        disposed.Dispose();
        bool rejected = false;
        try
        {
            _ = disposed.Poll();
        }
        catch (ObjectDisposedException)
        {
            rejected = true;
        }

        HostCheck.True(rejected);
    }

    private static byte[] Record(uint seconds, uint micros, int length, byte fill)
    {
        byte[] bytes = new byte[16 + length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, seconds);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), micros);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), (uint)length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), (uint)length);
        bytes.AsSpan(16).Fill(fill);
        return bytes;
    }

    private static void RejectBytes(byte[] bytes, string reason)
    {
        using var temp = new CaptureFile();
        temp.Append(bytes);
        using var capture = new ServicePacketCapture(temp.Path);
        Reject(() => capture.Poll(), reason);
    }

    private static void Reject(Action action, string reason)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (InvalidDataException error)
        {
            HostCheck.Equal(reason, error.Message);
            rejected = true;
        }

        HostCheck.True(rejected);
    }
}
