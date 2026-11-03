using OS.Std.Binary;
using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Storage;

public readonly struct GptPartitionDescriptor
{
    public const int NameOffsetWithinEntryBytes = 56;
    public const int NameFieldLengthBytes = 72;
    public const int MaximumNameUtf16CodeUnits = NameFieldLengthBytes / sizeof(ushort);

    public GptPartitionDescriptor(
        uint entryIndex,
        int entryByteOffset,
        Guid partitionTypeGuid,
        Guid uniquePartitionGuid,
        ulong firstLba,
        ulong lastLba,
        ulong attributes,
        int nameByteOffset,
        int nameUtf16CodeUnitLength,
        bool nameIsNullTerminated)
    {
        EntryIndex = entryIndex;
        EntryByteOffset = entryByteOffset;
        PartitionTypeGuid = partitionTypeGuid;
        UniquePartitionGuid = uniquePartitionGuid;
        FirstLba = firstLba;
        LastLba = lastLba;
        Attributes = attributes;
        NameByteOffset = nameByteOffset;
        NameUtf16CodeUnitLength = nameUtf16CodeUnitLength;
        NameIsNullTerminated = nameIsNullTerminated;
    }

    public uint EntryIndex
    {
        get;
    }

    public int EntryByteOffset
    {
        get;
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

    public int NameByteOffset
    {
        get;
    }

    public int NameByteLength => NameFieldLengthBytes;

    public int NameUtf16CodeUnitLength
    {
        get;
    }

    public bool NameIsNullTerminated
    {
        get;
    }
}
