using OS.Drivers;
using OS.Kernel.Platform;
using OS.Std.Binary;

namespace OS.Boot;
public readonly struct AcpiMcfgPhysicalDiscoveryResult
{
    private AcpiMcfgPhysicalDiscoveryResult(AcpiMcfgPhysicalDiscoveryStatus status, AcpiRootSdtSnapshotStatus rootSdtStatus, AcpiSdtParseResult pointerListResult, AcpiMcfgParseResult mcfgParseResult, PlatformPhysicalMemoryStatus physicalReadStatus, int pointerIndex, ulong physicalAddress, uint tableSignature, uint declaredLength, int requiredBytes, int bytesRead)
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

    public static AcpiMcfgPhysicalDiscoveryResult Success(AcpiSdtParseResult pointerListResult, int pointerIndex, ulong physicalAddress, uint declaredLength, AcpiMcfgParseResult mcfgParseResult)
    {
        return Create(AcpiMcfgPhysicalDiscoveryStatus.Success, pointerListResult: pointerListResult, mcfgParseResult: mcfgParseResult, physicalReadStatus: PlatformPhysicalMemoryStatus.Success, pointerIndex: pointerIndex, physicalAddress: physicalAddress, tableSignature: AcpiTableDirectory.McfgSignature, declaredLength: declaredLength, requiredBytes: (int)declaredLength, bytesRead: (int)declaredLength);
    }

    public static AcpiMcfgPhysicalDiscoveryResult InvalidRootSdtSnapshot(AcpiRootSdtSnapshotStatus rootSdtStatus)
    {
        return Create(AcpiMcfgPhysicalDiscoveryStatus.InvalidRootSdtSnapshot, rootSdtStatus: rootSdtStatus);
    }

    public static AcpiMcfgPhysicalDiscoveryResult PhysicalMemoryUnavailable()
    {
        return Create(AcpiMcfgPhysicalDiscoveryStatus.PhysicalMemoryUnavailable, physicalReadStatus: PlatformPhysicalMemoryStatus.SourceUnavailable);
    }

    public static AcpiMcfgPhysicalDiscoveryResult HeaderScratchTooSmall(int requiredBytes)
    {
        return Create(AcpiMcfgPhysicalDiscoveryStatus.HeaderScratchTooSmall, requiredBytes: requiredBytes);
    }

    public static AcpiMcfgPhysicalDiscoveryResult RootPointerListFailed(AcpiSdtParseResult pointerListResult)
    {
        return Create(AcpiMcfgPhysicalDiscoveryStatus.RootPointerListFailed, pointerListResult: pointerListResult, requiredBytes: RequiredBytesFrom(pointerListResult.RequiredLength));
    }

    public static AcpiMcfgPhysicalDiscoveryResult ChildHeaderReadFailed(AcpiSdtParseResult pointerListResult, int pointerIndex, ulong physicalAddress, PlatformPhysicalMemoryResult physicalRead)
    {
        return Create(AcpiMcfgPhysicalDiscoveryStatus.ChildHeaderReadFailed, pointerListResult: pointerListResult, physicalReadStatus: physicalRead.Status, pointerIndex: pointerIndex, physicalAddress: physicalAddress, requiredBytes: physicalRead.RequiredBytes, bytesRead: physicalRead.BytesRead);
    }

    public static AcpiMcfgPhysicalDiscoveryResult ChildTableLengthInvalid(AcpiSdtParseResult pointerListResult, int pointerIndex, ulong physicalAddress, uint tableSignature, uint declaredLength, int requiredBytes)
    {
        return Create(AcpiMcfgPhysicalDiscoveryStatus.ChildTableLengthInvalid, pointerListResult: pointerListResult, physicalReadStatus: PlatformPhysicalMemoryStatus.Success, pointerIndex: pointerIndex, physicalAddress: physicalAddress, tableSignature: tableSignature, declaredLength: declaredLength, requiredBytes: requiredBytes);
    }

    public static AcpiMcfgPhysicalDiscoveryResult ChildTableDestinationTooSmall(AcpiSdtParseResult pointerListResult, int pointerIndex, ulong physicalAddress, uint tableSignature, uint declaredLength, int requiredBytes)
    {
        return Create(AcpiMcfgPhysicalDiscoveryStatus.ChildTableDestinationTooSmall, pointerListResult: pointerListResult, physicalReadStatus: PlatformPhysicalMemoryStatus.Success, pointerIndex: pointerIndex, physicalAddress: physicalAddress, tableSignature: tableSignature, declaredLength: declaredLength, requiredBytes: requiredBytes);
    }

    public static AcpiMcfgPhysicalDiscoveryResult ChildFullReadFailed(AcpiSdtParseResult pointerListResult, int pointerIndex, ulong physicalAddress, uint tableSignature, uint declaredLength, PlatformPhysicalMemoryResult physicalRead)
    {
        return Create(AcpiMcfgPhysicalDiscoveryStatus.ChildFullReadFailed, pointerListResult: pointerListResult, physicalReadStatus: physicalRead.Status, pointerIndex: pointerIndex, physicalAddress: physicalAddress, tableSignature: tableSignature, declaredLength: declaredLength, requiredBytes: physicalRead.RequiredBytes, bytesRead: physicalRead.BytesRead);
    }

    public static AcpiMcfgPhysicalDiscoveryResult ChildTableChecksumInvalid(AcpiSdtParseResult pointerListResult, int pointerIndex, ulong physicalAddress, uint tableSignature, uint declaredLength)
    {
        return Create(AcpiMcfgPhysicalDiscoveryStatus.ChildTableChecksumInvalid, pointerListResult: pointerListResult, physicalReadStatus: PlatformPhysicalMemoryStatus.Success, pointerIndex: pointerIndex, physicalAddress: physicalAddress, tableSignature: tableSignature, declaredLength: declaredLength, requiredBytes: (int)declaredLength, bytesRead: (int)declaredLength);
    }

    public static AcpiMcfgPhysicalDiscoveryResult SignatureNotFound(AcpiSdtParseResult pointerListResult)
    {
        return Create(AcpiMcfgPhysicalDiscoveryStatus.SignatureNotFound, pointerListResult: pointerListResult);
    }

    public static AcpiMcfgPhysicalDiscoveryResult McfgParseFailed(AcpiSdtParseResult pointerListResult, int pointerIndex, ulong physicalAddress, uint tableSignature, uint declaredLength, AcpiMcfgParseResult mcfgParseResult)
    {
        return Create(AcpiMcfgPhysicalDiscoveryStatus.McfgParseFailed, pointerListResult: pointerListResult, mcfgParseResult: mcfgParseResult, physicalReadStatus: PlatformPhysicalMemoryStatus.Success, pointerIndex: pointerIndex, physicalAddress: physicalAddress, tableSignature: tableSignature, declaredLength: declaredLength, requiredBytes: RequiredBytesFrom(mcfgParseResult.RequiredLength), bytesRead: (int)declaredLength);
    }

    private static AcpiMcfgPhysicalDiscoveryResult Create(AcpiMcfgPhysicalDiscoveryStatus status, AcpiRootSdtSnapshotStatus rootSdtStatus = AcpiRootSdtSnapshotStatus.Success, AcpiSdtParseResult pointerListResult = default, AcpiMcfgParseResult mcfgParseResult = default, PlatformPhysicalMemoryStatus physicalReadStatus = PlatformPhysicalMemoryStatus.Unknown, int pointerIndex = -1, ulong physicalAddress = 0, uint tableSignature = 0, uint declaredLength = 0, int requiredBytes = 0, int bytesRead = 0)
    {
        return new AcpiMcfgPhysicalDiscoveryResult(status, rootSdtStatus, pointerListResult, mcfgParseResult, physicalReadStatus, pointerIndex, physicalAddress, tableSignature, declaredLength, requiredBytes, bytesRead);
    }

    private static int RequiredBytesFrom(uint requiredLength)
    {
        return requiredLength > int.MaxValue ? int.MaxValue : (int)requiredLength;
    }
}
