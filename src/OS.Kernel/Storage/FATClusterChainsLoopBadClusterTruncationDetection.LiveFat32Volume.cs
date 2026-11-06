using System;
using OS.Std.Binary;

namespace OS.Storage;

public struct LiveFat32Volume
{
    public LiveFat32Status Status;
    public ulong Capacity, PartitionFirstLba, PartitionLastLba, TotalSectors, FirstFatLba, FirstDataLba;
    public uint ReservedSectors, FatSectors, SectorsPerCluster, ClusterCount, RootCluster;
    public readonly bool Succeeded => Status == LiveFat32Status.Success;
}
