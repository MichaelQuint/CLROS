using System;
using OS.Std.Binary;

namespace OS.Storage;

/// <summary>Numeric FAT32 geometry and chain checks over stable caller-owned
/// bytes. No I/O, allocation, FSInfo interpretation or FAT-copy equality proof.
/// Ref outputs and scratch remain unchanged on failure. The caller authenticates
/// the partition and read LBA; a completed receipt alone does not identify bytes.</summary>
public static unsafe class FATClusterChainsLoopBadClusterTruncationDetection
{
    public const uint SectorBytes = 512, MaximumHistoryClusters = 4096;
    // Keep every data cluster below the FAT32 reserved-value range.
    public const uint MinimumClusters = 65525, MaximumClusters = 0x0fffffee;

    public static LiveFat32Status ParseBpb(Span<byte> sector, ulong capacity,
        ulong partitionFirst, ulong partitionLast, BlockReadResult read, ref LiveFat32Volume volume)
    {
        if (!Complete(read))
            return LiveFat32Status.ReadFailed;
        if (sector.Length != SectorBytes)
            return LiveFat32Status.InvalidLength;
        if (!Partition(capacity, partitionFirst, partitionLast))
            return LiveFat32Status.InvalidPartition;
        fixed (byte* bytes = sector)
        {
            if (bytes == null || (ulong)bytes > ulong.MaxValue - SectorBytes)
                return LiveFat32Status.InvalidLength;
            if (bytes[510] != 0x55 || bytes[511] != 0xaa)
                return LiveFat32Status.InvalidBpb;
            if ((EndianPrimitives.ReadUInt32LittleEndian(bytes + 11) & 0xffff) != SectorBytes || bytes[16] != 2 ||
                EndianPrimitives.ReadUInt32LittleEndian(bytes + 40) != 0)
                return LiveFat32Status.UnsupportedGeometry;
            if ((EndianPrimitives.ReadUInt32LittleEndian(bytes + 17) & 0xffff) != 0 ||
                (EndianPrimitives.ReadUInt32LittleEndian(bytes + 19) & 0xffff) != 0 ||
                (EndianPrimitives.ReadUInt32LittleEndian(bytes + 22) & 0xffff) != 0)
                return LiveFat32Status.InvalidBpb;
            for (int i = 52; i < 64; i++)
                if (bytes[i] != 0)
                    return LiveFat32Status.InvalidBpb;
            LiveFat32Volume parsed = default;
            parsed.Capacity = capacity;
            parsed.PartitionFirstLba = partitionFirst;
            parsed.PartitionLastLba = partitionLast;
            parsed.ReservedSectors = EndianPrimitives.ReadUInt32LittleEndian(bytes + 14) & 0xffff;
            parsed.FatSectors = EndianPrimitives.ReadUInt32LittleEndian(bytes + 36);
            parsed.SectorsPerCluster = bytes[13];
            parsed.RootCluster = EndianPrimitives.ReadUInt32LittleEndian(bytes + 44);
            parsed.TotalSectors = EndianPrimitives.ReadUInt32LittleEndian(bytes + 32);
            LiveFat32Status status = Geometry(ref parsed);
            if (status != LiveFat32Status.Success)
                return status;
            parsed.Status = LiveFat32Status.Success;
            volume = parsed;
            return LiveFat32Status.Success;
        }
    }

    public static LiveFat32Status ClusterFirstLba(LiveFat32Volume volume, uint cluster, ref ulong lba)
    {
        if (!ValidVolume(volume))
            return LiveFat32Status.InvalidVolume;
        if (!DataCluster(volume, cluster))
            return LiveFat32Status.InvalidCluster;
        lba = volume.FirstDataLba + (ulong)(cluster - 2) * volume.SectorsPerCluster;
        return LiveFat32Status.Success;
    }

    // Mirrored mode selects FAT0. The caller must compare corresponding FAT1
    // bytes separately if mirror agreement is part of its acceptance claim.
    // EndOfChain is a terminal classification and leaves nextCluster unchanged.
    public static LiveFat32Status ReadFatEntry(LiveFat32Volume volume, uint cluster,
        ulong sectorLba, Span<byte> sector, BlockReadResult read, ref uint nextCluster)
    {
        if (!ValidVolume(volume))
            return LiveFat32Status.InvalidVolume;
        if (!DataCluster(volume, cluster))
            return LiveFat32Status.InvalidCluster;
        if (!Complete(read))
            return LiveFat32Status.ReadFailed;
        if (sector.Length != SectorBytes)
            return LiveFat32Status.InvalidLength;
        if (sectorLba != volume.FirstFatLba + cluster / 128)
            return LiveFat32Status.UnexpectedFatSector;
        fixed (byte* bytes = sector)
        {
            if (bytes == null || (ulong)bytes > ulong.MaxValue - SectorBytes)
                return LiveFat32Status.InvalidLength;
            uint next = EndianPrimitives.ReadUInt32LittleEndian(bytes + (cluster % 128) * 4) & 0x0fffffff;
            if (next == 0)
                return LiveFat32Status.FreeCluster;
            if (next == 1 || (next >= 0x0ffffff0 && next <= 0x0ffffff6))
                return LiveFat32Status.ReservedCluster;
            if (next == 0x0ffffff7)
                return LiveFat32Status.BadCluster;
            if (next >= 0x0ffffff8)
                return LiveFat32Status.EndOfChain;
            if (!DataCluster(volume, next))
                return LiveFat32Status.InvalidCluster;
            nextCluster = next;
            return LiveFat32Status.Success;
        }
    }

    // The caller starts count at zero and retains exclusive history ownership.
    // Each call scans at most 4096 prior entries; this is a deliberate ceiling,
    // not a claim that arbitrary directory/file chains can be traversed.
    public static LiveFat32Status Visit(LiveFat32Volume volume, uint cluster, Span<uint> history, ref uint count)
    {
        if (!ValidVolume(volume))
            return LiveFat32Status.InvalidVolume;
        if (!DataCluster(volume, cluster))
            return LiveFat32Status.InvalidCluster;
        if (history.Length == 0 || history.Length > MaximumHistoryClusters)
            return LiveFat32Status.ScratchLimit;
        if (count > (uint)history.Length)
            return LiveFat32Status.InvalidHistory;
        fixed (uint* entries = history)
        fixed (uint* countPointer = &count)
        {
            ulong start = (ulong)entries, bytes = (ulong)history.Length * 4, counter = (ulong)countPointer;
            if (entries == null || start > ulong.MaxValue - bytes)
                return LiveFat32Status.InvalidLength;
            if (counter > ulong.MaxValue - 4 || (counter < start + bytes && start < counter + 4))
                return LiveFat32Status.InvalidHistory;
            for (uint i = 0; i < count; i++)
            {
                if (!DataCluster(volume, entries[i]))
                    return LiveFat32Status.InvalidHistory;
                if (entries[i] == cluster)
                    return LiveFat32Status.Loop;
            }
            if (count == (uint)history.Length)
                return LiveFat32Status.ScratchLimit;
            entries[count] = cluster;
            count++;
            return LiveFat32Status.Success;
        }
    }

    // Call only after observing EndOfChain, with a known positive required count.
    // Directory chains have no file length and can accept EndOfChain directly.
    public static LiveFat32Status CheckLength(uint visited, uint required)
    {
        if (required == 0)
            return LiveFat32Status.InvalidHistory;
        if (visited > MaximumHistoryClusters || required > MaximumHistoryClusters)
            return LiveFat32Status.ScratchLimit;
        if (visited < required)
            return LiveFat32Status.Truncated;
        return visited == required ? LiveFat32Status.Success : LiveFat32Status.Overlong;
    }

    private static bool Complete(BlockReadResult read) => read.Succeeded && read.BlocksRead == 1 && read.BytesRead == SectorBytes;
    private static bool Partition(ulong capacity, ulong first, ulong last) =>
        capacity != 0 && capacity <= ulong.MaxValue / SectorBytes && first != 0 && first <= last && last < capacity;
    private static bool DataCluster(LiveFat32Volume v, uint cluster) => cluster >= 2 && cluster - 2 < v.ClusterCount;

    private static bool ValidVolume(LiveFat32Volume v)
    {
        if (!v.Succeeded)
            return false;
        LiveFat32Volume checkedVolume = v;
        return Geometry(ref checkedVolume) == LiveFat32Status.Success &&
            checkedVolume.FirstFatLba == v.FirstFatLba && checkedVolume.FirstDataLba == v.FirstDataLba &&
            checkedVolume.ClusterCount == v.ClusterCount;
    }

    private static LiveFat32Status Geometry(ref LiveFat32Volume v)
    {
        if (!Partition(v.Capacity, v.PartitionFirstLba, v.PartitionLastLba))
            return LiveFat32Status.InvalidPartition;
        if (v.TotalSectors == 0 || v.TotalSectors > 0xffffffffUL ||
            v.TotalSectors > v.PartitionLastLba - v.PartitionFirstLba + 1)
            return LiveFat32Status.InvalidBpb;
        uint spc = v.SectorsPerCluster;
        if (spc == 0 || spc > 128 || (spc & (spc - 1)) != 0 || v.ReservedSectors == 0 || v.ReservedSectors > 65535)
            return LiveFat32Status.UnsupportedGeometry;
        ulong dataOffset = v.ReservedSectors + (ulong)v.FatSectors * 2;
        if (v.FatSectors == 0 || dataOffset >= v.TotalSectors)
            return LiveFat32Status.InvalidFatBounds;
        ulong clusters = (v.TotalSectors - dataOffset) / spc;
        if (clusters < MinimumClusters || clusters > MaximumClusters)
            return LiveFat32Status.UnsupportedGeometry;
        if (clusters + 2 > (ulong)v.FatSectors * 128)
            return LiveFat32Status.InvalidFatBounds;
        if (v.RootCluster < 2 || (ulong)v.RootCluster - 2 >= clusters)
            return LiveFat32Status.InvalidCluster;
        // Every derived address is within [first, first+TotalSectors), already
        // bounded by last+1 <= capacity <= ulong.MaxValue/512.
        v.FirstFatLba = v.PartitionFirstLba + v.ReservedSectors;
        v.FirstDataLba = v.PartitionFirstLba + dataOffset;
        v.ClusterCount = (uint)clusters;
        return LiveFat32Status.Success;
    }
}
