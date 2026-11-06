namespace OS.Storage;

public readonly struct GptPartitionEntry
{
    public GptPartitionEntry(
        Guid partitionTypeGuid,
        Guid uniquePartitionGuid,
        ulong firstLba,
        ulong lastLba,
        ulong attributes,
        string name)
    {
        PartitionTypeGuid = partitionTypeGuid;
        UniquePartitionGuid = uniquePartitionGuid;
        FirstLba = firstLba;
        LastLba = lastLba;
        Attributes = attributes;
        Name = name;
    }

    public Guid PartitionTypeGuid
    {
        get;
    }

    public Guid UniquePartitionGuid
    {
        get;
    }

    public ulong FirstLba
    {
        get;
    }

    public ulong LastLba
    {
        get;
    }

    public ulong Attributes
    {
        get;
    }

    public string Name
    {
        get;
    }
}
