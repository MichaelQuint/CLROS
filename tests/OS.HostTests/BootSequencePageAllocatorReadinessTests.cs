using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Memory;

internal static class BootSequencePageAllocatorReadinessTests
{
    private const ulong PageSize = BootMemoryRegionRecord.PageSize;

    [HostTest("boot.sequence page allocator readiness emits state ready marker", HostLane.Fast, 0)]
    private static bool TestPageAllocatorReady()
    {
        BootPageAllocatorStateInitResult allocatorState = CreateReadyAllocatorState();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunBootPageAllocatorReadiness(allocatorState, serial, events);
        string expectedMarker = BootSequence.CreateBootPageAllocatorReadyMarker(allocatorState);
        string[] snapshot = events.Snapshot();

        return HostCheck.Result(allocatorState.Succeeded
            && result.Succeeded
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && serial.LineAt(0).StartsWith(BootSequence.BootPageAllocatorReadyPrefix, StringComparison.Ordinal)
            && serial.LineAt(0).Contains("status=Ready state=Success freeList=Success", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("requiredNodes=2", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("availableNodes=2", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("visitedNodes=2", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("initialized=true", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("head=0", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("freeNodes=2", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("freePages=6", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("totalFreePages=6", StringComparison.Ordinal)
            && snapshot.Length == 1
            && snapshot[0] == expectedMarker);
    }

    [HostTest("boot.sequence page allocator readiness rejects state initialization failure", HostLane.Fast, 1)]
    private static bool TestPageAllocatorRejectsStateInitializationFailure()
    {
        BootPageAllocatorStateInitResult allocatorState = CreateFailedAllocatorState();
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunBootPageAllocatorReadiness(allocatorState, serial, events);
        string expectedMarker = BootSequence.CreateBootPageAllocatorNotReadyMarker(
            BootPageAllocatorReadinessStatus.StateInitializationFailed,
            allocatorState);
        string[] snapshot = events.Snapshot();

        return HostCheck.Result(!allocatorState.Succeeded
            && allocatorState.Status == BootPageAllocatorStateStatus.FreeListPlanFailed
            && allocatorState.FreeListPlanStatus == BootPageFreeListPlanStatus.AllocatorPlanFailed
            && !result.Succeeded
            && result.Reason.Contains(
                nameof(BootPageAllocatorReadinessStatus.StateInitializationFailed),
                StringComparison.Ordinal)
            && result.Reason.Contains(nameof(BootPageAllocatorStateStatus.FreeListPlanFailed), StringComparison.Ordinal)
            && result.Reason.Contains(nameof(BootPageFreeListPlanStatus.AllocatorPlanFailed), StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && serial.LineAt(0).StartsWith(BootSequence.BootPageAllocatorNotReadyPrefix, StringComparison.Ordinal)
            && serial.LineAt(0).Contains("status=StateInitializationFailed", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("state=FreeListPlanFailed", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("freeList=AllocatorPlanFailed", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("initialized=false", StringComparison.Ordinal)
            && snapshot.Length == 1
            && snapshot[0] == expectedMarker);
    }

    [HostTest("boot.sequence page allocator readiness rejects default state", HostLane.Fast, 2)]
    private static bool TestPageAllocatorRejectsDefaultState()
    {
        BootPageAllocatorStateInitResult allocatorState = default;
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunBootPageAllocatorReadiness(allocatorState, serial, events);
        string expectedMarker = BootSequence.CreateBootPageAllocatorNotReadyMarker(
            BootPageAllocatorReadinessStatus.StateNotInitialized,
            allocatorState);

        return HostCheck.Result(!allocatorState.Succeeded
            && allocatorState.Status == BootPageAllocatorStateStatus.Success
            && allocatorState.State.IsInitialized == false
            && !result.Succeeded
            && result.Reason.Contains(nameof(BootPageAllocatorReadinessStatus.StateNotInitialized), StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && serial.LineAt(0).Contains("status=StateNotInitialized", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("state=Success", StringComparison.Ordinal)
            && serial.LineAt(0).Contains("initialized=false", StringComparison.Ordinal));
    }

    [HostTest("boot.sequence page allocator readiness rejects empty state", HostLane.Fast, 3)]
    private static bool TestPageAllocatorRejectsEmptyState()
    {
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[0];
        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[0];
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[0];
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[0];
        BootPageAllocatorPlanResult allocatorPlan = BootPageAllocatorPlanner.Plan(
            PageSize,
            usable,
            reserved,
            freeSegments);
        BootPageFreeListPlanResult freeListPlan = BootPageFreeListBuilder.Build(
            allocatorPlan,
            freeSegments,
            nodes);
        BootPageAllocatorStateInitResult allocatorState = BootPageAllocatorStateInitializer.Initialize(
            freeListPlan,
            nodes);
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(8);

        BootResult result = BootSequence.RunBootPageAllocatorReadiness(allocatorState, serial, events);
        string expectedMarker = BootSequence.CreateBootPageAllocatorNotReadyMarker(
            BootPageAllocatorReadinessStatus.NoFreeNodes,
            allocatorState);

        return HostCheck.Result(allocatorPlan.Succeeded
            && freeListPlan.Succeeded
            && allocatorState.Status == BootPageAllocatorStateStatus.NoFreeNodes
            && !result.Succeeded
            && result.Reason.Contains(nameof(BootPageAllocatorReadinessStatus.NoFreeNodes), StringComparison.Ordinal)
            && serial.Count == 1
            && serial.LineAt(0) == expectedMarker
            && serial.LineAt(0).Contains("status=NoFreeNodes state=NoFreeNodes freeList=Success", StringComparison.Ordinal));
    }

    [HostTest("boot.sequence page allocator readiness preserves default markers", HostLane.Fast, 4)]
    private static bool TestPageAllocatorPreservesDefaultMarkers()
    {
        SequenceTestSerialSink serial = new SequenceTestSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);

        if (!result.Succeeded || serial.Count != BootSequencePlan.LineCount)
        {
            return HostCheck.Result(false);
        }

        for (int lineIndex = 0; lineIndex < BootSequencePlan.LineCount; lineIndex++)
        {
            if (serial.LineAt(lineIndex) != BootSequence.CreatePlanLine(lineIndex))
            {
                return HostCheck.Result(false);
            }
        }

        return HostCheck.Result(!serial.ContainsPrefix(BootSequence.BootPageAllocatorReadyPrefix)
            && !serial.ContainsPrefix(BootSequence.BootPageAllocatorNotReadyPrefix));
    }

    private static BootPageAllocatorPlanResult CreateReadyAllocatorPlan(Span<BootPageFreeSegment> freeSegments)
    {
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[2];
        usable[0] = Usable(sourceRegionIndex: 1, physicalStart: 0x100000, pageCount: 4);
        usable[1] = Usable(sourceRegionIndex: 2, physicalStart: 0x200000, pageCount: 2);

        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[1];
        reserved[0] = Reserved(sourceRegionIndex: 0, physicalStart: 0x80000, pageCount: 1);

        return BootPageAllocatorPlanner.Plan(PageSize, usable, reserved, freeSegments);
    }

    private static BootPageAllocatorStateInitResult CreateReadyAllocatorState()
    {
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[2];
        BootPageAllocatorPlanResult allocatorPlan = CreateReadyAllocatorPlan(freeSegments);
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[2];
        BootPageFreeListPlanResult freeListPlan = BootPageFreeListBuilder.Build(
            allocatorPlan,
            freeSegments,
            nodes);
        return BootPageAllocatorStateInitializer.Initialize(freeListPlan, nodes);
    }

    private static BootPageAllocatorStateInitResult CreateFailedAllocatorState()
    {
        Span<BootMemoryUsableRange> usable = stackalloc BootMemoryUsableRange[1];
        usable[0] = Usable(sourceRegionIndex: 1, physicalStart: 0x100000, pageCount: 1);

        Span<BootMemoryReservedRange> reserved = stackalloc BootMemoryReservedRange[0];
        Span<BootPageFreeSegment> freeSegments = stackalloc BootPageFreeSegment[1];
        BootPageAllocatorPlanResult allocatorPlan = BootPageAllocatorPlanner.Plan(
            pageSize: 0,
            usable,
            reserved,
            freeSegments);
        Span<BootPageFreeListNode> nodes = stackalloc BootPageFreeListNode[1];
        BootPageFreeListPlanResult freeListPlan = BootPageFreeListBuilder.Build(
            allocatorPlan,
            freeSegments,
            nodes);
        return BootPageAllocatorStateInitializer.Initialize(freeListPlan, nodes);
    }

    private static BootMemoryUsableRange Usable(
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

    private static BootMemoryReservedRange Reserved(
        int sourceRegionIndex,
        ulong physicalStart,
        ulong pageCount)
    {
        ulong byteLength = pageCount * PageSize;
        return new BootMemoryReservedRange(
            sourceRegionIndex,
            (uint)UefiMemoryType.LoaderData,
            physicalStart,
            physicalStart + byteLength,
            pageCount,
            byteLength,
            attribute: 0);
    }

    private sealed class SequenceTestSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }

        public bool ContainsPrefix(string value)
        {
            for (int index = 0; index < _lines.Count; index++)
            {
                if (_lines[index].StartsWith(value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
