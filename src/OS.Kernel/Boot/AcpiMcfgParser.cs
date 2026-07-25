using OS.Drivers;
using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiMcfgParseStatus
{
    Success = 0,
    SourceTooShort = 1,
    InvalidSignature = 2,
    DeclaredLengthTooSmall = 3,
    SourceTooShortForDeclaredLength = 4,
    InvalidReservedBytes = 5,
    TruncatedAllocationEntry = 6,
    DestinationTooSmall = 7,
    InvalidBusRange = 8
}

public readonly struct AcpiMcfgParseResult
{
    private AcpiMcfgParseResult(
        AcpiMcfgParseStatus status,
        int segmentCount,
        uint declaredLength,
        uint requiredLength,
        int requiredSegmentCapacity,
        int entryIndex)
    {
        Status = status;
        SegmentCount = segmentCount;
        DeclaredLength = declaredLength;
        RequiredLength = requiredLength;
        RequiredSegmentCapacity = requiredSegmentCapacity;
        EntryIndex = entryIndex;
    }

    public AcpiMcfgParseStatus Status { get; }

    public int SegmentCount { get; }

    public uint DeclaredLength { get; }

    public uint RequiredLength { get; }

    public int RequiredSegmentCapacity { get; }

    public int EntryIndex { get; }

    public bool Succeeded => Status == AcpiMcfgParseStatus.Success;

    public static AcpiMcfgParseResult Success(
        int segmentCount,
        uint declaredLength)
    {
        return new AcpiMcfgParseResult(
            AcpiMcfgParseStatus.Success,
            segmentCount,
            declaredLength,
            requiredLength: declaredLength,
            requiredSegmentCapacity: segmentCount,
            entryIndex: -1);
    }

    public static AcpiMcfgParseResult Fail(
        AcpiMcfgParseStatus status,
        uint declaredLength = 0,
        uint requiredLength = 0,
        int requiredSegmentCapacity = 0,
        int entryIndex = -1)
    {
        return new AcpiMcfgParseResult(
            status,
            segmentCount: 0,
            declaredLength,
            requiredLength,
            requiredSegmentCapacity,
            entryIndex);
    }
}

public static class AcpiMcfgParser
{
    public const int SystemDescriptionTableHeaderLength = 36;
    public const int SignatureOffset = 0;
    public const int LengthOffset = 4;
    public const int HeaderReservedOffset = SystemDescriptionTableHeaderLength;
    public const int HeaderReservedLength = 8;
    public const int AllocationEntryOffset = HeaderReservedOffset + HeaderReservedLength;
    public const int AllocationEntryLength = 16;
    public const int MinimumLength = AllocationEntryOffset + AllocationEntryLength;

    public const int AllocationBaseAddressOffset = 0;
    public const int AllocationSegmentGroupOffset = 8;
    public const int AllocationStartBusOffset = 10;
    public const int AllocationEndBusOffset = 11;

    public static AcpiMcfgParseResult TryParse(
        ReadOnlySpan<byte> table,
        Span<PciEcamSegment> destination)
    {
        if (table.Length < SystemDescriptionTableHeaderLength)
        {
            return AcpiMcfgParseResult.Fail(
                AcpiMcfgParseStatus.SourceTooShort,
                requiredLength: SystemDescriptionTableHeaderLength);
        }

        if (!HasMcfgSignature(table))
        {
            return AcpiMcfgParseResult.Fail(AcpiMcfgParseStatus.InvalidSignature);
        }

        uint declaredLength = EndianPrimitives.ReadUInt32LittleEndian(
            table.Slice(LengthOffset, sizeof(uint)));

        if (declaredLength < MinimumLength)
        {
            return AcpiMcfgParseResult.Fail(
                AcpiMcfgParseStatus.DeclaredLengthTooSmall,
                declaredLength,
                MinimumLength);
        }

        if (declaredLength > table.Length)
        {
            return AcpiMcfgParseResult.Fail(
                AcpiMcfgParseStatus.SourceTooShortForDeclaredLength,
                declaredLength,
                declaredLength);
        }

        ReadOnlySpan<byte> declaredTable = table.Slice(0, (int)declaredLength);
        if (!IsZeroed(declaredTable.Slice(HeaderReservedOffset, HeaderReservedLength)))
        {
            return AcpiMcfgParseResult.Fail(
                AcpiMcfgParseStatus.InvalidReservedBytes,
                declaredLength,
                declaredLength);
        }

        int allocationBytes = (int)declaredLength - AllocationEntryOffset;
        int trailingBytes = allocationBytes % AllocationEntryLength;
        if (trailingBytes != 0)
        {
            uint requiredLength = (uint)(declaredLength + (AllocationEntryLength - trailingBytes));
            return AcpiMcfgParseResult.Fail(
                AcpiMcfgParseStatus.TruncatedAllocationEntry,
                declaredLength,
                requiredLength,
                entryIndex: allocationBytes / AllocationEntryLength);
        }

        int entryCount = allocationBytes / AllocationEntryLength;
        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            ReadOnlySpan<byte> entry = GetAllocationEntry(declaredTable, entryIndex);
            byte startBus = entry[AllocationStartBusOffset];
            byte endBus = entry[AllocationEndBusOffset];

            if (startBus > endBus)
            {
                return AcpiMcfgParseResult.Fail(
                    AcpiMcfgParseStatus.InvalidBusRange,
                    declaredLength,
                    declaredLength,
                    entryIndex: entryIndex);
            }
        }

        if (destination.Length < entryCount)
        {
            return AcpiMcfgParseResult.Fail(
                AcpiMcfgParseStatus.DestinationTooSmall,
                declaredLength,
                declaredLength,
                requiredSegmentCapacity: entryCount);
        }

        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            destination[entryIndex] = ParseSegment(GetAllocationEntry(declaredTable, entryIndex));
        }

        return AcpiMcfgParseResult.Success(entryCount, declaredLength);
    }

    private static bool HasMcfgSignature(ReadOnlySpan<byte> table)
    {
        return table[SignatureOffset] == (byte)'M'
            && table[SignatureOffset + 1] == (byte)'C'
            && table[SignatureOffset + 2] == (byte)'F'
            && table[SignatureOffset + 3] == (byte)'G';
    }

    private static bool IsZeroed(ReadOnlySpan<byte> bytes)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static ReadOnlySpan<byte> GetAllocationEntry(
        ReadOnlySpan<byte> table,
        int entryIndex)
    {
        int offset = AllocationEntryOffset + (entryIndex * AllocationEntryLength);
        return table.Slice(offset, AllocationEntryLength);
    }

    private static PciEcamSegment ParseSegment(ReadOnlySpan<byte> entry)
    {
        ulong basePhysicalAddress = EndianPrimitives.ReadUInt64LittleEndian(
            entry.Slice(AllocationBaseAddressOffset, sizeof(ulong)));
        ushort segmentGroup = EndianPrimitives.ReadUInt16LittleEndian(
            entry.Slice(AllocationSegmentGroupOffset, sizeof(ushort)));

        return new PciEcamSegment(
            basePhysicalAddress,
            segmentGroup,
            entry[AllocationStartBusOffset],
            entry[AllocationEndBusOffset]);
    }
}
