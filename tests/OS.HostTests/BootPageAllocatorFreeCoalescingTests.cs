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

internal static partial class BootPageAllocatorFreeCoalescingTests
{
    private const string IssueTestName = "m01.009";
    private const string ReadyMarker = "CLROS_M01_009_READY";
    private const ulong PageSize = 4096;
    [HostFixture(HostLane.Native)]
    internal static void RunAll()
    {
        int failures = 0;
        Console.WriteLine("TEST_START " + IssueTestName);
        Run("m01.009 01 contracts defaults and api are exact", TestContractsDefaultsAndApi, ref failures);
        Run("m01.009 02 whole head restoration is exact", TestWholeHeadRestoration, ref failures);
        Run("m01.009 03 whole nonhead restoration is exact", TestWholeNonHeadRestoration, ref failures);
        Run("m01.009 04 head allocation coalesces next", TestNextOnlyRestoration, ref failures);
        Run("m01.009 05 tail allocation coalesces previous", TestPreviousOnlyRestoration, ref failures);
        Run("m01.009 06 middle split coalesces both", TestBothNeighborRestoration, ref failures);
        Run("m01.009 07 strict lifo restoration rejects replay", TestStrictLifoAndReplay, ref failures);
        Run("m01.009 08 result accounting and evidence are exact", TestResultEvidence, ref failures);
        Run("m01.009 09 reservations and capacity tails are preserved", TestReservationsAndCapacityTails, ref failures);
        Run("m01.009 10 default failed and nonrestorable receipts fail", TestDefaultFailedAndNonRestorable, ref failures);
        Run("m01.009 11 altered and nonmatching foreign receipts fail closed", TestAlteredAndForeignReceipts, ref failures);
        Run("m01.009 12 stale out-of-order and double free fail", TestStaleOutOfOrderAndDoubleFree, ref failures);
        Run("m01.009 13 overlap and reservation conflicts fail", TestOverlapAndReservationConflicts, ref failures);
        Run("m01.009 14 corrupt states and chains fail atomically", TestCorruptStatesAndChains, ref failures);
        Run("m01.009 15 count capacity overflow and aliases fail", TestCountsCapacitiesOverflowAndAliases, ref failures);
        Run("m01.009 16 every ordinary failure is byte atomic", TestFailureAtomicity, ref failures);
        Run("m01.009 17 execution is deterministic and allocation free", TestDeterminismAndAllocation, ref failures);
        Run("m01.009 18 hostile pointers fail in bounded subprocesses", TestHostilePointers, ref failures);
        Run("m01.009 19 source and evaluated efi firewalls hold", TestSourceAndEfiFirewalls, ref failures);
        Run("m01.009 20 exact sources compile under nostdlib efi", TestNoStdLibEfiCompileClosure, ref failures);
        Run("m01.009 21 forged successor ordering fails atomically", TestForgedSuccessorOrdering, ref failures);
        Run("m01.009 22 inactive tail padding is not claimable metadata", TestInactiveTailPadding, ref failures);
        Run("m01.009 23 readiness markers are unique ordered and host only", TestMarkerBoundary, ref failures);
        if (failures != 0)
        {
            Console.WriteLine("TEST_FAIL " + IssueTestName + " failures=" + failures);
            throw new InvalidOperationException("M01-009 one-page exact-inverse free tests failed: " + failures);
        }

        Console.WriteLine(ReadyMarker);
        Console.WriteLine("TEST_PASS " + IssueTestName);
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestContractsDefaultsAndApi()
    {
        (string Name, int Value)[] expectedStatuses = [("Success", 0), ("StateNotInitialized", 1), ("InvalidPageSize", 2), ("InvalidPageCount", 3), ("InvalidAlignment", 4), ("NodeCapacityTooSmall", 5), ("InvalidHeadNodeIndex", 6), ("InvalidNextNodeIndex", 7), ("InvalidNodeShape", 8), ("FreeListCycleDetected", 9), ("FreeListNodeCountMismatch", 10), ("TotalPageCountMismatch", 11), ("TotalByteCountMismatch", 12), ("AllocationByteCountOverflow", 13), ("AllocationAddressOverflow", 14), ("NoSuitableRange", 15), ("MetadataCapacityTooSmall", 16), ("FreeRangeOverlaps", 17), ("FreeRangeNotAllocated", 18), ("ContractLayoutMismatch", 19), ("NodeTableByteCountOverflow", 20), ("TooManyNodes", 21), ("MissingNodes", 22), ("MisalignedNodes", 23), ("NodeTableAddressRangeOverflow", 24), ("ReservationTableByteCountOverflow", 25), ("TooManyReservations", 26), ("ReservationCapacityTooSmall", 27), ("MissingReservations", 28), ("MisalignedReservations", 29), ("ReservationTableAddressRangeOverflow", 30), ("NodeReservationTablesOverlap", 31), ("InvalidStateAccounting", 32), ("InvalidReservation", 33), ("UnsortedReservation", 34), ("OverlappingReservation", 35), ("ReservationOverlapsFreeRange", 36), ("AllocatedPageCountOverflow", 37), ("AllocatedByteCountOverflow", 38), ("AllocationAddressConstructionFailed", 39)];
        (string Name, byte Value)[] expectedCoalescing = [("Uninitialized", 0), ("None", 1), ("Previous", 2), ("Next", 3), ("PreviousAndNext", 4)];
        BootPageAllocatorOnePageRestoreReceipt receipt = default;
        BootPageAllocatorOnePageResult allocation = default;
        BootPageAllocatorOnePageFreeResult result = default;
        Type receiptType = typeof(BootPageAllocatorOnePageRestoreReceipt);
        Type resultType = typeof(BootPageAllocatorOnePageFreeResult);
        return HasExactIntEnum<BootPageAllocatorMutationStatus>(expectedStatuses) && HasExactByteEnum<BootPageAllocatorOnePageCoalescingKind>(expectedCoalescing) && IsReadOnlySequentialUnmanaged<BootPageAllocatorOnePageRestoreReceipt>() && IsReadOnlySequentialUnmanaged<BootPageAllocatorOnePageFreeResult>() && receiptType.GetFields(BindingFlags.Instance | BindingFlags.Public).Length == 0 && resultType.GetFields(BindingFlags.Instance | BindingFlags.Public).Length == 0 && !receipt.IsInitialized && IsDefaultNode(receipt.SourceNodeBefore) && receipt.PreviousNodeIndexBefore == 0 && IsDefaultNode(receipt.PreviousNodeBefore) && !allocation.CanRestore && !allocation.RestoreReceipt.IsInitialized && !result.IsInitialized && !result.Succeeded && result.Status == BootPageAllocatorMutationStatus.Success && result.ReservationStatus == BootReservedRangeStatus.Uninitialized && result.AddressStatus == AddressValueStatus.Uninitialized && result.CoalescingKind == BootPageAllocatorOnePageCoalescingKind.Uninitialized && !result.PhysicalStart.IsValid && !result.PhysicalEndExclusive.IsValid && !result.BeforeState.IsInitialized && !result.AfterState.IsInitialized && !result.Allocation.IsInitialized && result.InvalidReservationIndex == 0 && result.ConflictingIndex == 0 && !BootPageAllocatorMutator.PerformsLiveAllocation && !BootPageAllocatorMutator.PerformsLiveFree && BootPageAllocatorMutator.MutatesCallerOwnedFreeList && !BootPageAllocatorMutator.ConfiguresPaging && !BootPageAllocatorMutator.EnforcesNxOrWx && HasExactFreeOnePageApi() && HasExactlyOnePublicOnePageApi("AllocateOnePage") && HasExactlyOnePublicOnePageApi("FreeOnePage");
    }

    private static bool TestWholeHeadRestoration()
    {
        BootPageFreeListNode[] nodes = [Node(7, 11, 0x200000, 1, 1), Node(8, 12, 0x400000, 2, -1), RawNode(91, 92, 3, 4, 5, 6, 7)];
        return RoundTrip(State(0, 2, 3, 4), nodes, nodeCount: 2, nodeCapacity: 3, [], reservationCount: 0, reservationCapacity: 0, alignment: PageSize, expectedStart: 0x200000, expectedKind: BootPageAllocatorOnePageCoalescingKind.None, expectedRestoredNodeIndex: 0, expectedPreviousNodeIndex: -1, expectedRemovedSuffixNodeIndex: -1);
    }

    private static bool TestWholeNonHeadRestoration()
    {
        BootPageFreeListNode[] nodes = [Node(20, 30, 0x101000, 1, 1), Node(21, 31, 0x400000, 1, -1), RawNode(81, 82, 3, 4, 5, 6, 7)];
        return RoundTrip(State(0, 2, 2, 9), nodes, 2, 3, [], 0, 0, alignment: 0x200000, expectedStart: 0x400000, BootPageAllocatorOnePageCoalescingKind.None, expectedRestoredNodeIndex: 1, expectedPreviousNodeIndex: 0, expectedRemovedSuffixNodeIndex: -1);
    }

    private static bool TestNextOnlyRestoration()
    {
        BootPageFreeListNode[] nodes = [Node(4, 6, 0x100000, 4, 1), Node(5, 7, 0x300000, 2, -1), RawNode(71, 72, 3, 4, 5, 6, 7)];
        return RoundTrip(State(0, 2, 6, 3), nodes, 2, 3, [], 0, 0, PageSize, 0x100000, BootPageAllocatorOnePageCoalescingKind.Next, 0, -1, -1);
    }

    private static bool TestPreviousOnlyRestoration()
    {
        BootPageFreeListNode[] nodes = [Node(14, 16, 0x101000, 4, -1), RawNode(61, 62, 3, 4, 5, 6, 7)];
        return RoundTrip(State(0, 1, 4, 5), nodes, 1, 2, [], 0, 0, 4 * PageSize, 0x104000, BootPageAllocatorOnePageCoalescingKind.Previous, 0, -1, -1);
    }

    private static bool TestBothNeighborRestoration()
    {
        BootPageFreeListNode[] nodes = [Node(24, 26, 0x101000, 5, -1), default, RawNode(51, 52, 3, 4, 5, 6, 7)];
        return RoundTrip(State(0, 1, 5, 8), nodes, 2, 3, [], 0, 0, 4 * PageSize, 0x104000, BootPageAllocatorOnePageCoalescingKind.PreviousAndNext, 0, -1, 1);
    }

    private static unsafe bool TestStrictLifoAndReplay()
    {
        BootPageFreeListNode[] nodes = [Node(10, 20, 0x100000, 4, -1), RawNode(41, 42, 3, 4, 5, 6, 7)];
        byte[] original = StructArrayBytes(nodes);
        BootPageAllocatorState initial = State(0, 1, 4, 0);
        fixed (BootPageFreeListNode* pointer = nodes)
        {
            BootPageAllocatorOnePageResult first = Allocate(initial, pointer, 1, 2, null, 0, 0, PageSize);
            if (!first.CanRestore || Raw(first.PhysicalStart) != 0x100000)
            {
                return false;
            }

            byte[] afterFirst = StructArrayBytes(nodes);
            BootPageAllocatorOnePageResult second = Allocate(first.AfterState, pointer, 1, 2, null, 0, 0, PageSize);
            if (!second.CanRestore || Raw(second.PhysicalStart) != 0x101000)
            {
                return false;
            }

            byte[] afterSecond = StructArrayBytes(nodes);
            BootPageAllocatorOnePageFreeResult outOfOrder = Free(second.AfterState, pointer, 1, 2, null, 0, 0, first);
            if (!IsExactFailure(outOfOrder, second.AfterState, first, BootPageAllocatorMutationStatus.FreeRangeNotAllocated, 1, 2, 0, 0) || !afterSecond.AsSpan().SequenceEqual(StructArrayBytes(nodes)))
            {
                return false;
            }

            BootPageAllocatorOnePageFreeResult freeSecond = Free(second.AfterState, pointer, 1, 2, null, 0, 0, second);
            if (!IsExactSuccess(freeSecond, second, BootPageAllocatorOnePageCoalescingKind.Next, 0, -1, -1) || !afterFirst.AsSpan().SequenceEqual(StructArrayBytes(nodes)))
            {
                return false;
            }

            BootPageAllocatorOnePageFreeResult freeFirst = Free(freeSecond.AfterState, pointer, 1, 2, null, 0, 0, first);
            if (!IsExactSuccess(freeFirst, first, BootPageAllocatorOnePageCoalescingKind.Next, 0, -1, -1) || !original.AsSpan().SequenceEqual(StructArrayBytes(nodes)))
            {
                return false;
            }

            byte[] beforeReplay = StructArrayBytes(nodes);
            BootPageAllocatorOnePageFreeResult replay = Free(freeFirst.AfterState, pointer, 1, 2, null, 0, 0, first);
            return IsExactFailure(replay, freeFirst.AfterState, first, BootPageAllocatorMutationStatus.FreeRangeNotAllocated, 1, 2, 0, 0) && beforeReplay.AsSpan().SequenceEqual(StructArrayBytes(nodes));
        }
    }

    private static unsafe bool TestResultEvidence()
    {
        BootPageFreeListNode[] nodes = [Node(31, 41, 0x101000, 5, -1), default, RawNode(91, 92, 3, 4, 5, 6, 7)];
        BootReservedRange[] reservations = [Reservation(BootReservationKind.LoadedImage, 0x800000, 2), Reservation(BootReservationKind.BootStack, 0x900000, 1), default];
        BootPageAllocatorState before = State(0, 1, 5, 7);
        fixed (BootPageFreeListNode* nodePointer = nodes)
        fixed (BootReservedRange* reservationPointer = reservations)
        {
            BootPageAllocatorOnePageResult allocation = Allocate(before, nodePointer, 2, 3, reservationPointer, 2, 3, 4 * PageSize);
            if (!allocation.CanRestore || !allocation.RestoreReceipt.IsInitialized || !NodesEqual(allocation.RestoreReceipt.SourceNodeBefore, Node(31, 41, 0x101000, 5, -1)) || allocation.RestoreReceipt.PreviousNodeIndexBefore != -1 || !IsDefaultNode(allocation.RestoreReceipt.PreviousNodeBefore))
            {
                return false;
            }

            BootPageAllocatorOnePageFreeResult result = Free(allocation.AfterState, nodePointer, 2, 3, reservationPointer, 2, 3, allocation);
            ulong beforeTotalPages = checked(result.BeforeFreePageCount + result.BeforeAllocatedPageCount);
            ulong afterTotalPages = checked(result.AfterFreePageCount + result.AfterAllocatedPageCount);
            ulong beforeTotalBytes = checked(result.BeforeFreeByteCount + result.BeforeAllocatedByteCount);
            ulong afterTotalBytes = checked(result.AfterFreeByteCount + result.AfterAllocatedByteCount);
            return IsExactSuccess(result, allocation, BootPageAllocatorOnePageCoalescingKind.PreviousAndNext, 0, -1, 1) && result.PageCount == 1 && result.ByteLength == PageSize && Raw(result.PhysicalStart) == 0x104000 && Raw(result.PhysicalEndExclusive) == 0x105000 && result.NodeCount == 2 && result.NodeCapacity == 3 && result.ReservationCount == 2 && result.ReservationCapacity == 3 && result.VisitedNodeCount == (ulong)allocation.AfterState.FreeNodeCount && result.InvalidReservationIndex == BootPageAllocatorMutator.NoIndex && result.ConflictingIndex == BootPageAllocatorMutator.NoIndex && beforeTotalPages == afterTotalPages && beforeTotalBytes == afterTotalBytes && beforeTotalBytes == beforeTotalPages * PageSize;
        }
    }

    private static unsafe bool TestReservationsAndCapacityTails()
    {
        BootPageFreeListNode[] nodes = [Node(51, 61, 0x200000, 3, -1), RawNode(81, 82, 3, 4, 5, 6, 7), RawNode(83, 84, 9, 10, 11, 12, 13)];
        BootReservedRange[] reservations = [Reservation(BootReservationKind.LoadedImage, 0x800000, 1), MutateByte(Reservation(BootReservationKind.BootStack, 0x900000, 1), 1, 0x7F)];
        byte[] originalNodes = StructArrayBytes(nodes);
        byte[] originalReservations = StructArrayBytes(reservations);
        BootPageAllocatorState before = State(0, 1, 3, 2);
        fixed (BootPageFreeListNode* nodePointer = nodes)
        fixed (BootReservedRange* reservationPointer = reservations)
        {
            BootPageAllocatorOnePageResult allocation = Allocate(before, nodePointer, 1, 3, reservationPointer, 1, 2, PageSize);
            if (!allocation.CanRestore || !NodesEqual(nodes[1], RawNode(81, 82, 3, 4, 5, 6, 7)) || !NodesEqual(nodes[2], RawNode(83, 84, 9, 10, 11, 12, 13)) || !originalReservations.AsSpan().SequenceEqual(StructArrayBytes(reservations)))
            {
                return false;
            }

            BootPageAllocatorOnePageFreeResult result = Free(allocation.AfterState, nodePointer, 1, 3, reservationPointer, 1, 2, allocation);
            return IsExactSuccess(result, allocation, BootPageAllocatorOnePageCoalescingKind.Next, 0, -1, -1) && originalNodes.AsSpan().SequenceEqual(StructArrayBytes(nodes)) && originalReservations.AsSpan().SequenceEqual(StructArrayBytes(reservations));
        }
    }

    private static unsafe bool TestDefaultFailedAndNonRestorable()
    {
        BootPageFreeListNode[] nodes = [Node(1, 2, 0x200000, 2, -1), default];
        BootPageAllocatorState state = State(0, 1, 2, 1);
        fixed (BootPageFreeListNode* pointer = nodes)
        {
            byte[] before = StructArrayBytes(nodes);
            BootPageAllocatorOnePageFreeResult defaultFailure = Free(state, pointer, 1, 2, null, 0, 0, default);
            if (!IsExactFailure(defaultFailure, state, default, BootPageAllocatorMutationStatus.FreeRangeNotAllocated, 1, 2, 0, 0) || !before.AsSpan().SequenceEqual(StructArrayBytes(nodes)))
            {
                return false;
            }

            BootPageAllocatorOnePageResult failed = Allocate(state, pointer, 1, 2, null, 0, 0, alignment: 0);
            if (failed.Succeeded || failed.CanRestore)
            {
                return false;
            }

            BootPageAllocatorOnePageFreeResult failedReceipt = Free(state, pointer, 1, 2, null, 0, 0, failed);
            if (!IsExactFailure(failedReceipt, state, failed, BootPageAllocatorMutationStatus.FreeRangeNotAllocated, 1, 2, 0, 0))
            {
                return false;
            }

            BootPageAllocatorOnePageResult forged = InvokeNonRestorableSuccessFactory(State(0, 1, 2, 0), State(0, 1, 1, 1), Address(0x200000), Address(0x201000), 1, 2, 0, 0, 0, -1, 1);
            nodes[0] = Node(1, 2, 0x201000, 1, -1);
            byte[] beforeForged = StructArrayBytes(nodes);
            return forged.Succeeded && !forged.CanRestore && IsExactFailure(Free(forged.AfterState, pointer, 1, 2, null, 0, 0, forged), forged.AfterState, forged, BootPageAllocatorMutationStatus.FreeRangeNotAllocated, 1, 2, 0, 0) && beforeForged.AsSpan().SequenceEqual(StructArrayBytes(nodes));
        }
    }

    private static unsafe bool TestAlteredAndForeignReceipts()
    {
        BootPageFreeListNode[] nodes = [Node(10, 20, 0x100000, 4, -1), default];
        BootPageAllocatorState before = State(0, 1, 4, 0);
        fixed (BootPageFreeListNode* pointer = nodes)
        {
            BootPageAllocatorOnePageResult allocation = Allocate(before, pointer, 1, 2, null, 0, 0, PageSize);
            byte[] afterAllocation = StructArrayBytes(nodes);
            BootPageAllocatorOnePageResult alteredRange = ForgeRestorable(allocation, physicalStart: Address(0x500000), physicalEndExclusive: Address(0x501000));
            BootPageAllocatorOnePageResult alteredSource = ForgeRestorable(allocation, sourceNodeBefore: Node(10, 20, 0x100000, 5, -1));
            BootPageAllocatorOnePageResult alteredProvenance = ForgeRestorable(allocation, sourceNodeBefore: Node(99, 98, 0x100000, 4, -1));
            BootPageAllocatorOnePageResult[] forgeries = [alteredRange, alteredSource, alteredProvenance];
            foreach (BootPageAllocatorOnePageResult forged in forgeries)
            {
                BootPageAllocatorOnePageFreeResult result = Free(allocation.AfterState, pointer, 1, 2, null, 0, 0, forged);
                if (result.Succeeded || result.Status == BootPageAllocatorMutationStatus.Success || !afterAllocation.AsSpan().SequenceEqual(StructArrayBytes(nodes)))
                {
                    return false;
                }
            }

            BootPageFreeListNode[] foreignNodes = [Node(30, 40, 0x500000, 4, -1), default];
            fixed (BootPageFreeListNode* foreignPointer = foreignNodes)
            {
                BootPageAllocatorOnePageResult foreign = Allocate(before, foreignPointer, 1, 2, null, 0, 0, PageSize);
                BootPageAllocatorOnePageFreeResult result = Free(allocation.AfterState, pointer, 1, 2, null, 0, 0, foreign);
                return !result.Succeeded && result.Status != BootPageAllocatorMutationStatus.Success && afterAllocation.AsSpan().SequenceEqual(StructArrayBytes(nodes)) && TestInconsistentPredecessorEvidence();
            }
        }
    }

    private static unsafe bool TestInconsistentPredecessorEvidence()
    {
        BootPageFreeListNode[] nodes = [Node(10, 20, 0x101000, 1, 1), Node(11, 21, 0x400000, 2, 2), Node(12, 22, 0x800000, 1, -1), RawNode(71, 72, 3, 4, 5, 6, 7)];
        BootReservedRange[] reservations = [Reservation(BootReservationKind.LoadedImage, 0xC00000, 1), MutateByte(Reservation(BootReservationKind.BootStack, 0xD00000, 1), 1, 0x7F)];
        BootPageAllocatorState before = State(0, 3, 4, 0);
        fixed (BootPageFreeListNode* nodePointer = nodes)
        fixed (BootReservedRange* reservationPointer = reservations)
        {
            BootPageAllocatorOnePageResult allocation = Allocate(before, nodePointer, 3, 4, reservationPointer, 1, 2, 0x200000);
            if (!allocation.CanRestore || allocation.NodeIndex != 1 || allocation.RestoreReceipt.PreviousNodeIndexBefore != 0 || !NodesEqual(allocation.RestoreReceipt.PreviousNodeBefore, Node(10, 20, 0x101000, 1, 1)))
            {
                return false;
            }

            byte[] nodesBefore = StructArrayBytes(nodes);
            byte[] reservationsBefore = StructArrayBytes(reservations);
            BootPageAllocatorOnePageResult[] forgeries = [ForgeRestorable(allocation, previousNodeIndexBefore: 2), ForgeRestorable(allocation, previousNodeBefore: Node(10, 20, 0x100000, 2, 1)), ForgeRestorable(allocation, previousNodeBefore: Node(99, 98, 0x101000, 1, 1)), ForgeRestorable(allocation, previousNodeBefore: Node(10, 20, 0x101000, 1, 2))];
            foreach (BootPageAllocatorOnePageResult forged in forgeries)
            {
                BootPageAllocatorOnePageFreeResult result = Free(allocation.AfterState, nodePointer, 3, 4, reservationPointer, 1, 2, forged);
                if (!forged.CanRestore || !forged.RestoreReceipt.IsInitialized || !IsExactFailure(result, allocation.AfterState, forged, BootPageAllocatorMutationStatus.FreeRangeNotAllocated, 3, 4, 1, 2) || !nodesBefore.AsSpan().SequenceEqual(StructArrayBytes(nodes)) || !reservationsBefore.AsSpan().SequenceEqual(StructArrayBytes(reservations)))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static bool TestStaleOutOfOrderAndDoubleFree()
    {
        return TestStrictLifoAndReplay();
    }

    private static unsafe bool TestForgedSuccessorOrdering()
    {
        BootPageFreeListNode[] headNodes = [default, Node(71, 81, 0x400000, 1, -1)];
        BootPageAllocatorState headBefore = State(0, 2, 2, 0);
        BootPageAllocatorState headAfter = State(1, 1, 1, 1);
        BootPageAllocatorOnePageResult headReceipt = InvokeRestorableSuccessFactory(headBefore, headAfter, Address(0x500000), Address(0x501000), nodeCount: 2, nodeCapacity: 2, reservationCount: 0, reservationCapacity: 0, nodeIndex: 0, insertedNodeIndex: -1, visitedNodeCount: 2, sourceNodeBefore: Node(70, 80, 0x500000, 1, 1), previousNodeIndexBefore: -1, previousNodeBefore: default);
        byte[] headBytes = StructArrayBytes(headNodes);
        fixed (BootPageFreeListNode* pointer = headNodes)
        {
            BootPageAllocatorOnePageFreeResult result = Free(headAfter, pointer, 2, 2, null, 0, 0, headReceipt);
            if (!headReceipt.CanRestore || !IsExactFailure(result, headAfter, headReceipt, BootPageAllocatorMutationStatus.FreeRangeNotAllocated, 2, 2, 0, 0) || !headBytes.AsSpan().SequenceEqual(StructArrayBytes(headNodes)))
            {
                return false;
            }
        }

        BootPageFreeListNode previousBefore = Node(90, 100, 0x100000, 1, 1);
        BootPageFreeListNode[] nonHeadNodes = [ReplaceNext(previousBefore, 2), default, Node(92, 102, 0x400000, 1, -1)];
        BootPageAllocatorState nonHeadBefore = State(0, 3, 3, 0);
        BootPageAllocatorState nonHeadAfter = State(0, 2, 2, 1);
        BootPageAllocatorOnePageResult nonHeadReceipt = InvokeRestorableSuccessFactory(nonHeadBefore, nonHeadAfter, Address(0x500000), Address(0x501000), nodeCount: 3, nodeCapacity: 3, reservationCount: 0, reservationCapacity: 0, nodeIndex: 1, insertedNodeIndex: -1, visitedNodeCount: 3, sourceNodeBefore: Node(91, 101, 0x500000, 1, 2), previousNodeIndexBefore: 0, previousNodeBefore: previousBefore);
        byte[] nonHeadBytes = StructArrayBytes(nonHeadNodes);
        fixed (BootPageFreeListNode* pointer = nonHeadNodes)
        {
            BootPageAllocatorOnePageFreeResult result = Free(nonHeadAfter, pointer, 3, 3, null, 0, 0, nonHeadReceipt);
            return nonHeadReceipt.CanRestore && IsExactFailure(result, nonHeadAfter, nonHeadReceipt, BootPageAllocatorMutationStatus.FreeRangeNotAllocated, 3, 3, 0, 0) && nonHeadBytes.AsSpan().SequenceEqual(StructArrayBytes(nonHeadNodes));
        }
    }

    private static unsafe bool TestInactiveTailPadding()
    {
        BootPageFreeListNode[] splitNodes = [Node(40, 50, 0x101000, 5, -1), MutateTailPadding(default, 0xA5A55A5A)];
        BootPageAllocatorState splitState = State(0, 1, 5, 0);
        byte[] splitBytes = StructArrayBytes(splitNodes);
        fixed (BootPageFreeListNode* pointer = splitNodes)
        {
            BootPageAllocatorOnePageResult result = Allocate(splitState, pointer, 2, 2, null, 0, 0, 4 * PageSize);
            if (!result.IsInitialized || result.Succeeded || result.CanRestore || result.Status != BootPageAllocatorMutationStatus.MetadataCapacityTooSmall || !StatesEqual(result.BeforeState, splitState) || !StatesEqual(result.AfterState, splitState) || !splitBytes.AsSpan().SequenceEqual(StructArrayBytes(splitNodes)))
            {
                return false;
            }
        }

        BootPageFreeListNode[] activeNodes = [MutateTailPadding(Node(60, 70, 0x200000, 3, -1), 0x11223344), default];
        byte[] activeBytes = StructArrayBytes(activeNodes);
        BootPageAllocatorState activeState = State(0, 1, 3, 0);
        fixed (BootPageFreeListNode* pointer = activeNodes)
        {
            BootPageAllocatorOnePageResult allocation = Allocate(activeState, pointer, 2, 2, null, 0, 0, PageSize);
            if (!allocation.CanRestore)
            {
                return false;
            }

            BootPageAllocatorOnePageFreeResult result = Free(allocation.AfterState, pointer, 2, 2, null, 0, 0, allocation);
            return IsExactSuccess(result, allocation, BootPageAllocatorOnePageCoalescingKind.Next, 0, -1, -1) && activeBytes.AsSpan().SequenceEqual(StructArrayBytes(activeNodes));
        }
    }

    private static unsafe bool TestOverlapAndReservationConflicts()
    {
        BootPageFreeListNode[] nodes = [Node(10, 20, 0x100000, 4, -1), default];
        BootPageAllocatorState before = State(0, 1, 4, 0);
        fixed (BootPageFreeListNode* pointer = nodes)
        {
            BootPageAllocatorOnePageResult allocation = Allocate(before, pointer, 1, 2, null, 0, 0, PageSize);
            if (!allocation.CanRestore)
            {
                return false;
            }

            nodes[0] = Node(10, 20, 0x100000, 3, -1);
            byte[] overlapBefore = StructArrayBytes(nodes);
            BootPageAllocatorOnePageFreeResult overlap = Free(allocation.AfterState, pointer, 1, 2, null, 0, 0, allocation);
            if (!IsExactFailure(overlap, allocation.AfterState, allocation, BootPageAllocatorMutationStatus.FreeRangeOverlaps, 1, 2, 0, 0) || overlap.ConflictingIndex != 0 || !overlapBefore.AsSpan().SequenceEqual(StructArrayBytes(nodes)))
            {
                return false;
            }

            nodes[0] = Node(10, 20, 0x101000, 3, -1);
            BootPageAllocatorOnePageResult withReservation = ForgeRestorable(allocation, reservationCount: 1, reservationCapacity: 2);
            BootReservedRange[] reservations = [Reservation(BootReservationKind.LoadedImage, 0x100000, 1), MutateByte(Reservation(BootReservationKind.BootStack, 0x900000, 1), 1, 0x7F)];
            byte[] nodesBefore = StructArrayBytes(nodes);
            byte[] reservationsBefore = StructArrayBytes(reservations);
            fixed (BootReservedRange* reservationPointer = reservations)
            {
                BootPageAllocatorOnePageFreeResult reserved = Free(allocation.AfterState, pointer, 1, 2, reservationPointer, 1, 2, withReservation);
                if (!IsExactFailure(reserved, allocation.AfterState, withReservation, BootPageAllocatorMutationStatus.ReservationOverlapsFreeRange, 1, 2, 1, 2) || reserved.InvalidReservationIndex != 0 || !nodesBefore.AsSpan().SequenceEqual(StructArrayBytes(nodes)) || !reservationsBefore.AsSpan().SequenceEqual(StructArrayBytes(reservations)))
                {
                    return false;
                }

                reservations[0] = Reservation(BootReservationKind.LoadedImage, 0x101000, 1);
                BootPageAllocatorOnePageFreeResult currentConflict = Free(allocation.AfterState, pointer, 1, 2, reservationPointer, 1, 2, withReservation);
                return IsExactFailure(currentConflict, allocation.AfterState, withReservation, BootPageAllocatorMutationStatus.ReservationOverlapsFreeRange, 1, 2, 1, 2) && currentConflict.InvalidReservationIndex == 0 && currentConflict.ConflictingIndex == 0;
            }
        }
    }

    private static unsafe bool TestCorruptStatesAndChains()
    {
        BootPageFreeListNode[] nodes = [Node(10, 20, 0x100000, 3, 1), Node(11, 21, 0x300000, 2, -1), default];
        BootPageAllocatorState before = State(0, 2, 5, 0);
        fixed (BootPageFreeListNode* pointer = nodes)
        {
            BootPageAllocatorOnePageResult allocation = Allocate(before, pointer, 3, 3, null, 0, 0, PageSize);
            if (!allocation.CanRestore)
            {
                return false;
            }

            BootPageFreeListNode[] canonical = (BootPageFreeListNode[])nodes.Clone();
            BootPageAllocatorState after = allocation.AfterState;
            if (!FailureWithStateAndNodes(default, nodes, 3, 3, allocation, BootPageAllocatorMutationStatus.StateNotInitialized) || !FailureWithStateAndNodes(new BootPageAllocatorState(true, 0, after.HeadNodeIndex, after.FreeNodeCount, after.FreePageCount, after.FreeByteCount, after.AllocatedPageCount, after.AllocatedByteCount), nodes, 3, 3, allocation, BootPageAllocatorMutationStatus.InvalidPageSize) || !FailureWithStateAndNodes(new BootPageAllocatorState(true, PageSize, 99, after.FreeNodeCount, after.FreePageCount, after.FreeByteCount, after.AllocatedPageCount, after.AllocatedByteCount), nodes, 3, 3, allocation, BootPageAllocatorMutationStatus.InvalidHeadNodeIndex) || !FailureWithStateAndNodes(new BootPageAllocatorState(true, PageSize, after.HeadNodeIndex, 4, after.FreePageCount, after.FreeByteCount, after.AllocatedPageCount, after.AllocatedByteCount), nodes, 3, 3, allocation, BootPageAllocatorMutationStatus.FreeListNodeCountMismatch))
            {
                return false;
            }

            nodes[0] = RawNode(10, 20, 0x101000, 0x103000, 3, 8192, 1);
            if (!FailureWithStateAndNodes(after, nodes, 3, 3, allocation, BootPageAllocatorMutationStatus.InvalidNodeShape))
            {
                return false;
            }

            CopyNodes(canonical, nodes);
            nodes[0] = ReplaceNext(nodes[0], 0);
            if (!FailureWithStateAndNodes(after, nodes, 3, 3, allocation, BootPageAllocatorMutationStatus.FreeListCycleDetected))
            {
                return false;
            }

            CopyNodes(canonical, nodes);
            nodes[0] = ReplaceNext(nodes[0], 2);
            nodes[2] = ReplaceNext(Node(12, 22, 0x100000, 1, -1), 1);
            BootPageAllocatorState threeNodeState = new(true, PageSize, 0, 3, after.FreePageCount + 1, after.FreeByteCount + PageSize, after.AllocatedPageCount, after.AllocatedByteCount);
            if (!FailureWithStateAndNodes(threeNodeState, nodes, 3, 3, allocation, BootPageAllocatorMutationStatus.InvalidNodeShape))
            {
                return false;
            }

            CopyNodes(canonical, nodes);
            BootPageAllocatorState wrongPages = new(true, PageSize, after.HeadNodeIndex, after.FreeNodeCount, after.FreePageCount + 1, after.FreeByteCount + PageSize, after.AllocatedPageCount - 1, after.AllocatedByteCount - PageSize);
            return FailureWithStateAndNodes(wrongPages, nodes, 3, 3, allocation, BootPageAllocatorMutationStatus.TotalPageCountMismatch);
        }
    }

    private static unsafe bool TestCountsCapacitiesOverflowAndAliases()
    {
        BootPageFreeListNode* nodes = stackalloc BootPageFreeListNode[3];
        nodes[0] = Node(10, 20, 0x100000, 3, -1);
        nodes[1] = default;
        nodes[2] = RawNode(71, 72, 3, 4, 5, 6, 7);
        BootPageAllocatorState before = State(0, 1, 3, 0);
        BootPageAllocatorOnePageResult allocation = Allocate(before, nodes, 2, 3, null, 0, 0, PageSize);
        if (!allocation.CanRestore)
        {
            return false;
        }

        byte[] canonical = PointerBytes(nodes, 3);
        if (!DirectPointerFailure(allocation.AfterState, nodes, 2, 1, null, 0, 0, allocation, BootPageAllocatorMutationStatus.NodeCapacityTooSmall) || !canonical.AsSpan().SequenceEqual(PointerBytes(nodes, 3)) || !DirectPointerFailure(allocation.AfterState, nodes, 3, 3, null, 0, 0, allocation, BootPageAllocatorMutationStatus.FreeRangeNotAllocated) || !canonical.AsSpan().SequenceEqual(PointerBytes(nodes, 3)))
        {
            return false;
        }

        BootPageAllocatorOnePageResult reservationAllocation = ForgeRestorable(allocation, reservationCount: 1, reservationCapacity: 1);
        BootReservedRange* reservations = stackalloc BootReservedRange[1];
        reservations[0] = Reservation(BootReservationKind.LoadedImage, 0x800000, 1);
        if (!DirectPointerFailure(allocation.AfterState, nodes, 2, 3, reservations, 1, 0, reservationAllocation, BootPageAllocatorMutationStatus.ReservationCapacityTooSmall))
        {
            return false;
        }

        BootPageAllocatorOnePageResult aliasAllocation = ForgeRestorable(allocation, reservationCount: 0, reservationCapacity: 1);
        if (!DirectPointerFailure(allocation.AfterState, nodes, 2, 2, (BootReservedRange*)nodes, 0, 1, aliasAllocation, BootPageAllocatorMutationStatus.NodeReservationTablesOverlap) || !canonical.AsSpan().SequenceEqual(PointerBytes(nodes, 3)))
        {
            return false;
        }

        BootPageAllocatorState overflowing = new(true, PageSize, allocation.AfterState.HeadNodeIndex, allocation.AfterState.FreeNodeCount, (ulong.MaxValue / PageSize) + 1, allocation.AfterState.FreeByteCount, allocation.AfterState.AllocatedPageCount, allocation.AfterState.AllocatedByteCount);
        return DirectPointerFailure(overflowing, nodes, 2, 3, null, 0, 0, allocation, BootPageAllocatorMutationStatus.InvalidStateAccounting) && canonical.AsSpan().SequenceEqual(PointerBytes(nodes, 3));
    }

    private static bool TestFailureAtomicity()
    {
        BootPageFreeListNode[] nodes = [Node(10, 20, 0x100000, 4, -1), default, RawNode(71, 72, 3, 4, 5, 6, 7)];
        BootReservedRange[] reservations = [Reservation(BootReservationKind.LoadedImage, 0x800000, 1), MutateByte(Reservation(BootReservationKind.BootStack, 0x900000, 1), 1, 0x7F)];
        BootPageAllocatorState before = State(0, 1, 4, 0);
        return AtomicFailureScenario(before, nodes, 2, 3, reservations, 1, 2, PageSize, static allocation => default, BootPageAllocatorMutationStatus.FreeRangeNotAllocated) && AtomicFailureScenario(before, nodes, 2, 3, reservations, 1, 2, PageSize, static allocation => ForgeRestorable(allocation, physicalStart: Address(0x100001), physicalEndExclusive: Address(0x101001)), BootPageAllocatorMutationStatus.FreeRangeNotAllocated) && AtomicFailureScenario(before, nodes, 2, 3, reservations, 1, 2, PageSize, static allocation => ForgeRestorable(allocation, sourceNodeBefore: Node(99, 98, 0x100000, 5, -1)), BootPageAllocatorMutationStatus.FreeRangeNotAllocated) && AtomicFailureScenario(before, nodes, 2, 3, reservations, 1, 2, PageSize, static allocation => ForgeRestorable(allocation, physicalStart: default(PhysicalAddress), physicalEndExclusive: default(PhysicalAddress)), BootPageAllocatorMutationStatus.FreeRangeNotAllocated, requireInitializedRestoreReceipt: true) && AtomicFailureScenario(before, nodes, 2, 3, reservations, 1, 2, PageSize, static allocation => ForgeRestorable(allocation, physicalStart: Address(0xFFFF_FFFF_FFFF_F000UL), physicalEndExclusive: Address(PageSize)), BootPageAllocatorMutationStatus.FreeRangeNotAllocated, requireInitializedRestoreReceipt: true);
    }

    private static unsafe bool TestDeterminismAndAllocation()
    {
        BootPageFreeListNode* nodes = stackalloc BootPageFreeListNode[2];
        BootPageAllocatorState initial = State(0, 1, 5, 7);
        BootPageAllocatorOnePageFreeResult baseline = default;
        for (int warmup = 0; warmup < 16; warmup++)
        {
            nodes[0] = Node(17, 19, 0x101000, 5, -1);
            nodes[1] = default;
            BootPageAllocatorOnePageResult allocation = Allocate(initial, nodes, 2, 2, null, 0, 0, 4 * PageSize);
            BootPageAllocatorOnePageFreeResult result = Free(allocation.AfterState, nodes, 2, 2, null, 0, 0, allocation);
            if (!result.Succeeded)
            {
                return false;
            }

            baseline = result;
        }

        long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 256; iteration++)
        {
            nodes[0] = Node(17, 19, 0x101000, 5, -1);
            nodes[1] = default;
            BootPageAllocatorOnePageResult allocation = Allocate(initial, nodes, 2, 2, null, 0, 0, 4 * PageSize);
            BootPageAllocatorOnePageFreeResult result = Free(allocation.AfterState, nodes, 2, 2, null, 0, 0, allocation);
            if (!FreeResultsEqual(result, baseline) || !NodesEqual(nodes[0], Node(17, 19, 0x101000, 5, -1)) || !IsDefaultNode(nodes[1]))
            {
                return false;
            }
        }

        long afterBytes = GC.GetAllocatedBytesForCurrentThread();
        return afterBytes == beforeBytes;
    }

    private static unsafe bool RoundTrip(BootPageAllocatorState beforeState, BootPageFreeListNode[] nodes, ulong nodeCount, ulong nodeCapacity, BootReservedRange[] reservations, ulong reservationCount, ulong reservationCapacity, ulong alignment, ulong expectedStart, BootPageAllocatorOnePageCoalescingKind expectedKind, int expectedRestoredNodeIndex, int expectedPreviousNodeIndex, int expectedRemovedSuffixNodeIndex)
    {
        byte[] nodesBefore = StructArrayBytes(nodes);
        byte[] reservationsBefore = StructArrayBytes(reservations);
        BootPageFreeListNode expectedSource = nodes[expectedRestoredNodeIndex];
        BootPageFreeListNode expectedPrevious = expectedPreviousNodeIndex < 0 ? default : nodes[expectedPreviousNodeIndex];
        fixed (BootPageFreeListNode* nodePointer = nodes)
        fixed (BootReservedRange* reservationPointer = reservations)
        {
            BootPageAllocatorOnePageResult allocation = Allocate(beforeState, nodePointer, nodeCount, nodeCapacity, reservationPointer, reservationCount, reservationCapacity, alignment);
            if (!allocation.IsInitialized || !allocation.Succeeded || !allocation.CanRestore || !allocation.RestoreReceipt.IsInitialized || Raw(allocation.PhysicalStart) != expectedStart || allocation.NodeIndex != expectedRestoredNodeIndex || allocation.RestoreReceipt.PreviousNodeIndexBefore != expectedPreviousNodeIndex || !NodesEqual(allocation.RestoreReceipt.SourceNodeBefore, expectedSource) || !NodesEqual(allocation.RestoreReceipt.PreviousNodeBefore, expectedPrevious) || !reservationsBefore.AsSpan().SequenceEqual(StructArrayBytes(reservations)))
            {
                return false;
            }

            BootPageAllocatorOnePageFreeResult result = Free(allocation.AfterState, nodePointer, nodeCount, nodeCapacity, reservationPointer, reservationCount, reservationCapacity, allocation);
            return IsExactSuccess(result, allocation, expectedKind, expectedRestoredNodeIndex, expectedPreviousNodeIndex, expectedRemovedSuffixNodeIndex) && StatesEqual(result.AfterState, beforeState) && nodesBefore.AsSpan().SequenceEqual(StructArrayBytes(nodes)) && reservationsBefore.AsSpan().SequenceEqual(StructArrayBytes(reservations));
        }
    }

    private static bool IsExactSuccess(BootPageAllocatorOnePageFreeResult result, BootPageAllocatorOnePageResult allocation, BootPageAllocatorOnePageCoalescingKind expectedKind, int expectedRestoredNodeIndex, int expectedPreviousNodeIndex, int expectedRemovedSuffixNodeIndex)
    {
        return result.IsInitialized && result.Succeeded && result.Status == BootPageAllocatorMutationStatus.Success && result.ReservationStatus == BootReservedRangeStatus.Success && result.AddressStatus == AddressValueStatus.Success && StatesEqual(result.BeforeState, allocation.AfterState) && StatesEqual(result.AfterState, allocation.BeforeState) && AllocationsEqual(result.Allocation, allocation) && result.CoalescingKind == expectedKind && result.PhysicalStart.Equals(allocation.PhysicalStart) && result.PhysicalEndExclusive.Equals(allocation.PhysicalEndExclusive) && result.PageCount == 1 && result.ByteLength == PageSize && result.NodeCount == allocation.NodeCount && result.NodeCapacity == allocation.NodeCapacity && result.ReservationCount == allocation.ReservationCount && result.ReservationCapacity == allocation.ReservationCapacity && result.RestoredNodeIndex == expectedRestoredNodeIndex && result.PreviousNodeIndex == expectedPreviousNodeIndex && result.RemovedSuffixNodeIndex == expectedRemovedSuffixNodeIndex && result.VisitedNodeCount == (ulong)allocation.AfterState.FreeNodeCount && result.InvalidReservationIndex == BootPageAllocatorMutator.NoIndex && result.ConflictingIndex == BootPageAllocatorMutator.NoIndex && result.BeforeFreeNodeCount == allocation.AfterState.FreeNodeCount && result.AfterFreeNodeCount == allocation.BeforeState.FreeNodeCount && result.BeforeFreePageCount == allocation.AfterState.FreePageCount && result.AfterFreePageCount == allocation.BeforeState.FreePageCount && result.BeforeFreeByteCount == allocation.AfterState.FreeByteCount && result.AfterFreeByteCount == allocation.BeforeState.FreeByteCount && result.BeforeAllocatedPageCount == allocation.AfterState.AllocatedPageCount && result.AfterAllocatedPageCount == allocation.BeforeState.AllocatedPageCount && result.BeforeAllocatedByteCount == allocation.AfterState.AllocatedByteCount && result.AfterAllocatedByteCount == allocation.BeforeState.AllocatedByteCount;
    }

    private static bool IsExactFailure(BootPageAllocatorOnePageFreeResult result, BootPageAllocatorState state, BootPageAllocatorOnePageResult allocation, BootPageAllocatorMutationStatus expectedStatus, ulong expectedNodeCount, ulong expectedNodeCapacity, ulong expectedReservationCount, ulong expectedReservationCapacity, BootReservedRangeStatus? expectedReservationStatus = null, AddressValueStatus? expectedAddressStatus = null)
    {
        BootReservedRangeStatus reservationStatus = expectedReservationStatus ?? (expectedStatus == BootPageAllocatorMutationStatus.FreeRangeNotAllocated ? allocation.ReservationStatus : BootReservedRangeStatus.Success);
        AddressValueStatus addressStatus = expectedAddressStatus ?? (expectedStatus == BootPageAllocatorMutationStatus.FreeRangeNotAllocated ? allocation.AddressStatus : AddressValueStatus.Success);
        return result.IsInitialized && !result.Succeeded && result.Status == expectedStatus && result.ReservationStatus == reservationStatus && result.AddressStatus == addressStatus && StatesEqual(result.BeforeState, state) && StatesEqual(result.AfterState, state) && AllocationsEqual(result.Allocation, allocation) && result.CoalescingKind == BootPageAllocatorOnePageCoalescingKind.Uninitialized && result.PhysicalStart.Equals(allocation.PhysicalStart) && result.PhysicalEndExclusive.Equals(allocation.PhysicalEndExclusive) && result.PageCount == allocation.PageCount && result.ByteLength == allocation.ByteLength && result.NodeCount == expectedNodeCount && result.NodeCapacity == expectedNodeCapacity && result.ReservationCount == expectedReservationCount && result.ReservationCapacity == expectedReservationCapacity && result.BeforeFreeNodeCount == state.FreeNodeCount && result.AfterFreeNodeCount == state.FreeNodeCount && result.BeforeFreePageCount == state.FreePageCount && result.AfterFreePageCount == state.FreePageCount && result.BeforeFreeByteCount == state.FreeByteCount && result.AfterFreeByteCount == state.FreeByteCount && result.BeforeAllocatedPageCount == state.AllocatedPageCount && result.AfterAllocatedPageCount == state.AllocatedPageCount && result.BeforeAllocatedByteCount == state.AllocatedByteCount && result.AfterAllocatedByteCount == state.AllocatedByteCount;
    }

    private static unsafe bool FailureWithStateAndNodes(BootPageAllocatorState state, BootPageFreeListNode[] nodes, ulong nodeCount, ulong nodeCapacity, BootPageAllocatorOnePageResult allocation, BootPageAllocatorMutationStatus expectedStatus)
    {
        byte[] before = StructArrayBytes(nodes);
        fixed (BootPageFreeListNode* pointer = nodes)
        {
            BootPageAllocatorOnePageFreeResult result = Free(state, pointer, nodeCount, nodeCapacity, null, 0, 0, allocation);
            return IsExactFailure(result, state, allocation, expectedStatus, nodeCount, nodeCapacity, 0, 0) && before.AsSpan().SequenceEqual(StructArrayBytes(nodes));
        }
    }

    private static unsafe bool DirectPointerFailure(BootPageAllocatorState state, BootPageFreeListNode* nodes, ulong nodeCount, ulong nodeCapacity, BootReservedRange* reservations, ulong reservationCount, ulong reservationCapacity, BootPageAllocatorOnePageResult allocation, BootPageAllocatorMutationStatus expectedStatus)
    {
        BootPageAllocatorOnePageFreeResult result = Free(state, nodes, nodeCount, nodeCapacity, reservations, reservationCount, reservationCapacity, allocation);
        return IsExactFailure(result, state, allocation, expectedStatus, nodeCount, nodeCapacity, reservationCount, reservationCapacity);
    }

    private static unsafe bool AtomicFailureScenario(BootPageAllocatorState before, BootPageFreeListNode[] sourceNodes, ulong nodeCount, ulong nodeCapacity, BootReservedRange[] sourceReservations, ulong reservationCount, ulong reservationCapacity, ulong alignment, Func<BootPageAllocatorOnePageResult, BootPageAllocatorOnePageResult> transform, BootPageAllocatorMutationStatus expectedStatus, bool requireInitializedRestoreReceipt = false)
    {
        BootPageFreeListNode[] nodes = (BootPageFreeListNode[])sourceNodes.Clone();
        BootReservedRange[] reservations = (BootReservedRange[])sourceReservations.Clone();
        fixed (BootPageFreeListNode* nodePointer = nodes)
        fixed (BootReservedRange* reservationPointer = reservations)
        {
            BootPageAllocatorOnePageResult real = Allocate(before, nodePointer, nodeCount, nodeCapacity, reservationPointer, reservationCount, reservationCapacity, alignment);
            if (!real.CanRestore)
            {
                return false;
            }

            BootPageAllocatorOnePageResult allocation = transform(real);
            if (requireInitializedRestoreReceipt && (!allocation.IsInitialized || !allocation.RestoreReceipt.IsInitialized || allocation.CanRestore))
            {
                return false;
            }

            byte[] nodesBefore = StructArrayBytes(nodes);
            byte[] reservationsBefore = StructArrayBytes(reservations);
            BootPageAllocatorOnePageFreeResult result = Free(real.AfterState, nodePointer, nodeCount, nodeCapacity, reservationPointer, reservationCount, reservationCapacity, allocation);
            return IsExactFailure(result, real.AfterState, allocation, expectedStatus, nodeCount, nodeCapacity, reservationCount, reservationCapacity) && nodesBefore.AsSpan().SequenceEqual(StructArrayBytes(nodes)) && reservationsBefore.AsSpan().SequenceEqual(StructArrayBytes(reservations));
        }
    }

    private static unsafe BootPageAllocatorOnePageResult Allocate(BootPageAllocatorState state, BootPageFreeListNode* nodes, ulong nodeCount, ulong nodeCapacity, BootReservedRange* reservations, ulong reservationCount, ulong reservationCapacity, ulong alignment)
    {
        return BootPageAllocatorMutator.AllocateOnePage(state, nodes, nodeCount, nodeCapacity, reservations, reservationCount, reservationCapacity, 1, alignment);
    }

    private static unsafe BootPageAllocatorOnePageFreeResult Free(BootPageAllocatorState state, BootPageFreeListNode* nodes, ulong nodeCount, ulong nodeCapacity, BootReservedRange* reservations, ulong reservationCount, ulong reservationCapacity, BootPageAllocatorOnePageResult allocation)
    {
        return BootPageAllocatorMutator.FreeOnePage(state, nodes, nodeCount, nodeCapacity, reservations, reservationCount, reservationCapacity, allocation);
    }

    private static BootPageAllocatorOnePageResult ForgeRestorable(BootPageAllocatorOnePageResult source, BootPageAllocatorState? beforeState = null, BootPageAllocatorState? afterState = null, PhysicalAddress? physicalStart = null, PhysicalAddress? physicalEndExclusive = null, ulong? nodeCount = null, ulong? nodeCapacity = null, ulong? reservationCount = null, ulong? reservationCapacity = null, int? nodeIndex = null, int? insertedNodeIndex = null, ulong? visitedNodeCount = null, BootPageFreeListNode? sourceNodeBefore = null, int? previousNodeIndexBefore = null, BootPageFreeListNode? previousNodeBefore = null)
    {
        MethodInfo? method = typeof(BootPageAllocatorOnePageResult).GetMethod("SuccessWithRestoreReceipt", BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
        {
            throw new InvalidOperationException("Restorable one-page success factory was not found.");
        }

        object? value = method.Invoke(null, [beforeState ?? source.BeforeState, afterState ?? source.AfterState, physicalStart ?? source.PhysicalStart, physicalEndExclusive ?? source.PhysicalEndExclusive, nodeCount ?? source.NodeCount, nodeCapacity ?? source.NodeCapacity, reservationCount ?? source.ReservationCount, reservationCapacity ?? source.ReservationCapacity, nodeIndex ?? source.NodeIndex, insertedNodeIndex ?? source.InsertedNodeIndex, visitedNodeCount ?? source.VisitedNodeCount, sourceNodeBefore ?? source.RestoreReceipt.SourceNodeBefore, previousNodeIndexBefore ?? source.RestoreReceipt.PreviousNodeIndexBefore, previousNodeBefore ?? source.RestoreReceipt.PreviousNodeBefore]);
        if (value is not BootPageAllocatorOnePageResult result)
        {
            throw new InvalidOperationException("Restorable one-page success factory returned no result.");
        }

        return result;
    }

    private static BootPageAllocatorOnePageResult InvokeRestorableSuccessFactory(BootPageAllocatorState beforeState, BootPageAllocatorState afterState, PhysicalAddress physicalStart, PhysicalAddress physicalEndExclusive, ulong nodeCount, ulong nodeCapacity, ulong reservationCount, ulong reservationCapacity, int nodeIndex, int insertedNodeIndex, ulong visitedNodeCount, BootPageFreeListNode sourceNodeBefore, int previousNodeIndexBefore, BootPageFreeListNode previousNodeBefore)
    {
        MethodInfo? method = typeof(BootPageAllocatorOnePageResult).GetMethod("SuccessWithRestoreReceipt", BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
        {
            throw new InvalidOperationException("Restorable one-page success factory was not found.");
        }

        object? value = method.Invoke(null, [beforeState, afterState, physicalStart, physicalEndExclusive, nodeCount, nodeCapacity, reservationCount, reservationCapacity, nodeIndex, insertedNodeIndex, visitedNodeCount, sourceNodeBefore, previousNodeIndexBefore, previousNodeBefore]);
        if (value is not BootPageAllocatorOnePageResult result)
        {
            throw new InvalidOperationException("Restorable one-page success factory returned no result.");
        }

        return result;
    }

    private static BootPageAllocatorOnePageResult InvokeNonRestorableSuccessFactory(BootPageAllocatorState beforeState, BootPageAllocatorState afterState, PhysicalAddress physicalStart, PhysicalAddress physicalEndExclusive, ulong nodeCount, ulong nodeCapacity, ulong reservationCount, ulong reservationCapacity, int nodeIndex, int insertedNodeIndex, ulong visitedNodeCount)
    {
        MethodInfo? method = typeof(BootPageAllocatorOnePageResult).GetMethod("Success", BindingFlags.NonPublic | BindingFlags.Static);
        if (method is null)
        {
            throw new InvalidOperationException("Non-restorable one-page success factory was not found.");
        }

        object? value = method.Invoke(null, [beforeState, afterState, physicalStart, physicalEndExclusive, nodeCount, nodeCapacity, reservationCount, reservationCapacity, nodeIndex, insertedNodeIndex, visitedNodeCount]);
        if (value is not BootPageAllocatorOnePageResult result)
        {
            throw new InvalidOperationException("Non-restorable factory returned no result.");
        }

        return result;
    }

    private static BootPageFreeListNode Node(int sourceFreeSegmentIndex, int sourceUsableRangeIndex, ulong physicalStart, ulong pageCount, int nextNodeIndex)
    {
        ulong byteLength = checked(pageCount * PageSize);
        return new BootPageFreeListNode(sourceFreeSegmentIndex, sourceUsableRangeIndex, physicalStart, checked(physicalStart + byteLength), pageCount, byteLength, nextNodeIndex);
    }

    private static BootPageFreeListNode RawNode(int sourceFreeSegmentIndex, int sourceUsableRangeIndex, ulong physicalStart, ulong physicalEndExclusive, ulong pageCount, ulong byteLength, int nextNodeIndex)
    {
        return new BootPageFreeListNode(sourceFreeSegmentIndex, sourceUsableRangeIndex, physicalStart, physicalEndExclusive, pageCount, byteLength, nextNodeIndex);
    }

    private static BootPageFreeListNode ReplaceNext(BootPageFreeListNode source, int nextNodeIndex)
    {
        return new BootPageFreeListNode(source.SourceFreeSegmentIndex, source.SourceUsableRangeIndex, source.PhysicalStart, source.PhysicalEndExclusive, source.PageCount, source.ByteLength, nextNodeIndex);
    }

    private static BootPageAllocatorState State(int headNodeIndex, int freeNodeCount, ulong freePages, ulong allocatedPages)
    {
        return new BootPageAllocatorState(true, PageSize, headNodeIndex, freeNodeCount, freePages, checked(freePages * PageSize), allocatedPages, checked(allocatedPages * PageSize));
    }

    private static BootReservedRange Reservation(BootReservationKind kind, ulong physicalStart, ulong pageCount)
    {
        BootReservedRangeStatus status = BootReservedRange.TryCreate(kind, Address(physicalStart), pageCount, out BootReservedRange reservation);
        if (status != BootReservedRangeStatus.Success)
        {
            throw new InvalidOperationException("Could not create reservation: " + status);
        }

        return reservation;
    }

    private static PhysicalAddress Address(ulong value)
    {
        AddressValueStatus status = PhysicalAddress.TryCreateFromUefiBoundary(value, out PhysicalAddress address);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("Could not create physical address: " + status);
        }

        return address;
    }

    private static ulong Raw(PhysicalAddress address)
    {
        AddressValueStatus status = address.TryGetRawValueForUefiBoundary(out ulong value);
        if (status != AddressValueStatus.Success)
        {
            throw new InvalidOperationException("Could not read physical address: " + status);
        }

        return value;
    }

    private static bool NodesEqual(BootPageFreeListNode left, BootPageFreeListNode right)
    {
        return left.SourceFreeSegmentIndex == right.SourceFreeSegmentIndex && left.SourceUsableRangeIndex == right.SourceUsableRangeIndex && left.PhysicalStart == right.PhysicalStart && left.PhysicalEndExclusive == right.PhysicalEndExclusive && left.PageCount == right.PageCount && left.ByteLength == right.ByteLength && left.NextNodeIndex == right.NextNodeIndex;
    }

    private static bool StatesEqual(BootPageAllocatorState left, BootPageAllocatorState right)
    {
        return left.IsInitialized == right.IsInitialized && left.PageSize == right.PageSize && left.HeadNodeIndex == right.HeadNodeIndex && left.FreeNodeCount == right.FreeNodeCount && left.FreePageCount == right.FreePageCount && left.FreeByteCount == right.FreeByteCount && left.AllocatedPageCount == right.AllocatedPageCount && left.AllocatedByteCount == right.AllocatedByteCount;
    }

    private static bool ReceiptsEqual(BootPageAllocatorOnePageRestoreReceipt left, BootPageAllocatorOnePageRestoreReceipt right)
    {
        return left.IsInitialized == right.IsInitialized && NodesEqual(left.SourceNodeBefore, right.SourceNodeBefore) && left.PreviousNodeIndexBefore == right.PreviousNodeIndexBefore && NodesEqual(left.PreviousNodeBefore, right.PreviousNodeBefore);
    }

    private static bool AllocationsEqual(BootPageAllocatorOnePageResult left, BootPageAllocatorOnePageResult right)
    {
        return left.IsInitialized == right.IsInitialized && left.Status == right.Status && left.ReservationStatus == right.ReservationStatus && left.AddressStatus == right.AddressStatus && left.Succeeded == right.Succeeded && left.CanRestore == right.CanRestore && StatesEqual(left.BeforeState, right.BeforeState) && StatesEqual(left.AfterState, right.AfterState) && left.PhysicalStart.Equals(right.PhysicalStart) && left.PhysicalEndExclusive.Equals(right.PhysicalEndExclusive) && left.PageCount == right.PageCount && left.ByteLength == right.ByteLength && left.NodeCount == right.NodeCount && left.NodeCapacity == right.NodeCapacity && left.ReservationCount == right.ReservationCount && left.ReservationCapacity == right.ReservationCapacity && left.NodeIndex == right.NodeIndex && left.InsertedNodeIndex == right.InsertedNodeIndex && left.VisitedNodeCount == right.VisitedNodeCount && left.InvalidReservationIndex == right.InvalidReservationIndex && left.ConflictingIndex == right.ConflictingIndex && ReceiptsEqual(left.RestoreReceipt, right.RestoreReceipt);
    }

    private static bool FreeResultsEqual(BootPageAllocatorOnePageFreeResult left, BootPageAllocatorOnePageFreeResult right)
    {
        return left.IsInitialized == right.IsInitialized && left.Status == right.Status && left.ReservationStatus == right.ReservationStatus && left.AddressStatus == right.AddressStatus && left.Succeeded == right.Succeeded && StatesEqual(left.BeforeState, right.BeforeState) && StatesEqual(left.AfterState, right.AfterState) && AllocationsEqual(left.Allocation, right.Allocation) && left.CoalescingKind == right.CoalescingKind && left.PhysicalStart.Equals(right.PhysicalStart) && left.PhysicalEndExclusive.Equals(right.PhysicalEndExclusive) && left.PageCount == right.PageCount && left.ByteLength == right.ByteLength && left.NodeCount == right.NodeCount && left.NodeCapacity == right.NodeCapacity && left.ReservationCount == right.ReservationCount && left.ReservationCapacity == right.ReservationCapacity && left.RestoredNodeIndex == right.RestoredNodeIndex && left.PreviousNodeIndex == right.PreviousNodeIndex && left.RemovedSuffixNodeIndex == right.RemovedSuffixNodeIndex && left.VisitedNodeCount == right.VisitedNodeCount && left.InvalidReservationIndex == right.InvalidReservationIndex && left.ConflictingIndex == right.ConflictingIndex;
    }

    private static bool IsDefaultNode(BootPageFreeListNode node)
    {
        return node.SourceFreeSegmentIndex == 0 && node.SourceUsableRangeIndex == 0 && node.PhysicalStart == 0 && node.PhysicalEndExclusive == 0 && node.PageCount == 0 && node.ByteLength == 0 && node.NextNodeIndex == 0;
    }

    private static void CopyNodes(BootPageFreeListNode[] source, BootPageFreeListNode[] destination)
    {
        for (int index = 0; index < source.Length; index++)
        {
            destination[index] = source[index];
        }
    }

    private static byte[] StructArrayBytes<T>(T[] values)
        where T : struct
    {
        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    }

    private static unsafe byte[] PointerBytes<T>(T* pointer, int count)
        where T : unmanaged
    {
        return new ReadOnlySpan<byte>(pointer, checked(count * sizeof(T))).ToArray();
    }

    private static T MutateByte<T>(T value, int byteOffset, byte replacement)
        where T : struct
    {
        Span<T> values = MemoryMarshal.CreateSpan(ref value, 1);
        Span<byte> bytes = MemoryMarshal.AsBytes(values);
        bytes[byteOffset] = replacement;
        return value;
    }

    private static BootPageFreeListNode MutateTailPadding(BootPageFreeListNode value, uint replacement)
    {
        if (Marshal.SizeOf<BootPageFreeListNode>() != 48)
        {
            throw new InvalidOperationException("BootPageFreeListNode layout changed.");
        }

        Span<BootPageFreeListNode> values = MemoryMarshal.CreateSpan(ref value, 1);
        Span<byte> bytes = MemoryMarshal.AsBytes(values);
        bytes[44] = (byte)replacement;
        bytes[45] = (byte)(replacement >> 8);
        bytes[46] = (byte)(replacement >> 16);
        bytes[47] = (byte)(replacement >> 24);
        return value;
    }

    private static bool IsReadOnlySequentialUnmanaged<T>()
        where T : unmanaged
    {
        Type type = typeof(T);
        return type.IsValueType && type.IsLayoutSequential && type.IsDefined(typeof(IsReadOnlyAttribute), inherit: false) && !RuntimeHelpers.IsReferenceOrContainsReferences<T>();
    }

    private static bool HasExactlyOnePublicOnePageApi(string name)
    {
        return typeof(BootPageAllocatorMutator).GetMethods(BindingFlags.Public | BindingFlags.Static).Count(method => method.Name == name) == 1;
    }

    private static bool HasExactFreeOnePageApi()
    {
        MethodInfo[] methods = typeof(BootPageAllocatorMutator).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(method => method.Name == nameof(BootPageAllocatorMutator.FreeOnePage)).ToArray();
        if (methods.Length != 1 || methods[0].ReturnType != typeof(BootPageAllocatorOnePageFreeResult))
        {
            return false;
        }

        ParameterInfo[] parameters = methods[0].GetParameters();
        (string Name, Type Type)[] expected = [("state", typeof(BootPageAllocatorState)), ("nodes", typeof(BootPageFreeListNode).MakePointerType()), ("nodeCount", typeof(ulong)), ("nodeCapacity", typeof(ulong)), ("reservations", typeof(BootReservedRange).MakePointerType()), ("reservationCount", typeof(ulong)), ("reservationCapacity", typeof(ulong)), ("allocation", typeof(BootPageAllocatorOnePageResult))];
        if (parameters.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (parameters[index].Name != expected[index].Name || parameters[index].ParameterType != expected[index].Type)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasExactIntEnum<T>((string Name, int Value)[] expected)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(int))
        {
            return false;
        }

        string[] names = Enum.GetNames(typeof(T));
        Array values = Enum.GetValues(typeof(T));
        if (names.Length != expected.Length || values.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (names[index] != expected[index].Name || Convert.ToInt32(values.GetValue(index)) != expected[index].Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasExactByteEnum<T>((string Name, byte Value)[] expected)
        where T : struct, Enum
    {
        if (Enum.GetUnderlyingType(typeof(T)) != typeof(byte))
        {
            return false;
        }

        string[] names = Enum.GetNames(typeof(T));
        Array values = Enum.GetValues(typeof(T));
        if (names.Length != expected.Length || values.Length != expected.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (names[index] != expected[index].Name || Convert.ToByte(values.GetValue(index)) != expected[index].Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TestHostilePointers()
    {
        string workspace = CreateHostilePointerWorkspace();
        try
        {
            ToolResult build = RunDotnetBuild(workspace, "M01.009.PointerPreflightProbe.csproj", timeoutMilliseconds: 120_000, noDependencies: true);
            if (build.TimedOut || build.ExitCode != 0)
            {
                Console.WriteLine("M01_009_POINTER_SUBPROCESS_DIAGNOSTIC build=" + NormalizeToolOutput(build.Output));
                return false;
            }

            string assemblyPath = Path.Combine(workspace, "out", "CLROS.M01.FreePointerPreflightProbe.dll");
            string[] cases = ["node-count-over", "node-capacity-over", "node-byte-over", "node-missing", "node-misaligned", "node-span-wrap", "reservation-count-over", "reservation-capacity-over", "reservation-byte-over", "reservation-missing", "reservation-misaligned", "reservation-span-wrap"];
            foreach (string testCase in cases)
            {
                ProcessStartInfo start = DotnetStartInfo(workspace);
                start.ArgumentList.Add(assemblyPath);
                start.ArgumentList.Add(testCase);
                ToolResult run = RunProcess(start, timeoutMilliseconds: 15_000);
                if (run.TimedOut || run.ExitCode != 0)
                {
                    Console.WriteLine("M01_009_POINTER_SUBPROCESS_DIAGNOSTIC case=" + testCase + ";exit=" + run.ExitCode + ";timedOut=" + run.TimedOut + ";output=" + NormalizeToolOutput(run.Output));
                    return false;
                }
            }

            return true;
        }
        finally
        {
            DeleteTemporaryWorkspace(workspace);
        }
    }

    private static string CreateHostilePointerWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = CreateTemporaryWorkspace();
        try
        {
            File.Copy(Path.Combine(repositoryRoot, "global.json"), Path.Combine(workspace, "global.json"));
            File.WriteAllText(Path.Combine(workspace, "Program.cs"), """
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
                            7,
                            9,
                            0x100000,
                            0x103000,
                            3,
                            12288,
                            -1);
                        nodes[1] = default;
                        BootPageAllocatorState before =
                            new BootPageAllocatorState(
                                true,
                                4096,
                                0,
                                1,
                                3,
                                12288,
                                0,
                                0);
                        BootPageAllocatorOnePageResult allocation =
                            BootPageAllocatorMutator.AllocateOnePage(
                                before,
                                nodes,
                                2,
                                2,
                                null,
                                0,
                                0,
                                1,
                                4096);
                        if (!allocation.CanRestore)
                        {
                            return 66;
                        }

                        BootReservedRange* reservations =
                            stackalloc BootReservedRange[1];
                        reservations[0] = default;
                        BootPageFreeListNode* nodePointer = nodes;
                        ulong nodeCount = 2;
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
                                nodeCount = 1;
                                nodeCapacity = 1;
                                expected =
                                    BootPageAllocatorMutationStatus
                                        .MissingNodes;
                                break;
                            case "node-misaligned":
                                nodePointer =
                                    (BootPageFreeListNode*)1;
                                nodeCount = 1;
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
                                nodeCount = 1;
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

                        BootPageAllocatorOnePageFreeResult result =
                            BootPageAllocatorMutator.FreeOnePage(
                                allocation.AfterState,
                                nodePointer,
                                nodeCount,
                                nodeCapacity,
                                reservationPointer,
                                reservationCount,
                                reservationCapacity,
                                allocation);
                        return !result.Succeeded
                                && result.Status == expected
                            ? 0
                            : 2;
                    }
                }
                """, Utf8NoBom);
            string project = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <AssemblyName>CLROS.M01.FreePointerPreflightProbe</AssemblyName>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net11.0</TargetFramework>
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
                """.Replace("__KERNEL_PROJECT__", EscapeXml(Path.Combine(repositoryRoot, "src", "OS.Kernel", "OS.Kernel.csproj")), StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(workspace, "M01.009.PointerPreflightProbe.csproj"), project, Utf8NoBom);
            return workspace;
        }
        catch
        {
            DeleteTemporaryWorkspace(workspace);
            throw;
        }
    }

    private static bool TestSourceAndEfiFirewalls()
    {
        var profile = OS.Tooling.EfiOptions.Parse([], false).Profile();
        var host = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var source = HostSourceCatalog.Efi(profile);
        return CheckFreeSource(host, source) && FreeIntegrationBoundary(source, new EfiAllocatorOwnershipSources(profile).Sources) && IsReadOnlySequentialUnmanaged<BootPageAllocatorOnePageRestoreReceipt>() && IsReadOnlySequentialUnmanaged<BootPageAllocatorOnePageFreeResult>() && EvaluatedReleaseEfiClosureExcludesLegacyAllocatorSources();
    }

    private static bool EvaluatedReleaseEfiClosureExcludesLegacyAllocatorSources()
    {
        var evaluation = HostSourceCatalog.EfiEvaluation(OS.Tooling.EfiOptions.Parse([], false).Profile());
        using var json = JsonDocument.Parse(evaluation.EvaluationJson);
        var properties = json.RootElement.GetProperty("Properties");
        var items = json.RootElement.GetProperty("Items");
        if (properties.GetProperty("EnableDefaultCompileItems").GetString() != "false" || properties.GetProperty("NoStdLib").GetString() != "true" || properties.GetProperty("DisableImplicitFrameworkReferences").GetString() != "true" || properties.GetProperty("Configuration").GetString() != "Release" || properties.GetProperty("OSWorkloadProfile").GetString() != "SingleMinimalApi" || items.GetProperty("ProjectReference").GetArrayLength() != 0 || items.GetProperty("Reference").GetArrayLength() != 0 || evaluation.Sources.Compilation.References.Any())
            return SourceFailure("efi-reference-or-property-boundary");
        string[] paths = items.GetProperty("Compile").EnumerateArray().Select(item => item.GetProperty("FullPath").GetString()!).ToArray();
        if (paths.Length == 0 || paths.Any(path => !Path.IsPathFullyQualified(path)) || !paths.Select(Path.GetFullPath).SequenceEqual(evaluation.Sources.Documents.Select(document => document.Path), StringComparer.OrdinalIgnoreCase))
            return SourceFailure("efi-compiler-input-order");
        _ = evaluation.Sources.Type("OS.Kernel.Memory.BootPageAllocatorState");
        _ = evaluation.Sources.Type("OS.Kernel.Memory.BootPageFreeListNode");
        _ = evaluation.Sources.Type(FreeOwner);
        return FreeLegacyLayersExcluded(evaluation.Sources);
    }

    private static bool TestNoStdLibEfiCompileClosure()
    {
        string workspace = CreateNoStdLibWorkspace();
        try
        {
            ToolResult build = RunDotnetBuild(workspace, "M01.009.NoStdLib.csproj", timeoutMilliseconds: 90_000, noDependencies: true);
            string assemblyPath = Path.Combine(workspace, "out", "CLROS.M01.BootPageAllocatorFreeCompileClosure.dll");
            if (build.TimedOut || build.ExitCode != 0 || !HasZeroAssemblyReferences(assemblyPath))
            {
                Console.WriteLine("M01_009_NOSTDLIB_DIAGNOSTIC " + NormalizeToolOutput(build.Output));
                return false;
            }

            return true;
        }
        finally
        {
            DeleteTemporaryWorkspace(workspace);
        }
    }

    private static string CreateNoStdLibWorkspace()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string workspace = CreateTemporaryWorkspace();
        try
        {
            File.Copy(Path.Combine(repositoryRoot, "global.json"), Path.Combine(workspace, "global.json"));
            File.WriteAllText(Path.Combine(workspace, "NoStdLibShim.cs"), """
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
                """, Utf8NoBom);
            File.WriteAllText(Path.Combine(workspace, "Probe.cs"), """
                namespace CLROS.M01.FreeCompileClosure;

                public static unsafe class Probe
                {
                    public static bool Run()
                    {
                        OS.Kernel.Memory.BootPageFreeListNode* nodes =
                            stackalloc OS.Kernel.Memory
                                .BootPageFreeListNode[2];
                        nodes[0] =
                            new OS.Kernel.Memory.BootPageFreeListNode(
                                7,
                                9,
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

                        OS.Kernel.Memory.BootPageAllocatorState before =
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
                            .BootPageAllocatorOnePageResult allocation =
                                OS.Kernel.Memory
                                    .BootPageAllocatorMutator
                                    .AllocateOnePage(
                                        before,
                                        nodes,
                                        2,
                                        2,
                                        &reservation,
                                        1,
                                        1,
                                        1,
                                        4096);
                        if (!allocation.CanRestore
                            || allocation.AfterState.FreePageCount != 1
                            || nodes[0].PhysicalStart != 0x201000)
                        {
                            return false;
                        }

                        OS.Kernel.Memory
                            .BootPageAllocatorOnePageFreeResult freed =
                                OS.Kernel.Memory
                                    .BootPageAllocatorMutator
                                    .FreeOnePage(
                                        allocation.AfterState,
                                        nodes,
                                        2,
                                        2,
                                        &reservation,
                                        1,
                                        1,
                                        allocation);
                        return freed.Succeeded
                            && freed.CoalescingKind
                                == OS.Kernel.Memory
                                    .BootPageAllocatorOnePageCoalescingKind
                                    .Next
                            && freed.AfterState.FreePageCount == 2
                            && freed.AfterState.AllocatedPageCount == 0
                            && nodes[0].SourceFreeSegmentIndex == 7
                            && nodes[0].SourceUsableRangeIndex == 9
                            && nodes[0].PhysicalStart == 0x200000
                            && nodes[0].PhysicalEndExclusive == 0x202000
                            && nodes[0].PageCount == 2
                            && nodes[0].ByteLength == 8192
                            && nodes[0].NextNodeIndex == -1;
                    }
                }
                """, Utf8NoBom);
            string[] sources = NoStdLibFreeSources(repositoryRoot, File.ReadAllText(Path.Combine(workspace, "NoStdLibShim.cs")));
            string includes = string.Join("\n", sources.Select(path => new System.Xml.Linq.XElement("Compile", new System.Xml.Linq.XAttribute("Include", path), new System.Xml.Linq.XAttribute("Link", Path.GetRelativePath(repositoryRoot, path))).ToString(System.Xml.Linq.SaveOptions.DisableFormatting)));
            string project = """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <AssemblyName>CLROS.M01.BootPageAllocatorFreeCompileClosure</AssemblyName>
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
                    __DISCOVERED_SOURCES__
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
                """.Replace("__DISCOVERED_SOURCES__", includes, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(workspace, "M01.009.NoStdLib.csproj"), project, Utf8NoBom);
            return workspace;
        }
        catch
        {
            DeleteTemporaryWorkspace(workspace);
            throw;
        }
    }

    private static bool TestMarkerBoundary()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        var source = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        string testSource = HostSourceText.Documents(source, nameof(BootPageAllocatorFreeCoalescingTests));
        string productionSource = HostSourceText.Documents(HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"), [.. FreeOwners, "OS.Kernel.Memory.BootPageAllocatorMutationResult"]);
        var familyPaths = HostSourceText.DocumentSources(source, nameof(BootPageAllocatorFreeCoalescingTests)).Select(document => document.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string canonicalBootDocPath = Path.GetFullPath(Path.Combine(repositoryRoot, "docs", "boot.md"));
        string canonicalArchitectureDocPath = Path.GetFullPath(Path.Combine(repositoryRoot, "docs", "architecture.md"));
        string canonicalBacklogPath = Path.GetFullPath(Path.Combine(repositoryRoot, ".gitea", "backlog", "clros-issues.json"));
        string startWrite = string.Concat("Console.WriteLine(\"TEST_START \" + ", "IssueTestName);");
        string failureWrite = string.Concat("Console.WriteLine(\"TEST_FAIL \" + ", "IssueTestName + \" failures=\" + failures);");
        string readyWrite = string.Concat("Console.WriteLine(", "ReadyMarker);");
        string passWrite = string.Concat("Console.WriteLine(\"TEST_PASS \" + ", "IssueTestName);");
        if (!FreeMarkerShape(source) || productionSource.Contains(ReadyMarker, StringComparison.Ordinal))
            return false;
        foreach (string path in Directory.EnumerateFiles(repositoryRoot, "*", SearchOption.TopDirectoryOnly))
        {
            if (!IsGeneratedOrReferencePath(path) && HasTextExtension(path) && File.ReadAllText(path).Contains(ReadyMarker, StringComparison.Ordinal))
            {
                return false;
            }
        }

        string[] scanRoots = [Path.Combine(repositoryRoot, "src"), Path.Combine(repositoryRoot, "std"), Path.Combine(repositoryRoot, "apps"), Path.Combine(repositoryRoot, "tools"), Path.Combine(repositoryRoot, "tests"), Path.Combine(repositoryRoot, "scripts"), Path.Combine(repositoryRoot, "docs"), Path.Combine(repositoryRoot, ".gitea")];
        foreach (string root in scanRoots)
        {
            if (!Directory.Exists(root))
            {
                return false;
            }

            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                if (IsGeneratedOrReferencePath(path) || !HasTextExtension(path))
                {
                    continue;
                }

                string contents = File.ReadAllText(path);
                int occurrences = CountOccurrences(contents, ReadyMarker);
                if (occurrences == 0)
                {
                    continue;
                }

                string canonicalPath = Path.GetFullPath(path);
                if (familyPaths.Contains(canonicalPath))
                {
                    if (occurrences != 1)
                    {
                        return false;
                    }

                    continue;
                }

                bool canonicalDeclaration = string.Equals(canonicalPath, canonicalBootDocPath, StringComparison.OrdinalIgnoreCase) || string.Equals(canonicalPath, canonicalArchitectureDocPath, StringComparison.OrdinalIgnoreCase) || string.Equals(canonicalPath, canonicalBacklogPath, StringComparison.OrdinalIgnoreCase);
                if (!canonicalDeclaration || contents.Contains(readyWrite, StringComparison.Ordinal) || contents.Contains("Console.WriteLine(\"" + ReadyMarker, StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        string bootGuestMarker = string.Concat("OS_BOOT_", "OK");
        string smokeGuestMarker = string.Concat("OS_SMOKE_", "PASS");
        return !testSource.Contains(bootGuestMarker, StringComparison.Ordinal) && !testSource.Contains(smokeGuestMarker, StringComparison.Ordinal);
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

    private static ToolResult RunDotnetBuild(string workspace, string project, int timeoutMilliseconds, bool noDependencies = false)
    {
        ProcessStartInfo start = DotnetStartInfo(workspace);
        start.ArgumentList.Add("build");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("-m:1");
        if (noDependencies)
        {
            start.ArgumentList.Add("--no-dependencies");
        }

        start.ArgumentList.Add("--no-incremental");
        start.ArgumentList.Add("--disable-build-servers");
        start.ArgumentList.Add("-v:minimal");
        start.ArgumentList.Add("-p:RestoreIgnoreFailedSources=true");
        start.ArgumentList.Add("-p:NuGetAudit=false");
        return RunProcess(start, timeoutMilliseconds);
    }

    private static ProcessStartInfo DotnetStartInfo(string workingDirectory)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        ConfigureDotnetEnvironment(start);
        return start;
    }

    private static ToolResult RunProcess(ProcessStartInfo start, int timeoutMilliseconds)
    {
        using Process process = new Process
        {
            StartInfo = start
        };
        if (!process.Start())
        {
            return new ToolResult(-1, timedOut: false, "process did not start");
        }

        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
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

        string output = standardOutput.Wait(5_000) ? standardOutput.Result : "stdout did not close";
        string error = standardError.Wait(5_000) ? standardError.Result : "stderr did not close";
        return new ToolResult(exited ? process.ExitCode : -1, timedOut: !exited, output + Environment.NewLine + error);
    }

    private static void ConfigureDotnetEnvironment(ProcessStartInfo start)
    {
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        start.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        start.Environment["NUGET_XMLDOC_MODE"] = "skip";
    }

    private static string CreateTemporaryWorkspace()
    {
        string workspace = Path.Combine(Path.GetTempPath(), "clros-m01-009-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        return workspace;
    }

    private static void DeleteTemporaryWorkspace(string workspace)
    {
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullWorkspace = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? parent = Directory.GetParent(fullWorkspace)?.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string leaf = Path.GetFileName(fullWorkspace);
        if (!string.Equals(parent, tempRoot, StringComparison.OrdinalIgnoreCase) || !Regex.IsMatch(leaf, @"\Aclros-m01-009-[0-9a-f]{32}\z", RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException("Refusing to delete an unexpected M01-009 workspace.");
        }

        if (!Directory.Exists(fullWorkspace))
        {
            return;
        }

        FileAttributes attributes = File.GetAttributes(fullWorkspace);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Refusing to delete a reparse-point M01-009 workspace.");
        }

        Directory.Delete(fullWorkspace, recursive: true);
    }

    private static string RemoveComments(string source)
    {
        string withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline | RegexOptions.CultureInvariant);
        return Regex.Replace(withoutBlocks, @"//.*?$", string.Empty, RegexOptions.Multiline | RegexOptions.CultureInvariant);
    }

    private static string RemoveHostOnlyBlocks(string source)
    {
        return Regex.Replace(source, @"(?ms)^\s*#if\s+!NOSTDLIB_EFI\s*$.*?^\s*#endif\s*$", string.Empty, RegexOptions.CultureInvariant);
    }

    private static bool SourceFailure(string reason)
    {
        Console.WriteLine("M01_009_SOURCE_DIAGNOSTIC " + reason);
        return false;
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int start = 0;
        while ((start = haystack.IndexOf(needle, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += needle.Length;
        }

        return count;
    }

    private static bool HasTextExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) || extension.Equals(".md", StringComparison.OrdinalIgnoreCase) || extension.Equals(".json", StringComparison.OrdinalIgnoreCase) || extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) || extension.Equals(".props", StringComparison.OrdinalIgnoreCase) || extension.Equals(".targets", StringComparison.OrdinalIgnoreCase) || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGeneratedOrReferencePath(string path)
    {
        string normalized = Path.GetFullPath(path).Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/artifacts/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/references/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/done/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/.git/", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string NormalizeToolOutput(string output)
    {
        string normalized = Regex.Replace(output, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
        return normalized.Length <= 1500 ? normalized : normalized[..1500] + "...";
    }

    private static UTF8Encoding Utf8NoBom { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}
