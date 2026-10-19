using System.Buffers.Binary;

namespace OS.Tools.QemuRunner;

/// <summary>Serialized, synchronous reader of one existing original QEMU PCAP.
/// Owns a read-only shared file handle until disposal; the producer may append.
/// Poll waits for incomplete headers/bodies without advancing their record.
/// Framing failure is terminal. Completion requires the producer to have stopped
/// and seals exact EOF; this reader establishes no TCP, HTTP or guest authority.</summary>
public sealed class ServicePacketCapture : IDisposable
{
    /// <summary>One complete original Ethernet record. Ordinals start at one;
    /// timestamps are microseconds since the PCAP epoch. The caller exclusively
    /// owns Bytes and the returned observation array.</summary>
    public readonly record struct FrameObservation(long Ordinal, ulong Microseconds, byte[] Bytes);

    private readonly FileStream file;
    private readonly byte[] header = new byte[24];
    private int headerBytes;
    private long cursor, maximumFileLength, ordinal;
    private ulong lastMicroseconds;
    private bool headerSeen, failed, completed, disposed;

    /// <summary>Open an existing capture without write/delete access. Missing or
    /// inaccessible files fail immediately; callers own creation/readiness timing.
    /// No buffered reads are retained across producer appends or header checks.</summary>
    public ServicePacketCapture(string path) => file = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 1);

    /// <summary>Return newly complete records once, in global file order. Partial
    /// input returns no observation for that record. Truncation, changed global
    /// header, malformed metadata or exhausted bounds permanently reject reuse.</summary>
    public FrameObservation[] Poll()
    {
        RequireUsable();
        try
        {
            long length = ObserveLength();
            int available = (int)Math.Min(24, length);
            Span<byte> current = stackalloc byte[24]; ReadAt(0, current[..available]);
            Require(current[..headerBytes].SequenceEqual(header.AsSpan(0, headerBytes)), "service_capture_header_changed");
            current[..available].CopyTo(header); headerBytes = available;
            if (!headerSeen)
            {
                if (available < 24) return [];
                Require(BinaryPrimitives.ReadUInt32LittleEndian(header) == 0xa1b2c3d4 &&
                    BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4)) == 2 &&
                    BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6)) == 4 &&
                    BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)) == 0 &&
                    BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12)) == 0 &&
                    BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(16)) == 65536 &&
                    BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(20)) == 1, "service_capture_header");
                headerSeen = true; cursor = 24;
            }
            List<FrameObservation> frames = [];
            Span<byte> record = stackalloc byte[16];
            while (length - cursor >= 16)
            {
                ReadAt(cursor, record);
                uint seconds = BinaryPrimitives.ReadUInt32LittleEndian(record), micros = BinaryPrimitives.ReadUInt32LittleEndian(record[4..]),
                    included = BinaryPrimitives.ReadUInt32LittleEndian(record[8..]), original = BinaryPrimitives.ReadUInt32LittleEndian(record[12..]);
                ulong timestamp = (ulong)seconds * 1_000_000 + micros;
                Require(micros < 1_000_000 && timestamp >= lastMicroseconds, "service_capture_timestamp");
                Require(included is >= 14 and <= 1514 && included == original, "service_capture_record_extent");
                if (length - cursor - 16 < included) break;
                Require(ordinal < 100_000, "service_capture_record_count");
                byte[] bytes = new byte[(int)included]; ReadAt(cursor + 16, bytes);
                frames.Add(new(++ordinal, timestamp, bytes)); lastMicroseconds = timestamp; cursor += 16 + included;
            }
            _ = ObserveLength(); return frames.ToArray();
        }
        catch { failed = true; throw; }
    }

    /// <summary>Poll once for final observations, then require a complete global
    /// header and exact record EOF. Success seals further reads. This is a local
    /// framing boundary; the caller must separately join the stopped producer.</summary>
    public FrameObservation[] Complete()
    {
        FrameObservation[] frames = Poll();
        try { Require(headerSeen && ObserveLength() == cursor, "service_capture_partial"); completed = true; return frames; }
        catch { failed = true; throw; }
    }

    private long ObserveLength()
    {
        long length = file.Length;
        Require(length >= maximumFileLength, "service_capture_truncated");
        Require(length <= 4 * 1024 * 1024, "service_capture_file_extent");
        maximumFileLength = length; return length;
    }
    private void ReadAt(long offset, Span<byte> bytes)
    {
        file.Position = offset;
        try { file.ReadExactly(bytes); }
        catch (EndOfStreamException) { throw new InvalidDataException("service_capture_truncated"); }
    }
    private void RequireUsable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Require(!failed, "service_capture_failed"); Require(!completed, "service_capture_completed");
    }
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidDataException(reason); }
    /// <summary>Release only this reader, idempotently. Does not stop the producer
    /// or imply that pending PCAP bytes form a completed capture.</summary>
    public void Dispose() { if (disposed) return; disposed = true; file.Dispose(); }
}
