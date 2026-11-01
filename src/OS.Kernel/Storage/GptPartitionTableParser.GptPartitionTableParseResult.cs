using OS.Std.Binary;
using OS.Std.Hash;
using OS.Std.Memory;

namespace OS.Storage;

public readonly struct GptPartitionTableParseResult
{
    private GptPartitionTableParseResult(
        GptPartitionTableStatus status,
        GptPartitionTableCrcStatus crcStatus,
        GptHeader header,
        int partitionsWritten,
        uint emptyEntriesSkipped,
        ulong partitionEntryBytesRequired,
        ulong partitionEntrySectorsRequired,
        uint failingEntryIndex,
        uint expectedHeaderCrc32,
        uint computedHeaderCrc32,
        uint expectedPartitionEntryArrayCrc32,
        uint computedPartitionEntryArrayCrc32)
    {
        IsInitialized = true;
        Status = status;
        CrcStatus = crcStatus;
        Header = header;
        PartitionsWritten = partitionsWritten;
        EmptyEntriesSkipped = emptyEntriesSkipped;
        PartitionEntryBytesRequired = partitionEntryBytesRequired;
        PartitionEntrySectorsRequired = partitionEntrySectorsRequired;
        FailingEntryIndex = failingEntryIndex;
        ExpectedHeaderCrc32 = expectedHeaderCrc32;
        ComputedHeaderCrc32 = computedHeaderCrc32;
        ExpectedPartitionEntryArrayCrc32 = expectedPartitionEntryArrayCrc32;
        ComputedPartitionEntryArrayCrc32 = computedPartitionEntryArrayCrc32;
    }

    public bool IsInitialized { get; }

    public GptPartitionTableStatus Status { get; }

    public GptPartitionTableCrcStatus CrcStatus { get; }

    public GptHeader Header { get; }

    public int PartitionsWritten { get; }

    public uint EmptyEntriesSkipped { get; }

    public ulong PartitionEntryBytesRequired { get; }

    public ulong PartitionEntrySectorsRequired { get; }

    public uint FailingEntryIndex { get; }

    public uint ExpectedHeaderCrc32 { get; }

    public uint ComputedHeaderCrc32 { get; }

    public uint ExpectedPartitionEntryArrayCrc32 { get; }

    public uint ComputedPartitionEntryArrayCrc32 { get; }

    public bool Succeeded => IsInitialized && Status == GptPartitionTableStatus.Success;

    public static GptPartitionTableParseResult Success(
        GptHeader header,
        int partitionsWritten,
        uint emptyEntriesSkipped,
        ulong partitionEntryBytesRequired,
        ulong partitionEntrySectorsRequired,
        uint expectedHeaderCrc32,
        uint computedHeaderCrc32,
        uint expectedPartitionEntryArrayCrc32,
        uint computedPartitionEntryArrayCrc32)
    {
        return new GptPartitionTableParseResult(
            GptPartitionTableStatus.Success,
            GptPartitionTableCrcStatus.Validated,
            header,
            partitionsWritten,
            emptyEntriesSkipped,
            partitionEntryBytesRequired,
            partitionEntrySectorsRequired,
            0,
            expectedHeaderCrc32,
            computedHeaderCrc32,
            expectedPartitionEntryArrayCrc32,
            computedPartitionEntryArrayCrc32);
    }

    public static GptPartitionTableParseResult Fail(
        GptPartitionTableStatus status,
        GptHeader header = default,
        int partitionsWritten = 0,
        uint emptyEntriesSkipped = 0,
        ulong partitionEntryBytesRequired = 0,
        ulong partitionEntrySectorsRequired = 0,
        uint failingEntryIndex = 0,
        GptPartitionTableCrcStatus crcStatus = GptPartitionTableCrcStatus.NotValidated,
        uint expectedHeaderCrc32 = 0,
        uint computedHeaderCrc32 = 0,
        uint expectedPartitionEntryArrayCrc32 = 0,
        uint computedPartitionEntryArrayCrc32 = 0)
    {
        return new GptPartitionTableParseResult(
            status,
            crcStatus,
            header,
            partitionsWritten,
            emptyEntriesSkipped,
            partitionEntryBytesRequired,
            partitionEntrySectorsRequired,
            failingEntryIndex,
            expectedHeaderCrc32,
            computedHeaderCrc32,
            expectedPartitionEntryArrayCrc32,
            computedPartitionEntryArrayCrc32);
    }
}
