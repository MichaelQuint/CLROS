using OS.Drivers;
using OS.Kernel.Memory;

namespace OS.Kernel.Platform;

public enum PhysicalMmioOwnershipPlanStatus
{
    Success = 0,
    InvalidSegmentCount = 1,
    InvalidBusRange = 2,
    InvalidAlignment = 3,
    AddressRangeOverflow = 4,
    OverlappingMmioRange = 5,
    UsableMemoryOverlap = 6,
    InvalidUsableRange = 7,
    DestinationTooSmall = 8,
    TotalByteCountOverflow = 9
}

public readonly struct PhysicalMmioOwnershipRange
{
    public PhysicalMmioOwnershipRange(
        int sourceSegmentIndex,
        ushort segmentGroup,
        byte startBus,
        byte endBus,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong byteLength)
    {
        SourceSegmentIndex = sourceSegmentIndex;
        SegmentGroup = segmentGroup;
        StartBus = startBus;
        EndBus = endBus;
        PhysicalStart = physicalStart;
        PhysicalEndExclusive = physicalEndExclusive;
        ByteLength = byteLength;
    }

    public int SourceSegmentIndex { get; }

    public ushort SegmentGroup { get; }

    public byte StartBus { get; }

    public byte EndBus { get; }

    public ulong PhysicalStart { get; }

    public ulong PhysicalEndExclusive { get; }

    public ulong ByteLength { get; }

    public int BusCount => EndBus - StartBus + 1;
}

public readonly struct PhysicalMmioOwnershipPlanResult
{
    private PhysicalMmioOwnershipPlanResult(
        bool isInitialized,
        PhysicalMmioOwnershipPlanStatus status,
        int sourceSegmentCount,
        int rangeCount,
        int requiredRangeCapacity,
        int availableRangeCapacity,
        int failingSegmentIndex,
        int conflictingRangeIndex,
        int failingUsableRangeIndex,
        ulong totalBytes,
        PciEcamSegment failingSegment,
        ulong failingPhysicalStart,
        ulong failingPhysicalEndExclusive)
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

    public bool IsInitialized { get; }

    public PhysicalMmioOwnershipPlanStatus Status { get; }

    public bool Succeeded =>
        IsInitialized && Status == PhysicalMmioOwnershipPlanStatus.Success;

    public int SourceSegmentCount { get; }

    public int RangeCount { get; }

    public int RequiredRangeCapacity { get; }

    public int AvailableRangeCapacity { get; }

    public int FailingSegmentIndex { get; }

    public int ConflictingRangeIndex { get; }

    public int FailingUsableRangeIndex { get; }

    public ulong TotalBytes { get; }

    public PciEcamSegment FailingSegment { get; }

    public ulong FailingPhysicalStart { get; }

    public ulong FailingPhysicalEndExclusive { get; }

    public static PhysicalMmioOwnershipPlanResult Success(
        int sourceSegmentCount,
        int rangeCount,
        int availableRangeCapacity,
        ulong totalBytes)
    {
        return new PhysicalMmioOwnershipPlanResult(
            true,
            PhysicalMmioOwnershipPlanStatus.Success,
            sourceSegmentCount,
            rangeCount,
            rangeCount,
            availableRangeCapacity,
            NoIndex,
            NoIndex,
            NoIndex,
            totalBytes,
            default,
            failingPhysicalStart: 0,
            failingPhysicalEndExclusive: 0);
    }

    public static PhysicalMmioOwnershipPlanResult Fail(
        PhysicalMmioOwnershipPlanStatus status,
        int sourceSegmentCount,
        int requiredRangeCapacity,
        int availableRangeCapacity,
        int failingSegmentIndex = NoIndex,
        int conflictingRangeIndex = NoIndex,
        int failingUsableRangeIndex = NoIndex,
        ulong totalBytes = 0,
        PciEcamSegment failingSegment = default,
        ulong failingPhysicalStart = 0,
        ulong failingPhysicalEndExclusive = 0)
    {
        return new PhysicalMmioOwnershipPlanResult(
            true,
            status,
            sourceSegmentCount,
            rangeCount: 0,
            requiredRangeCapacity,
            availableRangeCapacity,
            failingSegmentIndex,
            conflictingRangeIndex,
            failingUsableRangeIndex,
            totalBytes,
            failingSegment,
            failingPhysicalStart,
            failingPhysicalEndExclusive);
    }
}

public static class PhysicalMmioOwnershipPlan
{
    public const ulong EcamWindowAlignment = PciEcamConfigurationSpace.BytesPerBus;
    public const bool MapsPages = false;
    public const bool TouchesPhysicalMemory = false;
    public const bool PerformsVolatileMmio = false;
    public const bool PerformsPciEnumeration = false;

    public static PhysicalMmioOwnershipPlanResult PlanEcam(
        ReadOnlySpan<PciEcamSegment> ecamSegments,
        ReadOnlySpan<BootMemoryUsableRange> usableMemoryRanges,
        Span<PhysicalMmioOwnershipRange> destination)
    {
        PhysicalMmioOwnershipPlanResult summary = SummarizeEcam(
            ecamSegments,
            usableMemoryRanges,
            destination.Length);
        if (!summary.Succeeded)
        {
            return summary;
        }

        for (int index = 0; index < ecamSegments.Length; index++)
        {
            PciEcamSegment segment = ecamSegments[index];
            ulong byteLength = CalculateEcamByteLengthUnchecked(segment);
            destination[index] = new PhysicalMmioOwnershipRange(
                index,
                segment.SegmentGroup,
                segment.StartBus,
                segment.EndBus,
                segment.BasePhysicalAddress,
                segment.BasePhysicalAddress + byteLength,
                byteLength);
        }

        return summary;
    }

    private static PhysicalMmioOwnershipPlanResult SummarizeEcam(
        ReadOnlySpan<PciEcamSegment> ecamSegments,
        ReadOnlySpan<BootMemoryUsableRange> usableMemoryRanges,
        int availableRangeCapacity)
    {
        if (ecamSegments.Length == 0)
        {
            return PhysicalMmioOwnershipPlanResult.Fail(
                PhysicalMmioOwnershipPlanStatus.InvalidSegmentCount,
                sourceSegmentCount: 0,
                requiredRangeCapacity: 0,
                availableRangeCapacity);
        }

        ulong totalBytes = 0;
        for (int segmentIndex = 0; segmentIndex < ecamSegments.Length; segmentIndex++)
        {
            PciEcamSegment segment = ecamSegments[segmentIndex];
            PhysicalMmioOwnershipPlanStatus status = ValidateSegment(
                segment,
                out ulong byteLength,
                out ulong physicalEndExclusive);
            if (status != PhysicalMmioOwnershipPlanStatus.Success)
            {
                return PhysicalMmioOwnershipPlanResult.Fail(
                    status,
                    ecamSegments.Length,
                    ecamSegments.Length,
                    availableRangeCapacity,
                    failingSegmentIndex: segmentIndex,
                    failingSegment: segment,
                    failingPhysicalStart: segment.BasePhysicalAddress,
                    failingPhysicalEndExclusive: physicalEndExclusive);
            }

            for (int previousIndex = 0; previousIndex < segmentIndex; previousIndex++)
            {
                PciEcamSegment previous = ecamSegments[previousIndex];
                ulong previousLength = CalculateEcamByteLengthUnchecked(previous);
                ulong previousEndExclusive = previous.BasePhysicalAddress + previousLength;
                if (RangesOverlap(
                    segment.BasePhysicalAddress,
                    physicalEndExclusive,
                    previous.BasePhysicalAddress,
                    previousEndExclusive))
                {
                    return PhysicalMmioOwnershipPlanResult.Fail(
                        PhysicalMmioOwnershipPlanStatus.OverlappingMmioRange,
                        ecamSegments.Length,
                        ecamSegments.Length,
                        availableRangeCapacity,
                        failingSegmentIndex: segmentIndex,
                        conflictingRangeIndex: previousIndex,
                        failingSegment: segment,
                        failingPhysicalStart: segment.BasePhysicalAddress,
                        failingPhysicalEndExclusive: physicalEndExclusive);
                }
            }

            status = ValidateUsableMemoryRanges(
                segment.BasePhysicalAddress,
                physicalEndExclusive,
                usableMemoryRanges,
                out int failingUsableRangeIndex);
            if (status != PhysicalMmioOwnershipPlanStatus.Success)
            {
                return PhysicalMmioOwnershipPlanResult.Fail(
                    status,
                    ecamSegments.Length,
                    ecamSegments.Length,
                    availableRangeCapacity,
                    failingSegmentIndex: segmentIndex,
                    failingUsableRangeIndex: failingUsableRangeIndex,
                    failingSegment: segment,
                    failingPhysicalStart: segment.BasePhysicalAddress,
                    failingPhysicalEndExclusive: physicalEndExclusive);
            }

            if (!TryAdd(totalBytes, byteLength, out totalBytes))
            {
                return PhysicalMmioOwnershipPlanResult.Fail(
                    PhysicalMmioOwnershipPlanStatus.TotalByteCountOverflow,
                    ecamSegments.Length,
                    ecamSegments.Length,
                    availableRangeCapacity,
                    failingSegmentIndex: segmentIndex,
                    failingSegment: segment,
                    failingPhysicalStart: segment.BasePhysicalAddress,
                    failingPhysicalEndExclusive: physicalEndExclusive);
            }
        }

        if (availableRangeCapacity < ecamSegments.Length)
        {
            return PhysicalMmioOwnershipPlanResult.Fail(
                PhysicalMmioOwnershipPlanStatus.DestinationTooSmall,
                ecamSegments.Length,
                ecamSegments.Length,
                availableRangeCapacity,
                totalBytes: totalBytes);
        }

        return PhysicalMmioOwnershipPlanResult.Success(
            ecamSegments.Length,
            ecamSegments.Length,
            availableRangeCapacity,
            totalBytes);
    }

    private static PhysicalMmioOwnershipPlanStatus ValidateSegment(
        PciEcamSegment segment,
        out ulong byteLength,
        out ulong physicalEndExclusive)
    {
        byteLength = 0;
        physicalEndExclusive = 0;

        if (segment.StartBus > segment.EndBus)
        {
            return PhysicalMmioOwnershipPlanStatus.InvalidBusRange;
        }

        if ((segment.BasePhysicalAddress % EcamWindowAlignment) != 0)
        {
            return PhysicalMmioOwnershipPlanStatus.InvalidAlignment;
        }

        byteLength = CalculateEcamByteLengthUnchecked(segment);
        if (!TryAdd(segment.BasePhysicalAddress, byteLength, out physicalEndExclusive))
        {
            return PhysicalMmioOwnershipPlanStatus.AddressRangeOverflow;
        }

        return PhysicalMmioOwnershipPlanStatus.Success;
    }

    private static PhysicalMmioOwnershipPlanStatus ValidateUsableMemoryRanges(
        ulong physicalStart,
        ulong physicalEndExclusive,
        ReadOnlySpan<BootMemoryUsableRange> usableMemoryRanges,
        out int failingUsableRangeIndex)
    {
        for (int index = 0; index < usableMemoryRanges.Length; index++)
        {
            BootMemoryUsableRange usable = usableMemoryRanges[index];
            if (usable.PhysicalStart >= usable.PhysicalEndExclusive ||
                usable.ByteLength == 0 ||
                !TryAdd(usable.PhysicalStart, usable.ByteLength, out ulong usableEndExclusive) ||
                usableEndExclusive != usable.PhysicalEndExclusive)
            {
                failingUsableRangeIndex = index;
                return PhysicalMmioOwnershipPlanStatus.InvalidUsableRange;
            }

            if (RangesOverlap(
                physicalStart,
                physicalEndExclusive,
                usable.PhysicalStart,
                usable.PhysicalEndExclusive))
            {
                failingUsableRangeIndex = index;
                return PhysicalMmioOwnershipPlanStatus.UsableMemoryOverlap;
            }
        }

        failingUsableRangeIndex = PhysicalMmioOwnershipPlanResult.NoIndex;
        return PhysicalMmioOwnershipPlanStatus.Success;
    }

    private static ulong CalculateEcamByteLengthUnchecked(PciEcamSegment segment)
    {
        ulong busCount = (ulong)(segment.EndBus - segment.StartBus + 1);
        return busCount * PciEcamConfigurationSpace.BytesPerBus;
    }

    private static bool RangesOverlap(
        ulong leftStart,
        ulong leftEndExclusive,
        ulong rightStart,
        ulong rightEndExclusive)
    {
        return leftStart < rightEndExclusive && rightStart < leftEndExclusive;
    }

    private static bool TryAdd(
        ulong left,
        ulong right,
        out ulong value)
    {
        if (left > ulong.MaxValue - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }
}
