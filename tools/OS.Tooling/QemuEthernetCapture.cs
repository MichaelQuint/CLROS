using System.Buffers.Binary;
using OS.Std.Hash;

namespace OS.Tooling;
/// <summary>Bounded classic little-endian libpcap evidence from QEMU filter-dump.
/// One complete experimental Ethernet frame is required; unrelated traffic is allowed.</summary>

public static class QemuEthernetCapture
{
    private const int MaximumBytes = 4 * 1024 * 1024;
    public static QemuEthernetCaptureReceipt Assert(string path, uint expectedFrameCrc) => Inspect(ReadCapture(path), expectedFrameCrc);
    public static QemuArpCaptureReceipt AssertArp(string path, QemuArpReceiveReceipt expected) => InspectArp(ReadCapture(path), expected);
    private static byte[] ReadCapture(string path)
    {
        using var file = File.OpenRead(path);
        long length = file.Length;
        Need(length <= MaximumBytes, "file_extent");
        byte[] bytes = new byte[(int)length];
        try
        {
            file.ReadExactly(bytes);
        }
        catch (EndOfStreamException error)
        {
            throw new InvalidDataException("m07_capture_file_truncated", error);
        }

        Need(file.ReadByte() == -1, "file_changed");
        return bytes;
    }

    public static QemuEthernetCaptureReceipt Inspect(ReadOnlySpan<byte> bytes, uint expectedFrameCrc, bool diskStaticFull = false) => InspectCore(bytes, expectedFrameCrc, null, diskStaticFull).Frame;
    public static QemuArpCaptureReceipt InspectArp(ReadOnlySpan<byte> bytes, QemuArpReceiveReceipt expected) => InspectCore(bytes, 0xd24c49dd, expected).Arp;
    private static (QemuEthernetCaptureReceipt Frame, QemuArpCaptureReceipt Arp) InspectCore(ReadOnlySpan<byte> bytes, uint expectedFrameCrc, QemuArpReceiveReceipt? arpExpected, bool diskStaticFull = false)
    {
        Need(bytes.Length <= (diskStaticFull ? DiskStaticCapturePolicy.PacketBytes : MaximumBytes), "file_extent");
        Need(bytes.Length >= 24, "header_truncated");
        Need(BinaryPrimitives.ReadUInt32LittleEndian(bytes) == 0xa1b2c3d4 && BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]) == 2 && BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]) == 4 && BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]) == 0 && BinaryPrimitives.ReadUInt32LittleEndian(bytes[12..]) == 0 && BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]) == 65536 && BinaryPrimitives.ReadUInt32LittleEndian(bytes[20..]) == 1, "header");
        uint records = 0, matches = 0, frameCrc = 0;
        uint requests = 0, replies = 0, requestCrc = 0, replyCrc = 0;
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
            if (IsFrame(frame))
            {
                matches++;
                frameCrc = Crc32.Compute(frame);
            }

            // Firmware traffic before the independently checked parent frame
            // is outside the owned request/reply transaction.
            if (arpExpected is { } expected && matches == 1)
            {
                if (IsArp(frame, false, expected))
                {
                    Need(replies == 0, "arp_request_order");
                    requests++;
                    requestCrc = Crc32.Compute(frame);
                }
                else if (IsArp(frame, true, expected))
                {
                    Need(requests == 1, "arp_reply_order");
                    replies++;
                    replyCrc = Crc32.Compute(frame);
                }
            }

            offset += (int)included;
        }

        Need(matches == 1, "frame_count");
        Need(frameCrc == expectedFrameCrc, "frame_crc");
        if (arpExpected is { } wanted)
            Need(requests == 1 && replies == 1 && requestCrc == wanted.RequestCrc32 && replyCrc == wanted.ReceiveCrc32, "arp_association");
        return (new(records, matches, frameCrc), new(records, requests, replies, requestCrc, replyCrc, arpExpected?.PeerMac ?? 0));
    }

    private static bool IsArp(ReadOnlySpan<byte> frame, bool reply, QemuArpReceiveReceipt expected)
    {
        if (frame.Length != (reply ? expected.ReceiveBytes : 60) || frame.Length < 42 || frame.Length > 64)
            return false;
        ReadOnlySpan<byte> localMac = [2, 0, 0, 0, 0, 1];
        Span<byte> peerMac = stackalloc byte[6];
        for (int i = 0; i < 6; i++)
            peerMac[i] = (byte)(expected.PeerMac >> ((5 - i) * 8));
        ReadOnlySpan<byte> broadcast = [255, 255, 255, 255, 255, 255];
        ReadOnlySpan<byte> zeroMac = [0, 0, 0, 0, 0, 0];
        ReadOnlySpan<byte> localIp = [10, 0, 2, 15], peerIp = [10, 0, 2, 2];
        ReadOnlySpan<byte> arpHeader = [8, 6, 0, 1, 8, 0, 6, 4];
        if (!frame[..6].SequenceEqual(reply ? localMac : broadcast) || !frame.Slice(6, 6).SequenceEqual(reply ? peerMac : localMac) || !frame.Slice(12, 8).SequenceEqual(arpHeader) || frame[20] != 0 || frame[21] != (reply ? 2 : 1) || !frame.Slice(22, 6).SequenceEqual(reply ? peerMac : localMac) || !frame.Slice(28, 4).SequenceEqual(reply ? peerIp : localIp) || !frame.Slice(32, 6).SequenceEqual(reply ? localMac : zeroMac) || !frame.Slice(38, 4).SequenceEqual(reply ? localIp : peerIp))
            return false;
        for (int i = 42; i < frame.Length; i++)
            if (frame[i] != 0)
                return false;
        return true;
    }

    private static bool IsFrame(ReadOnlySpan<byte> frame)
    {
        if (frame.Length != 60)
            return false;
        ReadOnlySpan<byte> ethernet = [2, 0, 0, 0, 0, 2, 2, 0, 0, 0, 0, 1, 0x88, 0xb5];
        ReadOnlySpan<byte> tag = "CLROS-M07"u8;
        if (!frame[..14].SequenceEqual(ethernet) || !frame.Slice(14, tag.Length).SequenceEqual(tag))
            return false;
        for (int i = 14 + tag.Length; i < frame.Length; i++)
            if (frame[i] != 0)
                return false;
        return true;
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidDataException("m07_capture_" + reason);
    }
}
