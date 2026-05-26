using OS.Drivers;
using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;

public enum AcpiMcfgDiscoveryStatus
{
    Success = 0,
    DirectoryFailed = 1,
    McfgParseFailed = 2,
    SelectedSourceInvalid = 3
}

public readonly struct AcpiMcfgDiscoveryResult
{
    private AcpiMcfgDiscoveryResult(
        AcpiMcfgDiscoveryStatus status,
        AcpiTableDirectoryResult directoryResult,
        AcpiMcfgParseResult mcfgParseResult,
        bool mcfgParseAttempted)
    {
        Status = status;
        DirectoryResult = directoryResult;
        McfgParseResult = mcfgParseResult;
        McfgParseAttempted = mcfgParseAttempted;
    }

    public AcpiMcfgDiscoveryStatus Status { get; }

    public AcpiTableDirectoryResult DirectoryResult { get; }

    public AcpiMcfgParseResult McfgParseResult { get; }

    public bool McfgParseAttempted { get; }

    public bool Succeeded => Status == AcpiMcfgDiscoveryStatus.Success;

    public AcpiTableDirectoryStatus DirectoryStatus => DirectoryResult.Status;

    public AcpiMcfgParseStatus McfgParseStatus => McfgParseResult.Status;

    public ulong SelectedPhysicalAddress => DirectoryResult.PhysicalAddress;

    public int SelectedPointerIndex => DirectoryResult.PointerIndex;

    public int SelectedSourceIndex => DirectoryResult.SourceIndex;

    public int PointerCount => DirectoryResult.PointerCount;

    public int SegmentCount => McfgParseResult.SegmentCount;

    public int RequiredSegmentCapacity => McfgParseResult.RequiredSegmentCapacity;

    public uint SelectedTableLength => DirectoryResult.DeclaredLength;

    public static AcpiMcfgDiscoveryResult Success(
        AcpiTableDirectoryResult directoryResult,
        AcpiMcfgParseResult mcfgParseResult)
    {
        return new AcpiMcfgDiscoveryResult(
            AcpiMcfgDiscoveryStatus.Success,
            directoryResult,
            mcfgParseResult,
            mcfgParseAttempted: true);
    }

    public static AcpiMcfgDiscoveryResult DirectoryFailed(
        AcpiTableDirectoryResult directoryResult)
    {
        return new AcpiMcfgDiscoveryResult(
            AcpiMcfgDiscoveryStatus.DirectoryFailed,
            directoryResult,
            default,
            mcfgParseAttempted: false);
    }

    public static AcpiMcfgDiscoveryResult McfgParseFailed(
        AcpiTableDirectoryResult directoryResult,
        AcpiMcfgParseResult mcfgParseResult)
    {
        return new AcpiMcfgDiscoveryResult(
            AcpiMcfgDiscoveryStatus.McfgParseFailed,
            directoryResult,
            mcfgParseResult,
            mcfgParseAttempted: true);
    }

    public static AcpiMcfgDiscoveryResult SelectedSourceInvalid(
        AcpiTableDirectoryResult directoryResult)
    {
        return new AcpiMcfgDiscoveryResult(
            AcpiMcfgDiscoveryStatus.SelectedSourceInvalid,
            directoryResult,
            default,
            mcfgParseAttempted: false);
    }
}

public static class AcpiMcfgDiscovery
{
    public static AcpiMcfgPhysicalDiscoveryResult TryDiscoverFromRootSdtSnapshot(
        AcpiRootSdtSnapshotResult rootSdtSnapshot,
        ReadOnlySpan<byte> rootSdtTable,
        IPlatformPhysicalMemory physicalMemory,
        Span<ulong> pointerScratch,
        Span<byte> childHeaderScratch,
        Span<byte> childTableDestination,
        Span<PciEcamSegment> destination)
    {
        if (!rootSdtSnapshot.Succeeded)
        {
            return AcpiMcfgPhysicalDiscoveryResult.InvalidRootSdtSnapshot(
                rootSdtSnapshot.Status);
        }

        if (physicalMemory is null)
        {
            return AcpiMcfgPhysicalDiscoveryResult.PhysicalMemoryUnavailable();
        }

        if (childHeaderScratch.Length < AcpiSdtParser.HeaderLength)
        {
            return AcpiMcfgPhysicalDiscoveryResult.HeaderScratchTooSmall(
                AcpiSdtParser.HeaderLength);
        }

        ReadOnlySpan<byte> rootTable = rootSdtTable;
        if (rootSdtSnapshot.BytesRead > 0
            && rootSdtSnapshot.BytesRead <= rootSdtTable.Length)
        {
            rootTable = rootSdtTable.Slice(0, rootSdtSnapshot.BytesRead);
        }

        AcpiSdtParseResult pointerListResult = AcpiSdtParser.TryParsePointerList(
            rootTable,
            pointerScratch);

        if (!pointerListResult.Succeeded)
        {
            return AcpiMcfgPhysicalDiscoveryResult.RootPointerListFailed(
                pointerListResult);
        }

        ReadOnlySpan<ulong> tablePointers = pointerScratch.Slice(0, pointerListResult.PointerCount);
        for (int pointerIndex = 0; pointerIndex < tablePointers.Length; pointerIndex++)
        {
            ulong physicalAddress = tablePointers[pointerIndex];
            PlatformPhysicalMemoryResult headerRead = physicalMemory.ReadBytes(
                physicalAddress,
                AcpiSdtParser.HeaderLength,
                childHeaderScratch);

            if (!headerRead.Succeeded)
            {
                return AcpiMcfgPhysicalDiscoveryResult.ChildHeaderReadFailed(
                    pointerListResult,
                    pointerIndex,
                    physicalAddress,
                    headerRead);
            }

            uint signature = EndianPrimitives.ReadUInt32LittleEndian(
                childHeaderScratch.Slice(AcpiSdtParser.SignatureOffset, sizeof(uint)));
            uint declaredLength = EndianPrimitives.ReadUInt32LittleEndian(
                childHeaderScratch.Slice(AcpiSdtParser.LengthOffset, sizeof(uint)));

            if (declaredLength < AcpiSdtParser.HeaderLength
                || declaredLength > int.MaxValue)
            {
                return AcpiMcfgPhysicalDiscoveryResult.ChildTableLengthInvalid(
                    pointerListResult,
                    pointerIndex,
                    physicalAddress,
                    signature,
                    declaredLength,
                    AcpiSdtParser.HeaderLength);
            }

            if (signature != AcpiTableDirectory.McfgSignature)
            {
                continue;
            }

            int tableLength = (int)declaredLength;
            if (childTableDestination.Length < tableLength)
            {
                return AcpiMcfgPhysicalDiscoveryResult.ChildTableDestinationTooSmall(
                    pointerListResult,
                    pointerIndex,
                    physicalAddress,
                    signature,
                    declaredLength,
                    tableLength);
            }

            PlatformPhysicalMemoryResult fullRead = physicalMemory.ReadBytes(
                physicalAddress,
                tableLength,
                childTableDestination);

            if (!fullRead.Succeeded)
            {
                return AcpiMcfgPhysicalDiscoveryResult.ChildFullReadFailed(
                    pointerListResult,
                    pointerIndex,
                    physicalAddress,
                    signature,
                    declaredLength,
                    fullRead);
            }

            ReadOnlySpan<byte> mcfgTable = childTableDestination.Slice(0, tableLength);
            if (!IsChecksumValid(mcfgTable))
            {
                return AcpiMcfgPhysicalDiscoveryResult.ChildTableChecksumInvalid(
                    pointerListResult,
                    pointerIndex,
                    physicalAddress,
                    signature,
                    declaredLength);
            }

            AcpiMcfgParseResult mcfgParseResult = AcpiMcfgParser.TryParse(
                mcfgTable,
                destination);

            if (!mcfgParseResult.Succeeded)
            {
                return AcpiMcfgPhysicalDiscoveryResult.McfgParseFailed(
                    pointerListResult,
                    pointerIndex,
                    physicalAddress,
                    signature,
                    declaredLength,
                    mcfgParseResult);
            }

            return AcpiMcfgPhysicalDiscoveryResult.Success(
                pointerListResult,
                pointerIndex,
                physicalAddress,
                declaredLength,
                mcfgParseResult);
        }

        return AcpiMcfgPhysicalDiscoveryResult.SignatureNotFound(pointerListResult);
    }

    public static AcpiMcfgDiscoveryResult TryDiscover(
        ReadOnlySpan<byte> pointerTable,
        Span<ulong> pointerScratch,
        ReadOnlySpan<AcpiTableSource> tableSources,
        Span<PciEcamSegment> destination)
    {
        Span<AcpiTableSnapshot> selectedTable = stackalloc AcpiTableSnapshot[1];
        AcpiTableDirectoryResult directoryResult = AcpiTableDirectory.TrySelect(
            pointerTable,
            pointerScratch,
            tableSources,
            AcpiTableDirectory.McfgSignature,
            selectedTable);

        return ParseSelectedTable(directoryResult, tableSources, destination);
    }

    public static AcpiMcfgDiscoveryResult TryDiscover(
        ReadOnlySpan<ulong> tablePointers,
        ReadOnlySpan<AcpiTableSource> tableSources,
        Span<PciEcamSegment> destination)
    {
        Span<AcpiTableSnapshot> selectedTable = stackalloc AcpiTableSnapshot[1];
        AcpiTableDirectoryResult directoryResult = AcpiTableDirectory.TrySelect(
            tablePointers,
            tableSources,
            AcpiTableDirectory.McfgSignature,
            selectedTable);

        return ParseSelectedTable(directoryResult, tableSources, destination);
    }

    private static AcpiMcfgDiscoveryResult ParseSelectedTable(
        AcpiTableDirectoryResult directoryResult,
        ReadOnlySpan<AcpiTableSource> tableSources,
        Span<PciEcamSegment> destination)
    {
        if (!directoryResult.Succeeded)
        {
            return AcpiMcfgDiscoveryResult.DirectoryFailed(directoryResult);
        }

        int sourceIndex = directoryResult.SourceIndex;
        if ((uint)sourceIndex >= (uint)tableSources.Length)
        {
            return AcpiMcfgDiscoveryResult.SelectedSourceInvalid(directoryResult);
        }

        AcpiMcfgParseResult mcfgParseResult = AcpiMcfgParser.TryParse(
            tableSources[sourceIndex].TableBytes.Span,
            destination);

        if (!mcfgParseResult.Succeeded)
        {
            return AcpiMcfgDiscoveryResult.McfgParseFailed(
                directoryResult,
                mcfgParseResult);
        }

        return AcpiMcfgDiscoveryResult.Success(
            directoryResult,
            mcfgParseResult);
    }

    private static bool IsChecksumValid(ReadOnlySpan<byte> table)
    {
        uint sum = 0;
        for (int index = 0; index < table.Length; index++)
        {
            sum += table[index];
        }

        return (sum & 0xff) == 0;
    }
}

public enum AcpiMcfgPhysicalDiscoveryStatus
{
    Success = 0,
    InvalidRootSdtSnapshot = 1,
    PhysicalMemoryUnavailable = 2,
    HeaderScratchTooSmall = 3,
    RootPointerListFailed = 4,
    ChildHeaderReadFailed = 5,
    ChildTableLengthInvalid = 6,
    ChildTableDestinationTooSmall = 7,
    ChildFullReadFailed = 8,
    ChildTableChecksumInvalid = 9,
    SignatureNotFound = 10,
    McfgParseFailed = 11
}

public readonly struct AcpiMcfgPhysicalDiscoveryResult
{
    private AcpiMcfgPhysicalDiscoveryResult(
        AcpiMcfgPhysicalDiscoveryStatus status,
        AcpiRootSdtSnapshotStatus rootSdtStatus,
        AcpiSdtParseResult pointerListResult,
        AcpiMcfgParseResult mcfgParseResult,
        PlatformPhysicalMemoryStatus physicalReadStatus,
        int pointerIndex,
        ulong physicalAddress,
        uint tableSignature,
        uint declaredLength,
        int requiredBytes,
        int bytesRead)
    {
        Status = status;
        RootSdtStatus = rootSdtStatus;
        PointerListResult = pointerListResult;
        McfgParseResult = mcfgParseResult;
        PhysicalReadStatus = physicalReadStatus;
        PointerIndex = pointerIndex;
        PhysicalAddress = physicalAddress;
        TableSignature = tableSignature;
        DeclaredLength = declaredLength;
        RequiredBytes = requiredBytes;
        BytesRead = bytesRead;
    }

    public AcpiMcfgPhysicalDiscoveryStatus Status { get; }

    public bool Succeeded => Status == AcpiMcfgPhysicalDiscoveryStatus.Success;

    public AcpiRootSdtSnapshotStatus RootSdtStatus { get; }

    public AcpiSdtParseResult PointerListResult { get; }

    public AcpiSdtParseStatus PointerListStatus => PointerListResult.Status;

    public AcpiMcfgParseResult McfgParseResult { get; }

    public AcpiMcfgParseStatus McfgParseStatus => McfgParseResult.Status;

    public PlatformPhysicalMemoryStatus PhysicalReadStatus { get; }

    public int PointerIndex { get; }

    public int PointerCount => PointerListResult.PointerCount;

    public ulong PhysicalAddress { get; }

    public uint TableSignature { get; }

    public uint DeclaredLength { get; }

    public int RequiredBytes { get; }

    public int BytesRead { get; }

    public int SegmentCount => McfgParseResult.SegmentCount;

    public int RequiredSegmentCapacity => McfgParseResult.RequiredSegmentCapacity;

    public static AcpiMcfgPhysicalDiscoveryResult Success(
        AcpiSdtParseResult pointerListResult,
        int pointerIndex,
        ulong physicalAddress,
        uint declaredLength,
        AcpiMcfgParseResult mcfgParseResult)
    {
        return Create(
            AcpiMcfgPhysicalDiscoveryStatus.Success,
            pointerListResult: pointerListResult,
            mcfgParseResult: mcfgParseResult,
            physicalReadStatus: PlatformPhysicalMemoryStatus.Success,
            pointerIndex: pointerIndex,
            physicalAddress: physicalAddress,
            tableSignature: AcpiTableDirectory.McfgSignature,
            declaredLength: declaredLength,
            requiredBytes: (int)declaredLength,
            bytesRead: (int)declaredLength);
    }

    public static AcpiMcfgPhysicalDiscoveryResult InvalidRootSdtSnapshot(
        AcpiRootSdtSnapshotStatus rootSdtStatus)
    {
        return Create(
            AcpiMcfgPhysicalDiscoveryStatus.InvalidRootSdtSnapshot,
            rootSdtStatus: rootSdtStatus);
    }

    public static AcpiMcfgPhysicalDiscoveryResult PhysicalMemoryUnavailable()
    {
        return Create(
            AcpiMcfgPhysicalDiscoveryStatus.PhysicalMemoryUnavailable,
            physicalReadStatus: PlatformPhysicalMemoryStatus.SourceUnavailable);
    }

    public static AcpiMcfgPhysicalDiscoveryResult HeaderScratchTooSmall(
        int requiredBytes)
    {
        return Create(
            AcpiMcfgPhysicalDiscoveryStatus.HeaderScratchTooSmall,
            requiredBytes: requiredBytes);
    }

    public static AcpiMcfgPhysicalDiscoveryResult RootPointerListFailed(
        AcpiSdtParseResult pointerListResult)
    {
        return Create(
            AcpiMcfgPhysicalDiscoveryStatus.RootPointerListFailed,
            pointerListResult: pointerListResult,
            requiredBytes: RequiredBytesFrom(pointerListResult.RequiredLength));
    }

    public static AcpiMcfgPhysicalDiscoveryResult ChildHeaderReadFailed(
        AcpiSdtParseResult pointerListResult,
        int pointerIndex,
        ulong physicalAddress,
        PlatformPhysicalMemoryResult physicalRead)
    {
        return Create(
            AcpiMcfgPhysicalDiscoveryStatus.ChildHeaderReadFailed,
            pointerListResult: pointerListResult,
            physicalReadStatus: physicalRead.Status,
            pointerIndex: pointerIndex,
            physicalAddress: physicalAddress,
            requiredBytes: physicalRead.RequiredBytes,
            bytesRead: physicalRead.BytesRead);
    }

    public static AcpiMcfgPhysicalDiscoveryResult ChildTableLengthInvalid(
        AcpiSdtParseResult pointerListResult,
        int pointerIndex,
        ulong physicalAddress,
        uint tableSignature,
        uint declaredLength,
        int requiredBytes)
    {
        return Create(
            AcpiMcfgPhysicalDiscoveryStatus.ChildTableLengthInvalid,
            pointerListResult: pointerListResult,
            physicalReadStatus: PlatformPhysicalMemoryStatus.Success,
            pointerIndex: pointerIndex,
            physicalAddress: physicalAddress,
            tableSignature: tableSignature,
            declaredLength: declaredLength,
            requiredBytes: requiredBytes);
    }

    public static AcpiMcfgPhysicalDiscoveryResult ChildTableDestinationTooSmall(
        AcpiSdtParseResult pointerListResult,
        int pointerIndex,
        ulong physicalAddress,
        uint tableSignature,
        uint declaredLength,
        int requiredBytes)
    {
        return Create(
            AcpiMcfgPhysicalDiscoveryStatus.ChildTableDestinationTooSmall,
            pointerListResult: pointerListResult,
            physicalReadStatus: PlatformPhysicalMemoryStatus.Success,
            pointerIndex: pointerIndex,
            physicalAddress: physicalAddress,
            tableSignature: tableSignature,
            declaredLength: declaredLength,
            requiredBytes: requiredBytes);
    }

    public static AcpiMcfgPhysicalDiscoveryResult ChildFullReadFailed(
        AcpiSdtParseResult pointerListResult,
        int pointerIndex,
        ulong physicalAddress,
        uint tableSignature,
        uint declaredLength,
        PlatformPhysicalMemoryResult physicalRead)
    {
        return Create(
            AcpiMcfgPhysicalDiscoveryStatus.ChildFullReadFailed,
            pointerListResult: pointerListResult,
            physicalReadStatus: physicalRead.Status,
            pointerIndex: pointerIndex,
            physicalAddress: physicalAddress,
            tableSignature: tableSignature,
            declaredLength: declaredLength,
            requiredBytes: physicalRead.RequiredBytes,
            bytesRead: physicalRead.BytesRead);
    }

    public static AcpiMcfgPhysicalDiscoveryResult ChildTableChecksumInvalid(
        AcpiSdtParseResult pointerListResult,
        int pointerIndex,
        ulong physicalAddress,
        uint tableSignature,
        uint declaredLength)
    {
        return Create(
            AcpiMcfgPhysicalDiscoveryStatus.ChildTableChecksumInvalid,
            pointerListResult: pointerListResult,
            physicalReadStatus: PlatformPhysicalMemoryStatus.Success,
            pointerIndex: pointerIndex,
            physicalAddress: physicalAddress,
            tableSignature: tableSignature,
            declaredLength: declaredLength,
            requiredBytes: (int)declaredLength,
            bytesRead: (int)declaredLength);
    }

    public static AcpiMcfgPhysicalDiscoveryResult SignatureNotFound(
        AcpiSdtParseResult pointerListResult)
    {
        return Create(
            AcpiMcfgPhysicalDiscoveryStatus.SignatureNotFound,
            pointerListResult: pointerListResult);
    }

    public static AcpiMcfgPhysicalDiscoveryResult McfgParseFailed(
        AcpiSdtParseResult pointerListResult,
        int pointerIndex,
        ulong physicalAddress,
        uint tableSignature,
        uint declaredLength,
        AcpiMcfgParseResult mcfgParseResult)
    {
        return Create(
            AcpiMcfgPhysicalDiscoveryStatus.McfgParseFailed,
            pointerListResult: pointerListResult,
            mcfgParseResult: mcfgParseResult,
            physicalReadStatus: PlatformPhysicalMemoryStatus.Success,
            pointerIndex: pointerIndex,
            physicalAddress: physicalAddress,
            tableSignature: tableSignature,
            declaredLength: declaredLength,
            requiredBytes: RequiredBytesFrom(mcfgParseResult.RequiredLength),
            bytesRead: (int)declaredLength);
    }

    private static AcpiMcfgPhysicalDiscoveryResult Create(
        AcpiMcfgPhysicalDiscoveryStatus status,
        AcpiRootSdtSnapshotStatus rootSdtStatus = AcpiRootSdtSnapshotStatus.Success,
        AcpiSdtParseResult pointerListResult = default,
        AcpiMcfgParseResult mcfgParseResult = default,
        PlatformPhysicalMemoryStatus physicalReadStatus = PlatformPhysicalMemoryStatus.Unknown,
        int pointerIndex = -1,
        ulong physicalAddress = 0,
        uint tableSignature = 0,
        uint declaredLength = 0,
        int requiredBytes = 0,
        int bytesRead = 0)
    {
        return new AcpiMcfgPhysicalDiscoveryResult(
            status,
            rootSdtStatus,
            pointerListResult,
            mcfgParseResult,
            physicalReadStatus,
            pointerIndex,
            physicalAddress,
            tableSignature,
            declaredLength,
            requiredBytes,
            bytesRead);
    }

    private static int RequiredBytesFrom(uint requiredLength)
    {
        return requiredLength > int.MaxValue
            ? int.MaxValue
            : (int)requiredLength;
    }
}
