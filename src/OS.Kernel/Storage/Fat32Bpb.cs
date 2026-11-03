namespace OS.Storage;

public readonly struct Fat32Bpb
{
    public Fat32Bpb(
        ulong partitionStartLba,
        ushort bytesPerSector,
        byte sectorsPerCluster,
        ushort reservedSectorCount,
        byte fatCount,
        uint fatSizeSectors,
        uint rootCluster,
        uint totalSectors,
        ulong firstFatLba,
        ulong firstDataLba,
        ulong rootDirectoryFirstLba,
        uint clusterCount)
    {
        PartitionStartLba = partitionStartLba;
        BytesPerSector = bytesPerSector;
        SectorsPerCluster = sectorsPerCluster;
        ReservedSectorCount = reservedSectorCount;
        FatCount = fatCount;
        FatSizeSectors = fatSizeSectors;
        RootCluster = rootCluster;
        TotalSectors = totalSectors;
        FirstFatLba = firstFatLba;
        FirstDataLba = firstDataLba;
        RootDirectoryFirstLba = rootDirectoryFirstLba;
        ClusterCount = clusterCount;
    }

    public ulong PartitionStartLba
    {
        get;
    }

    public ushort BytesPerSector
    {
        get;
    }

    public byte SectorsPerCluster
    {
        get;
    }

    public ushort ReservedSectorCount
    {
        get;
    }

    public byte FatCount
    {
        get;
    }

    public uint FatSizeSectors
    {
        get;
    }

    public uint RootCluster
    {
        get;
    }

    public uint TotalSectors
    {
        get;
    }

    public ulong FirstFatLba
    {
        get;
    }

    public ulong FirstDataLba
    {
        get;
    }

    public ulong RootDirectoryFirstLba
    {
        get;
    }

    public uint ClusterCount
    {
        get;
    }

    public ulong ClusterSizeBytes => (ulong)BytesPerSector * SectorsPerCluster;
}
