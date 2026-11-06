namespace OS.Storage;

public readonly struct GptHeader
{
    public GptHeader(
        uint revision,
        uint headerSize,
        ulong currentLba,
        ulong backupLba,
        ulong firstUsableLba,
        ulong lastUsableLba,
        Guid diskGuid,
        ulong partitionEntryLba,
        uint partitionEntryCount,
        uint partitionEntrySize,
        uint partitionEntryArrayCrc32)
    {
        Revision = revision;
        HeaderSize = headerSize;
        CurrentLba = currentLba;
        BackupLba = backupLba;
        FirstUsableLba = firstUsableLba;
        LastUsableLba = lastUsableLba;
        DiskGuid = diskGuid;
        PartitionEntryLba = partitionEntryLba;
        PartitionEntryCount = partitionEntryCount;
        PartitionEntrySize = partitionEntrySize;
        PartitionEntryArrayCrc32 = partitionEntryArrayCrc32;
    }

    public uint Revision
    {
        get;
    }

    public uint HeaderSize
    {
        get;
    }

    public ulong CurrentLba
    {
        get;
    }

    public ulong BackupLba
    {
        get;
    }

    public ulong FirstUsableLba
    {
        get;
    }

    public ulong LastUsableLba
    {
        get;
    }

    public Guid DiskGuid
    {
        get;
    }

    public ulong PartitionEntryLba
    {
        get;
    }

    public uint PartitionEntryCount
    {
        get;
    }

    public uint PartitionEntrySize
    {
        get;
    }

    public uint PartitionEntryArrayCrc32
    {
        get;
    }
}
