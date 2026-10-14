using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Memory;

internal static class BootPageAllocatorStateMaterializerTests
{
    private const string IssueTestName = "m01.006";
    private const ulong PageSize =
        BootPageAllocatorStateMaterializer.RequiredPageSize;

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run(
            "m01.006 statuses constants and pod layouts are stable",
            TestStableSurfaceAndLayouts,
            ref failures);
        Run(
            "m01.006 default values fail closed",
            TestDefaultsFailClosed,
            ref failures);
        Run(
            "m01.006 nontrivial head materializes exact caller state",
            TestSuccessfulNontrivialHead,
            ref failures);
        Run(
            "m01.006 host initializer remains compatible",
            TestHostInitializerCompatibility,
            ref failures);
        Run(
            "m01.006 poison pointer preflight is deterministic",
            TestPoisonPointerPreflight,
            ref failures);
        Run(
            "m01.006 page range and expected metadata fail closed",
            TestPageRangeAndExpectedMetadata,
            ref failures);
        Run(
            "m01.006 malformed nodes are classified",
            TestMalformedNodes,
            ref failures);
        Run(
            "m01.006 head and next indexes are bounded",
            TestHeadAndNextIndexes,
            ref failures);
        Run(
            "m01.006 self and multi node cycles are rejected",
            TestCycles,
            ref failures);
        Run(
            "m01.006 short and disconnected chains are rejected",
            TestShortAndDisconnectedChains,
            ref failures);
        Run(
            "m01.006 provenance physical ordering and adjacency are exact",
            TestProvenanceAndPhysicalOrdering,
            ref failures);
        Run(
            "m01.006 page and byte totals are exact",
            TestTotals,
            ref failures);
        Run(
            "m01.006 materialization is atomic deterministic and allocation free",
            TestAtomicityDeterminismAndAllocation,
            ref failures);
        Run(
            "m01.006 source and integration boundary is enforced",
            TestSourceAndIntegrationBoundary,
            ref failures);
        Run(
            "m01.006 host readiness marker is unique",
            TestMarkerUniqueness,
            ref failures);
        Run(
            "m01.006 exact sources compile under nostdlib efi",
            TestNoStdLibEfiCompileClosure,
            ref failures);

        if (failures != 0)
        {
            Console.WriteLine(
                "TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-006 allocator-state materializer host tests failed: "
                + failures);
        }

        Console.WriteLine("CLROS_M01_006_READY");
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    private static void Run(
        string name,
        Func<bool> test,
        ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestStableSurfaceAndLayouts()
    {
        BootPageAllocatorStateMaterializationStatus[] statuses =
        [
            BootPageAllocatorStateMaterializationStatus.Uninitialized,
            BootPageAllocatorStateMaterializationStatus.Success,
            BootPageAllocatorStateMaterializationStatus.InvalidSourceNodeCount,
            BootPageAllocatorStateMaterializationStatus
                .SourceNodeTableByteCountOverflow,
            BootPageAllocatorStateMaterializationStatus.TooManySourceNodes,
            BootPageAllocatorStateMaterializationStatus
                .SourceNodeCapacityTooSmall,
            BootPageAllocatorStateMaterializationStatus.MissingSourceNodes,
            BootPageAllocatorStateMaterializationStatus.MisalignedSourceNodes,
            BootPageAllocatorStateMaterializationStatus
                .SourceNodeTableAddressRangeOverflow,
            BootPageAllocatorStateMaterializationStatus
                .DestinationStateCapacityTooSmall,
            BootPageAllocatorStateMaterializationStatus
                .DestinationStateByteCountOverflow,
            BootPageAllocatorStateMaterializationStatus
                .MissingDestinationState,
            BootPageAllocatorStateMaterializationStatus
                .MisalignedDestinationState,
            BootPageAllocatorStateMaterializationStatus
                .DestinationStateAddressRangeOverflow,
            BootPageAllocatorStateMaterializationStatus
                .DestinationOverlapsSourceNodes,
            BootPageAllocatorStateMaterializationStatus.InvalidPageSize,
            BootPageAllocatorStateMaterializationStatus
                .InvalidSourceRangeCount,
            BootPageAllocatorStateMaterializationStatus.InvalidHeadNodeIndex,
            BootPageAllocatorStateMaterializationStatus.NoFreePages,
            BootPageAllocatorStateMaterializationStatus
                .ExpectedByteCountOverflow,
            BootPageAllocatorStateMaterializationStatus
                .ExpectedByteCountMismatch,
            BootPageAllocatorStateMaterializationStatus
                .InvalidSourceFreeSegmentIndex,
            BootPageAllocatorStateMaterializationStatus
                .InvalidSourceRangeIndex,
            BootPageAllocatorStateMaterializationStatus
                .UnsortedSourceRangeIndex,
            BootPageAllocatorStateMaterializationStatus.InvalidNodePageCount,
            BootPageAllocatorStateMaterializationStatus
                .InvalidNodePhysicalStart,
            BootPageAllocatorStateMaterializationStatus
                .UnalignedNodePhysicalStart,
            BootPageAllocatorStateMaterializationStatus.NodeByteCountOverflow,
            BootPageAllocatorStateMaterializationStatus.NodeByteLengthMismatch,
            BootPageAllocatorStateMaterializationStatus
                .NodeAddressRangeOverflow,
            BootPageAllocatorStateMaterializationStatus
                .NodePhysicalEndMismatch,
            BootPageAllocatorStateMaterializationStatus.UnsortedNode,
            BootPageAllocatorStateMaterializationStatus.OverlappingNode,
            BootPageAllocatorStateMaterializationStatus.InvalidNextNodeIndex,
            BootPageAllocatorStateMaterializationStatus.FreeListCycleDetected,
            BootPageAllocatorStateMaterializationStatus
                .FreeListNodeCountMismatch,
            BootPageAllocatorStateMaterializationStatus
                .TotalPageCountMismatch,
            BootPageAllocatorStateMaterializationStatus
                .TotalByteCountMismatch,
            BootPageAllocatorStateMaterializationStatus
                .InvalidMaterializedState
        ];

        if (statuses.Length != 39)
        {
            return false;
        }

        for (int index = 0; index < statuses.Length; index++)
        {
            if ((byte)statuses[index] != index)
            {
                return false;
            }
        }

        return BootPageFreeListNode.StructureSize == 48
            && BootPageAllocatorState.StructureSize == 56
            && BootPageFreeListNode.StructureSize
                == (ulong)Unsafe.SizeOf<BootPageFreeListNode>()
            && BootPageAllocatorState.StructureSize
                == (ulong)Unsafe.SizeOf<BootPageAllocatorState>()
            && BootPageAllocatorStateMaterializer.NoNodeIndex == -1
            && BootPageAllocatorStateMaterializer.MaximumSourceNodeCount
                == 4096
            && BootPageAllocatorStateMaterializer.MaximumSourceRangeCount
                == 4096
            && BootPageAllocatorStateMaterializer
                    .RequiredDestinationStateCapacity
                == 1
            && BootPageAllocatorStateMaterializer.RequiredPointerAlignment
                == 8
            && BootPageAllocatorStateMaterializer.RequiredPageSize == 4096
            && Unsafe.SizeOf<BootPageFreeListNode>() == 48
            && Unsafe.SizeOf<BootPageAllocatorState>() == 56
            && Unsafe.SizeOf<
                BootPageAllocatorStateMaterializationResult>() == 176
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<BootPageFreeListNode>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<BootPageAllocatorState>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                BootPageAllocatorStateMaterializationResult>()
            && HasLayout<BootPageFreeListNode>(
                48,
                [
                    ("<SourceFreeSegmentIndex>k__BackingField", 0),
                    ("<SourceUsableRangeIndex>k__BackingField", 4),
                    ("<PhysicalStart>k__BackingField", 8),
                    ("<PhysicalEndExclusive>k__BackingField", 16),
                    ("<PageCount>k__BackingField", 24),
                    ("<ByteLength>k__BackingField", 32),
                    ("<NextNodeIndex>k__BackingField", 40)
                ])
            && HasLayout<BootPageAllocatorState>(
                56,
                [
                    ("<IsInitialized>k__BackingField", 0),
                    ("<PageSize>k__BackingField", 8),
                    ("<HeadNodeIndex>k__BackingField", 16),
                    ("<FreeNodeCount>k__BackingField", 20),
                    ("<FreePageCount>k__BackingField", 24),
                    ("<FreeByteCount>k__BackingField", 32),
                    ("<AllocatedPageCount>k__BackingField", 40),
                    ("<AllocatedByteCount>k__BackingField", 48)
                ])
            && HasLayout<BootPageAllocatorStateMaterializationResult>(
                176,
                [
                    ("_reserved0", 0),
                    ("_reserved1", 1),
                    ("_reserved2", 2),
                    ("_reserved3", 3),
                    ("_reserved4", 4),
                    ("_reserved5", 5),
                    ("_reserved6", 6),
                    ("<Status>k__BackingField", 7),
                    ("<SourceNodeCount>k__BackingField", 8),
                    ("<SourceNodeCapacity>k__BackingField", 16),
                    ("<SourceRangeCount>k__BackingField", 24),
                    ("<RequiredDestinationStateCapacity>k__BackingField", 32),
                    ("<AvailableDestinationStateCapacity>k__BackingField", 40),
                    ("<MaterializedStateCount>k__BackingField", 48),
                    ("<HeadNodeIndex>k__BackingField", 56),
                    ("<InvalidNodeIndex>k__BackingField", 60),
                    ("<ConflictingNodeIndex>k__BackingField", 64),
                    ("<VisitedNodeCount>k__BackingField", 72),
                    ("<PageSize>k__BackingField", 80),
                    ("<ExpectedFreePages>k__BackingField", 88),
                    ("<ExpectedFreeBytes>k__BackingField", 96),
                    ("<ObservedFreePages>k__BackingField", 104),
                    ("<ObservedFreeBytes>k__BackingField", 112),
                    ("<State>k__BackingField", 120)
                ]);
    }

    private static unsafe bool TestDefaultsFailClosed()
    {
        BootPageAllocatorStateMaterializationResult defaultResult = default;
        BootPageAllocatorState defaultState = default;
        BootPageFreeListNode defaultNode = default;
        BootPageAllocatorState destination = SentinelState();
        byte[] before = StructBytes(destination);

        BootPageAllocatorStateMaterializationResult materialized =
            BootPageAllocatorStateMaterializer.Materialize(
                null,
                sourceNodeCount: 0,
                sourceNodeCapacity: 0,
                sourceRangeCount: 0,
                headNodeIndex: 0,
                pageSize: 0,
                expectedFreePages: 0,
                expectedFreeBytes: 0,
                &destination,
                destinationStateCapacity: 0);

        return defaultResult.Status
                == BootPageAllocatorStateMaterializationStatus.Uninitialized
            && !defaultResult.Succeeded
            && defaultResult.MaterializedStateCount == 0
            && !defaultResult.State.IsInitialized
            && !defaultState.IsInitialized
            && defaultState.PageSize == 0
            && defaultNode.PageCount == 0
            && materialized.Status
                == BootPageAllocatorStateMaterializationStatus
                    .InvalidSourceNodeCount
            && !materialized.Succeeded
            && materialized.VisitedNodeCount == 0
            && materialized.MaterializedStateCount == 0
            && before.AsSpan().SequenceEqual(StructBytes(destination));
    }

    private static unsafe bool TestSuccessfulNontrivialHead()
    {
        BootPageFreeListNode* nodes =
            stackalloc BootPageFreeListNode[4];
        nodes[0] = Node(
            sourceFreeSegmentIndex: 1,
            sourceRangeIndex: 1,
            physicalStart: 0x0030_0000,
            pageCount: 3,
            nextNodeIndex: 1);
        nodes[1] = Node(
            sourceFreeSegmentIndex: 2,
            sourceRangeIndex: 2,
            physicalStart: 0x0040_0000,
            pageCount: 4,
            nextNodeIndex: -1);
        nodes[2] = Node(
            sourceFreeSegmentIndex: 0,
            sourceRangeIndex: 0,
            physicalStart: 0x0010_0000,
            pageCount: 2,
            nextNodeIndex: 0);
        nodes[3] = new BootPageFreeListNode(
            sourceFreeSegmentIndex: 77,
            sourceUsableRangeIndex: 77,
            physicalStart: 1,
            physicalEndExclusive: 2,
            pageCount: 0,
            byteLength: 3,
            nextNodeIndex: 77);

        BootPageAllocatorState* destination =
            stackalloc BootPageAllocatorState[2];
        destination[0] = SentinelState();
        destination[1] = SentinelState();
        byte[] sourceBefore = PointerBytes(nodes, 4);
        byte[] trailingBefore = StructBytes(destination[1]);

        BootPageAllocatorStateMaterializationResult result =
            BootPageAllocatorStateMaterializer.Materialize(
                nodes,
                sourceNodeCount: 3,
                sourceNodeCapacity: 4,
                sourceRangeCount: 3,
                headNodeIndex: 2,
                PageSize,
                expectedFreePages: 9,
                expectedFreeBytes: 9 * PageSize,
                destination,
                destinationStateCapacity: 2);

        return result.Succeeded
            && result.Status
                == BootPageAllocatorStateMaterializationStatus.Success
            && result.SourceNodeCount == 3
            && result.SourceNodeCapacity == 4
            && result.SourceRangeCount == 3
            && result.RequiredDestinationStateCapacity == 1
            && result.AvailableDestinationStateCapacity == 2
            && result.MaterializedStateCount == 1
            && result.HeadNodeIndex == 2
            && result.InvalidNodeIndex == -1
            && result.ConflictingNodeIndex == -1
            && result.VisitedNodeCount == 3
            && result.PageSize == PageSize
            && result.ExpectedFreePages == 9
            && result.ExpectedFreeBytes == 9 * PageSize
            && result.ObservedFreePages == 9
            && result.ObservedFreeBytes == 9 * PageSize
            && StatesEqual(result.State, destination[0])
            && destination[0].IsInitialized
            && destination[0].PageSize == PageSize
            && destination[0].HeadNodeIndex == 2
            && destination[0].FreeNodeCount == 3
            && destination[0].FreePageCount == 9
            && destination[0].FreeByteCount == 9 * PageSize
            && destination[0].AllocatedPageCount == 0
            && destination[0].AllocatedByteCount == 0
            && sourceBefore.AsSpan().SequenceEqual(
                PointerBytes(nodes, 4))
            && trailingBefore.AsSpan().SequenceEqual(
                StructBytes(destination[1]));
    }

    private static unsafe bool TestHostInitializerCompatibility()
    {
        Span<BootMemoryUsableRange> usable =
            stackalloc BootMemoryUsableRange[2];
        usable[0] = Usable(0, 0x0010_0000, 4);
        usable[1] = Usable(1, 0x0030_0000, 6);
        Span<BootMemoryReservedRange> reserved =
            stackalloc BootMemoryReservedRange[1];
        reserved[0] = Reserved(2, 0x0020_0000, 1);
        Span<BootPageFreeSegment> freeSegments =
            stackalloc BootPageFreeSegment[2];

        BootPageAllocatorPlanResult allocatorPlan =
            BootPageAllocatorPlanner.Plan(
                PageSize,
                usable,
                reserved,
                freeSegments);
        Span<BootPageFreeListNode> nodes =
            stackalloc BootPageFreeListNode[2];
        BootPageFreeListPlanResult freeListPlan =
            BootPageFreeListBuilder.Build(
                allocatorPlan,
                freeSegments,
                nodes);
        BootPageAllocatorStateInitResult initialized =
            BootPageAllocatorStateInitializer.Initialize(
                freeListPlan,
                nodes);
        BootPageAllocatorState destination = SentinelState();

        fixed (BootPageFreeListNode* nodePointer = nodes)
        {
            BootPageAllocatorStateMaterializationResult materialized =
                BootPageAllocatorStateMaterializer.Materialize(
                    nodePointer,
                    (ulong)nodes.Length,
                    (ulong)nodes.Length,
                    sourceRangeCount: 2,
                    freeListPlan.HeadNodeIndex,
                    freeListPlan.PageSize,
                    freeListPlan.TotalFreePages,
                    freeListPlan.TotalFreeBytes,
                    &destination,
                    destinationStateCapacity: 1);

            return allocatorPlan.Succeeded
                && freeListPlan.Succeeded
                && initialized.Succeeded
                && materialized.Succeeded
                && StatesEqual(initialized.State, destination)
                && StatesEqual(materialized.State, destination);
        }
    }

    private static unsafe bool TestPoisonPointerPreflight()
    {
        BootPageFreeListNode* nodes =
            stackalloc BootPageFreeListNode[1];
        nodes[0] = Node(0, 0, 0x0010_0000, 1, -1);
        BootPageAllocatorState destination = SentinelState();

        ulong sourceTableOverflow =
            (~0UL / BootPageFreeListNode.StructureSize) + 1;
        ulong destinationTableOverflow =
            (~0UL / BootPageAllocatorState.StructureSize) + 1;

        BootPageAllocatorStateMaterializationResult zeroCount =
            Invoke(
                null,
                0,
                0,
                1,
                0,
                PageSize,
                1,
                PageSize,
                null,
                0);
        BootPageAllocatorStateMaterializationResult sourceByteOverflow =
            Invoke(
                (BootPageFreeListNode*)8,
                sourceTableOverflow,
                sourceTableOverflow,
                1,
                0,
                PageSize,
                1,
                PageSize,
                &destination,
                1);
        BootPageAllocatorStateMaterializationResult capacityByteOverflow =
            Invoke(
                (BootPageFreeListNode*)8,
                1,
                sourceTableOverflow,
                1,
                0,
                PageSize,
                1,
                PageSize,
                &destination,
                1);
        BootPageAllocatorStateMaterializationResult tooMany =
            Invoke(
                (BootPageFreeListNode*)8,
                4097,
                4097,
                1,
                0,
                PageSize,
                1,
                PageSize,
                &destination,
                1);
        BootPageAllocatorStateMaterializationResult shortCapacity =
            Invoke(
                (BootPageFreeListNode*)1,
                2,
                1,
                1,
                0,
                PageSize,
                1,
                PageSize,
                &destination,
                1);
        BootPageAllocatorStateMaterializationResult missingSource =
            Invoke(
                null,
                1,
                1,
                1,
                0,
                PageSize,
                1,
                PageSize,
                &destination,
                1);
        BootPageAllocatorStateMaterializationResult misalignedSource =
            Invoke(
                (BootPageFreeListNode*)1,
                1,
                1,
                1,
                0,
                PageSize,
                1,
                PageSize,
                &destination,
                1);
        BootPageAllocatorStateMaterializationResult sourceAddressOverflow =
            Invoke(
                (BootPageFreeListNode*)(~0UL - 7),
                1,
                1,
                1,
                0,
                PageSize,
                1,
                PageSize,
                &destination,
                1);
        BootPageAllocatorStateMaterializationResult shortDestination =
            Invoke(
                nodes,
                1,
                1,
                1,
                0,
                PageSize,
                1,
                PageSize,
                (BootPageAllocatorState*)1,
                0);
        BootPageAllocatorStateMaterializationResult destinationByteOverflow =
            Invoke(
                nodes,
                1,
                1,
                1,
                0,
                PageSize,
                1,
                PageSize,
                (BootPageAllocatorState*)8,
                destinationTableOverflow);
        BootPageAllocatorStateMaterializationResult missingDestination =
            Invoke(
                nodes,
                1,
                1,
                1,
                0,
                PageSize,
                1,
                PageSize,
                null,
                1);
        BootPageAllocatorStateMaterializationResult misalignedDestination =
            Invoke(
                nodes,
                1,
                1,
                1,
                0,
                PageSize,
                1,
                PageSize,
                (BootPageAllocatorState*)1,
                1);
        BootPageAllocatorStateMaterializationResult
            destinationAddressOverflow =
                Invoke(
                    nodes,
                    1,
                    1,
                    1,
                    0,
                    PageSize,
                    1,
                    PageSize,
                    (BootPageAllocatorState*)(~0UL - 7),
                    1);
        BootPageAllocatorStateMaterializationResult overlap =
            Invoke(
                nodes,
                1,
                1,
                1,
                0,
                PageSize,
                1,
                PageSize,
                (BootPageAllocatorState*)nodes,
                1);

        return StatusIs(
                zeroCount,
                BootPageAllocatorStateMaterializationStatus
                    .InvalidSourceNodeCount)
            && StatusIs(
                sourceByteOverflow,
                BootPageAllocatorStateMaterializationStatus
                    .SourceNodeTableByteCountOverflow)
            && StatusIs(
                capacityByteOverflow,
                BootPageAllocatorStateMaterializationStatus
                    .SourceNodeTableByteCountOverflow)
            && StatusIs(
                tooMany,
                BootPageAllocatorStateMaterializationStatus
                    .TooManySourceNodes)
            && StatusIs(
                shortCapacity,
                BootPageAllocatorStateMaterializationStatus
                    .SourceNodeCapacityTooSmall)
            && StatusIs(
                missingSource,
                BootPageAllocatorStateMaterializationStatus
                    .MissingSourceNodes)
            && StatusIs(
                misalignedSource,
                BootPageAllocatorStateMaterializationStatus
                    .MisalignedSourceNodes)
            && StatusIs(
                sourceAddressOverflow,
                BootPageAllocatorStateMaterializationStatus
                    .SourceNodeTableAddressRangeOverflow)
            && StatusIs(
                shortDestination,
                BootPageAllocatorStateMaterializationStatus
                    .DestinationStateCapacityTooSmall)
            && StatusIs(
                destinationByteOverflow,
                BootPageAllocatorStateMaterializationStatus
                    .DestinationStateByteCountOverflow)
            && StatusIs(
                missingDestination,
                BootPageAllocatorStateMaterializationStatus
                    .MissingDestinationState)
            && StatusIs(
                misalignedDestination,
                BootPageAllocatorStateMaterializationStatus
                    .MisalignedDestinationState)
            && StatusIs(
                destinationAddressOverflow,
                BootPageAllocatorStateMaterializationStatus
                    .DestinationStateAddressRangeOverflow)
            && StatusIs(
                overlap,
                BootPageAllocatorStateMaterializationStatus
                    .DestinationOverlapsSourceNodes)
            && StructBytes(SentinelState()).AsSpan().SequenceEqual(
                StructBytes(destination));
    }

    private static unsafe bool TestPageRangeAndExpectedMetadata()
    {
        BootPageFreeListNode* nodes =
            stackalloc BootPageFreeListNode[1];
        nodes[0] = Node(0, 0, 0x0010_0000, 1, -1);
        BootPageAllocatorState destination = SentinelState();
        byte[] before = StructBytes(destination);
        ulong expectedOverflowPages = (~0UL / PageSize) + 1;

        BootPageAllocatorStateMaterializationResult zeroPage =
            InvokeValidPointers(
                nodes,
                &destination,
                sourceRangeCount: 1,
                headNodeIndex: 0,
                pageSize: 0,
                expectedFreePages: 1,
                expectedFreeBytes: PageSize);
        BootPageAllocatorStateMaterializationResult wrongPage =
            InvokeValidPointers(
                nodes,
                &destination,
                sourceRangeCount: 1,
                headNodeIndex: 0,
                pageSize: PageSize * 2,
                expectedFreePages: 1,
                expectedFreeBytes: PageSize);
        BootPageAllocatorStateMaterializationResult zeroRanges =
            InvokeValidPointers(
                nodes,
                &destination,
                sourceRangeCount: 0,
                headNodeIndex: 0,
                PageSize,
                expectedFreePages: 1,
                expectedFreeBytes: PageSize);
        BootPageAllocatorStateMaterializationResult tooManyRanges =
            InvokeValidPointers(
                nodes,
                &destination,
                sourceRangeCount: 4097,
                headNodeIndex: 0,
                PageSize,
                expectedFreePages: 1,
                expectedFreeBytes: PageSize);
        BootPageAllocatorStateMaterializationResult noPages =
            InvokeValidPointers(
                nodes,
                &destination,
                sourceRangeCount: 1,
                headNodeIndex: 0,
                PageSize,
                expectedFreePages: 0,
                expectedFreeBytes: 0);
        BootPageAllocatorStateMaterializationResult expectedOverflow =
            InvokeValidPointers(
                nodes,
                &destination,
                sourceRangeCount: 1,
                headNodeIndex: 0,
                PageSize,
                expectedOverflowPages,
                expectedFreeBytes: 0);
        BootPageAllocatorStateMaterializationResult expectedMismatch =
            InvokeValidPointers(
                nodes,
                &destination,
                sourceRangeCount: 1,
                headNodeIndex: 0,
                PageSize,
                expectedFreePages: 1,
                expectedFreeBytes: PageSize + 1);

        return StatusIs(
                zeroPage,
                BootPageAllocatorStateMaterializationStatus.InvalidPageSize)
            && StatusIs(
                wrongPage,
                BootPageAllocatorStateMaterializationStatus.InvalidPageSize)
            && StatusIs(
                zeroRanges,
                BootPageAllocatorStateMaterializationStatus
                    .InvalidSourceRangeCount)
            && StatusIs(
                tooManyRanges,
                BootPageAllocatorStateMaterializationStatus
                    .InvalidSourceRangeCount)
            && StatusIs(
                noPages,
                BootPageAllocatorStateMaterializationStatus.NoFreePages)
            && StatusIs(
                expectedOverflow,
                BootPageAllocatorStateMaterializationStatus
                    .ExpectedByteCountOverflow)
            && StatusIs(
                expectedMismatch,
                BootPageAllocatorStateMaterializationStatus
                    .ExpectedByteCountMismatch)
            && before.AsSpan().SequenceEqual(StructBytes(destination));
    }

    private static unsafe bool TestMalformedNodes()
    {
        BootPageFreeListNode valid =
            Node(0, 0, 0x0010_0000, 1, -1);
        BootPageAllocatorStateMaterializationStatus[] expected =
        [
            BootPageAllocatorStateMaterializationStatus
                .InvalidSourceFreeSegmentIndex,
            BootPageAllocatorStateMaterializationStatus
                .InvalidSourceRangeIndex,
            BootPageAllocatorStateMaterializationStatus
                .InvalidSourceRangeIndex,
            BootPageAllocatorStateMaterializationStatus.InvalidNodePageCount,
            BootPageAllocatorStateMaterializationStatus
                .InvalidNodePhysicalStart,
            BootPageAllocatorStateMaterializationStatus
                .UnalignedNodePhysicalStart,
            BootPageAllocatorStateMaterializationStatus
                .NodeByteCountOverflow,
            BootPageAllocatorStateMaterializationStatus
                .NodeByteLengthMismatch,
            BootPageAllocatorStateMaterializationStatus
                .NodeAddressRangeOverflow,
            BootPageAllocatorStateMaterializationStatus
                .NodePhysicalEndMismatch
        ];
        BootPageFreeListNode[] malformed =
        [
            new BootPageFreeListNode(
                1,
                0,
                valid.PhysicalStart,
                valid.PhysicalEndExclusive,
                valid.PageCount,
                valid.ByteLength,
                -1),
            new BootPageFreeListNode(
                0,
                -1,
                valid.PhysicalStart,
                valid.PhysicalEndExclusive,
                valid.PageCount,
                valid.ByteLength,
                -1),
            new BootPageFreeListNode(
                0,
                1,
                valid.PhysicalStart,
                valid.PhysicalEndExclusive,
                valid.PageCount,
                valid.ByteLength,
                -1),
            new BootPageFreeListNode(
                0,
                0,
                valid.PhysicalStart,
                valid.PhysicalStart,
                0,
                0,
                -1),
            new BootPageFreeListNode(
                0,
                0,
                0,
                PageSize,
                1,
                PageSize,
                -1),
            new BootPageFreeListNode(
                0,
                0,
                valid.PhysicalStart + 1,
                valid.PhysicalEndExclusive + 1,
                1,
                PageSize,
                -1),
            new BootPageFreeListNode(
                0,
                0,
                valid.PhysicalStart,
                0,
                (~0UL / PageSize) + 1,
                0,
                -1),
            new BootPageFreeListNode(
                0,
                0,
                valid.PhysicalStart,
                valid.PhysicalEndExclusive,
                1,
                PageSize * 2,
                -1),
            new BootPageFreeListNode(
                0,
                0,
                ~0UL - (PageSize - 1),
                0,
                1,
                PageSize,
                -1),
            new BootPageFreeListNode(
                0,
                0,
                valid.PhysicalStart,
                valid.PhysicalEndExclusive + PageSize,
                1,
                PageSize,
                -1)
        ];

        for (int index = 0; index < malformed.Length; index++)
        {
            BootPageAllocatorState destination = SentinelState();
            byte[] before = StructBytes(destination);
            BootPageFreeListNode node = malformed[index];
            BootPageAllocatorStateMaterializationResult result =
                BootPageAllocatorStateMaterializer.Materialize(
                    &node,
                    1,
                    1,
                    1,
                    0,
                    PageSize,
                    1,
                    PageSize,
                    &destination,
                    1);

            if (result.Status != expected[index]
                || result.Succeeded
                || result.InvalidNodeIndex != 0
                || result.VisitedNodeCount != 0
                || !before.AsSpan().SequenceEqual(
                    StructBytes(destination)))
            {
                return false;
            }
        }

        return true;
    }

    private static unsafe bool TestHeadAndNextIndexes()
    {
        BootPageFreeListNode* nodes =
            stackalloc BootPageFreeListNode[2];
        nodes[0] = Node(0, 0, 0x0010_0000, 1, 1);
        nodes[1] = Node(1, 1, 0x0020_0000, 1, -1);
        BootPageAllocatorState destination = SentinelState();

        BootPageAllocatorStateMaterializationResult negativeHead =
            Invoke(
                nodes,
                2,
                2,
                2,
                -1,
                PageSize,
                2,
                2 * PageSize,
                &destination,
                1);
        BootPageAllocatorStateMaterializationResult outsideHead =
            Invoke(
                nodes,
                2,
                2,
                2,
                2,
                PageSize,
                2,
                2 * PageSize,
                &destination,
                1);

        nodes[0] = Node(0, 0, 0x0010_0000, 1, 2);
        BootPageAllocatorStateMaterializationResult outsideNext =
            Invoke(
                nodes,
                2,
                2,
                2,
                0,
                PageSize,
                2,
                2 * PageSize,
                &destination,
                1);
        nodes[0] = Node(0, 0, 0x0010_0000, 1, -2);
        BootPageAllocatorStateMaterializationResult negativeNext =
            Invoke(
                nodes,
                2,
                2,
                2,
                0,
                PageSize,
                2,
                2 * PageSize,
                &destination,
                1);

        return StatusIs(
                negativeHead,
                BootPageAllocatorStateMaterializationStatus
                    .InvalidHeadNodeIndex)
            && negativeHead.InvalidNodeIndex == -1
            && negativeHead.VisitedNodeCount == 0
            && StatusIs(
                outsideHead,
                BootPageAllocatorStateMaterializationStatus
                    .InvalidHeadNodeIndex)
            && outsideHead.InvalidNodeIndex == 2
            && outsideHead.VisitedNodeCount == 0
            && StatusIs(
                outsideNext,
                BootPageAllocatorStateMaterializationStatus
                    .InvalidNextNodeIndex)
            && outsideNext.InvalidNodeIndex == 2
            && outsideNext.ConflictingNodeIndex == 0
            && outsideNext.VisitedNodeCount == 1
            && StatusIs(
                negativeNext,
                BootPageAllocatorStateMaterializationStatus
                    .InvalidNextNodeIndex)
            && negativeNext.InvalidNodeIndex == -2
            && negativeNext.ConflictingNodeIndex == 0
            && negativeNext.VisitedNodeCount == 1;
    }

    private static unsafe bool TestCycles()
    {
        BootPageAllocatorState destination = SentinelState();

        BootPageFreeListNode self =
            Node(0, 0, 0x0010_0000, 1, 0);
        BootPageAllocatorStateMaterializationResult selfCycle =
            Invoke(
                &self,
                1,
                1,
                1,
                0,
                PageSize,
                1,
                PageSize,
                &destination,
                1);

        BootPageFreeListNode* pair =
            stackalloc BootPageFreeListNode[2];
        pair[0] = Node(0, 0, 0x0010_0000, 1, 1);
        pair[1] = Node(1, 1, 0x0020_0000, 1, 0);
        BootPageAllocatorStateMaterializationResult pairCycle =
            Invoke(
                pair,
                2,
                2,
                2,
                0,
                PageSize,
                2,
                2 * PageSize,
                &destination,
                1);

        BootPageFreeListNode* longer =
            stackalloc BootPageFreeListNode[3];
        longer[0] = Node(0, 0, 0x0010_0000, 1, 2);
        longer[1] = Node(2, 2, 0x0030_0000, 1, -1);
        longer[2] = Node(1, 1, 0x0020_0000, 1, 0);
        BootPageAllocatorStateMaterializationResult longerCycle =
            Invoke(
                longer,
                3,
                3,
                3,
                0,
                PageSize,
                3,
                3 * PageSize,
                &destination,
                1);

        return StatusIs(
                selfCycle,
                BootPageAllocatorStateMaterializationStatus
                    .FreeListCycleDetected)
            && selfCycle.InvalidNodeIndex == 0
            && selfCycle.ConflictingNodeIndex == 0
            && selfCycle.VisitedNodeCount == 1
            && StatusIs(
                pairCycle,
                BootPageAllocatorStateMaterializationStatus
                    .FreeListCycleDetected)
            && pairCycle.InvalidNodeIndex == 0
            && pairCycle.ConflictingNodeIndex == 1
            && pairCycle.VisitedNodeCount == 2
            && StatusIs(
                longerCycle,
                BootPageAllocatorStateMaterializationStatus
                    .FreeListCycleDetected)
            && longerCycle.InvalidNodeIndex == 0
            && longerCycle.ConflictingNodeIndex == 2
            && longerCycle.VisitedNodeCount == 2;
    }

    private static unsafe bool TestShortAndDisconnectedChains()
    {
        BootPageFreeListNode* nodes =
            stackalloc BootPageFreeListNode[3];
        nodes[0] = Node(0, 0, 0x0010_0000, 1, 1);
        nodes[1] = Node(1, 1, 0x0020_0000, 1, -1);
        nodes[2] = Node(2, 2, 0x0030_0000, 1, -1);
        BootPageAllocatorState destination = SentinelState();

        BootPageAllocatorStateMaterializationResult shortChain =
            Invoke(
                nodes,
                3,
                3,
                3,
                0,
                PageSize,
                3,
                3 * PageSize,
                &destination,
                1);

        nodes[0] = Node(0, 0, 0x0010_0000, 1, 2);
        nodes[2] = Node(1, 1, 0x0020_0000, 1, -1);
        nodes[1] = Node(2, 2, 0x0030_0000, 1, -1);
        BootPageAllocatorStateMaterializationResult disconnected =
            Invoke(
                nodes,
                3,
                3,
                3,
                0,
                PageSize,
                3,
                3 * PageSize,
                &destination,
                1);

        return StatusIs(
                shortChain,
                BootPageAllocatorStateMaterializationStatus
                    .FreeListNodeCountMismatch)
            && shortChain.InvalidNodeIndex == -1
            && shortChain.ConflictingNodeIndex == 1
            && shortChain.VisitedNodeCount == 2
            && shortChain.ObservedFreePages == 2
            && StatusIs(
                disconnected,
                BootPageAllocatorStateMaterializationStatus
                    .FreeListNodeCountMismatch)
            && disconnected.InvalidNodeIndex == -1
            && disconnected.ConflictingNodeIndex == 2
            && disconnected.VisitedNodeCount == 2
            && disconnected.ObservedFreePages == 2;
    }

    private static unsafe bool TestProvenanceAndPhysicalOrdering()
    {
        BootPageAllocatorState destination = SentinelState();
        BootPageFreeListNode* nodes =
            stackalloc BootPageFreeListNode[2];

        nodes[0] = Node(0, 1, 0x0010_0000, 1, 1);
        nodes[1] = Node(1, 0, 0x0020_0000, 1, -1);
        BootPageAllocatorStateMaterializationResult rangeOrder =
            MaterializeTwo(nodes, &destination);

        nodes[0] = Node(0, 0, 0x0030_0000, 1, 1);
        nodes[1] = Node(1, 1, 0x0020_0000, 1, -1);
        BootPageAllocatorStateMaterializationResult physicalOrder =
            MaterializeTwo(nodes, &destination);

        nodes[0] = Node(0, 0, 0x0010_0000, 2, 1);
        nodes[1] = Node(1, 1, 0x0010_1000, 1, -1);
        BootPageAllocatorStateMaterializationResult overlap =
            Invoke(
                nodes,
                2,
                2,
                2,
                0,
                PageSize,
                3,
                3 * PageSize,
                &destination,
                1);

        nodes[0] = Node(0, 0, 0x0010_0000, 2, 1);
        nodes[1] = Node(1, 1, 0x0010_2000, 1, -1);
        byte[] adjacencySource = PointerBytes(nodes, 2);
        BootPageAllocatorStateMaterializationResult adjacency =
            Invoke(
                nodes,
                2,
                2,
                2,
                0,
                PageSize,
                3,
                3 * PageSize,
                &destination,
                1);
        bool adjacencyPreserved = adjacencySource.AsSpan().SequenceEqual(
            PointerBytes(nodes, 2));

        nodes[0] = Node(1, 0, 0x0010_0000, 1, 1);
        nodes[1] = Node(0, 1, 0x0020_0000, 1, -1);
        BootPageAllocatorStateMaterializationResult segmentOrder =
            MaterializeTwo(nodes, &destination);

        return StatusIs(
                rangeOrder,
                BootPageAllocatorStateMaterializationStatus
                    .UnsortedSourceRangeIndex)
            && rangeOrder.InvalidNodeIndex == 1
            && rangeOrder.ConflictingNodeIndex == 0
            && StatusIs(
                physicalOrder,
                BootPageAllocatorStateMaterializationStatus.UnsortedNode)
            && physicalOrder.InvalidNodeIndex == 1
            && physicalOrder.ConflictingNodeIndex == 0
            && StatusIs(
                overlap,
                BootPageAllocatorStateMaterializationStatus.OverlappingNode)
            && overlap.InvalidNodeIndex == 1
            && overlap.ConflictingNodeIndex == 0
            && adjacency.Succeeded
            && adjacency.ObservedFreePages == 3
            && adjacencyPreserved
            && StatusIs(
                segmentOrder,
                BootPageAllocatorStateMaterializationStatus
                    .InvalidSourceFreeSegmentIndex);
    }

    private static unsafe bool TestTotals()
    {
        BootPageFreeListNode node =
            Node(0, 0, 0x0010_0000, 2, -1);
        BootPageAllocatorState destination = SentinelState();
        byte[] before = StructBytes(destination);

        BootPageAllocatorStateMaterializationResult pageMismatch =
            Invoke(
                &node,
                1,
                1,
                1,
                0,
                PageSize,
                expectedFreePages: 1,
                expectedFreeBytes: PageSize,
                &destination,
                1);
        bool mismatchAtomic = before.AsSpan().SequenceEqual(
            StructBytes(destination));

        BootPageAllocatorStateMaterializationResult exact =
            Invoke(
                &node,
                1,
                1,
                1,
                0,
                PageSize,
                expectedFreePages: 2,
                expectedFreeBytes: 2 * PageSize,
                &destination,
                1);

        return StatusIs(
                pageMismatch,
                BootPageAllocatorStateMaterializationStatus
                    .TotalPageCountMismatch)
            && pageMismatch.VisitedNodeCount == 1
            && pageMismatch.ExpectedFreePages == 1
            && pageMismatch.ExpectedFreeBytes == PageSize
            && pageMismatch.ObservedFreePages == 2
            && pageMismatch.ObservedFreeBytes == 2 * PageSize
            && mismatchAtomic
            && exact.Succeeded
            && exact.ObservedFreePages == exact.ExpectedFreePages
            && exact.ObservedFreeBytes == exact.ExpectedFreeBytes
            && AllMalformedSuccessEvidenceFails(exact);
    }

    private static unsafe bool TestAtomicityDeterminismAndAllocation()
    {
        BootPageFreeListNode* nodes =
            stackalloc BootPageFreeListNode[2];
        nodes[0] = Node(0, 0, 0x0010_0000, 2, 1);
        nodes[1] = Node(1, 1, 0x0020_0000, 3, -1);
        BootPageAllocatorState destination = SentinelState();
        byte[] sourceBefore = PointerBytes(nodes, 2);
        byte[] destinationBefore = StructBytes(destination);

        BootPageFreeListNode malformed = nodes[0];
        malformed = new BootPageFreeListNode(
            malformed.SourceFreeSegmentIndex,
            malformed.SourceUsableRangeIndex,
            malformed.PhysicalStart,
            malformed.PhysicalEndExclusive,
            pageCount: 0,
            byteLength: 0,
            malformed.NextNodeIndex);
        BootPageAllocatorStateMaterializationResult failed =
            Invoke(
                &malformed,
                1,
                1,
                1,
                0,
                PageSize,
                1,
                PageSize,
                &destination,
                1);
        bool failureAtomic =
            failed.Status
                == BootPageAllocatorStateMaterializationStatus
                    .InvalidNodePageCount
            && destinationBefore.AsSpan().SequenceEqual(
                StructBytes(destination));

        BootPageAllocatorStateMaterializationResult first =
            Invoke(
                nodes,
                2,
                2,
                2,
                0,
                PageSize,
                5,
                5 * PageSize,
                &destination,
                1);
        BootPageAllocatorState firstState = destination;
        BootPageAllocatorStateMaterializationResult second =
            Invoke(
                nodes,
                2,
                2,
                2,
                0,
                PageSize,
                5,
                5 * PageSize,
                &destination,
                1);

        _ = Invoke(
            nodes,
            2,
            2,
            2,
            0,
            PageSize,
            5,
            5 * PageSize,
            &destination,
            1);
        long beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
        BootPageAllocatorStateMaterializationResult allocationProbe = default;
        for (int iteration = 0; iteration < 64; iteration++)
        {
            allocationProbe = Invoke(
                nodes,
                2,
                2,
                2,
                0,
                PageSize,
                5,
                5 * PageSize,
                &destination,
                1);
        }
        long afterAllocation = GC.GetAllocatedBytesForCurrentThread();

        return failureAtomic
            && first.Succeeded
            && second.Succeeded
            && allocationProbe.Succeeded
            && StructBytes(first).AsSpan().SequenceEqual(
                StructBytes(second))
            && StatesEqual(firstState, destination)
            && sourceBefore.AsSpan().SequenceEqual(
                PointerBytes(nodes, 2))
            && afterAllocation == beforeAllocation;
    }

    private static bool TestSourceAndIntegrationBoundary()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string corePath = GetCoreSourcePath();
        string materializerPath = GetMaterializerSourcePath();
        string statePath = Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Memory",
            "BootPageAllocatorState.cs");
        string freeListPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Memory",
            "BootPageFreeListPlan.cs");
        string efiEntryPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "Boot",
            "EfiEntry.cs");
        string core = RemoveComments(File.ReadAllText(corePath));
        string materializer =
            RemoveComments(File.ReadAllText(materializerPath));
        string state = RemoveComments(File.ReadAllText(statePath));
        string freeList = RemoveComments(File.ReadAllText(freeListPath));
        string efiEntry = RemoveComments(File.ReadAllText(efiEntryPath));
        string combined = core + Environment.NewLine + materializer;

        string[] forbiddenPatterns =
        [
            @"\bReadOnlySpan\s*<",
            @"\bSpan\s*<",
            @"\bstring\b",
            @"\bthrow\b",
            @"\bException\b",
            @"\bSystem\.Reflection\b",
            @"\bSystem\.Linq\b",
            @"\bdelegate\b",
            @"\bTask\b",
            @"\bThread\b",
            @"\bConsole\b",
            @"\bGC\.",
            @"\bBootPageFreeListPlanResult\b",
            @"\bBootPageAllocatorPlanResult\b",
            @"\bBootMemoryRegionPlan\b",
            @"\bBootPageAllocatorStateInitializer\b",
            @"\bBootPageFreeListBuilder\b",
            @"\bEfiEntry\b",
            @"\bBootSequence\b",
            @"\bulong\.MaxValue\b",
            @"\bCLROS_M01_006_READY\b"
        ];
        foreach (string pattern in forbiddenPatterns)
        {
            if (Regex.IsMatch(
                    combined,
                    pattern,
                    RegexOptions.CultureInvariant))
            {
                return SourceFailure("forbidden=" + pattern);
            }
        }

        if (!core.Contains(
                "public readonly struct BootPageFreeListNode",
                StringComparison.Ordinal)
            || !core.Contains(
                "public readonly struct BootPageAllocatorState",
                StringComparison.Ordinal)
            || Regex.IsMatch(
                state,
                @"\bpublic\s+readonly\s+struct\s+"
                    + @"BootPageAllocatorState\s*\{",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                freeList,
                @"\bpublic\s+readonly\s+struct\s+"
                    + @"BootPageFreeListNode\s*\{",
                RegexOptions.CultureInvariant)
            || !materializer.Contains(
                "BootPageFreeListNode* sourceNodes",
                StringComparison.Ordinal)
            || !materializer.Contains(
                "ulong sourceNodeCount",
                StringComparison.Ordinal)
            || !materializer.Contains(
                "ulong sourceNodeCapacity",
                StringComparison.Ordinal)
            || !materializer.Contains(
                "ulong sourceRangeCount",
                StringComparison.Ordinal)
            || !materializer.Contains(
                "BootPageAllocatorState* destinationState",
                StringComparison.Ordinal)
            || !materializer.Contains(
                "private const ulong MaximumUnsignedLong = ~0UL",
                StringComparison.Ordinal)
            || !materializer.Contains(
                "ActualSourceNodeStructureSize",
                StringComparison.Ordinal)
            || !materializer.Contains(
                "(ulong)sizeof(BootPageFreeListNode)",
                StringComparison.Ordinal)
            || !materializer.Contains(
                "ActualDestinationStateStructureSize",
                StringComparison.Ordinal)
            || !materializer.Contains(
                "(ulong)sizeof(BootPageAllocatorState)",
                StringComparison.Ordinal)
            || !materializer.Contains(
                ".IsRepresentableSuccessSummary(",
                StringComparison.Ordinal))
        {
            return SourceFailure("required-contract");
        }

        if (!HasOrderedFragmentsInMethod(
                materializer,
                "public static BootPageAllocatorStateMaterializationResult Materialize(",
                "private static bool TryValidatePointerAndSummaryMetadata(",
                [
                    "TryValidatePointerAndSummaryMetadata(",
                    "BootPageFreeListNode node = sourceNodes[currentNodeIndex]",
                    "if (observedFreePages != expectedFreePages)",
                    "if (observedFreeBytes != expectedFreeBytes)",
                    "destinationState[0] = state"
                ])
            || !HasOrderedFragmentsInMethod(
                materializer,
                "private static bool TryValidatePointerAndSummaryMetadata(",
                "private static BootPageAllocatorStateMaterializationStatus ValidateNode(",
                [
                    "if (sourceNodeCount == 0)",
                    "SourceNodeTableByteCountOverflow",
                    "if (sourceNodeCount > MaximumSourceNodeCount)",
                    "if (sourceNodeCapacity < sourceNodeCount)",
                    "if (sourceNodeAddress == 0)",
                    "MisalignedSourceNodes",
                    "SourceNodeTableAddressRangeOverflow",
                    "DestinationStateCapacityTooSmall",
                    "DestinationStateByteCountOverflow",
                    "if (destinationStateAddress == 0)",
                    "MisalignedDestinationState",
                    "DestinationStateAddressRangeOverflow",
                    "DestinationOverlapsSourceNodes",
                    "if (pageSize != RequiredPageSize)",
                    "InvalidSourceRangeCount",
                    "InvalidHeadNodeIndex",
                    "if (expectedFreePages == 0)",
                     "ExpectedByteCountOverflow",
                     "ExpectedByteCountMismatch"
                ])
            || !HasOrderedFragmentsInMethod(
                materializer,
                "internal static bool IsRepresentableSuccessSummary(",
                "private static bool TryAdd(",
                [
                    "ActualSourceNodeStructureSize",
                    "ActualDestinationStateStructureSize",
                    "BootPageFreeListNode.StructureSize",
                    "BootPageAllocatorState.StructureSize",
                    "sourceNodeCount",
                    "sourceNodeCapacity",
                    "destinationStateCapacity",
                    "pageSize == RequiredPageSize",
                    "expectedFreePages != 0",
                    "expectedFreePages * pageSize == expectedFreeBytes"
                ]))
        {
            return SourceFailure("validation-order");
        }

        Type[] podTypes =
        [
            typeof(BootPageFreeListNode),
            typeof(BootPageAllocatorState),
            typeof(BootPageAllocatorStateMaterializationResult)
        ];
        foreach (Type type in podTypes)
        {
            foreach (FieldInfo field in type.GetFields(
                BindingFlags.Instance
                    | BindingFlags.Static
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly))
            {
                if (!field.FieldType.IsValueType
                    || field.FieldType.IsPointer
                    || field.FieldType.IsArray
                    || field.IsStatic && !field.IsLiteral)
                {
                    return SourceFailure(
                        "managed-field=" + type.Name + "." + field.Name);
                }
            }
        }

        if (!EfiProfileTestControls.RemoveOwnedCooperativeMaterializer(ref efiEntry, out string reason))
            return SourceFailure("owned-cooperative-materializer " + reason);
        return !efiEntry.Contains(
                nameof(BootPageAllocatorStateMaterializer),
                StringComparison.Ordinal)
            && BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Memory\BootPageAllocatorCoreContracts.cs",
                @"Memory\BootPageAllocatorCoreContracts.cs")
            && BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Memory\BootPageAllocatorStateMaterializer.cs",
                @"Memory\BootPageAllocatorStateMaterializer.cs");
    }

    private static bool TestMarkerUniqueness()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string marker = "CLROS_" + "M01_006_READY";
        string expectedSource =
            Path.GetFullPath(GetTestSourcePath());
        List<string> markerSources = [];

        foreach (string candidate in Directory.EnumerateFiles(
            repositoryRoot,
            "*.cs",
            SearchOption.AllDirectories))
        {
            string fullCandidate = Path.GetFullPath(candidate);
            if (IsGeneratedOrReferencePath(fullCandidate)
                || !File.ReadAllText(fullCandidate).Contains(
                    marker,
                    StringComparison.Ordinal))
            {
                continue;
            }

            markerSources.Add(fullCandidate);
        }

        string testSource = File.ReadAllText(expectedSource);
        int emissionCount = Regex.Matches(
            testSource,
            "Console\\.WriteLine\\(\"CLROS_"
                + "M01_006_READY\"\\)",
            RegexOptions.CultureInvariant).Count;
        int umbrellaStart = testSource.IndexOf(
            "Console.WriteLine(\"TEST_START \" + IssueTestName)",
            StringComparison.Ordinal);
        int failureBlock = testSource.IndexOf(
            "\"TEST_FAIL \" + IssueTestName + \" failures=\"",
            StringComparison.Ordinal);
        int ready = testSource.IndexOf(
            "Console.WriteLine(\"CLROS_" + "M01_006_READY\")",
            StringComparison.Ordinal);
        int pass = testSource.IndexOf(
            "Console.WriteLine(\"TEST_PASS \" + IssueTestName)",
            StringComparison.Ordinal);

        return markerSources.Count == 1
            && string.Equals(
                markerSources[0],
                expectedSource,
                StringComparison.OrdinalIgnoreCase)
            && emissionCount == 1
            && umbrellaStart >= 0
            && failureBlock > umbrellaStart
            && ready > failureBlock
            && pass > ready;
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string workspace = CreateNoStdLibWorkspace();

        try
        {
            File.WriteAllText(
                Path.Combine(workspace, "Probe.cs"),
                """
                #if !NOSTDLIB_EFI
                #error NOSTDLIB_EFI must be defined.
                #endif

                using OS.Kernel.Memory;

                internal static unsafe class PositiveAllocatorStateProbe
                {
                    public static BootPageAllocatorStateMaterializationStatus Exercise()
                    {
                        BootPageFreeListNode* nodes =
                            stackalloc BootPageFreeListNode[2];
                        nodes[0] = new BootPageFreeListNode(
                            0,
                            0,
                            0x100000UL,
                            0x102000UL,
                            2UL,
                            8192UL,
                            1);
                        nodes[1] = new BootPageFreeListNode(
                            1,
                            1,
                            0x200000UL,
                            0x203000UL,
                            3UL,
                            12288UL,
                            -1);
                        BootPageAllocatorState destination = default;
                        BootPageAllocatorStateMaterializationResult result =
                            BootPageAllocatorStateMaterializer.Materialize(
                                nodes,
                                2UL,
                                2UL,
                                2UL,
                                0,
                                4096UL,
                                5UL,
                                20480UL,
                                &destination,
                                1UL);
                        BootPageAllocatorStateMaterializationResult failure =
                            BootPageAllocatorStateMaterializer.Materialize(
                                null,
                                1UL,
                                1UL,
                                1UL,
                                0,
                                4096UL,
                                1UL,
                                4096UL,
                                &destination,
                                1UL);
                        return result.Succeeded
                            && destination.IsInitialized
                            && failure.Status
                                == BootPageAllocatorStateMaterializationStatus
                                    .MissingSourceNodes
                            ? result.Status
                            : failure.Status;
                    }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            NoStdLibBuildResult result =
                RunNoStdLibBuild(workspace);
            string outputAssembly = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.BootPageAllocatorStateMaterializerCompileClosure.dll");
            if (result.TimedOut
                || result.ExitCode != 0
                || ContainsInfrastructureFailure(result.Output)
                || !HasZeroAssemblyReferences(outputAssembly))
            {
                Console.WriteLine(
                    "M01_006_NOSTDLIB_COMPILE_DIAGNOSTIC "
                    + NormalizeCompilerOutput(result.Output));
                return false;
            }

            return true;
        }
        finally
        {
            DeleteNoStdLibWorkspace(workspace);
        }
    }

    private static unsafe BootPageAllocatorStateMaterializationResult
        MaterializeTwo(
            BootPageFreeListNode* nodes,
            BootPageAllocatorState* destination)
    {
        return Invoke(
            nodes,
            2,
            2,
            2,
            0,
            PageSize,
            2,
            2 * PageSize,
            destination,
            1);
    }

    private static unsafe BootPageAllocatorStateMaterializationResult
        InvokeValidPointers(
            BootPageFreeListNode* nodes,
            BootPageAllocatorState* destination,
            ulong sourceRangeCount,
            int headNodeIndex,
            ulong pageSize,
            ulong expectedFreePages,
            ulong expectedFreeBytes)
    {
        return Invoke(
            nodes,
            1,
            1,
            sourceRangeCount,
            headNodeIndex,
            pageSize,
            expectedFreePages,
            expectedFreeBytes,
            destination,
            1);
    }

    private static unsafe BootPageAllocatorStateMaterializationResult
        Invoke(
            BootPageFreeListNode* sourceNodes,
            ulong sourceNodeCount,
            ulong sourceNodeCapacity,
            ulong sourceRangeCount,
            int headNodeIndex,
            ulong pageSize,
            ulong expectedFreePages,
            ulong expectedFreeBytes,
            BootPageAllocatorState* destinationState,
            ulong destinationStateCapacity)
    {
        return BootPageAllocatorStateMaterializer.Materialize(
            sourceNodes,
            sourceNodeCount,
            sourceNodeCapacity,
            sourceRangeCount,
            headNodeIndex,
            pageSize,
            expectedFreePages,
            expectedFreeBytes,
            destinationState,
            destinationStateCapacity);
    }

    private static bool StatusIs(
        BootPageAllocatorStateMaterializationResult result,
        BootPageAllocatorStateMaterializationStatus status)
    {
        return result.Status == status
            && !result.Succeeded
            && result.MaterializedStateCount == 0
            && !result.State.IsInitialized;
    }

    private static BootPageFreeListNode Node(
        int sourceFreeSegmentIndex,
        int sourceRangeIndex,
        ulong physicalStart,
        ulong pageCount,
        int nextNodeIndex)
    {
        ulong byteLength = pageCount * PageSize;
        return new BootPageFreeListNode(
            sourceFreeSegmentIndex,
            sourceRangeIndex,
            physicalStart,
            physicalStart + byteLength,
            pageCount,
            byteLength,
            nextNodeIndex);
    }

    private static BootPageAllocatorState SentinelState()
    {
        return new BootPageAllocatorState(
            isInitialized: true,
            pageSize: 0x1000,
            headNodeIndex: 77,
            freeNodeCount: 66,
            freePageCount: 55,
            freeByteCount: 44,
            allocatedPageCount: 33,
            allocatedByteCount: 22);
    }

    private static bool StatesEqual(
        BootPageAllocatorState left,
        BootPageAllocatorState right)
    {
        return left.IsInitialized == right.IsInitialized
            && left.PageSize == right.PageSize
            && left.HeadNodeIndex == right.HeadNodeIndex
            && left.FreeNodeCount == right.FreeNodeCount
            && left.FreePageCount == right.FreePageCount
            && left.FreeByteCount == right.FreeByteCount
            && left.AllocatedPageCount == right.AllocatedPageCount
            && left.AllocatedByteCount == right.AllocatedByteCount;
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

    private static bool HasLayout<T>(
        int expectedSize,
        (string Name, int Offset)[] expected)
        where T : struct
    {
        if (Unsafe.SizeOf<T>() != expectedSize)
        {
            return false;
        }

        Type type = typeof(T);
        foreach ((string name, int offset) in expected)
        {
            FieldInfo? field = type.GetField(
                name,
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic);
            if (field is null
                || Marshal.OffsetOf<T>(name).ToInt32() != offset)
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] StructBytes<T>(T value)
        where T : struct
    {
        ReadOnlySpan<T> values =
            MemoryMarshal.CreateReadOnlySpan(ref value, 1);
        return MemoryMarshal.AsBytes(values).ToArray();
    }

    private static unsafe byte[] PointerBytes<T>(
        T* pointer,
        int count)
        where T : unmanaged
    {
        return new ReadOnlySpan<byte>(
            pointer,
            checked(count * sizeof(T))).ToArray();
    }

    private static T MutateByte<T>(
        T value,
        int byteOffset,
        byte replacement)
        where T : struct
    {
        Span<T> values = MemoryMarshal.CreateSpan(ref value, 1);
        Span<byte> bytes = MemoryMarshal.AsBytes(values);
        bytes[byteOffset] = replacement;
        return value;
    }

    private static bool AllMalformedSuccessEvidenceFails(
        BootPageAllocatorStateMaterializationResult valid)
    {
        (int Offset, byte Replacement)[] corruptions =
        [
            (0, 1),
            (7, 0),
            (8, 0),
            (16, 0),
            (24, 0),
            (32, 0),
            (40, 0),
            (48, 0),
            (56, 1),
            (60, 0),
            (64, 0),
            (72, 0),
            (80, 1),
            (88, 0),
            (96, 1),
            (104, 1),
            (112, 1),
            (120, 0)
        ];

        foreach ((int offset, byte replacement) in corruptions)
        {
            if (MutateByte(valid, offset, replacement).Succeeded)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasOrderedFragmentsInMethod(
        string source,
        string methodStartMarker,
        string nextMethodMarker,
        string[] orderedFragments)
    {
        int methodStart = source.IndexOf(
            methodStartMarker,
            StringComparison.Ordinal);
        if (methodStart < 0)
        {
            return false;
        }

        int methodEnd = source.IndexOf(
            nextMethodMarker,
            methodStart + methodStartMarker.Length,
            StringComparison.Ordinal);
        if (methodEnd < 0)
        {
            return false;
        }

        int cursor = methodStart + methodStartMarker.Length;
        foreach (string fragment in orderedFragments)
        {
            int found = source.IndexOf(
                fragment,
                cursor,
                StringComparison.Ordinal);
            if (found < 0 || found >= methodEnd)
            {
                return false;
            }

            cursor = found + fragment.Length;
        }

        return true;
    }

    private static string RemoveComments(string source)
    {
        string withoutBlocks = Regex.Replace(
            source,
            @"/\*.*?\*/",
            string.Empty,
            RegexOptions.Singleline
                | RegexOptions.CultureInvariant);
        return Regex.Replace(
            withoutBlocks,
            @"//.*?$",
            string.Empty,
            RegexOptions.Multiline
                | RegexOptions.CultureInvariant);
    }

    private static bool SourceFailure(string reason)
    {
        Console.WriteLine(
            "M01_006_SOURCE_FIREWALL_DIAGNOSTIC " + reason);
        return false;
    }

    private static string CreateNoStdLibWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-006-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            File.Copy(
                Path.Combine(repositoryRoot, "global.json"),
                Path.Combine(workspace, "global.json"));
            File.WriteAllText(
                Path.Combine(workspace, "NoStdLibShim.cs"),
                """
                namespace System
                {
                    public class Object { }
                    public abstract class ValueType : Object { }
                    public abstract class Enum : ValueType { }
                    public abstract class Delegate : Object { }
                    public abstract class MulticastDelegate : Delegate { }
                    public class Attribute : Object { }
                    public struct Void { }
                    public struct Boolean { }
                    public struct Byte { }
                    public struct Int32 { }
                    public struct UInt32 { }
                    public struct UInt64 { }
                    public struct IntPtr { }
                    public struct UIntPtr { }
                    public enum AttributeTargets { }
                    public sealed class AttributeUsageAttribute : Attribute
                    {
                        public AttributeUsageAttribute(AttributeTargets validOn) { }
                        public bool AllowMultiple { get; set; }
                        public bool Inherited { get; set; }
                    }
                }

                namespace System.Runtime.CompilerServices
                {
                    public sealed class IsReadOnlyAttribute : System.Attribute { }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            string projectText =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>CLROS.M01.BootPageAllocatorStateMaterializerCompileClosure</AssemblyName>
                <TargetFramework>net11.0</TargetFramework>
                <LangVersion>14.0</LangVersion>
                <OutputType>Library</OutputType>
                <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                <ImplicitUsings>disable</ImplicitUsings>
                <Nullable>disable</Nullable>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <NoStdLib>true</NoStdLib>
                <NoConfig>true</NoConfig>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
                <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles>
                <RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
                <EnableNETAnalyzers>false</EnableNETAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
                <DefineConstants>NOSTDLIB_EFI</DefineConstants>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <Deterministic>true</Deterministic>
                <DebugType>none</DebugType>
                <DebugSymbols>false</DebugSymbols>
                <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
                <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
                <OutputPath>$(MSBuildProjectDirectory)\out\</OutputPath>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="NoStdLibShim.cs" />
                <Compile Include="__CORE__" Link="Memory\BootPageAllocatorCoreContracts.cs" />
                <Compile Include="__MATERIALIZER__" Link="Memory\BootPageAllocatorStateMaterializer.cs" />
                <Compile Include="Probe.cs" />
              </ItemGroup>
              <Target Name="CustomizeReferences"
                      BeforeTargets="BeforeCompile"
                      AfterTargets="FindReferenceAssembliesForReferences">
                <ItemGroup>
                  <ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
                  <ReferencePath Remove="@(ReferencePath)" />
                </ItemGroup>
              </Target>
            </Project>
            """
            .Replace(
                "__CORE__",
                EscapeXml(GetCoreSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__MATERIALIZER__",
                EscapeXml(GetMaterializerSourcePath()),
                StringComparison.Ordinal);

            File.WriteAllText(
                Path.Combine(workspace, "M01.006.NoStdLib.csproj"),
                projectText,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
            return workspace;
        }
        catch
        {
            try
            {
                DeleteNoStdLibWorkspace(workspace);
            }
            catch
            {
            }

            throw;
        }
    }

    private static NoStdLibBuildResult RunNoStdLibBuild(
        string workspace)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workspace
        };
        start.ArgumentList.Add("build");
        start.ArgumentList.Add("M01.006.NoStdLib.csproj");
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("-m:1");
        start.ArgumentList.Add("--no-dependencies");
        start.ArgumentList.Add("--no-incremental");
        start.ArgumentList.Add("--disable-build-servers");
        start.ArgumentList.Add("-v:minimal");
        start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
        start.ArgumentList.Add("-p:NuGetAudit=false");
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["NUGET_XMLDOC_MODE"] = "skip";

        using Process process = new Process { StartInfo = start };
        if (!process.Start())
        {
            return new NoStdLibBuildResult(
                -1,
                timedOut: false,
                "dotnet process did not start");
        }

        Task<string> standardOutput =
            process.StandardOutput.ReadToEndAsync();
        Task<string> standardError =
            process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90_000);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            process.WaitForExit(5_000);
        }

        string output = standardOutput.Wait(5_000)
            ? standardOutput.Result
            : "stdout did not close";
        string error = standardError.Wait(5_000)
            ? standardError.Result
            : "stderr did not close";
        return new NoStdLibBuildResult(
            exited ? process.ExitCode : -1,
            timedOut: !exited,
            output + Environment.NewLine + error);
    }

    private static bool HasZeroAssemblyReferences(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            return false;
        }

        using FileStream stream = File.OpenRead(assemblyPath);
        using PEReader reader = new PEReader(stream);
        if (!reader.HasMetadata)
        {
            return false;
        }

        MetadataReader metadata = reader.GetMetadataReader();
        return metadata.AssemblyReferences.Count == 0;
    }

    private static bool ContainsInfrastructureFailure(string output)
    {
        return output.Contains(
                "MSB1009",
                StringComparison.OrdinalIgnoreCase)
            || output.Contains(
                "NU1301",
                StringComparison.OrdinalIgnoreCase)
            || output.Contains(
                "A compatible .NET SDK was not found",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCompilerOutput(string output)
    {
        string normalized = Regex.Replace(
            output,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant).Trim();
        return normalized.Length <= 1500
            ? normalized
            : normalized[..1500] + "...";
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string GetCoreSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Memory",
            "BootPageAllocatorCoreContracts.cs");
    }

    private static string GetMaterializerSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Memory",
            "BootPageAllocatorStateMaterializer.cs");
    }

    private static string GetTestSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "tests",
            "OS.HostTests",
            "BootPageAllocatorStateMaterializerTests.cs");
    }

    private static bool IsGeneratedOrReferencePath(string path)
    {
        string[] excludedSegments =
        [
            "bin",
            "obj",
            "artifacts",
            "references",
            ".git"
        ];
        foreach (string segment in excludedSegments)
        {
            string needle =
                Path.DirectorySeparatorChar
                + segment
                + Path.DirectorySeparatorChar;
            if (path.Contains(
                    needle,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void DeleteNoStdLibWorkspace(string workspace)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        string fullWorkspace = Path.GetFullPath(workspace)
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        string? parent = Directory.GetParent(fullWorkspace)?.FullName
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        string leaf = Path.GetFileName(fullWorkspace);

        if (!string.Equals(
                parent,
                tempRoot,
                StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(
                leaf,
                @"\Aclros-m01-006-[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected M01-006 workspace.");
        }

        if (!Directory.Exists(fullWorkspace))
        {
            return;
        }

        FileAttributes attributes =
            File.GetAttributes(fullWorkspace);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Refusing to delete a reparse-point M01-006 workspace.");
        }

        Directory.Delete(fullWorkspace, recursive: true);
    }

    private readonly struct NoStdLibBuildResult
    {
        public NoStdLibBuildResult(
            int exitCode,
            bool timedOut,
            string output)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Output = output;
        }

        public int ExitCode { get; }

        public bool TimedOut { get; }

        public string Output { get; }
    }
}
