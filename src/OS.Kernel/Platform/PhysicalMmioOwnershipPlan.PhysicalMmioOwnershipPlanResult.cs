using OS.Drivers;
using OS.Kernel.Memory;

namespace OS.Kernel.Platform;

public readonly struct PhysicalMmioOwnershipPlanResult
{
    private PhysicalMmioOwnershipPlanResult(bool isInitialized, PhysicalMmioOwnershipPlanStatus status, int sourceSegmentCount, int rangeCount, int requiredRangeCapacity, int availableRangeCapacity, int failingSegmentIndex, int conflictingRangeIndex, int failingUsableRangeIndex, ulong totalBytes, PciEcamSegment failingSegment, ulong failingPhysicalStart, ulong failingPhysicalEndExclusive)
    {
        IsInitialized = isInitialized;
        Status = status;
        SourceSegmentCount = sourceSegmentCount;
        RangeCount = rangeCount;
        RequiredRangeCapacity = requiredRangeCapacity;
        AvailableRangeCapacity = availableRangeCapacity;
        FailingSegmentIndex = failingSegmentIndex;
        ConflictingRangeIndex = conflictingRangeIndex;
        FailingUsableRangeIndex = failingUsableRangeIndex;
        TotalBytes = totalBytes;
        FailingSegment = failingSegment;
        FailingPhysicalStart = failingPhysicalStart;
        FailingPhysicalEndExclusive = failingPhysicalEndExclusive;
    }

    public const int NoIndex = -1;
    public bool IsInitialized
    {
        get;
    }
    public PhysicalMmioOwnershipPlanStatus Status
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == PhysicalMmioOwnershipPlanStatus.Success;
    public int SourceSegmentCount
    {
        get;
    }
    public int RangeCount
    {
        get;
    }
    public int RequiredRangeCapacity
    {
        get;
    }
    public int AvailableRangeCapacity
    {
        get;
    }
    public int FailingSegmentIndex
    {
        get;
    }
    public int ConflictingRangeIndex
    {
        get;
    }
    public int FailingUsableRangeIndex
    {
        get;
    }
    public ulong TotalBytes
    {
        get;
    }
    public PciEcamSegment FailingSegment
    {
        get;
    }
    public ulong FailingPhysicalStart
    {
        get;
    }
    public ulong FailingPhysicalEndExclusive
    {
        get;
    }

    public static PhysicalMmioOwnershipPlanResult Success(int sourceSegmentCount, int rangeCount, int availableRangeCapacity, ulong totalBytes)
    {
        return new PhysicalMmioOwnershipPlanResult(true, PhysicalMmioOwnershipPlanStatus.Success, sourceSegmentCount, rangeCount, rangeCount, availableRangeCapacity, NoIndex, NoIndex, NoIndex, totalBytes, default, failingPhysicalStart: 0, failingPhysicalEndExclusive: 0);
    }

    public static PhysicalMmioOwnershipPlanResult Fail(PhysicalMmioOwnershipPlanStatus status, int sourceSegmentCount, int requiredRangeCapacity, int availableRangeCapacity, int failingSegmentIndex = NoIndex, int conflictingRangeIndex = NoIndex, int failingUsableRangeIndex = NoIndex, ulong totalBytes = 0, PciEcamSegment failingSegment = default, ulong failingPhysicalStart = 0, ulong failingPhysicalEndExclusive = 0)
    {
        return new PhysicalMmioOwnershipPlanResult(true, status, sourceSegmentCount, rangeCount: 0, requiredRangeCapacity, availableRangeCapacity, failingSegmentIndex, conflictingRangeIndex, failingUsableRangeIndex, totalBytes, failingSegment, failingPhysicalStart, failingPhysicalEndExclusive);
    }
}
