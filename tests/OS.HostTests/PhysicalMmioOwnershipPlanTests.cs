using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Drivers;
using OS.Kernel.Memory;
using OS.Kernel.Platform;

internal static class PhysicalMmioOwnershipPlanTests
{
    private const ulong PageSize = BootMemoryRegionRecord.PageSize;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("platform.mmio ownership accepts ecam", TestAcceptsEcam, ref failures);
        Run("platform.mmio ownership rejects usable-memory overlap", TestRejectsUsableMemoryOverlap, ref failures);
        Run("platform.mmio ownership reports capacity", TestReportsCapacity, ref failures);
        Run("platform.mmio ownership rejects arithmetic overflow", TestRejectsArithmeticOverflow, ref failures);
        Run("platform.mmio ownership default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Physical MMIO ownership plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestAcceptsEcam()
    {
        PciEcamSegment[] segments =
        {
            new PciEcamSegment(0xE0000000, segmentGroup: 0, startBus: 0, endBus: 0x7F),
            new PciEcamSegment(0xF0000000, segmentGroup: 1, startBus: 0, endBus: 0x0F)
        };
        BootMemoryUsableRange[] usable =
        {
            UsableRange(0, 0x100000, 0x200)
        };
        PhysicalMmioOwnershipRange[] ranges = new PhysicalMmioOwnershipRange[2];

        PhysicalMmioOwnershipPlanResult result =
            PhysicalMmioOwnershipPlan.PlanEcam(segments, usable, ranges);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == PhysicalMmioOwnershipPlanStatus.Success
            && result.SourceSegmentCount == 2
            && result.RangeCount == 2
            && result.RequiredRangeCapacity == 2
            && result.AvailableRangeCapacity == 2
            && result.FailingSegmentIndex == PhysicalMmioOwnershipPlanResult.NoIndex
            && result.TotalBytes == (144UL * PciEcamConfigurationSpace.BytesPerBus)
            && ranges[0].SourceSegmentIndex == 0
            && ranges[0].SegmentGroup == 0
            && ranges[0].PhysicalStart == 0xE0000000
            && ranges[0].PhysicalEndExclusive == 0xE8000000
            && ranges[0].ByteLength == 128UL * PciEcamConfigurationSpace.BytesPerBus
            && ranges[0].BusCount == 128
            && ranges[1].SourceSegmentIndex == 1
            && ranges[1].SegmentGroup == 1
            && ranges[1].PhysicalEndExclusive == 0xF1000000
            && !PhysicalMmioOwnershipPlan.MapsPages
            && !PhysicalMmioOwnershipPlan.TouchesPhysicalMemory
            && !PhysicalMmioOwnershipPlan.PerformsVolatileMmio
            && !PhysicalMmioOwnershipPlan.PerformsPciEnumeration
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PhysicalMmioOwnershipRange>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<PhysicalMmioOwnershipPlanResult>();
    }

    private static bool TestRejectsUsableMemoryOverlap()
    {
        PciEcamSegment[] segments =
        {
            new PciEcamSegment(0x200000, segmentGroup: 0, startBus: 0, endBus: 0)
        };
        BootMemoryUsableRange[] usable =
        {
            UsableRange(3, 0x280000, 2)
        };
        PhysicalMmioOwnershipRange sentinel = new PhysicalMmioOwnershipRange(
            sourceSegmentIndex: 99,
            segmentGroup: 7,
            startBus: 0,
            endBus: 0,
            physicalStart: 0xDEAD0000,
            physicalEndExclusive: 0xDEAE0000,
            byteLength: PciEcamConfigurationSpace.BytesPerBus);
        PhysicalMmioOwnershipRange[] ranges = { sentinel };

        PhysicalMmioOwnershipPlanResult result =
            PhysicalMmioOwnershipPlan.PlanEcam(segments, usable, ranges);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == PhysicalMmioOwnershipPlanStatus.UsableMemoryOverlap
            && result.FailingSegmentIndex == 0
            && result.FailingUsableRangeIndex == 0
            && result.FailingPhysicalStart == 0x200000
            && result.FailingPhysicalEndExclusive == 0x300000
            && result.FailingSegment.BasePhysicalAddress == 0x200000
            && ranges[0].SourceSegmentIndex == sentinel.SourceSegmentIndex
            && ranges[0].PhysicalStart == sentinel.PhysicalStart;
    }

    private static bool TestReportsCapacity()
    {
        PciEcamSegment[] segments =
        {
            new PciEcamSegment(0xE0000000, segmentGroup: 0, startBus: 0, endBus: 0),
            new PciEcamSegment(0xE1000000, segmentGroup: 1, startBus: 0, endBus: 0)
        };
        PhysicalMmioOwnershipRange sentinel = new PhysicalMmioOwnershipRange(
            sourceSegmentIndex: 88,
            segmentGroup: 9,
            startBus: 0,
            endBus: 0,
            physicalStart: 0xABCD0000,
            physicalEndExclusive: 0xABDD0000,
            byteLength: PciEcamConfigurationSpace.BytesPerBus);
        PhysicalMmioOwnershipRange[] ranges = { sentinel };

        PhysicalMmioOwnershipPlanResult result =
            PhysicalMmioOwnershipPlan.PlanEcam(segments, ReadOnlySpan<BootMemoryUsableRange>.Empty, ranges);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == PhysicalMmioOwnershipPlanStatus.DestinationTooSmall
            && result.SourceSegmentCount == 2
            && result.RequiredRangeCapacity == 2
            && result.AvailableRangeCapacity == 1
            && result.TotalBytes == 2UL * PciEcamConfigurationSpace.BytesPerBus
            && ranges[0].SourceSegmentIndex == sentinel.SourceSegmentIndex
            && ranges[0].PhysicalStart == sentinel.PhysicalStart;
    }

    private static bool TestRejectsArithmeticOverflow()
    {
        PciEcamSegment[] segments =
        {
            new PciEcamSegment(0xFFFFFFFFFFF00000, segmentGroup: 0, startBus: 0, endBus: 0)
        };
        PhysicalMmioOwnershipRange[] ranges = new PhysicalMmioOwnershipRange[1];

        PhysicalMmioOwnershipPlanResult result =
            PhysicalMmioOwnershipPlan.PlanEcam(segments, ReadOnlySpan<BootMemoryUsableRange>.Empty, ranges);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == PhysicalMmioOwnershipPlanStatus.AddressRangeOverflow
            && result.FailingSegmentIndex == 0
            && result.FailingSegment.BasePhysicalAddress == 0xFFFFFFFFFFF00000
            && result.FailingPhysicalStart == 0xFFFFFFFFFFF00000
            && result.FailingPhysicalEndExclusive == 0;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        PhysicalMmioOwnershipPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == PhysicalMmioOwnershipPlanStatus.Success
            && result.SourceSegmentCount == 0
            && result.RangeCount == 0
            && result.RequiredRangeCapacity == 0
            && result.AvailableRangeCapacity == 0
            && result.FailingSegmentIndex == 0
            && result.ConflictingRangeIndex == 0
            && result.FailingUsableRangeIndex == 0
            && result.TotalBytes == 0
            && result.FailingPhysicalStart == 0
            && result.FailingPhysicalEndExclusive == 0;
    }

    private static BootMemoryUsableRange UsableRange(
        int sourceRegionIndex,
        ulong physicalStart,
        ulong pageCount)
    {
        ulong byteLength = pageCount * PageSize;
        return new BootMemoryUsableRange(
            sourceRegionIndex,
            physicalStart,
            physicalStart + byteLength,
            pageCount,
            byteLength,
            attribute: 0);
    }
}
