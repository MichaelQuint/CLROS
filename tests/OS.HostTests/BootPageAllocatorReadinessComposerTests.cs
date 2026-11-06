using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

internal static partial class BootPageAllocatorReadinessComposerTests
{
    private const ulong PageSize = BootMemoryRegionRecord.PageSize;
    [HostTest("memory.boot allocator readiness composes", HostLane.Fast, 0)]
    private static bool TestComposesReadiness()
    {
        Span<BootMemoryRegionRecord> source = stackalloc BootMemoryRegionRecord[4];
        source[0] = Region(UefiMemoryType.Reserved, 0, 1);
        source[1] = Region(UefiMemoryType.ConventionalMemory, 0x100000, 4);
        source[2] = Region(UefiMemoryType.BootServicesData, 0x200000, 2);
        source[3] = Region(UefiMemoryType.ConventionalMemory, 0x300000, 6);
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[2];
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[2];
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[2];
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);
        BootPageAllocatorReadinessComposeResult result = BootPageAllocatorReadinessComposer.ComposeAndRun(source, PageSize, usable, reserved, freeSegments, nodes, serial, events);
        string expectedMarker = BootSequence.CreateBootPageAllocatorReadyMarker(result.AllocatorState);
        string[] snapshot = events.Snapshot();
        return HostCheck.Result(result.Succeeded && result.Status == BootPageAllocatorReadinessComposeStatus.Success && result.RegionPlan.Status == BootMemoryRegionPlanStatus.Success && result.RegionPlan.UsableRangeCount == 2 && result.RegionPlan.ReservedRangeCount == 2 && result.AllocatorPlan.Status == BootPageAllocatorPlanStatus.Success && result.AllocatorPlan.FreeSegmentCount == 2 && result.FreeListPlan.Status == BootPageFreeListPlanStatus.Success && result.AllocatorState.Status == BootPageAllocatorStateStatus.Success && result.AllocatorState.State.IsInitialized && result.AllocatorState.State.FreeNodeCount == 2 && result.AllocatorState.State.FreePageCount == 10 && result.BootReadinessInvoked && result.BootReadinessSucceeded && serial.Count == 1 && serial.LineAt(0) == expectedMarker && serial.LineAt(0).StartsWith(BootSequence.BootPageAllocatorReadyPrefix, StringComparison.Ordinal) && snapshot.Length == 1 && snapshot[0] == expectedMarker && !RuntimeHelpers.IsReferenceOrContainsReferences<BootPageAllocatorReadinessComposeResult>());
    }

    [HostTest("memory.boot allocator readiness reports scratch capacity", HostLane.Fast, 1)]
    private static bool TestReportsScratchCapacity()
    {
        Span<BootMemoryRegionRecord> source = stackalloc BootMemoryRegionRecord[3];
        source[0] = Region(UefiMemoryType.ConventionalMemory, 0x100000, 1);
        source[1] = Region(UefiMemoryType.BootServicesData, 0x200000, 1);
        source[2] = Region(UefiMemoryType.ConventionalMemory, 0x300000, 1);
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[1];
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[1];
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[1];
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[1];
        BootPageFreeSegment segmentSentinel = SentinelSegment();
        BootPageFreeListNode nodeSentinel = SentinelNode();
        freeSegments[0] = segmentSentinel;
        nodes[0] = nodeSentinel;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);
        BootPageAllocatorReadinessComposeResult result = BootPageAllocatorReadinessComposer.ComposeAndRun(source, PageSize, usable, reserved, freeSegments, nodes, serial, events);
        return HostCheck.Result(!result.Succeeded && result.Status == BootPageAllocatorReadinessComposeStatus.MemoryRegionPlanFailed && result.RegionPlan.Status == BootMemoryRegionPlanStatus.UsableDestinationTooSmall && result.RegionPlan.RequiredUsableRangeCapacity == 2 && result.RegionPlan.AvailableUsableRangeCapacity == 1 && !result.BootReadinessInvoked && !result.BootReadinessSucceeded && serial.Count == 0 && events.Snapshot().Length == 0 && freeSegments[0].PhysicalStart == segmentSentinel.PhysicalStart && nodes[0].PhysicalStart == nodeSentinel.PhysicalStart && nodes[0].NextNodeIndex == nodeSentinel.NextNodeIndex);
    }

    [HostTest("memory.boot allocator readiness rejects invalid regions", HostLane.Fast, 2)]
    private static bool TestRejectsInvalidRegions()
    {
        Span<BootMemoryRegionRecord> source = stackalloc BootMemoryRegionRecord[2];
        source[0] = Region(UefiMemoryType.ConventionalMemory, 0x100000, 4);
        source[1] = Region(UefiMemoryType.BootServicesData, 0x103000, 2);
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[2];
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[1];
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[2];
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);
        BootPageAllocatorReadinessComposeResult result = BootPageAllocatorReadinessComposer.ComposeAndRun(source, PageSize, usable, reserved, freeSegments, nodes, serial, events);
        return HostCheck.Result(!result.Succeeded && result.Status == BootPageAllocatorReadinessComposeStatus.MemoryRegionPlanFailed && result.RegionPlan.Status == BootMemoryRegionPlanStatus.OverlappingRange && result.RegionPlan.InvalidRegionIndex == 1 && !result.BootReadinessInvoked && serial.Count == 0 && events.Snapshot().Length == 0);
    }

    [HostTest("memory.boot allocator readiness emits boot marker metadata", HostLane.Fast, 3)]
    private static bool TestEmitsBootMarkerMetadata()
    {
        Span<BootMemoryRegionRecord> source = stackalloc BootMemoryRegionRecord[1];
        source[0] = Region(UefiMemoryType.ConventionalMemory, 0x100000, 1);
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[1];
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[0];
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[1];
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[1];
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);
        BootPageAllocatorReadinessComposeResult result = BootPageAllocatorReadinessComposer.ComposeAndRun(source, pageSize: 0, usable, reserved, freeSegments, nodes, serial, events);
        string expectedMarker = BootSequence.CreateBootPageAllocatorNotReadyMarker(BootPageAllocatorReadinessStatus.StateInitializationFailed, result.AllocatorState);
        return HostCheck.Result(!result.Succeeded && result.Status == BootPageAllocatorReadinessComposeStatus.AllocatorPlanFailed && result.RegionPlan.Succeeded && result.AllocatorPlan.Status == BootPageAllocatorPlanStatus.InvalidPageSize && result.FreeListPlan.Status == BootPageFreeListPlanStatus.AllocatorPlanFailed && result.AllocatorState.Status == BootPageAllocatorStateStatus.FreeListPlanFailed && result.BootReadinessInvoked && !result.BootReadinessSucceeded && serial.Count == 1 && serial.LineAt(0) == expectedMarker && serial.LineAt(0).StartsWith(BootSequence.BootPageAllocatorNotReadyPrefix, StringComparison.Ordinal) && serial.LineAt(0).Contains("status=StateInitializationFailed state=FreeListPlanFailed freeList=AllocatorPlanFailed", StringComparison.Ordinal) && events.Snapshot().Length == 1 && events.Snapshot()[0] == expectedMarker);
    }

    private static BootMemoryRegionRecord Region(UefiMemoryType type, ulong physicalStart, ulong pageCount)
    {
        ulong byteLength = pageCount * PageSize;
        return new BootMemoryRegionRecord((uint)type, physicalStart, virtualStart: 0, pageCount, attribute: 0, byteLength, physicalStart + byteLength);
    }

    private static BootPageFreeSegment SentinelSegment()
    {
        return new BootPageFreeSegment(99, 0xDEAD0000, 0xDEAD1000, 1, PageSize);
    }

    private static BootPageFreeListNode SentinelNode()
    {
        return new BootPageFreeListNode(sourceFreeSegmentIndex: 99, sourceUsableRangeIndex: 98, physicalStart: 0xBEEF0000, physicalEndExclusive: 0xBEEF1000, pageCount: 1, byteLength: PageSize, nextNodeIndex: 77);
    }
}
