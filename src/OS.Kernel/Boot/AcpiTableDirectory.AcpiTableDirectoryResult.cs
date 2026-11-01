using OS.Std.Binary;

namespace OS.Boot;
public readonly struct AcpiTableDirectoryResult
{
    private AcpiTableDirectoryResult(AcpiTableDirectoryStatus status, int selectedCount, int snapshotCount, int pointerCount, int pointerIndex, int sourceIndex, ulong physicalAddress, uint requestedSignature, uint tableSignature, uint declaredLength, uint requiredLength, int requiredDestinationCapacity, int requiredSnapshotCapacity, AcpiSdtParseStatus pointerListStatus)
    {
        Status = status;
        SelectedCount = selectedCount;
        SnapshotCount = snapshotCount;
        PointerCount = pointerCount;
        PointerIndex = pointerIndex;
        SourceIndex = sourceIndex;
        PhysicalAddress = physicalAddress;
        RequestedSignature = requestedSignature;
        TableSignature = tableSignature;
        DeclaredLength = declaredLength;
        RequiredLength = requiredLength;
        RequiredDestinationCapacity = requiredDestinationCapacity;
        RequiredSnapshotCapacity = requiredSnapshotCapacity;
        PointerListStatus = pointerListStatus;
    }

    public AcpiTableDirectoryStatus Status { get; }
    public int SelectedCount { get; }
    public int SnapshotCount { get; }
    public int PointerCount { get; }
    public int PointerIndex { get; }
    public int SourceIndex { get; }
    public ulong PhysicalAddress { get; }
    public uint RequestedSignature { get; }
    public uint TableSignature { get; }
    public uint DeclaredLength { get; }
    public uint RequiredLength { get; }
    public int RequiredDestinationCapacity { get; }
    public int RequiredSnapshotCapacity { get; }
    public AcpiSdtParseStatus PointerListStatus { get; }
    public bool Succeeded => Status == AcpiTableDirectoryStatus.Success;

    public static AcpiTableDirectoryResult SuccessSelected(int pointerCount, int pointerIndex, int sourceIndex, ulong physicalAddress, uint requestedSignature, uint tableSignature, uint declaredLength)
    {
        return new AcpiTableDirectoryResult(AcpiTableDirectoryStatus.Success, selectedCount: 1, snapshotCount: 0, pointerCount, pointerIndex, sourceIndex, physicalAddress, requestedSignature, tableSignature, declaredLength, requiredLength: declaredLength, requiredDestinationCapacity: 1, requiredSnapshotCapacity: 0, AcpiSdtParseStatus.Success);
    }

    public static AcpiTableDirectoryResult SuccessSnapshots(int pointerCount, int snapshotCount)
    {
        return new AcpiTableDirectoryResult(AcpiTableDirectoryStatus.Success, selectedCount: 0, snapshotCount, pointerCount, pointerIndex: -1, sourceIndex: -1, physicalAddress: 0, requestedSignature: 0, tableSignature: 0, declaredLength: 0, requiredLength: 0, requiredDestinationCapacity: 0, requiredSnapshotCapacity: snapshotCount, AcpiSdtParseStatus.Success);
    }

    public static AcpiTableDirectoryResult Fail(AcpiTableDirectoryStatus status, int pointerCount = 0, int pointerIndex = -1, int sourceIndex = -1, ulong physicalAddress = 0, uint requestedSignature = 0, uint tableSignature = 0, uint declaredLength = 0, uint requiredLength = 0, int requiredDestinationCapacity = 0, int requiredSnapshotCapacity = 0, AcpiSdtParseStatus pointerListStatus = AcpiSdtParseStatus.Success)
    {
        return new AcpiTableDirectoryResult(status, selectedCount: 0, snapshotCount: 0, pointerCount, pointerIndex, sourceIndex, physicalAddress, requestedSignature, tableSignature, declaredLength, requiredLength, requiredDestinationCapacity, requiredSnapshotCapacity, pointerListStatus);
    }
}
