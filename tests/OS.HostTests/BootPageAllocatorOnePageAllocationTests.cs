using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OS.Boot;
using OS.Kernel.Memory;

internal static class BootPageAllocatorOnePageAllocationTests
{
    private const string IssueTestName = "m01.008";
    private const string ReadyMarker = "CLROS_M01_008_READY";
    private const ulong PageSize = 4096;

    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        int failures = 0;

        Console.WriteLine("TEST_START " + IssueTestName);

        Run("m01.008 01 statuses contracts and defaults are stable", TestStatusesContractsAndDefaults, ref failures);
        Run("m01.008 02 one page allocates from range head", TestHeadAllocation, ref failures);
        Run("m01.008 03 one page allocates from range tail", TestTailAllocation, ref failures);
        Run("m01.008 04 one page consumes an exact range", TestExactAllocation, ref failures);
        Run("m01.008 05 one page splits a range", TestSplitAllocation, ref failures);
        Run("m01.008 06 result accounting capacities and typed range are exact", TestResultEvidence, ref failures);
        Run("m01.008 07 canonical reservations and adjacency are preserved", TestCanonicalReservations, ref failures);
        Run("m01.008 08 only logical caller prefix mutates", TestCallerPrefixAndCanaries, ref failures);
        Run("m01.008 09 corrupt and default states fail closed", TestInvalidStates, ref failures);
        Run("m01.008 10 page count size and alignment requests fail closed", TestInvalidRequestShape, ref failures);
        Run("m01.008 11 malformed nodes fail closed", TestMalformedNodes, ref failures);
        Run("m01.008 12 node indexes chains and cycles fail closed", TestNodeIndexesChainsAndCycles, ref failures);
        Run("m01.008 13 node order overlap and accounting fail closed", TestNodeOrderingAndAccounting, ref failures);
        Run("m01.008 14 malformed and unordered reservations fail closed", TestInvalidReservations, ref failures);
        Run("m01.008 15 reservation overlap with free ranges fails", TestReservationOverlap, ref failures);
        Run("m01.008 16 exhaustion and split metadata limits fail", TestExhaustionAndMetadataCapacity, ref failures);
        Run("m01.008 17 pointer count capacity and spans are bounded", TestPointerPreflight, ref failures);
        Run("m01.008 18 caller table aliases are rejected", TestTableAliases, ref failures);
        Run("m01.008 19 every failure is byte atomic", TestFailureAtomicity, ref failures);
        Run("m01.008 20 execution is deterministic and allocation free", TestDeterminismAndAllocation, ref failures);
        Run("m01.008 21 coordinated forged successes fail closed", TestForgedSuccessEvidence, ref failures);
        Run("m01.008 22 legacy host span compatibility remains", TestLegacySpanCompatibility, ref failures);
        Run("m01.008 23 source and evaluated efi firewalls hold", TestSourceAndEfiFirewalls, ref failures);
        Run("m01.008 24 exact sources compile under nostdlib efi", TestNoStdLibEfiCompileClosure, ref failures);
        Run("m01.008 25 readiness marker is unique and host only", TestMarkerBoundary, ref failures);

        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException(
                "M01-008 one-page allocator host tests failed: " + failures);
        }

        Console.WriteLine(ReadyMarker);
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestStatusesContractsAndDefaults()
    {
        (string Name, int Value)[] expectedStatuses =
        [
            ("Success", 0),
            ("StateNotInitialized", 1),
            ("InvalidPageSize", 2),
            ("InvalidPageCount", 3),
            ("InvalidAlignment", 4),
            ("NodeCapacityTooSmall", 5),
            ("InvalidHeadNodeIndex", 6),
            ("InvalidNextNodeIndex", 7),
            ("InvalidNodeShape", 8),
            ("FreeListCycleDetected", 9),
            ("FreeListNodeCountMismatch", 10),
            ("TotalPageCountMismatch", 11),
            ("TotalByteCountMismatch", 12),
            ("AllocationByteCountOverflow", 13),
            ("AllocationAddressOverflow", 14),
            ("NoSuitableRange", 15),
            ("MetadataCapacityTooSmall", 16),
            ("FreeRangeOverlaps", 17),
            ("FreeRangeNotAllocated", 18),
            ("ContractLayoutMismatch", 19),
            ("NodeTableByteCountOverflow", 20),
            ("TooManyNodes", 21),
            ("MissingNodes", 22),
            ("MisalignedNodes", 23),
            ("NodeTableAddressRangeOverflow", 24),
            ("ReservationTableByteCountOverflow", 25),
            ("TooManyReservations", 26),
            ("ReservationCapacityTooSmall", 27),
            ("MissingReservations", 28),
            ("MisalignedReservations", 29),
            ("ReservationTableAddressRangeOverflow", 30),
            ("NodeReservationTablesOverlap", 31),
            ("InvalidStateAccounting", 32),
            ("InvalidReservation", 33),
            ("UnsortedReservation", 34),
            ("OverlappingReservation", 35),
            ("ReservationOverlapsFreeRange", 36),
            ("AllocatedPageCountOverflow", 37),
            ("AllocatedByteCountOverflow", 38),
            ("AllocationAddressConstructionFailed", 39)
        ];
        BootPageAllocatorOnePageResult result = default;
        Type resultType = typeof(BootPageAllocatorOnePageResult);

        return HasExactIntEnum<BootPageAllocatorMutationStatus>(
                expectedStatuses)
            && BootPageAllocatorMutator.RequiredPageSize == PageSize
            && BootPageAllocatorMutator.RequiredPageCount == 1
            && BootPageAllocatorMutator.RequiredPointerAlignment == 8
            && BootPageAllocatorMutator.MaximumNodeCount == 4096
            && BootPageAllocatorMutator.MaximumReservationCount == 256
            && BootPageAllocatorMutator.NoIndex == ~0UL
            && BootPageFreeListNode.StructureSize == 48
            && BootPageAllocatorState.StructureSize == 56
            && Marshal.SizeOf<BootReservedRange>() == 40
            && resultType.IsValueType
            && resultType.IsLayoutSequential
            && resultType.IsDefined(
                typeof(IsReadOnlyAttribute),
                inherit: false)
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<BootPageAllocatorOnePageResult>()
            && IsUnmanagedValue<BootPageAllocatorOnePageResult>()
            && !result.IsInitialized
            && !result.Succeeded
            && result.Status == BootPageAllocatorMutationStatus.Success
            && result.ReservationStatus
                == BootReservedRangeStatus.Uninitialized
            && result.AddressStatus == AddressValueStatus.Uninitialized
            && !result.PhysicalStart.IsValid
            && !result.PhysicalEndExclusive.IsValid
            && !result.BeforeState.IsInitialized
            && !result.AfterState.IsInitialized
            && HasExactAllocateOnePageApi();
    }

    private static unsafe bool TestHeadAllocation()
    {
        BootPageFreeListNode* nodes = stackalloc BootPageFreeListNode[3];
        nodes[0] = Node(0, 0, 0x100000, 4, 1);
        nodes[1] = Node(1, 1, 0x300000, 3, -1);
        nodes[2] = default;
        BootPageFreeListNode tailCanary = nodes[2];
        BootPageAllocatorState state = State(
            headNodeIndex: 0,
            freeNodeCount: 2,
            freePages: 7,
            allocatedPages: 2);

        BootPageAllocatorOnePageResult result = Allocate(
            state,
            nodes,
            nodeCount: 2,
            nodeCapacity: 3,
            reservations: null,
            reservationCount: 0,
            reservationCapacity: 0,
            alignment: PageSize);

        return IsExactSuccess(
                result,
                state,
                expectedFreeNodes: 2,
                expectedFreePages: 6,
                expectedAllocatedPages: 3,
                expectedPhysicalStart: 0x100000,
                expectedNodeCount: 2,
                expectedNodeCapacity: 3,
                expectedReservationCount: 0,
                expectedReservationCapacity: 0,
                expectedNodeIndex: 0,
                expectedInsertedNodeIndex: -1,
                expectedVisitedNodeCount: 2)
            && nodes[0].PhysicalStart == 0x101000
            && nodes[0].PhysicalEndExclusive == 0x104000
            && nodes[0].PageCount == 3
            && nodes[0].ByteLength == 3 * PageSize
            && nodes[0].NextNodeIndex == 1
            && nodes[1].Equals(Node(1, 1, 0x300000, 3, -1))
            && nodes[2].Equals(tailCanary);
    }

    private static unsafe bool TestTailAllocation()
    {
        BootPageFreeListNode* nodes = stackalloc BootPageFreeListNode[2];
        nodes[0] = Node(0, 0, 0x101000, 4, -1);
        nodes[1] = default;
        BootPageFreeListNode tailCanary = nodes[1];
        BootPageAllocatorState state = State(0, 1, 4, 0);

        BootPageAllocatorOnePageResult result = Allocate(
            state,
            nodes,
            1,
            2,
            null,
            0,
            0,
            alignment: 4 * PageSize);

        return IsExactSuccess(
                result,
                state,
                1,
                3,
                1,
                0x104000,
                1,
                2,
                0,
                0,
                0,
                -1,
                1)
            && nodes[0].Equals(Node(0, 0, 0x101000, 3, -1))
            && nodes[1].Equals(tailCanary);
    }

    private static unsafe bool TestExactAllocation()
    {
        BootPageFreeListNode* nodes = stackalloc BootPageFreeListNode[2];
        nodes[0] = Node(0, 0, 0x500000, 1, -1);
        nodes[1] = default;
        BootPageFreeListNode tailCanary = nodes[1];
        BootPageAllocatorState state = State(0, 1, 1, 3);

        BootPageAllocatorOnePageResult result = Allocate(
            state,
            nodes,
            1,
            2,
            null,
            0,
            0,
            PageSize);

        BootPageFreeListNode* laterNodes =
            stackalloc BootPageFreeListNode[3];
        laterNodes[0] = Node(0, 0, 0x101000, 1, 1);
        laterNodes[1] = Node(1, 1, 0x104000, 1, -1);
        laterNodes[2] = default;
        BootPageFreeListNode laterTailCanary = laterNodes[2];
        BootPageAllocatorState laterState = State(0, 2, 2, 3);
        BootPageAllocatorOnePageResult laterResult = Allocate(
            laterState,
            laterNodes,
            2,
            3,
            null,
            0,
            0,
            4 * PageSize);

        return IsExactSuccess(
                result,
                state,
                0,
                0,
                4,
                0x500000,
                1,
                2,
                0,
                0,
                0,
                -1,
                1)
            && result.AfterState.HeadNodeIndex == -1
            && IsDefaultNode(nodes[0])
            && nodes[1].Equals(tailCanary)
            && IsExactSuccess(
                laterResult,
                laterState,
                1,
                1,
                4,
                0x104000,
                2,
                3,
                0,
                0,
                1,
                -1,
                2)
            && laterResult.AfterState.HeadNodeIndex == 0
            && laterNodes[0].NextNodeIndex == -1
            && IsDefaultNode(laterNodes[1])
            && laterNodes[2].Equals(laterTailCanary);
    }

    private static unsafe bool TestSplitAllocation()
    {
        BootPageFreeListNode* nodes = stackalloc BootPageFreeListNode[3];
        nodes[0] = Node(0, 0, 0x101000, 8, -1);
        nodes[1] = default;
        nodes[2] = RawNode(
            77,
            77,
            0,
            0,
            0,
            0,
            -1);
        BootPageFreeListNode tailCanary = nodes[2];
        BootPageAllocatorState state = State(0, 1, 8, 1);

        BootPageAllocatorOnePageResult result = Allocate(
            state,
            nodes,
            2,
            3,
            null,
            0,
            0,
            4 * PageSize);

        return IsExactSuccess(
                result,
                state,
                2,
                7,
                2,
                0x104000,
                2,
                3,
                0,
                0,
                0,
                1,
                1)
            && nodes[0].Equals(Node(0, 0, 0x101000, 3, 1))
            && nodes[1].Equals(Node(0, 0, 0x105000, 4, -1))
            && nodes[2].Equals(tailCanary);
    }

    private static unsafe bool TestResultEvidence()
    {
        BootPageFreeListNode* nodes = stackalloc BootPageFreeListNode[2];
        nodes[0] = Node(0, 0, 0x800000, 2, -1);
        nodes[1] = default;
        BootPageAllocatorState state = State(0, 1, 2, 5);

        BootPageAllocatorOnePageResult result = Allocate(
            state,
            nodes,
            1,
            2,
            null,
            0,
            0,
            PageSize);

        return IsExactSuccess(
                result,
                state,
                1,
                1,
                6,
                0x800000,
                1,
                2,
                0,
                0,
                0,
                -1,
                1)
            && result.BeforeFreeNodeCount == 1
            && result.AfterFreeNodeCount == 1
            && result.BeforeFreePageCount == 2
            && result.AfterFreePageCount == 1
            && result.BeforeFreeByteCount == 2 * PageSize
            && result.AfterFreeByteCount == PageSize
            && result.BeforeAllocatedPageCount == 5
            && result.AfterAllocatedPageCount == 6
            && result.BeforeAllocatedByteCount == 5 * PageSize
            && result.AfterAllocatedByteCount == 6 * PageSize;
    }

    private static unsafe bool TestCanonicalReservations()
    {
        BootPageFreeListNode* nodes = stackalloc BootPageFreeListNode[2];
        nodes[0] = Node(0, 0, 0x200000, 2, -1);
        nodes[1] = Node(77, 77, 0x900000, 1, -1);
        BootReservedRange* reservations = stackalloc BootReservedRange[3];
        reservations[0] = Reservation(
            BootReservationKind.LoadedImage,
            0x100000,
            2);
        reservations[1] = Reservation(
            BootReservationKind.BootStack,
            0x202000,
            1);
        reservations[2] = default;
        byte[] reservationsBefore = PointerBytes(reservations, 3);
        BootReservedRange tailCanary = reservations[2];
        BootPageAllocatorState state = State(0, 1, 2, 0);

        BootPageAllocatorOnePageResult result = Allocate(
            state,
            nodes,
            1,
            2,
            reservations,
            reservationCount: 2,
            reservationCapacity: 3,
            alignment: PageSize);

        return IsExactSuccess(
                result,
                state,
                1,
                1,
                1,
                0x200000,
                1,
                2,
                2,
                3,
                0,
                -1,
                1)
            && reservationsBefore.AsSpan().SequenceEqual(
                PointerBytes(reservations, 3))
            && reservations[2].Equals(tailCanary);
    }

    private static unsafe bool TestCallerPrefixAndCanaries()
    {
        BootPageFreeListNode* nodes = stackalloc BootPageFreeListNode[4];
        nodes[0] = Node(0, 0, 0x101000, 8, -1);
        nodes[1] = default;
        nodes[2] = default;
        nodes[3] = RawNode(
            78,
            78,
            0,
            0,
            0,
            0,
            -1);
        BootPageFreeListNode canary2 = nodes[2];
        BootPageFreeListNode canary3 = nodes[3];
        BootPageAllocatorState state = State(0, 1, 8, 0);

        BootPageAllocatorOnePageResult result = Allocate(
            state,
            nodes,
            nodeCount: 2,
            nodeCapacity: 4,
            reservations: null,
            reservationCount: 0,
            reservationCapacity: 0,
            alignment: 4 * PageSize);

        return result.Succeeded
            && result.NodeCount == 2
            && result.NodeCapacity == 4
            && nodes[2].Equals(canary2)
            && nodes[3].Equals(canary3);
    }

    private static bool TestInvalidStates()
    {
        BootPageFreeListNode[] nodes =
        [
            Node(0, 0, 0x100000, 2, -1),
            Node(77, 77, 0x700000, 1, -1)
        ];
        BootPageAllocatorState wrongPageSize = new BootPageAllocatorState(
            isInitialized: true,
            pageSize: 8192,
            headNodeIndex: 0,
            freeNodeCount: 1,
            freePageCount: 2,
            freeByteCount: 2 * 8192,
            allocatedPageCount: 0,
            allocatedByteCount: 0);
        BootPageAllocatorState zeroPageSize = new BootPageAllocatorState(
            isInitialized: true,
            pageSize: 0,
            headNodeIndex: 0,
            freeNodeCount: 1,
            freePageCount: 2,
            freeByteCount: 2 * PageSize,
            allocatedPageCount: 0,
            allocatedByteCount: 0);
        BootPageAllocatorState freeByteMismatch = new BootPageAllocatorState(
            isInitialized: true,
            pageSize: PageSize,
            headNodeIndex: 0,
            freeNodeCount: 1,
            freePageCount: 2,
            freeByteCount: PageSize,
            allocatedPageCount: 0,
            allocatedByteCount: 0);
        BootPageAllocatorState allocatedByteMismatch =
            new BootPageAllocatorState(
                isInitialized: true,
                pageSize: PageSize,
                headNodeIndex: 0,
                freeNodeCount: 1,
                freePageCount: 2,
                freeByteCount: 2 * PageSize,
                allocatedPageCount: 2,
                allocatedByteCount: PageSize);
        BootPageAllocatorState negativeNodeCount =
            new BootPageAllocatorState(
                isInitialized: true,
                pageSize: PageSize,
                headNodeIndex: 0,
                freeNodeCount: -1,
                freePageCount: 2,
                freeByteCount: 2 * PageSize,
                allocatedPageCount: 0,
                allocatedByteCount: 0);
        ulong maximumRepresentablePageCount =
            ulong.MaxValue / PageSize;
        BootPageAllocatorState aggregatePageOverflow =
            new BootPageAllocatorState(
                isInitialized: true,
                pageSize: PageSize,
                headNodeIndex: 0,
                freeNodeCount: 1,
                freePageCount: 1,
                freeByteCount: PageSize,
                allocatedPageCount: maximumRepresentablePageCount,
                allocatedByteCount:
                    maximumRepresentablePageCount * PageSize);
        BootPageAllocatorState allocatedDomainOverflow =
            new BootPageAllocatorState(
                isInitialized: true,
                pageSize: PageSize,
                headNodeIndex: 0,
                freeNodeCount: 1,
                freePageCount: 2,
                freeByteCount: 2 * PageSize,
                allocatedPageCount: ulong.MaxValue,
                allocatedByteCount: ulong.MaxValue);
        BootPageAllocatorState fewerPagesThanNodes =
            new BootPageAllocatorState(
                isInitialized: true,
                pageSize: PageSize,
                headNodeIndex: 0,
                freeNodeCount: 2,
                freePageCount: 1,
                freeByteCount: PageSize,
                allocatedPageCount: 0,
                allocatedByteCount: 0);

        return FailurePreserves(
                default,
                nodes,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.StateNotInitialized)
            && FailurePreserves(
                wrongPageSize,
                nodes,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidPageSize)
            && FailurePreserves(
                zeroPageSize,
                nodes,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidPageSize)
            && FailurePreserves(
                freeByteMismatch,
                nodes,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidStateAccounting)
            && FailurePreserves(
                allocatedByteMismatch,
                nodes,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidStateAccounting)
            && FailurePreserves(
                negativeNodeCount,
                nodes,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidStateAccounting)
            && FailurePreserves(
                aggregatePageOverflow,
                nodes,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidStateAccounting)
            && FailurePreserves(
                allocatedDomainOverflow,
                nodes,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidStateAccounting)
            && FailurePreserves(
                fewerPagesThanNodes,
                nodes,
                2,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidStateAccounting);
    }

    private static bool TestInvalidRequestShape()
    {
        BootPageFreeListNode[] nodes =
        [
            Node(0, 0, 0x100000, 2, -1),
            Node(77, 77, 0x700000, 1, -1)
        ];
        BootPageAllocatorState state = State(0, 1, 2, 0);

        return FailurePreserves(
                state,
                nodes,
                1,
                2,
                [],
                0,
                0,
                0,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidPageCount)
            && FailurePreserves(
                state,
                nodes,
                1,
                2,
                [],
                0,
                0,
                2,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidPageCount)
            && FailurePreserves(
                state,
                nodes,
                1,
                2,
                [],
                0,
                0,
                ~0UL,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidPageCount)
            && FailurePreserves(
                state,
                nodes,
                1,
                2,
                [],
                0,
                0,
                1,
                0,
                BootPageAllocatorMutationStatus.InvalidAlignment)
            && FailurePreserves(
                state,
                nodes,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize / 2,
                BootPageAllocatorMutationStatus.InvalidAlignment)
            && FailurePreserves(
                state,
                nodes,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize + 1,
                BootPageAllocatorMutationStatus.InvalidAlignment)
            && FailurePreserves(
                state,
                nodes,
                1,
                2,
                [],
                0,
                0,
                1,
                3 * PageSize,
                BootPageAllocatorMutationStatus.InvalidAlignment);
    }

    private static bool TestMalformedNodes()
    {
        BootPageAllocatorState state = State(0, 1, 2, 0);
        BootPageFreeListNode valid = Node(0, 0, 0x100000, 2, -1);
        BootPageFreeListNode[] defaults = [default, valid];
        BootPageFreeListNode[] negativeProvenance =
        [
            new BootPageFreeListNode(
                -1,
                0,
                0x100000,
                0x102000,
                2,
                2 * PageSize,
                -1),
            valid
        ];
        BootPageFreeListNode[] zeroStart =
        [
            RawNode(0, 0, 0, 0x2000, 2, 2 * PageSize, -1),
            valid
        ];
        BootPageFreeListNode[] misaligned =
        [
            RawNode(
                0,
                0,
                0x100001,
                0x102001,
                2,
                2 * PageSize,
                -1),
            valid
        ];
        BootPageFreeListNode[] zeroPages =
        [
            RawNode(0, 0, 0x100000, 0x100000, 0, 0, -1),
            valid
        ];
        BootPageFreeListNode[] byteMismatch =
        [
            RawNode(0, 0, 0x100000, 0x102000, 2, PageSize, -1),
            valid
        ];
        BootPageFreeListNode[] endMismatch =
        [
            RawNode(
                0,
                0,
                0x100000,
                0x103000,
                2,
                2 * PageSize,
                -1),
            valid
        ];
        BootPageFreeListNode[] wrapping =
        [
            RawNode(
                0,
                0,
                0xFFFF_FFFF_FFFF_F000,
                0x1000,
                1,
                PageSize,
                -1),
            valid
        ];

        return InvalidNodeFailure(state, defaults)
            && InvalidNodeFailure(state, negativeProvenance)
            && InvalidNodeFailure(state, zeroStart)
            && InvalidNodeFailure(state, misaligned)
            && InvalidNodeFailure(state, zeroPages)
            && InvalidNodeFailure(state, byteMismatch)
            && InvalidNodeFailure(state, endMismatch)
            && InvalidNodeFailure(state, wrapping);
    }

    private static bool TestNodeIndexesChainsAndCycles()
    {
        BootPageFreeListNode tail = Node(77, 77, 0x700000, 1, -1);
        BootPageFreeListNode[] valid =
        [
            Node(0, 0, 0x100000, 2, -1),
            tail
        ];
        BootPageFreeListNode[] negativeNext =
        [
            RawNode(0, 0, 0x100000, 0x102000, 2, 2 * PageSize, -2),
            tail
        ];
        BootPageFreeListNode[] highNext =
        [
            Node(0, 0, 0x100000, 2, 2),
            tail
        ];
        BootPageFreeListNode[] selfCycle =
        [
            Node(0, 0, 0x100000, 2, 0),
            tail
        ];
        BootPageFreeListNode[] multiCycle =
        [
            Node(0, 0, 0x100000, 1, 1),
            Node(1, 1, 0x200000, 1, 0),
            tail
        ];
        BootPageFreeListNode[] disconnected =
        [
            Node(0, 0, 0x100000, 2, -1),
            Node(1, 1, 0x200000, 1, -1),
            tail
        ];

        return FailurePreserves(
                State(-2, 1, 2, 0),
                valid,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidHeadNodeIndex)
            && FailurePreserves(
                State(2, 1, 2, 0),
                valid,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidHeadNodeIndex)
            && FailurePreserves(
                State(0, 1, 2, 0),
                negativeNext,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidNextNodeIndex)
            && FailurePreserves(
                State(0, 1, 2, 0),
                highNext,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidNextNodeIndex)
            && FailurePreserves(
                State(0, 1, 2, 0),
                selfCycle,
                1,
                2,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.FreeListCycleDetected)
            && FailurePreserves(
                State(0, 2, 2, 0),
                multiCycle,
                2,
                3,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.FreeListCycleDetected)
            && FailurePreserves(
                State(0, 2, 3, 0),
                disconnected,
                2,
                3,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.FreeListNodeCountMismatch);
    }

    private static bool TestNodeOrderingAndAccounting()
    {
        BootPageFreeListNode[] unsorted =
        [
            Node(0, 0, 0x200000, 2, 1),
            Node(1, 1, 0x100000, 2, -1),
            Node(77, 77, 0x700000, 1, -1)
        ];
        BootPageFreeListNode[] overlap =
        [
            Node(0, 0, 0x100000, 3, 1),
            Node(1, 1, 0x102000, 2, -1),
            Node(77, 77, 0x700000, 1, -1)
        ];
        BootPageFreeListNode[] valid =
        [
            Node(0, 0, 0x100000, 2, 1),
            Node(1, 1, 0x200000, 2, -1),
            Node(77, 77, 0x700000, 1, -1)
        ];

        return FailurePreserves(
                State(0, 2, 4, 0),
                unsorted,
                2,
                3,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidNodeShape)
            && FailurePreserves(
                State(0, 2, 5, 0),
                overlap,
                2,
                3,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidNodeShape)
            && FailurePreserves(
                State(0, 2, 3, 0),
                valid,
                2,
                3,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.TotalPageCountMismatch)
            && FailurePreserves(
                new BootPageAllocatorState(
                    true,
                    PageSize,
                    0,
                    2,
                    4,
                    3 * PageSize,
                    0,
                    0),
                valid,
                2,
                3,
                [],
                0,
                0,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidStateAccounting);
    }

    private static bool TestInvalidReservations()
    {
        BootPageFreeListNode[] nodes =
        [
            Node(0, 0, 0x300000, 2, -1),
            Node(77, 77, 0x700000, 1, -1)
        ];
        BootPageAllocatorState state = State(0, 1, 2, 0);
        BootReservedRange malformed = MutateByte(
            Reservation(BootReservationKind.LoadedImage, 0x100000, 1),
            1,
            1);
        BootReservedRange[] defaults = [default, default];
        BootReservedRange[] malformedTable = [malformed, default];
        BootReservedRange[] unsorted =
        [
            Reservation(BootReservationKind.LoadedImage, 0x200000, 1),
            Reservation(BootReservationKind.BootStack, 0x100000, 1),
            default
        ];
        BootReservedRange[] overlapping =
        [
            Reservation(BootReservationKind.LoadedImage, 0x100000, 2),
            Reservation(BootReservationKind.BootStack, 0x101000, 2),
            default
        ];
        BootReservedRange[] duplicate =
        [
            Reservation(BootReservationKind.LoadedImage, 0x100000, 1),
            Reservation(BootReservationKind.BootStack, 0x100000, 1),
            default
        ];

        return ReservationFailure(
                state,
                nodes,
                defaults,
                1,
                BootPageAllocatorMutationStatus.InvalidReservation,
                BootReservedRangeStatus.UninitializedKind)
            && ReservationFailure(
                state,
                nodes,
                malformedTable,
                1,
                BootPageAllocatorMutationStatus.InvalidReservation,
                BootReservedRangeStatus.ReservedBitsSet)
            && ReservationFailure(
                state,
                nodes,
                unsorted,
                2,
                BootPageAllocatorMutationStatus.UnsortedReservation,
                BootReservedRangeStatus.Success)
            && ReservationFailure(
                state,
                nodes,
                overlapping,
                2,
                BootPageAllocatorMutationStatus.OverlappingReservation,
                BootReservedRangeStatus.Success)
            && ReservationFailure(
                state,
                nodes,
                duplicate,
                2,
                BootPageAllocatorMutationStatus.OverlappingReservation,
                BootReservedRangeStatus.Success);
    }

    private static bool TestReservationOverlap()
    {
        BootPageFreeListNode[] nodes =
        [
            Node(0, 0, 0x300000, 4, -1),
            Node(77, 77, 0x700000, 1, -1)
        ];
        BootPageAllocatorState state = State(0, 1, 4, 0);
        BootReservedRange[][] overlaps =
        [
            [Reservation(BootReservationKind.LoadedImage, 0x300000, 1)],
            [Reservation(BootReservationKind.LoadedImage, 0x302000, 1)],
            [Reservation(BootReservationKind.LoadedImage, 0x303000, 2)],
            [Reservation(BootReservationKind.LoadedImage, 0x2FF000, 2)]
        ];

        foreach (BootReservedRange[] reservations in overlaps)
        {
            if (!FailurePreserves(
                    state,
                    nodes,
                    1,
                    2,
                    reservations,
                    1,
                    1,
                    1,
                    PageSize,
                    BootPageAllocatorMutationStatus
                        .ReservationOverlapsFreeRange))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestExhaustionAndMetadataCapacity()
    {
        BootPageFreeListNode[] noRange =
        [
            Node(0, 0, 0x101000, 1, -1),
            Node(77, 77, 0x700000, 1, -1)
        ];
        BootPageFreeListNode[] noSplitMetadata =
        [
            Node(0, 0, 0x101000, 8, -1),
            default
        ];
        BootPageFreeListNode[] staleUnlinkedSlot =
        [
            Node(0, 0, 0x101000, 8, -1),
            Node(77, 77, 0x700000, 1, -1)
        ];
        BootPageFreeListNode[] alignmentWrap =
        [
            Node(
                0,
                0,
                0xFFFF_FFFF_FFFF_E000,
                1,
                -1),
            default
        ];

        return FailurePreserves(
                State(0, 1, 1, 0),
                noRange,
                1,
                2,
                [],
                0,
                0,
                1,
                4 * PageSize,
                BootPageAllocatorMutationStatus.NoSuitableRange)
            && FailurePreserves(
                State(0, 1, 8, 0),
                noSplitMetadata,
                nodeCount: 1,
                nodeCapacity: 2,
                reservations: [],
                reservationCount: 0,
                reservationCapacity: 0,
                pageCount: 1,
                alignment: 4 * PageSize,
                BootPageAllocatorMutationStatus.MetadataCapacityTooSmall)
            && FailurePreserves(
                State(0, 1, 8, 0),
                staleUnlinkedSlot,
                nodeCount: 2,
                nodeCapacity: 2,
                reservations: [],
                reservationCount: 0,
                reservationCapacity: 0,
                pageCount: 1,
                alignment: 4 * PageSize,
                BootPageAllocatorMutationStatus.MetadataCapacityTooSmall)
            && FailurePreserves(
                State(0, 1, 1, 0),
                alignmentWrap,
                nodeCount: 1,
                nodeCapacity: 2,
                reservations: [],
                reservationCount: 0,
                reservationCapacity: 0,
                pageCount: 1,
                alignment: 4 * PageSize,
                BootPageAllocatorMutationStatus
                    .AllocationAddressOverflow,
                expectedAddressStatus:
                    AddressValueStatus.AddressOverflow);
    }

    private static unsafe bool TestPointerPreflight()
    {
        BootPageAllocatorState populated = State(0, 1, 2, 0);
        BootPageAllocatorState empty = State(-1, 0, 0, 0);
        BootPageFreeListNode* nodes =
            stackalloc BootPageFreeListNode[2];
        nodes[0] = Node(0, 0, 0x100000, 2, -1);
        nodes[1] = Node(77, 77, 0x700000, 1, -1);
        BootReservedRange* reservations =
            stackalloc BootReservedRange[2];
        reservations[0] = Reservation(
            BootReservationKind.LoadedImage,
            0x500000,
            1);
        reservations[1] = Reservation(
            BootReservationKind.BootStack,
            0x600000,
            1);
        BootReservedRange* spareReservations =
            stackalloc BootReservedRange[2];
        spareReservations[0] = default;
        spareReservations[1] = MutateByte(
            Reservation(
                BootReservationKind.LoadedImage,
                0x800000,
                1),
            1,
            1);

        if (!DirectPointerFailure(
                populated,
                nodes,
                2,
                1,
                null,
                0,
                0,
                BootPageAllocatorMutationStatus.NodeCapacityTooSmall))
        {
            return false;
        }

        if (!DirectPointerFailure(
                populated,
                nodes,
                1,
                2,
                reservations,
                2,
                1,
                BootPageAllocatorMutationStatus
                    .ReservationCapacityTooSmall)
            || !RunHostilePointerPreflightSubprocesses())
        {
            return false;
        }

        BootPageAllocatorOnePageResult emptyResult =
            BootPageAllocatorMutator.AllocateOnePage(
                empty,
                null,
                0,
                0,
                null,
                0,
                0,
                1,
                PageSize);
        BootPageAllocatorOnePageResult spareReservationResult =
            BootPageAllocatorMutator.AllocateOnePage(
                populated,
                nodes,
                1,
                2,
                spareReservations,
                0,
                2,
                1,
                PageSize);
        return IsExactFailure(
                emptyResult,
                empty,
                BootPageAllocatorMutationStatus.NoSuitableRange)
            && spareReservationResult.Succeeded
            && spareReservationResult.ReservationCount == 0
            && spareReservationResult.ReservationCapacity == 2;
    }

    private static bool RunHostilePointerPreflightSubprocesses()
    {
        string workspace = CreateHostilePointerWorkspace();
        try
        {
            ToolResult build = RunHostilePointerBuild(workspace);
            if (build.TimedOut || build.ExitCode != 0)
            {
                Console.WriteLine(
                    "M01_008_POINTER_SUBPROCESS_DIAGNOSTIC "
                    + "build="
                    + NormalizeCompilerOutput(build.Output));
                return false;
            }

            string assemblyPath = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.PointerPreflightProbe.dll");
            string[] cases =
            [
                "node-count-over",
                "node-capacity-over",
                "node-byte-over",
                "node-missing",
                "node-misaligned",
                "node-span-wrap",
                "reservation-count-over",
                "reservation-capacity-over",
                "reservation-byte-over",
                "reservation-missing",
                "reservation-misaligned",
                "reservation-span-wrap"
            ];
            foreach (string testCase in cases)
            {
                ToolResult run = RunHostilePointerCase(
                    workspace,
                    assemblyPath,
                    testCase);
                if (run.TimedOut || run.ExitCode != 0)
                {
                    Console.WriteLine(
                        "M01_008_POINTER_SUBPROCESS_DIAGNOSTIC "
                        + "case="
                        + testCase
                        + ";exit="
                        + run.ExitCode
                        + ";timedOut="
                        + run.TimedOut
                        + ";output="
                        + NormalizeCompilerOutput(run.Output));
                    return false;
                }
            }

            return true;
        }
        finally
        {
            DeleteNoStdLibWorkspace(workspace);
        }
    }

    private static string CreateHostilePointerWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-008-"
                + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);

        try
        {
            File.Copy(
                Path.Combine(repositoryRoot, "global.json"),
                Path.Combine(workspace, "global.json"));
            File.WriteAllText(
                Path.Combine(workspace, "Program.cs"),
                """
                using OS.Kernel.Memory;

                internal static unsafe class Program
                {
                    private const ulong MaximumUnsignedLong = ~0UL;

                    private static int Main(string[] args)
                    {
                        if (args.Length != 1)
                        {
                            return 64;
                        }

                        BootPageFreeListNode* nodes =
                            stackalloc BootPageFreeListNode[2];
                        nodes[0] = new BootPageFreeListNode(
                            0,
                            0,
                            0x100000,
                            0x102000,
                            2,
                            8192,
                            -1);
                        nodes[1] = default;
                        BootReservedRange* reservations =
                            stackalloc BootReservedRange[1];
                        reservations[0] = default;
                        BootPageAllocatorState state =
                            new BootPageAllocatorState(
                                true,
                                4096,
                                0,
                                1,
                                2,
                                8192,
                                0,
                                0);

                        BootPageFreeListNode* nodePointer = nodes;
                        ulong nodeCount = 1;
                        ulong nodeCapacity = 2;
                        BootReservedRange* reservationPointer = null;
                        ulong reservationCount = 0;
                        ulong reservationCapacity = 0;
                        BootPageAllocatorMutationStatus expected;

                        switch (args[0])
                        {
                            case "node-count-over":
                                nodeCount =
                                    BootPageAllocatorMutator
                                        .MaximumNodeCount + 1;
                                nodeCapacity = nodeCount;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .TooManyNodes;
                                break;
                            case "node-capacity-over":
                                nodeCapacity =
                                    BootPageAllocatorMutator
                                        .MaximumNodeCount + 1;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .TooManyNodes;
                                break;
                            case "node-byte-over":
                                nodePointer =
                                    (BootPageFreeListNode*)8;
                                nodeCapacity =
                                    (MaximumUnsignedLong
                                        / BootPageFreeListNode
                                            .StructureSize) + 1;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .NodeTableByteCountOverflow;
                                break;
                            case "node-missing":
                                nodePointer = null;
                                nodeCapacity = 1;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .MissingNodes;
                                break;
                            case "node-misaligned":
                                nodePointer =
                                    (BootPageFreeListNode*)1;
                                nodeCapacity = 1;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .MisalignedNodes;
                                break;
                            case "node-span-wrap":
                                nodePointer =
                                    (BootPageFreeListNode*)unchecked(
                                        (nuint)
                                            0xFFFF_FFFF_FFFF_FFF8UL);
                                nodeCapacity = 1;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .NodeTableAddressRangeOverflow;
                                break;
                            case "reservation-count-over":
                                reservationPointer = reservations;
                                reservationCount =
                                    BootPageAllocatorMutator
                                        .MaximumReservationCount + 1;
                                reservationCapacity = reservationCount;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .TooManyReservations;
                                break;
                            case "reservation-capacity-over":
                                reservationPointer = reservations;
                                reservationCount = 1;
                                reservationCapacity =
                                    BootPageAllocatorMutator
                                        .MaximumReservationCount + 1;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .TooManyReservations;
                                break;
                            case "reservation-byte-over":
                                reservationPointer =
                                    (BootReservedRange*)8;
                                reservationCount = 1;
                                reservationCapacity =
                                    (MaximumUnsignedLong / 40) + 1;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .ReservationTableByteCountOverflow;
                                break;
                            case "reservation-missing":
                                reservationCount = 1;
                                reservationCapacity = 1;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .MissingReservations;
                                break;
                            case "reservation-misaligned":
                                reservationPointer =
                                    (BootReservedRange*)1;
                                reservationCount = 1;
                                reservationCapacity = 1;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .MisalignedReservations;
                                break;
                            case "reservation-span-wrap":
                                reservationPointer =
                                    (BootReservedRange*)unchecked(
                                        (nuint)
                                            0xFFFF_FFFF_FFFF_FFF8UL);
                                reservationCount = 1;
                                reservationCapacity = 1;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .ReservationTableAddressRangeOverflow;
                                break;
                            default:
                                return 65;
                        }

                        BootPageAllocatorOnePageResult result =
                            BootPageAllocatorMutator.AllocateOnePage(
                                state,
                                nodePointer,
                                nodeCount,
                                nodeCapacity,
                                reservationPointer,
                                reservationCount,
                                reservationCapacity,
                                1,
                                4096);
                        if (result.Succeeded
                            || result.Status != expected)
                        {
                            return 2;
                        }

                        return 0;
                    }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            string projectText =
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <AssemblyName>CLROS.M01.PointerPreflightProbe</AssemblyName>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <LangVersion>14.0</LangVersion>
                    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <Deterministic>true</Deterministic>
                    <DebugType>none</DebugType>
                    <DebugSymbols>false</DebugSymbols>
                    <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
                    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
                    <OutputPath>$(MSBuildProjectDirectory)\out\</OutputPath>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="__KERNEL_PROJECT__" />
                  </ItemGroup>
                </Project>
                """
                .Replace(
                    "__KERNEL_PROJECT__",
                    EscapeXml(
                        Path.Combine(
                            repositoryRoot,
                            "src",
                            "OS.Kernel",
                            "OS.Kernel.csproj")),
                    StringComparison.Ordinal);
            File.WriteAllText(
                Path.Combine(
                    workspace,
                    "M01.008.PointerPreflightProbe.csproj"),
                projectText,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
            return workspace;
        }
        catch
        {
            DeleteNoStdLibWorkspace(workspace);
            throw;
        }
    }

    private static ToolResult RunHostilePointerBuild(string workspace)
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
        start.ArgumentList.Add(
            "M01.008.PointerPreflightProbe.csproj");
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("-m:1");
        start.ArgumentList.Add("--no-incremental");
        start.ArgumentList.Add("--disable-build-servers");
        start.ArgumentList.Add("-v:minimal");
        start.ArgumentList.Add(
            "-p:RestoreIgnoreFailedSources=true");
        start.ArgumentList.Add("-p:NuGetAudit=false");
        ConfigureDotnetEnvironment(start);
        return RunProcess(start, timeoutMilliseconds: 120_000);
    }

    private static ToolResult RunHostilePointerCase(
        string workspace,
        string assemblyPath,
        string testCase)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workspace
        };
        start.ArgumentList.Add(assemblyPath);
        start.ArgumentList.Add(testCase);
        ConfigureDotnetEnvironment(start);
        return RunProcess(start, timeoutMilliseconds: 15_000);
    }

    private static unsafe bool TestTableAliases()
    {
        BootPageAllocatorState state = State(0, 1, 2, 0);
        BootPageFreeListNode* table =
            stackalloc BootPageFreeListNode[3];
        table[0] = Node(0, 0, 0x100000, 2, -1);
        table[1] = Node(71, 71, 0x710000, 1, -1);
        table[2] = Node(72, 72, 0x720000, 1, -1);
        byte[] before = PointerBytes(table, 3);

        BootPageAllocatorOnePageResult sameBase =
            BootPageAllocatorMutator.AllocateOnePage(
                state,
                table,
                1,
                1,
                (BootReservedRange*)table,
                0,
                1,
                1,
                PageSize);
        if (!IsExactFailure(
                sameBase,
                state,
                BootPageAllocatorMutationStatus
                    .NodeReservationTablesOverlap)
            || !before.AsSpan().SequenceEqual(PointerBytes(table, 3)))
        {
            return false;
        }

        BootPageAllocatorOnePageResult nodeTail =
            BootPageAllocatorMutator.AllocateOnePage(
                state,
                table,
                1,
                2,
                (BootReservedRange*)(table + 1),
                0,
                1,
                1,
                PageSize);
        if (!IsExactFailure(
                nodeTail,
                state,
                BootPageAllocatorMutationStatus
                    .NodeReservationTablesOverlap)
            || !before.AsSpan().SequenceEqual(PointerBytes(table, 3)))
        {
            return false;
        }

        byte* bytes = stackalloc byte[160];
        nuint aligned = ((nuint)bytes + 7) & ~(nuint)7;
        BootReservedRange* reservationTable =
            (BootReservedRange*)aligned;
        BootPageFreeListNode* nodeInsideReservationTail =
            (BootPageFreeListNode*)(aligned + 40);
        byte[] reverseBefore = PointerBytes(
            (byte*)aligned,
            128);
        BootPageAllocatorOnePageResult reverse =
            BootPageAllocatorMutator.AllocateOnePage(
                State(-1, 0, 0, 0),
                nodeInsideReservationTail,
                0,
                1,
                reservationTable,
                0,
                2,
                1,
                PageSize);
        return IsExactFailure(
                reverse,
                State(-1, 0, 0, 0),
                BootPageAllocatorMutationStatus
                    .NodeReservationTablesOverlap)
            && reverseBefore.AsSpan().SequenceEqual(
                PointerBytes((byte*)aligned, 128));
    }

    private static bool TestFailureAtomicity()
    {
        BootPageFreeListNode[] nodes =
        [
            Node(0, 0, 0x101000, 8, -1),
            Node(77, 77, 0x700000, 1, -1),
            Node(78, 78, 0x710000, 1, -1)
        ];
        BootReservedRange[] reservations =
        [
            Reservation(BootReservationKind.LoadedImage, 0x500000, 1),
            Reservation(BootReservationKind.BootStack, 0x600000, 1)
        ];
        BootPageAllocatorState state = State(0, 1, 8, 5);

        return FailurePreserves(
                state,
                nodes,
                1,
                3,
                reservations,
                1,
                2,
                1,
                4 * PageSize,
                BootPageAllocatorMutationStatus.MetadataCapacityTooSmall)
            && FailurePreserves(
                state,
                nodes,
                1,
                3,
                reservations,
                1,
                2,
                0,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidPageCount)
            && FailurePreserves(
                new BootPageAllocatorState(
                    true,
                    PageSize,
                    0,
                    1,
                    8,
                    7 * PageSize,
                    5,
                    5 * PageSize),
                nodes,
                1,
                3,
                reservations,
                1,
                2,
                1,
                PageSize,
                BootPageAllocatorMutationStatus.InvalidStateAccounting)
            && FailurePreserves(
                state,
                nodes,
                1,
                3,
                [
                    Reservation(
                        BootReservationKind.LoadedImage,
                        0x101000,
                        1),
                    reservations[1]
                ],
                1,
                2,
                1,
                PageSize,
                BootPageAllocatorMutationStatus
                    .ReservationOverlapsFreeRange);
    }

    private static unsafe bool TestDeterminismAndAllocation()
    {
        BootPageFreeListNode* first =
            stackalloc BootPageFreeListNode[3];
        BootPageFreeListNode* second =
            stackalloc BootPageFreeListNode[3];
        for (int index = 0; index < 3; index++)
        {
            BootPageFreeListNode value = index switch
            {
                0 => Node(0, 0, 0x101000, 8, -1),
                1 => default,
                _ => Node(
                    72,
                    72,
                    0x720000,
                    1,
                    -1)
            };
            first[index] = value;
            second[index] = value;
        }

        BootReservedRange* firstReservations =
            stackalloc BootReservedRange[2];
        BootReservedRange* secondReservations =
            stackalloc BootReservedRange[2];
        for (int index = 0; index < 2; index++)
        {
            BootReservedRange value = Reservation(
                index == 0
                    ? BootReservationKind.LoadedImage
                    : BootReservationKind.BootStack,
                0x500000 + ((ulong)index * 0x10000),
                1);
            firstReservations[index] = value;
            secondReservations[index] = value;
        }

        BootPageAllocatorState state = State(0, 1, 8, 3);
        _ = BootPageAllocatorMutator.AllocateOnePage(
            state,
            second,
            2,
            3,
            secondReservations,
            1,
            2,
            1,
            4 * PageSize);

        second[0] = first[0];
        second[1] = first[1];
        second[2] = first[2];
        long allocatedBefore =
            GC.GetAllocatedBytesForCurrentThread();
        BootPageAllocatorOnePageResult firstResult =
            BootPageAllocatorMutator.AllocateOnePage(
                state,
                first,
                2,
                3,
                firstReservations,
                1,
                2,
                1,
                4 * PageSize);
        long allocatedAfter =
            GC.GetAllocatedBytesForCurrentThread();
        BootPageAllocatorOnePageResult secondResult =
            BootPageAllocatorMutator.AllocateOnePage(
                state,
                second,
                2,
                3,
                secondReservations,
                1,
                2,
                1,
                4 * PageSize);

        return firstResult.Succeeded
            && secondResult.Succeeded
            && allocatedAfter == allocatedBefore
            && OnePageResultsEqual(firstResult, secondResult)
            && PointerBytes(first, 3).AsSpan().SequenceEqual(
                PointerBytes(second, 3))
            && PointerBytes(firstReservations, 2).AsSpan().SequenceEqual(
                PointerBytes(secondReservations, 2));
    }

    private static bool TestForgedSuccessEvidence()
    {
        BootPageAllocatorState before = State(0, 1, 4, 7);
        BootPageAllocatorState after = State(0, 1, 3, 8);
        PhysicalAddress start = Address(0x100000);
        PhysicalAddress end = Address(0x101000);
        BootPageAllocatorOnePageResult coherent = InvokeSuccessFactory(
            before,
            after,
            start,
            end,
            1,
            2,
            0,
            0,
            0,
            -1,
            1);
        if (!coherent.Succeeded)
        {
            return false;
        }

        BootPageAllocatorOnePageResult[] forged =
        [
            InvokeSuccessFactory(
                before, after, start, Address(0x102000),
                1, 2, 0, 0, 0, -1, 1),
            InvokeSuccessFactory(
                before, after, start, end,
                2, 1, 0, 0, 0, -1, 1),
            InvokeSuccessFactory(
                before, after, start, end,
                1, 2, 1, 0, 0, -1, 1),
            InvokeSuccessFactory(
                before,
                new BootPageAllocatorState(
                    true,
                    PageSize,
                    0,
                    1,
                    4,
                    4 * PageSize,
                    8,
                    8 * PageSize),
                start, end, 1, 2, 0, 0, 0, -1, 1),
            InvokeSuccessFactory(
                before, after, start, end,
                1, 2, 0, 0, 1, -1, 1),
            InvokeSuccessFactory(
                before, after, start, end,
                1, 2, 0, 0, 0, 0, 1),
            InvokeSuccessFactory(
                before, after, start, end,
                1, 2, 0, 0, 0, -1, 0),
            InvokeSuccessFactory(
                before, after, start, end,
                1, 2, 0, 0, 0, -1, 2),
            InvokeSuccessFactory(
                before, after, start, end,
                2, 2, 0, 0, 1, -1, 1),
            InvokeSuccessFactory(
                State(0, 2, 2, 7),
                State(0, 2, 1, 8),
                start,
                end,
                2,
                2,
                0,
                0,
                0,
                -1,
                2)
        ];
        foreach (BootPageAllocatorOnePageResult result in forged)
        {
            if (result.Succeeded)
            {
                return false;
            }
        }

        BootPageAllocatorOnePageResult defaultWithInitializedByte =
            MutateByte(default(BootPageAllocatorOnePageResult), 0, 1);
        return defaultWithInitializedByte.IsInitialized
            && !defaultWithInitializedByte.Succeeded;
    }

    private static bool TestLegacySpanCompatibility()
    {
        BootPageFreeListNode[] nodes =
        [
            Node(0, 0, 0x100000, 3, -1),
            Node(77, 77, 0x700000, 1, -1)
        ];
        BootPageAllocatorMutationResult result =
            BootPageAllocatorMutator.Allocate(
                State(0, 1, 3, 0),
                nodes.AsSpan(),
                1,
                PageSize);

        return result.Succeeded
            && result.IsInitialized
            && result.Status
                == BootPageAllocatorMutationStatus.Success
            && result.PhysicalStart == 0x100000
            && result.PhysicalEndExclusive == 0x101000
            && result.PageCount == 1
            && result.ByteLength == PageSize
            && result.NodeIndex == 0
            && result.InsertedNodeIndex == -1
            && result.VisitedNodeCount == 1
            && result.State.IsInitialized
            && result.State.FreePageCount == 2
            && result.State.FreeByteCount == 2 * PageSize
            && result.State.AllocatedPageCount == 1
            && result.State.AllocatedByteCount == PageSize
            && nodes[0].Equals(Node(0, 0, 0x101000, 2, -1))
            && nodes[1].Equals(Node(77, 77, 0x700000, 1, -1));
    }

    private static bool TestSourceAndEfiFirewalls()
    {
        string productionPath = GetMutationSourcePath();
        string production = RemoveComments(
            File.ReadAllText(productionPath));
        string noStdLibCore = RemoveHostOnlyBlocks(production);
        string[] forbiddenCoreFragments =
        [
            "BootPageAllocatorState.cs",
            "BootPageFreeListPlan.cs",
            "BootPageAllocatorPlan.cs",
            "BootMemoryRegionPlan.cs",
            "BootPageFreeListPlanResult",
            "BootPageAllocatorPlanResult",
            "Span<",
            "ReadOnlySpan<",
            "System.",
            "string",
            "String",
            "Exception",
            "Reflection",
            "Activator",
            "typeof",
            "object",
            "dynamic",
            "delegate",
            "record ",
            "List<",
            "Dictionary<",
            "IEnumerable<",
            "ulong.MaxValue",
            "EfiEntry",
            "BootSequence",
            "Qemu",
            "QEMU",
            "OSHost_",
            "RuntimeExport",
            "UnmanagedCallersOnly"
        ];
        foreach (string fragment in forbiddenCoreFragments)
        {
            if (noStdLibCore.Contains(
                    fragment,
                    StringComparison.Ordinal))
            {
                return SourceFailure(
                    "nostdlib-fragment=" + fragment);
            }
        }

        if (Regex.IsMatch(
                noStdLibCore,
                @"(?m)^\s*(?:global\s+)?using\b",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                noStdLibCore,
                @"\b(?:throw|try|catch|finally)\b",
                RegexOptions.CultureInvariant)
            || Regex.IsMatch(
                noStdLibCore,
                @"\b(object|dynamic|delegate|record|Exception|String|Array|Type)\b",
                RegexOptions.CultureInvariant)
            || !noStdLibCore.Contains(
                "public static unsafe class BootPageAllocatorMutator",
                StringComparison.Ordinal)
            || !noStdLibCore.Contains(
                "public static BootPageAllocatorOnePageResult AllocateOnePage(",
                StringComparison.Ordinal)
            || !noStdLibCore.Contains(
                "BootPageFreeListNode* nodes",
                StringComparison.Ordinal)
            || !noStdLibCore.Contains(
                "BootReservedRange* reservations",
                StringComparison.Ordinal)
            || !production.Contains(
                "#if !NOSTDLIB_EFI",
                StringComparison.Ordinal)
            || !production.Contains(
                "Span<BootPageFreeListNode>",
                StringComparison.Ordinal)
            || noStdLibCore.Contains(
                "Span<BootPageFreeListNode>",
                StringComparison.Ordinal))
        {
            return SourceFailure("production-surface");
        }

        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string[] integrationPaths =
        [
            Path.Combine(
                repositoryRoot,
                "src",
                "OS",
                "Boot",
                "EfiEntry.cs"),
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Boot",
                "BootSequence.cs"),
            Path.Combine(
                repositoryRoot,
                "src",
                "OS.Kernel",
                "Diagnostics",
                "Probes.cs")
        ];
        string[] forbiddenIntegrationNames =
        [
            nameof(BootPageAllocatorOnePageResult),
            "AllocateOnePage",
            ReadyMarker
        ];
        foreach (string path in integrationPaths)
        {
            string source = File.ReadAllText(path);
            foreach (string name in forbiddenIntegrationNames)
            {
                if (source.Contains(name, StringComparison.Ordinal))
                {
                    return SourceFailure(
                        "integration="
                        + Path.GetFileName(path)
                        + ";name="
                        + name);
                }
            }
        }

        return !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    BootPageAllocatorState>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    BootPageFreeListNode>()
            && !RuntimeHelpers
                .IsReferenceOrContainsReferences<
                    BootPageAllocatorOnePageResult>()
            && BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Memory\BootPageAllocatorCoreContracts.cs",
                @"Memory\BootPageAllocatorCoreContracts.cs")
            && BuildEfiShellPreflightTests.HasExactCanonicalEfiCompileItem(
                @"..\OS.Kernel\Memory\BootPageAllocatorMutation.cs",
                @"Memory\BootPageAllocatorMutation.cs")
            && EvaluatedEfiCompileClosureExcludesLegacyOnePageAllocatorSources();
    }

    private static bool EvaluatedEfiCompileClosureExcludesLegacyOnePageAllocatorSources()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "OS",
            "OS.csproj");
        string projectSource = File.ReadAllText(projectPath);
        if (!Regex.IsMatch(
                projectSource,
                @"<EnableDefaultCompileItems>\s*false\s*</EnableDefaultCompileItems>",
                RegexOptions.CultureInvariant
                    | RegexOptions.IgnoreCase))
        {
            return SourceFailure("efi-default-compile-items");
        }

        ToolResult evaluation = RunEfiCompileItemEvaluation(
            repositoryRoot,
            projectPath);
        if (evaluation.TimedOut || evaluation.ExitCode != 0)
        {
            return SourceFailure(
                "efi-compile-evaluation="
                + NormalizeCompilerOutput(evaluation.Output));
        }

        try
        {
            using JsonDocument document =
                JsonDocument.Parse(evaluation.Output);
            if (!document.RootElement.TryGetProperty(
                    "Items",
                    out JsonElement items)
                || !items.TryGetProperty(
                    "Compile",
                    out JsonElement compileItems)
                || compileItems.ValueKind != JsonValueKind.Array
                || !items.TryGetProperty(
                    "ProjectReference",
                    out JsonElement projectReferences)
                || projectReferences.ValueKind != JsonValueKind.Array
                || !items.TryGetProperty(
                    "Reference",
                    out JsonElement references)
                || references.ValueKind != JsonValueKind.Array)
            {
                return SourceFailure("efi-compile-json-shape");
            }

            if (projectReferences.GetArrayLength() != 0
                || references.GetArrayLength() != 0)
            {
                return SourceFailure(
                    "efi-managed-reference"
                    + ";project="
                    + projectReferences.GetArrayLength()
                    + ";assembly="
                    + references.GetArrayLength());
            }

            string[] forbiddenNames =
            [
                "BootPageAllocatorState.cs",
                "BootPageFreeListPlan.cs",
                "BootPageAllocatorPlan.cs",
                "BootMemoryRegionPlan.cs"
            ];
            foreach (JsonElement item in compileItems.EnumerateArray())
            {
                string identity = item.TryGetProperty(
                        "Identity",
                        out JsonElement identityElement)
                    ? identityElement.GetString() ?? string.Empty
                    : string.Empty;
                string fullPath = item.TryGetProperty(
                        "FullPath",
                        out JsonElement fullPathElement)
                    ? fullPathElement.GetString() ?? string.Empty
                    : string.Empty;
                foreach (string forbiddenName in forbiddenNames)
                {
                    if (identity.EndsWith(
                            forbiddenName,
                            StringComparison.OrdinalIgnoreCase)
                        || fullPath.EndsWith(
                            forbiddenName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return SourceFailure(
                            "efi-compile-import=" + identity);
                    }
                }
            }

            return true;
        }
        catch (JsonException ex)
        {
            return SourceFailure(
                "efi-compile-json=" + ex.GetType().Name);
        }
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string workspace = CreateNoStdLibWorkspace();
        try
        {
            ToolResult build = RunNoStdLibBuild(workspace);
            string assemblyPath = Path.Combine(
                workspace,
                "out",
                "CLROS.M01.BootPageAllocatorOnePageCompileClosure.dll");
            if (build.TimedOut
                || build.ExitCode != 0
                || !HasZeroAssemblyReferences(assemblyPath))
            {
                Console.WriteLine(
                    "M01_008_NOSTDLIB_DIAGNOSTIC "
                    + NormalizeCompilerOutput(build.Output));
                return false;
            }

            return true;
        }
        finally
        {
            DeleteNoStdLibWorkspace(workspace);
        }
    }

    private static bool TestMarkerBoundary()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string testSource = File.ReadAllText(GetTestSourcePath());
        string productionSource =
            File.ReadAllText(GetMutationSourcePath());
        string canonicalTestPath =
            Path.GetFullPath(GetTestSourcePath());
        string canonicalBootDocPath = Path.GetFullPath(
            Path.Combine(repositoryRoot, "docs", "boot.md"));
        string canonicalBacklogPath = Path.GetFullPath(
            Path.Combine(
                repositoryRoot,
                ".gitea",
                "backlog",
                "clros-issues.json"));
        string readyWriteCall = string.Concat(
            "Console.WriteLine(",
            "ReadyMarker);");
        int runAllStart = testSource.IndexOf(
            "internal static void RunAll()",
            StringComparison.Ordinal);
        int runHelperStart = testSource.IndexOf(
            "private static void Run(",
            runAllStart < 0 ? 0 : runAllStart,
            StringComparison.Ordinal);
        if (runAllStart < 0 || runHelperStart <= runAllStart)
        {
            return false;
        }

        string runAllSource = testSource.Substring(
            runAllStart,
            runHelperStart - runAllStart);
        int failureGate = runAllSource.IndexOf(
            "if (failures != 0)",
            StringComparison.Ordinal);
        int failureThrow = runAllSource.IndexOf(
            "throw new InvalidOperationException(",
            StringComparison.Ordinal);
        int readyWrite = runAllSource.IndexOf(
            readyWriteCall,
            StringComparison.Ordinal);
        int passWrite = runAllSource.IndexOf(
            "Console.WriteLine(\"TEST_PASS \" + IssueTestName);",
            StringComparison.Ordinal);
        if (CountOccurrences(
                testSource,
                "\"" + ReadyMarker + "\"") != 1
            || CountOccurrences(
                testSource,
                readyWriteCall) != 1
            || CountOccurrences(runAllSource, "ReadyMarker") != 1
            || failureGate < 0
            || failureThrow <= failureGate
            || readyWrite <= failureThrow
            || passWrite <= readyWrite
            || productionSource.Contains(
                ReadyMarker,
                StringComparison.Ordinal))
        {
            return false;
        }

        foreach (string path in Directory.EnumerateFiles(
            repositoryRoot,
            "*",
            SearchOption.TopDirectoryOnly))
        {
            if (!IsGeneratedOrReferencePath(path)
                && HasTextExtension(path)
                && File.ReadAllText(path).Contains(
                    ReadyMarker,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        string[] scanRoots =
        [
            Path.Combine(repositoryRoot, "src"),
            Path.Combine(repositoryRoot, "std"),
            Path.Combine(repositoryRoot, "apps"),
            Path.Combine(repositoryRoot, "tools"),
            Path.Combine(repositoryRoot, "tests"),
            Path.Combine(repositoryRoot, "scripts"),
            Path.Combine(repositoryRoot, "docs"),
            Path.Combine(repositoryRoot, ".gitea")
        ];
        foreach (string root in scanRoots)
        {
            if (!Directory.Exists(root))
            {
                return false;
            }

            foreach (string path in Directory.EnumerateFiles(
                root,
                "*",
                SearchOption.AllDirectories))
            {
                if (IsGeneratedOrReferencePath(path)
                    || !HasTextExtension(path))
                {
                    continue;
                }

                string contents = File.ReadAllText(path);
                int occurrences =
                    CountOccurrences(contents, ReadyMarker);
                if (occurrences == 0)
                {
                    continue;
                }

                string canonicalPath = Path.GetFullPath(path);
                if (string.Equals(
                        canonicalPath,
                        canonicalTestPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (occurrences != 1
                        || CountOccurrences(
                            contents,
                            readyWriteCall) != 1)
                    {
                        return false;
                    }

                    continue;
                }

                bool isCanonicalDeclaration =
                    string.Equals(
                        canonicalPath,
                        canonicalBootDocPath,
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        canonicalPath,
                        canonicalBacklogPath,
                        StringComparison.OrdinalIgnoreCase);
                if (!isCanonicalDeclaration
                    || contents.Contains(
                        readyWriteCall,
                        StringComparison.Ordinal)
                    || contents.Contains(
                        "Console.WriteLine(\"" + ReadyMarker,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        string bootGuestMarker =
            string.Concat("OS_BOOT_", "OK");
        string smokeGuestMarker =
            string.Concat("OS_SMOKE_", "PASS");
        return !testSource.Contains(
                bootGuestMarker,
                StringComparison.Ordinal)
            && !testSource.Contains(
                smokeGuestMarker,
                StringComparison.Ordinal);
    }

    private static bool HasExactIntEnum<T>(
        (string Name, int Value)[] expected)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(int))
        {
            return false;
        }

        Array values = Enum.GetValues(typeof(T));
        string[] names = Enum.GetNames(typeof(T));
        if (values.Length != expected.Length
            || names.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            int value = Convert.ToInt32(values.GetValue(index));
            if (names[index] != expected[index].Name
                || value != expected[index].Value)
            {
                return false;
            }
        }

        string source = RemoveComments(
            File.ReadAllText(GetMutationSourcePath()));
        Match match = Regex.Match(
            source,
            @"public\s+enum\s+"
                + Regex.Escape(typeof(T).Name)
                + @"\s*\{(?<body>[^}]*)\}",
            RegexOptions.CultureInvariant
                | RegexOptions.Singleline);
        if (!match.Success)
        {
            return false;
        }

        MatchCollection declarations = Regex.Matches(
            match.Groups["body"].Value,
            @"(?m)^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>[0-9]+)\s*,?\s*$",
            RegexOptions.CultureInvariant);
        if (declarations.Count != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            Match declaration = declarations[index];
            if (declaration.Groups["name"].Value
                    != expected[index].Name
                || !int.TryParse(
                    declaration.Groups["value"].Value,
                    out int value)
                || value != expected[index].Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsUnmanagedValue<T>()
        where T : unmanaged
    {
        return true;
    }

    private static bool HasExactAllocateOnePageApi()
    {
        MethodInfo[] methods = typeof(BootPageAllocatorMutator)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(
                method => method.Name
                    == nameof(BootPageAllocatorMutator.AllocateOnePage))
            .ToArray();
        if (methods.Length != 1
            || methods[0].ReturnType
                != typeof(BootPageAllocatorOnePageResult))
        {
            return false;
        }

        ParameterInfo[] parameters = methods[0].GetParameters();
        (string Name, Type Type)[] expected =
        [
            ("state", typeof(BootPageAllocatorState)),
            ("nodes", typeof(BootPageFreeListNode).MakePointerType()),
            ("nodeCount", typeof(ulong)),
            ("nodeCapacity", typeof(ulong)),
            ("reservations", typeof(BootReservedRange).MakePointerType()),
            ("reservationCount", typeof(ulong)),
            ("reservationCapacity", typeof(ulong)),
            ("pageCount", typeof(ulong)),
            ("alignment", typeof(ulong))
        ];
        if (parameters.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (parameters[index].Name != expected[index].Name
                || parameters[index].ParameterType
                    != expected[index].Type)
            {
                return false;
            }
        }

        return true;
    }

    private static BootPageFreeListNode Node(
        int sourceFreeSegmentIndex,
        int sourceUsableRangeIndex,
        ulong physicalStart,
        ulong pageCount,
        int nextNodeIndex)
    {
        ulong byteLength = checked(pageCount * PageSize);
        return new BootPageFreeListNode(
            sourceFreeSegmentIndex,
            sourceUsableRangeIndex,
            physicalStart,
            checked(physicalStart + byteLength),
            pageCount,
            byteLength,
            nextNodeIndex);
    }

    private static BootPageFreeListNode RawNode(
        int sourceFreeSegmentIndex,
        int sourceUsableRangeIndex,
        ulong physicalStart,
        ulong physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        int nextNodeIndex)
    {
        return new BootPageFreeListNode(
            sourceFreeSegmentIndex,
            sourceUsableRangeIndex,
            physicalStart,
            physicalEndExclusive,
            pageCount,
            byteLength,
            nextNodeIndex);
    }

    private static BootPageAllocatorState State(
        int headNodeIndex,
        int freeNodeCount,
        ulong freePages,
        ulong allocatedPages)
    {
        return new BootPageAllocatorState(
            isInitialized: true,
            pageSize: PageSize,
            headNodeIndex,
            freeNodeCount,
            freePages,
            checked(freePages * PageSize),
            allocatedPages,
            checked(allocatedPages * PageSize));
    }

    private static unsafe BootPageAllocatorOnePageResult Allocate(
        BootPageAllocatorState state,
        BootPageFreeListNode* nodes,
        ulong nodeCount,
        ulong nodeCapacity,
        BootReservedRange* reservations,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong alignment)
    {
        return BootPageAllocatorMutator.AllocateOnePage(
            state,
            nodes,
            nodeCount,
            nodeCapacity,
            reservations,
            reservationCount,
            reservationCapacity,
            1,
            alignment);
    }

    private static bool IsExactSuccess(
        BootPageAllocatorOnePageResult result,
        BootPageAllocatorState beforeState,
        int expectedFreeNodes,
        ulong expectedFreePages,
        ulong expectedAllocatedPages,
        ulong expectedPhysicalStart,
        ulong expectedNodeCount,
        ulong expectedNodeCapacity,
        ulong expectedReservationCount,
        ulong expectedReservationCapacity,
        int expectedNodeIndex,
        int expectedInsertedNodeIndex,
        ulong expectedVisitedNodeCount)
    {
        return result.IsInitialized
            && result.Succeeded
            && result.Status
                == BootPageAllocatorMutationStatus.Success
            && result.ReservationStatus
                == BootReservedRangeStatus.Success
            && result.AddressStatus == AddressValueStatus.Success
            && StatesEqual(result.BeforeState, beforeState)
            && result.AfterState.IsInitialized
            && result.AfterState.PageSize == PageSize
            && result.AfterState.FreeNodeCount
                == expectedFreeNodes
            && result.AfterState.FreePageCount
                == expectedFreePages
            && result.AfterState.FreeByteCount
                == expectedFreePages * PageSize
            && result.AfterState.AllocatedPageCount
                == expectedAllocatedPages
            && result.AfterState.AllocatedByteCount
                == expectedAllocatedPages * PageSize
            && Raw(result.PhysicalStart) == expectedPhysicalStart
            && Raw(result.PhysicalEndExclusive)
                == expectedPhysicalStart + PageSize
            && result.PageCount == 1
            && result.ByteLength == PageSize
            && result.NodeCount == expectedNodeCount
            && result.NodeCapacity == expectedNodeCapacity
            && result.ReservationCount
                == expectedReservationCount
            && result.ReservationCapacity
                == expectedReservationCapacity
            && result.NodeIndex == expectedNodeIndex
            && result.InsertedNodeIndex
                == expectedInsertedNodeIndex
            && result.VisitedNodeCount
                == expectedVisitedNodeCount
            && result.InvalidReservationIndex
                == BootPageAllocatorMutator.NoIndex
            && result.ConflictingIndex
                == BootPageAllocatorMutator.NoIndex
            && result.BeforeFreeNodeCount
                == beforeState.FreeNodeCount
            && result.AfterFreeNodeCount == expectedFreeNodes
            && result.BeforeFreePageCount
                == beforeState.FreePageCount
            && result.AfterFreePageCount == expectedFreePages
            && result.BeforeFreeByteCount
                == beforeState.FreeByteCount
            && result.AfterFreeByteCount
                == expectedFreePages * PageSize
            && result.BeforeAllocatedPageCount
                == beforeState.AllocatedPageCount
            && result.AfterAllocatedPageCount
                == expectedAllocatedPages
            && result.BeforeAllocatedByteCount
                == beforeState.AllocatedByteCount
            && result.AfterAllocatedByteCount
                == expectedAllocatedPages * PageSize
            && !BootPageAllocatorMutator.PerformsLiveAllocation
            && BootPageAllocatorMutator
                .MutatesCallerOwnedFreeList
            && !BootPageAllocatorMutator.ConfiguresPaging
            && !BootPageAllocatorMutator.EnforcesNxOrWx;
    }

    private static bool IsDefaultNode(
        BootPageFreeListNode node)
    {
        return node.SourceFreeSegmentIndex == 0
            && node.SourceUsableRangeIndex == 0
            && node.PhysicalStart == 0
            && node.PhysicalEndExclusive == 0
            && node.PageCount == 0
            && node.ByteLength == 0
            && node.NextNodeIndex == 0;
    }

    private static BootReservedRange Reservation(
        BootReservationKind kind,
        ulong physicalStart,
        ulong pageCount)
    {
        BootReservedRangeStatus status =
            BootReservedRange.TryCreate(
                kind,
                Address(physicalStart),
                pageCount,
                out BootReservedRange reservation);
        if (status != BootReservedRangeStatus.Success)
        {
            throw new InvalidOperationException(
                "Could not create reservation: " + status);
        }

        return reservation;
    }

    private static bool InvalidNodeFailure(
        BootPageAllocatorState state,
        BootPageFreeListNode[] nodes)
    {
        return FailurePreserves(
            state,
            nodes,
            1,
            (ulong)nodes.Length,
            [],
            0,
            0,
            1,
            PageSize,
            BootPageAllocatorMutationStatus.InvalidNodeShape);
    }

    private static unsafe bool ReservationFailure(
        BootPageAllocatorState state,
        BootPageFreeListNode[] nodes,
        BootReservedRange[] reservations,
        ulong reservationCount,
        BootPageAllocatorMutationStatus expectedStatus,
        BootReservedRangeStatus expectedReservationStatus)
    {
        byte[] nodesBefore = StructArrayBytes(nodes);
        byte[] reservationsBefore =
            StructArrayBytes(reservations);
        fixed (BootPageFreeListNode* nodesPointer = nodes)
        fixed (BootReservedRange* reservationPointer = reservations)
        {
            BootPageAllocatorOnePageResult result =
                BootPageAllocatorMutator.AllocateOnePage(
                    state,
                    nodesPointer,
                    1,
                    (ulong)nodes.Length,
                    reservationPointer,
                    reservationCount,
                    (ulong)reservations.Length,
                    1,
                    PageSize);
            ulong expectedInvalidIndex =
                expectedStatus
                    == BootPageAllocatorMutationStatus.InvalidReservation
                    ? 0UL
                    : 1UL;
            ulong expectedConflict =
                expectedStatus
                    == BootPageAllocatorMutationStatus.InvalidReservation
                    ? BootPageAllocatorMutator.NoIndex
                    : 0UL;
            return IsExactFailure(result, state, expectedStatus)
                && result.ReservationStatus
                    == expectedReservationStatus
                && result.InvalidReservationIndex
                    == expectedInvalidIndex
                && result.ConflictingIndex == expectedConflict
                && nodesBefore.AsSpan().SequenceEqual(
                    StructArrayBytes(nodes))
                && reservationsBefore.AsSpan().SequenceEqual(
                    StructArrayBytes(reservations));
        }
    }

    private static unsafe bool FailurePreserves(
        BootPageAllocatorState state,
        BootPageFreeListNode[] nodes,
        ulong nodeCount,
        ulong nodeCapacity,
        BootReservedRange[] reservations,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong pageCount,
        ulong alignment,
        BootPageAllocatorMutationStatus expectedStatus,
        BootReservedRangeStatus expectedReservationStatus =
            BootReservedRangeStatus.Success,
        AddressValueStatus expectedAddressStatus =
            AddressValueStatus.Success)
    {
        byte[] nodesBefore = StructArrayBytes(nodes);
        byte[] reservationsBefore =
            StructArrayBytes(reservations);
        fixed (BootPageFreeListNode* nodesPointer = nodes)
        fixed (BootReservedRange* reservationPointer = reservations)
        {
            BootPageAllocatorOnePageResult result =
                BootPageAllocatorMutator.AllocateOnePage(
                    state,
                    nodesPointer,
                    nodeCount,
                    nodeCapacity,
                    reservationPointer,
                    reservationCount,
                    reservationCapacity,
                    pageCount,
                    alignment);
            ulong expectedByteLength =
                pageCount <= ulong.MaxValue / PageSize
                    ? pageCount * PageSize
                    : 0;
            return IsExactFailure(result, state, expectedStatus)
                && result.ReservationStatus
                    == expectedReservationStatus
                && result.AddressStatus
                    == expectedAddressStatus
                && result.PageCount == pageCount
                && result.ByteLength == expectedByteLength
                && result.NodeCount == nodeCount
                && result.NodeCapacity == nodeCapacity
                && result.ReservationCount == reservationCount
                && result.ReservationCapacity
                    == reservationCapacity
                && nodesBefore.AsSpan().SequenceEqual(
                    StructArrayBytes(nodes))
                && reservationsBefore.AsSpan().SequenceEqual(
                    StructArrayBytes(reservations));
        }
    }

    private static unsafe bool DirectPointerFailure(
        BootPageAllocatorState state,
        BootPageFreeListNode* nodes,
        ulong nodeCount,
        ulong nodeCapacity,
        BootReservedRange* reservations,
        ulong reservationCount,
        ulong reservationCapacity,
        BootPageAllocatorMutationStatus expectedStatus)
    {
        BootPageAllocatorOnePageResult result =
            BootPageAllocatorMutator.AllocateOnePage(
                state,
                nodes,
                nodeCount,
                nodeCapacity,
                reservations,
                reservationCount,
                reservationCapacity,
                1,
                PageSize);
        return IsExactFailure(result, state, expectedStatus)
            && result.NodeCount == nodeCount
            && result.NodeCapacity == nodeCapacity
            && result.ReservationCount == reservationCount
            && result.ReservationCapacity == reservationCapacity;
    }

    private static bool IsExactFailure(
        BootPageAllocatorOnePageResult result,
        BootPageAllocatorState state,
        BootPageAllocatorMutationStatus expectedStatus)
    {
        return result.IsInitialized
            && !result.Succeeded
            && result.Status == expectedStatus
            && StatesEqual(result.BeforeState, state)
            && StatesEqual(result.AfterState, state)
            && !result.PhysicalStart.IsValid
            && !result.PhysicalEndExclusive.IsValid
            && result.BeforeFreeNodeCount == state.FreeNodeCount
            && result.AfterFreeNodeCount == state.FreeNodeCount
            && result.BeforeFreePageCount == state.FreePageCount
            && result.AfterFreePageCount == state.FreePageCount
            && result.BeforeFreeByteCount == state.FreeByteCount
            && result.AfterFreeByteCount == state.FreeByteCount
            && result.BeforeAllocatedPageCount
                == state.AllocatedPageCount
            && result.AfterAllocatedPageCount
                == state.AllocatedPageCount
            && result.BeforeAllocatedByteCount
                == state.AllocatedByteCount
            && result.AfterAllocatedByteCount
                == state.AllocatedByteCount;
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

    private static PhysicalAddress Address(ulong value)
    {
        AddressValueStatus status =
            PhysicalAddress.TryCreateFromUefiBoundary(
                value,
                out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Could not create physical address: " + status);
        }

        return address;
    }

    private static ulong Raw(PhysicalAddress address)
    {
        AddressValueStatus status =
            address.TryGetRawValueForUefiBoundary(out ulong value);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException(
                "Could not read physical address: " + status);
        }

        return value;
    }

    private static BootPageAllocatorOnePageResult InvokeSuccessFactory(
        BootPageAllocatorState beforeState,
        BootPageAllocatorState afterState,
        PhysicalAddress physicalStart,
        PhysicalAddress physicalEndExclusive,
        ulong nodeCount,
        ulong nodeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        int nodeIndex,
        int insertedNodeIndex,
        ulong visitedNodeCount)
    {
        MethodInfo? method = typeof(BootPageAllocatorOnePageResult)
            .GetMethod(
                "Success",
                BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
        {
            throw new InvalidOperationException(
                "Internal one-page success factory was not found.");
        }

        object? value = method.Invoke(
            null,
            [
                beforeState,
                afterState,
                physicalStart,
                physicalEndExclusive,
                nodeCount,
                nodeCapacity,
                reservationCount,
                reservationCapacity,
                nodeIndex,
                insertedNodeIndex,
                visitedNodeCount
            ]);
        if (value is not BootPageAllocatorOnePageResult result)
        {
            throw new InvalidOperationException(
                "Internal one-page success factory returned no result.");
        }

        return result;
    }

    private static byte[] StructArrayBytes<T>(T[] values)
        where T : struct
    {
        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
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

    private static bool OnePageResultsEqual(
        BootPageAllocatorOnePageResult left,
        BootPageAllocatorOnePageResult right)
    {
        return left.IsInitialized == right.IsInitialized
            && left.Status == right.Status
            && left.ReservationStatus == right.ReservationStatus
            && left.AddressStatus == right.AddressStatus
            && left.Succeeded == right.Succeeded
            && AllocatorStatesEqual(
                left.BeforeState,
                right.BeforeState)
            && AllocatorStatesEqual(
                left.AfterState,
                right.AfterState)
            && left.PhysicalStart.Equals(right.PhysicalStart)
            && left.PhysicalEndExclusive.Equals(
                right.PhysicalEndExclusive)
            && left.PageCount == right.PageCount
            && left.ByteLength == right.ByteLength
            && left.NodeCount == right.NodeCount
            && left.NodeCapacity == right.NodeCapacity
            && left.ReservationCount == right.ReservationCount
            && left.ReservationCapacity == right.ReservationCapacity
            && left.NodeIndex == right.NodeIndex
            && left.InsertedNodeIndex == right.InsertedNodeIndex
            && left.VisitedNodeCount == right.VisitedNodeCount
            && left.InvalidReservationIndex
                == right.InvalidReservationIndex
            && left.ConflictingIndex == right.ConflictingIndex;
    }

    private static bool AllocatorStatesEqual(
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

    private static string RemoveHostOnlyBlocks(string source)
    {
        return Regex.Replace(
            source,
            @"(?ms)^\s*#if\s+!NOSTDLIB_EFI\s*$.*?^\s*#endif\s*$",
            string.Empty,
            RegexOptions.CultureInvariant);
    }

    private static bool SourceFailure(string reason)
    {
        Console.WriteLine(
            "M01_008_SOURCE_FIREWALL_DIAGNOSTIC " + reason);
        return false;
    }

    private static int CountOccurrences(
        string source,
        string value)
    {
        int count = 0;
        int offset = 0;
        while (offset <= source.Length - value.Length)
        {
            int found = source.IndexOf(
                value,
                offset,
                StringComparison.Ordinal);
            if (found < 0)
            {
                break;
            }

            count++;
            offset = found + value.Length;
        }

        return count;
    }

    private static bool HasTextExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(
                ".cs",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".ps1",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".json",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".xml",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".props",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".targets",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".csproj",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".md",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".yml",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".yaml",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".slnx",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGeneratedOrReferencePath(string path)
    {
        string[] excludedSegments =
        [
            "bin",
            "obj",
            "artifacts",
            "references",
            "done",
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

    private static ToolResult RunEfiCompileItemEvaluation(
        string repositoryRoot,
        string projectPath)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = repositoryRoot
        };
        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(projectPath);
        start.ArgumentList.Add("-nologo");
        start.ArgumentList.Add(
            "-getItem:Compile,ProjectReference,Reference");
        start.ArgumentList.Add("-p:Configuration=Release");
        start.ArgumentList.Add(
            "-p:OSWorkloadProfile=SingleMinimalApi");
        start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
        ConfigureDotnetEnvironment(start);
        return RunProcess(start, timeoutMilliseconds: 60_000);
    }

    private static ToolResult RunProcess(
        ProcessStartInfo start,
        int timeoutMilliseconds)
    {
        using Process process = new Process { StartInfo = start };
        if (!process.Start())
        {
            return new ToolResult(
                -1,
                timedOut: false,
                "process did not start");
        }

        Task<string> standardOutput =
            process.StandardOutput.ReadToEndAsync();
        Task<string> standardError =
            process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(timeoutMilliseconds);
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
        return new ToolResult(
            exited ? process.ExitCode : -1,
            timedOut: !exited,
            output + Environment.NewLine + error);
    }

    private static void ConfigureDotnetEnvironment(
        ProcessStartInfo start)
    {
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["NUGET_XMLDOC_MODE"] = "skip";
    }

    private static string CreateNoStdLibWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = Path.Combine(
            Path.GetTempPath(),
            "clros-m01-008-" + Guid.NewGuid().ToString("N"));
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
                        public AttributeUsageAttribute(
                            AttributeTargets validOn) { }
                        public bool AllowMultiple { get; set; }
                        public bool Inherited { get; set; }
                    }
                }

                namespace System.Runtime.CompilerServices
                {
                    public sealed class IsReadOnlyAttribute
                        : System.Attribute { }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            File.WriteAllText(
                Path.Combine(workspace, "Probe.cs"),
                """
                namespace CLROS.M01.CompileClosure;

                public static unsafe class Probe
                {
                    public static bool Run()
                    {
                        OS.Kernel.Memory.BootPageFreeListNode* nodes =
                            stackalloc OS.Kernel.Memory
                                .BootPageFreeListNode[2];
                        nodes[0] =
                            new OS.Kernel.Memory.BootPageFreeListNode(
                                0,
                                0,
                                0x200000,
                                0x202000,
                                2,
                                8192,
                                -1);
                        nodes[1] = default;

                        OS.Boot.AddressValueStatus addressStatus =
                            OS.Boot.PhysicalAddress
                                .TryCreateAlignedFromUefiBoundary(
                                    0x100000,
                                    4096,
                                    out OS.Boot.PhysicalAddress
                                        reservationStart);
                        if (addressStatus
                            != OS.Boot.AddressValueStatus.Success)
                        {
                            return false;
                        }

                        OS.Kernel.Memory.BootReservedRangeStatus
                            reservationStatus =
                                OS.Kernel.Memory.BootReservedRange
                                    .TryCreate(
                                        OS.Kernel.Memory
                                            .BootReservationKind
                                            .LoadedImage,
                                        reservationStart,
                                        1,
                                        out OS.Kernel.Memory
                                            .BootReservedRange
                                            reservation);
                        if (reservationStatus
                            != OS.Kernel.Memory
                                .BootReservedRangeStatus.Success)
                        {
                            return false;
                        }

                        OS.Kernel.Memory.BootPageAllocatorState state =
                            new OS.Kernel.Memory
                                .BootPageAllocatorState(
                                    true,
                                    4096,
                                    0,
                                    1,
                                    2,
                                    8192,
                                    0,
                                    0);
                        OS.Kernel.Memory
                            .BootPageAllocatorOnePageResult result =
                                OS.Kernel.Memory
                                    .BootPageAllocatorMutator
                                    .AllocateOnePage(
                                        state,
                                        nodes,
                                        2,
                                        2,
                                        &reservation,
                                        1,
                                        1,
                                        1,
                                        4096);
                        if (!result.Succeeded
                            || result.PageCount != 1
                            || result.ByteLength != 4096
                            || result.AfterState.FreePageCount != 1
                            || result.AfterState
                                .AllocatedPageCount != 1)
                        {
                            return false;
                        }

                        addressStatus =
                            result.PhysicalStart
                                .TryGetRawValueForUefiBoundary(
                                    out ulong rawStart);
                        return addressStatus
                                == OS.Boot.AddressValueStatus.Success
                            && rawStart == 0x200000;
                    }
                }
                """,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));

            string projectText =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>CLROS.M01.BootPageAllocatorOnePageCompileClosure</AssemblyName>
                <TargetFramework>net10.0</TargetFramework>
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
                <Compile Include="__ADDRESSES__"
                         Link="Boot\TypedPhysicalVirtualDeviceAddressPODWrappers.cs" />
                <Compile Include="__RESERVATION__"
                         Link="Memory\BootReservedRange.cs" />
                <Compile Include="__CONTRACTS__"
                         Link="Memory\BootPageAllocatorCoreContracts.cs" />
                <Compile Include="__MUTATION__"
                         Link="Memory\BootPageAllocatorMutation.cs" />
                <Compile Include="Probe.cs" />
              </ItemGroup>
              <Target Name="CustomizeReferences"
                      BeforeTargets="BeforeCompile"
                      AfterTargets="FindReferenceAssembliesForReferences">
                <ItemGroup>
                  <ReferencePathWithRefAssemblies
                      Remove="@(ReferencePathWithRefAssemblies)" />
                  <ReferencePath Remove="@(ReferencePath)" />
                </ItemGroup>
              </Target>
            </Project>
            """
            .Replace(
                "__ADDRESSES__",
                EscapeXml(GetAddressSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__RESERVATION__",
                EscapeXml(GetReservationSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__CONTRACTS__",
                EscapeXml(GetCoreContractsSourcePath()),
                StringComparison.Ordinal)
            .Replace(
                "__MUTATION__",
                EscapeXml(GetMutationSourcePath()),
                StringComparison.Ordinal);

            File.WriteAllText(
                Path.Combine(workspace, "M01.008.NoStdLib.csproj"),
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

    private static ToolResult RunNoStdLibBuild(string workspace)
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
        start.ArgumentList.Add("M01.008.NoStdLib.csproj");
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("-m:1");
        start.ArgumentList.Add("--no-dependencies");
        start.ArgumentList.Add("--no-incremental");
        start.ArgumentList.Add("--disable-build-servers");
        start.ArgumentList.Add("-v:minimal");
        start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
        start.ArgumentList.Add("-p:NuGetAudit=false");
        ConfigureDotnetEnvironment(start);
        return RunProcess(start, timeoutMilliseconds: 90_000);
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

    private static string GetAddressSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Boot",
            "TypedPhysicalVirtualDeviceAddressPODWrappers.cs");
    }

    private static string GetReservationSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Memory",
            "BootReservedRange.cs");
    }

    private static string GetCoreContractsSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Memory",
            "BootPageAllocatorCoreContracts.cs");
    }

    private static string GetMutationSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Memory",
            "BootPageAllocatorMutation.cs");
    }

    private static string GetTestSourcePath()
    {
        return Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "tests",
            "OS.HostTests",
            "BootPageAllocatorOnePageAllocationTests.cs");
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
                @"\Aclros-m01-008-[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "Refusing to delete an unexpected M01-008 workspace.");
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
                "Refusing to delete a reparse-point M01-008 workspace.");
        }

        Directory.Delete(fullWorkspace, recursive: true);
    }

    private readonly struct ToolResult
    {
        public ToolResult(
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
