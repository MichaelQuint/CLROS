using System.Buffers.Binary;
using OS.Std.Hash;

namespace OS.Tooling;

public readonly record struct QemuEthernetCaptureReceipt(uint Records, uint Matches, uint FrameCrc32);

/// <summary>Bounded classic little-endian libpcap evidence from QEMU filter-dump.
/// One complete experimental Ethernet frame is required; unrelated traffic is allowed.</summary>
public static class QemuEthernetCapture
{
    private const int MaximumBytes = 4 * 1024 * 1024;

    public static QemuEthernetCaptureReceipt Assert(string path, uint expectedFrameCrc)
    {
        using var file = File.OpenRead(path);
        long length = file.Length;
        Need(length <= MaximumBytes, "file_extent");
        byte[] bytes = new byte[(int)length];
        try { file.ReadExactly(bytes); }
        catch (EndOfStreamException error) { throw new InvalidDataException("m07_capture_file_truncated", error); }
        Need(file.ReadByte() == -1, "file_changed");
        return Inspect(bytes, expectedFrameCrc);
    }

    public static QemuEthernetCaptureReceipt Inspect(ReadOnlySpan<byte> bytes, uint expectedFrameCrc)
    {
        Need(bytes.Length <= MaximumBytes, "file_extent");
        Need(bytes.Length >= 24, "header_truncated");
        Need(BinaryPrimitives.ReadUInt32LittleEndian(bytes) == 0xa1b2c3d4 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]) == 2 &&
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]) == 4 &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]) == 0 &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..]) == 0 &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]) == 65536 &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[20..]) == 1, "header");
        uint records = 0, matches = 0, frameCrc = 0;
        int offset = 24;
        while (offset < bytes.Length)
        {
            Need(bytes.Length - offset >= 16, "record_truncated");
            ReadOnlySpan<byte> header = bytes.Slice(offset, 16);
            uint microseconds = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            uint included = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
            uint original = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
            Need(microseconds < 1000000, "record_timestamp");
            Need(included > 0 && included <= 65536 && original == included, "record_length");
            offset += 16;
            Need(included <= (uint)(bytes.Length - offset), "record_payload_truncated");
            ReadOnlySpan<byte> frame = bytes.Slice(offset, (int)included);
            records++;
            if (IsFrame(frame)) { matches++; frameCrc = Crc32.Compute(frame); }
            offset += (int)included;
        }
        Need(matches == 1, "frame_count");
        Need(frameCrc == expectedFrameCrc, "frame_crc");
        return new(records, matches, frameCrc);
    }

    private static bool IsFrame(ReadOnlySpan<byte> frame)
    {
        if (frame.Length != 60) return false;
        ReadOnlySpan<byte> ethernet = [2, 0, 0, 0, 0, 2, 2, 0, 0, 0, 0, 1, 0x88, 0xb5];
        ReadOnlySpan<byte> tag = "CLROS-M07"u8;
        if (!frame[..14].SequenceEqual(ethernet) || !frame.Slice(14, tag.Length).SequenceEqual(tag)) return false;
        for (int i = 14 + tag.Length; i < frame.Length; i++) if (frame[i] != 0) return false;
        return true;
    }

    private static void Need(bool condition, string reason)
    { if (!condition) throw new InvalidDataException("m07_capture_" + reason); }
}
