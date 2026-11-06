using System;
using System.Runtime.CompilerServices;
using OS.Std.Binary;
using OS.Std.Hash;

namespace OS.Storage;

/// <summary>Bounded 512-byte GPT metadata and 128 fixed-size entries. Input stays
/// unchanged; the caller keeps each span mapped and stable through return.
/// Results are mutable numeric metadata, not device or mapping authority.
/// Partition names and attributes remain opaque; no filesystem is interpreted.</summary>
public static unsafe class PrimaryGPTIndependentlyBoundsCRC
{
    public const int SectorBytes = 512, EntryCount = 128, EntrySize = 128;
    public const int ArrayBytes = EntryCount * EntrySize, ArraySectors = ArrayBytes / SectorBytes;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static BlockReadResult Read<T>(ref T device, ulong lba, Span<byte> destination)
        where T : struct, IReadOnlyBlockDevice => device.ReadBlocks(lba, 1, destination);

    public static LiveGptHeaderResult ValidateHeader(Span<byte> sector, ulong capacity,
        BlockReadResult read, bool backup = false)
    {
        LiveGptHeaderResult r = default;
        r.Status = LiveGptStatus.ReadFailed;
        if (!read.Succeeded || read.BlocksRead != 1 || read.BytesRead != SectorBytes)
            return r;
        r.Status = LiveGptStatus.InvalidLength;
        if (sector.Length != SectorBytes)
            return r;
        r.Status = LiveGptStatus.InvalidCapacity;
        if (capacity < 68 || capacity > ulong.MaxValue / SectorBytes)
            return r;
        fixed (byte* bytes = sector)
        {
            r.Status = LiveGptStatus.InvalidLength;
            if (bytes == null || (ulong)bytes > ulong.MaxValue - SectorBytes)
                return r;
            r.Status = LiveGptStatus.BadSignature;
            if (EndianPrimitives.ReadUInt64LittleEndian(bytes) != 0x5452415020494645UL)
                return r;
            r.Status = LiveGptStatus.UnsupportedRevision;
            if (EndianPrimitives.ReadUInt32LittleEndian(bytes + 8) != 0x00010000)
                return r;
            r.HeaderSize = EndianPrimitives.ReadUInt32LittleEndian(bytes + 12);
            r.Status = LiveGptStatus.InvalidHeaderSize;
            if (r.HeaderSize < 92 || r.HeaderSize > SectorBytes)
                return r;
            r.Status = LiveGptStatus.ReservedNonZero;
            if (EndianPrimitives.ReadUInt32LittleEndian(bytes + 20) != 0)
                return r;
            for (uint i = r.HeaderSize; i < SectorBytes; i++)
                if (bytes[i] != 0)
                    return r;
            r.Capacity = capacity;
            r.CurrentLba = EndianPrimitives.ReadUInt64LittleEndian(bytes + 24);
            r.AlternateLba = EndianPrimitives.ReadUInt64LittleEndian(bytes + 32);
            r.FirstUsableLba = EndianPrimitives.ReadUInt64LittleEndian(bytes + 40);
            r.LastUsableLba = EndianPrimitives.ReadUInt64LittleEndian(bytes + 48);
            r.DiskGuidLow = EndianPrimitives.ReadUInt64LittleEndian(bytes + 56);
            r.DiskGuidHigh = EndianPrimitives.ReadUInt64LittleEndian(bytes + 64);
            r.EntryLba = EndianPrimitives.ReadUInt64LittleEndian(bytes + 72);
            r.ExpectedHeaderCrc = EndianPrimitives.ReadUInt32LittleEndian(bytes + 16);
            r.ArrayCrc = EndianPrimitives.ReadUInt32LittleEndian(bytes + 88);
            r.Status = LiveGptStatus.InvalidHeaderPosition;
            if (r.CurrentLba != (backup ? capacity - 1 : 1UL))
                return r;
            r.Status = LiveGptStatus.UnsupportedArrayLayout;
            if (EndianPrimitives.ReadUInt32LittleEndian(bytes + 80) != EntryCount ||
                EndianPrimitives.ReadUInt32LittleEndian(bytes + 84) != EntrySize)
                return r;
            r.Status = Geometry(r);
            if (r.Status != LiveGptStatus.Success)
                return r;
            uint crc = Crc32.InitialState;
            for (uint i = 0; i < r.HeaderSize; i++)
                crc = Crc32.UpdateByte(crc, i >= 16 && i < 20 ? (byte)0 : bytes[i]);
            r.ComputedHeaderCrc = Crc32.Finish(crc);
            r.Status = r.ComputedHeaderCrc == r.ExpectedHeaderCrc ? LiveGptStatus.Success : LiveGptStatus.HeaderCrcMismatch;
            return r;
        }
    }

    // Recheck mutable header geometry before trusting its usable range. The
    // computed CRC remains available after CRC/entry failures for diagnostics.
    public static LiveGptStatus ValidateArray(LiveGptHeaderResult header, Span<byte> entries, out uint computedCrc)
    {
        computedCrc = 0;
        if (!header.Succeeded || Geometry(header) != LiveGptStatus.Success ||
            header.ExpectedHeaderCrc != header.ComputedHeaderCrc)
            return LiveGptStatus.InvalidHeader;
        if (entries.Length != ArrayBytes)
            return LiveGptStatus.InvalidLength;
        fixed (byte* bytes = entries)
        {
            if (bytes == null || (ulong)bytes > ulong.MaxValue - ArrayBytes)
                return LiveGptStatus.InvalidLength;
            uint crc = Crc32.InitialState;
            for (int i = 0; i < ArrayBytes; i++)
                crc = Crc32.UpdateByte(crc, bytes[i]);
            computedCrc = Crc32.Finish(crc);
            if (computedCrc != header.ArrayCrc)
                return LiveGptStatus.ArrayCrcMismatch;
            for (int i = 0; i < EntryCount; i++)
            {
                byte* entry = bytes + i * EntrySize;
                if ((EndianPrimitives.ReadUInt64LittleEndian(entry) | EndianPrimitives.ReadUInt64LittleEndian(entry + 8)) == 0)
                {
                    for (int n = 16; n < EntrySize; n++)
                        if (entry[n] != 0)
                            return LiveGptStatus.InvalidUnusedEntry;
                    continue;
                }
                ulong low = EndianPrimitives.ReadUInt64LittleEndian(entry + 16), high = EndianPrimitives.ReadUInt64LittleEndian(entry + 24);
                if ((low | high) == 0)
                    return LiveGptStatus.InvalidPartitionGuid;
                ulong first = EndianPrimitives.ReadUInt64LittleEndian(entry + 32), last = EndianPrimitives.ReadUInt64LittleEndian(entry + 40);
                if (first < header.FirstUsableLba || last > header.LastUsableLba || first > last)
                    return LiveGptStatus.InvalidPartitionExtent;
                for (int j = 0; j < i; j++)
                {
                    byte* previous = bytes + j * EntrySize;
                    if ((EndianPrimitives.ReadUInt64LittleEndian(previous) | EndianPrimitives.ReadUInt64LittleEndian(previous + 8)) == 0)
                        continue;
                    if (low == EndianPrimitives.ReadUInt64LittleEndian(previous + 16) && high == EndianPrimitives.ReadUInt64LittleEndian(previous + 24))
                        return LiveGptStatus.DuplicatePartitionGuid;
                    if (first <= EndianPrimitives.ReadUInt64LittleEndian(previous + 40) && EndianPrimitives.ReadUInt64LittleEndian(previous + 32) <= last)
                        return LiveGptStatus.OverlappingPartitions;
                }
            }
            return LiveGptStatus.Success;
        }
    }

    private static LiveGptStatus Geometry(LiveGptHeaderResult h)
    {
        if (h.Capacity < 68 || h.Capacity > ulong.MaxValue / SectorBytes)
            return LiveGptStatus.InvalidCapacity;
        if (h.HeaderSize < 92 || h.HeaderSize > SectorBytes)
            return LiveGptStatus.InvalidHeaderSize;
        bool primary = h.CurrentLba == 1;
        if ((primary && h.AlternateLba != h.Capacity - 1) ||
            (!primary && (h.CurrentLba != h.Capacity - 1 || h.AlternateLba != 1)))
            return LiveGptStatus.InvalidHeaderPosition;
        if (h.FirstUsableLba < 34 || h.LastUsableLba > h.Capacity - 34 || h.FirstUsableLba > h.LastUsableLba)
            return LiveGptStatus.InvalidUsableRange;
        if ((h.DiskGuidLow | h.DiskGuidHigh) == 0)
            return LiveGptStatus.InvalidDiskGuid;
        if (primary ? h.EntryLba < 2 || h.EntryLba > h.FirstUsableLba - ArraySectors :
            h.EntryLba <= h.LastUsableLba || h.EntryLba > h.CurrentLba - ArraySectors)
            return LiveGptStatus.InvalidArrayPlacement;
        return LiveGptStatus.Success;
    }
}
