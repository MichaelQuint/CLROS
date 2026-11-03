using System.Buffers.Binary;
using System.Text;
using OS.Tooling;

internal static class QemuEthernetCaptureTests
{
    [HostTest("qemu.ARP capture associates one ordered request reply after the owned parent frame", HostLane.Fast, 4)]
    private static void Arp()
    {
        byte[] request = EthernetFrameItExistingClassifierTests.Request.ToArray();
        byte[] reply = EthernetFrameItExistingClassifierTests.Reply().AsSpan(12, 42).ToArray();
        var expected = new QemuArpReceiveReceipt(ReferenceCrc(reply), 42, ReferenceCrc(request), 0x52550a000202);
        var r = QemuEthernetCapture.InspectArp(Capture(request, reply, Frame(), request, reply), expected);
        HostCheck.True(r.Records == 5 && r.Requests == 1 && r.Replies == 1 &&
            r.PeerMac == expected.PeerMac && r.RequestCrc32 == expected.RequestCrc32 && r.ReplyCrc32 == expected.ReceiveCrc32);
        void Bad(byte[] capture, QemuArpReceiveReceipt receipt)
        {
            bool rejected = false;
            try
            {
                _ = QemuEthernetCapture.InspectArp(capture, receipt);
            }
            catch (InvalidDataException) { rejected = true; }
            HostCheck.True(rejected);
        }
        Bad(Capture(Frame(), reply, request), expected);
        Bad(Capture(Frame(), request), expected);
        Bad(Capture(Frame(), request, request, reply), expected);
        Bad(Capture(Frame(), request, reply, reply), expected);
        Bad(Capture(Frame(), request, reply), expected with
        {
            RequestCrc32 = expected.RequestCrc32 ^ 1
        });
        Bad(Capture(Frame(), request, reply), expected with
        {
            ReceiveCrc32 = expected.ReceiveCrc32 ^ 1
        });
        byte[] padded = new byte[64];
        reply.CopyTo(padded, 0);
        var paddedExpected = expected with
        {
            ReceiveBytes = 64,
            ReceiveCrc32 = ReferenceCrc(padded)
        };
        HostCheck.True(QemuEthernetCapture.InspectArp(Capture(Frame(), request, padded), paddedExpected).Replies == 1);
        for (int i = 42; i < padded.Length; i++)
        {
            byte[] changed = (byte[])padded.Clone();
            changed[i] = 1;
            Bad(Capture(Frame(), request, changed), paddedExpected with
            {
                ReceiveCrc32 = ReferenceCrc(changed)
            });
        }
        byte[] oversized = new byte[65];
        reply.CopyTo(oversized, 0);
        Bad(Capture(Frame(), request, oversized), expected with
        {
            ReceiveBytes = 65,
            ReceiveCrc32 = ReferenceCrc(oversized)
        });
        for (int i = 0; i < reply.Length; i++)
        {
            byte[] changed = (byte[])reply.Clone();
            changed[i] ^= 1;
            Bad(Capture(Frame(), request, changed), expected);
        }
        for (int i = 0; i < request.Length; i++)
        {
            byte[] changed = (byte[])request.Clone();
            changed[i] ^= 1;
            Bad(Capture(Frame(), changed, reply), expected);
        }
    }
    [HostTest("qemu.ethernet.capture requires one complete independent frame among unrelated traffic", HostLane.Fast, 0)]
    private static void Valid()
    {
        byte[] frame = Frame(), unrelated = new byte[60];
        unrelated.AsSpan(0, 6).Fill(0xff);
        unrelated[12] = 8;
        unrelated[13] = 6;
        uint crc = ReferenceCrc(frame);
        HostCheck.Equal(0xd24c49ddU, crc);
        byte[] bytes = Capture(unrelated, frame, unrelated);
        var r = QemuEthernetCapture.Inspect(bytes, crc);
        HostCheck.Equal(3U, r.Records);
        HostCheck.Equal(1U, r.Matches);
        HostCheck.Equal(crc, r.FrameCrc32);
        string directory = Path.Combine(RepoRoot(), "artifacts", "host-tests", "qemu-ethernet-capture", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "wire.pcap");
        try
        {
            File.WriteAllBytes(path, bytes);
            HostCheck.Equal(r, QemuEthernetCapture.Assert(path, crc));
            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
                file.SetLength(4 * 1024 * 1024 + 1);
            RejectFile(path, crc, "m07_capture_file_extent");
        }
        finally { File.Delete(path); Directory.Delete(directory); }
    }

    [HostTest("qemu.ethernet.capture rejects malformed global and record headers and every truncation", HostLane.Fast, 1)]
    private static void Malformed()
    {
        byte[] frame = Frame(), valid = Capture(frame);
        uint crc = ReferenceCrc(frame);
        foreach (int field in new[] { 0, 4, 6, 8, 12, 16, 20 })
        {
            byte[] changed = (byte[])valid.Clone();
            changed[field] ^= 1;
            Reject(changed, crc, "m07_capture_header");
        }
        byte[] bigEndian = (byte[])valid.Clone();
        bigEndian[0] = 0xa1;
        bigEndian[1] = 0xb2;
        bigEndian[2] = 0xc3;
        bigEndian[3] = 0xd4;
        Reject(bigEndian, crc, "m07_capture_header");
        byte[] nanoseconds = (byte[])valid.Clone();
        nanoseconds[0] = 0x4d;
        nanoseconds[1] = 0x3c;
        Reject(nanoseconds, crc, "m07_capture_header");
        foreach ((int offset, uint value, string reason) in new[]
        {
            (28, 1000000U, "record_timestamp"), (32, 0U, "record_length"),
            (32, 65537U, "record_length"), (36, 59U, "record_length")
        })
        {
            byte[] changed = (byte[])valid.Clone();
            BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(offset), value);
            Reject(changed, crc, "m07_capture_" + reason);
        }
        for (int length = 0; length < valid.Length; length++)
        {
            string reason = length < 24 ? "header_truncated" : length == 24 ? "frame_count" :
                length < 40 ? "record_truncated" : "record_payload_truncated";
            Reject(valid[..length], crc, "m07_capture_" + reason);
        }
        Reject([.. valid, 0], crc, "m07_capture_record_truncated");
        Reject(new byte[4 * 1024 * 1024 + 1], crc, "m07_capture_file_extent");
    }

    [HostTest("qemu.ethernet.capture checks every frame byte rejects duplicates missing frames and wrong CRC", HostLane.Fast, 2)]
    private static void Identity()
    {
        byte[] frame = Frame();
        uint crc = ReferenceCrc(frame);
        for (int i = 0; i < frame.Length; i++)
        {
            byte[] changed = (byte[])frame.Clone();
            changed[i] ^= 1;
            Reject(Capture(changed), crc, "m07_capture_frame_count");
        }
        Reject(Capture(frame, frame), crc, "m07_capture_frame_count");
        Reject(Capture(new byte[60]), crc, "m07_capture_frame_count");
        Reject(Capture(new byte[59]), crc, "m07_capture_frame_count");
        Reject(Capture((byte[])[.. frame, 0]), crc, "m07_capture_frame_count");
        Reject(Capture(frame), crc ^ 1, "m07_capture_frame_crc");
    }

    private static byte[] Frame()
    {
        byte[] frame = new byte[60];
        Convert.FromHexString("02000000000202000000000188B5").CopyTo(frame, 0);
        Encoding.ASCII.GetBytes("CLROS-M07").CopyTo(frame, 14);
        return frame;
    }
    private static byte[] Capture(params byte[][] frames)
    {
        using var stream = new MemoryStream();
        // Independent classic-pcap global header, little endian, microseconds,
        // version 2.4, zero zone/sigfigs, snap length 65536, Ethernet link type 1.
        stream.Write(Convert.FromHexString("D4C3B2A10200040000000000000000000000010001000000"));
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        foreach (byte[] frame in frames)
        {
            writer.Write(uint.MaxValue);
            writer.Write(999999U);
            writer.Write((uint)frame.Length);
            writer.Write((uint)frame.Length);
            writer.Write(frame);
        }
        writer.Flush();
        return stream.ToArray();
    }
    private static uint ReferenceCrc(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320U : 0);
        }
        return ~crc;
    }
    private static void Reject(byte[] bytes, uint crc, string reason)
    {
        try
        {
            _ = QemuEthernetCapture.Inspect(bytes, crc);
        }
        catch (InvalidDataException error) { HostCheck.Equal(reason, error.Message); return; }
        throw new InvalidOperationException("capture_accepts_invalid " + reason);
    }
    private static void RejectFile(string path, uint crc, string reason)
    {
        try
        {
            _ = QemuEthernetCapture.Assert(path, crc);
        }
        catch (InvalidDataException error) { HostCheck.Equal(reason, error.Message); return; }
        throw new InvalidOperationException("capture_file_accepts_invalid " + reason);
    }
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CLROS.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("capture_test_repo_root");
    }
}
