using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using Heap = OS.Kernel.Runtime.ManagedHeapPageRangeLiveAllocator;
using Status = OS.Kernel.Runtime.ManagedHeapPageRangeStatus;

internal static unsafe class ManagedHeapPageRangeLiveAllocatorTests
{
    [HostFixture(HostLane.Native)]
    internal static void Run()
    {
        HostCheck.Run("m04.006", () =>
        {
            CheckOriginalAuthorityAndLaterAllocation();
            CheckOffsetAndContentObservations();
            CheckExactCapacityAndPhysicalBoundaries();
            CheckStorageRejections();
            CheckParentAndAllocatorRejections();
            CheckRetainedRejections();
            CheckPodAndAllocation();
            CheckNoStdLibCompilation();
            Console.WriteLine("CLROS_M04_006_HOST_READY pages=1 bytes=4096 retained=true"
                + " hostConformance=true guestIntegrated=false objectAllocation=false gc=false"
                + " hardwareNxProved=false postExitBootServices=false gate4=Fail");
        });
    }

    private static void CheckOriginalAuthorityAndLaterAllocation()
    {
        using Fixture f = new();
        f.Input.NodeCapacity = 1; // The actual M01 authority has exactly one slot.
        byte[] original = f.Snapshot();
        ManagedHeapPageRangeResult first = Reserve(f.Input);
        Require(first.IsReady && first.ReadBytes == 4096 && first.ContentsAreZeroNow, "initial actual readback");
        Require(first.PhysicalStart == 0x90000000 && first.PhysicalEndExclusive == 0x90001000 &&
            first.MappedStart == (ulong)f.Input.Backing && first.PhysicalStart != first.MappedStart,
            "literal synthetic physical range and distinct real backing");
        CheckAccounting(first.InitialAllocatorState, 2, 8192, 0, 0);
        CheckAccounting(first.CurrentAllocatorState, 1, 4096, 1, 4096);
        CheckAccounting(*f.Input.Allocator, 1, 4096, 1, 4096);
        Require(f.Input.Nodes[0].PhysicalStart == 0x90001000 &&
            f.Input.Nodes[0].PhysicalEndExclusive == 0x90002000 &&
            f.Input.Nodes[0].PageCount == 1 && f.Input.Nodes[0].NextNodeIndex == -1,
            "original node was actually mutated");
        Require(f.Input.Heap->SelfAddress == (ulong)f.Input.Heap &&
            f.Input.Heap->AllocatorAddress == (ulong)f.Input.Allocator &&
            f.Input.Heap->NodesAddress == (ulong)f.Input.Nodes &&
            f.Input.Heap->LedgerAddress == (ulong)f.Input.Ledger && f.Input.Heap->ParentIndex == 1 &&
            f.Input.Heap->BackingAddress == (ulong)f.Input.Backing, "stable original identities");
        CheckPage(f.Input.Backing, 0);
        f.AssertOnlyChanged(original, (0, 56), (0x100, 48), (0x2000, (int)Heap.RequiredStateBytes), (0x4000, 4096));
        CheckReadOnlyRetention(f, true);

        BootPageAllocatorOnePageResult later = BootPageAllocatorMutator.AllocateOnePage(
            *f.Input.Allocator, f.Input.Nodes, 1, 1, null, 0, 0, 1, 4096);
        Require(later.Succeeded, "later actual allocator call");
        later.PhysicalStart.TryGetRawValueForUefiBoundary(out ulong laterStart);
        Require(laterStart == 0x90001000 && laterStart != first.PhysicalStart, "later page independent address");
        *f.Input.Allocator = later.AfterState;
        Require(f.Input.Allocator->HeadNodeIndex == -1 && f.Input.Allocator->FreeNodeCount == 0,
            "valid exhausted topology");
        CheckAccounting(*f.Input.Allocator, 0, 0, 2, 8192);
        CheckReadOnlyRetention(f, true);

        BootPageAllocatorOnePageFreeResult freed = BootPageAllocatorMutator.FreeOnePage(
            *f.Input.Allocator, f.Input.Nodes, 1, 1, null, 0, 0, later);
        Require(freed.Succeeded, "strict LIFO releases only later allocation");
        *f.Input.Allocator = freed.AfterState;
        CheckAccounting(*f.Input.Allocator, 1, 4096, 1, 4096);
        CheckReadOnlyRetention(f, true);
        CheckPage(f.Input.Backing, 0);
        Require(f.Input.Heap->PhysicalStart == 0x90000000, "heap remains reserved after later free");
    }

    private static void CheckOffsetAndContentObservations()
    {
        using Fixture f = new();
        // First parent page is already unavailable/accounted; allocate its second
        // page and prove that the physical offset is applied to mapped backing.
        f.Input.Nodes[0] = Node(0x90001000, 1);
        *f.Input.Allocator = State(1, 1);
        byte[] before = f.Snapshot();
        ManagedHeapPageRangeResult result = Reserve(f.Input);
        Require(result.IsReady && result.PhysicalStart == 0x90001000 &&
            result.PhysicalEndExclusive == 0x90002000 && result.MappedStart == (ulong)(f.Input.Backing + 4096),
            "nonzero backing offset");
        CheckAccounting(*f.Input.Allocator, 0, 0, 2, 8192);
        CheckPage(f.Input.Backing, 0xA5);
        CheckPage(f.Input.Backing + 4096, 0);
        f.AssertOnlyChanged(before, (0, 56), (0x100, 48), (0x2000, (int)Heap.RequiredStateBytes), (0x5000, 4096));

        // Every byte position must be read, including the final byte; do not stop
        // early or infer zero from the initializer's previous store loop.
        for (int i = 0; i < 4096; i++)
        {
            f.Input.Backing[4096 + i] = 0x5A;
            result = Retained(f.Input);
            Require(result.IsReady && result.ReadBytes == 4096 && !result.ContentsAreZeroNow,
                "nonzero retained byte at " + i);
            f.Input.Backing[4096 + i] = 0;
        }
        f.Input.Backing[8191] = 0xE7;
        ExpectUnchanged(f, f.Input, Status.AlreadyInitialized);
        Require(f.Input.Backing[8191] == 0xE7, "repeat must not clear changed heap byte");
        CheckReadOnlyRetention(f, false);

        f.Reset();
        f.Input.BackingCapacity = 8193; // Protect the supplied tail, not only two pages.
        before = f.Snapshot();
        Require(Reserve(f.Input).IsReady, "extra backing capacity boundary");
        f.AssertOnlyChanged(before, (0, 56), (0x100, 48), (0x2000, (int)Heap.RequiredStateBytes), (0x4000, 4096));

        f.Reset();
        // Exact touching spans are legal. The ledger ends exactly at the heap slot.
        f.Input.Ledger = (BootReservedRange*)((byte*)f.Input.Heap - 80);
        f.Input.ReservationCapacity = 2;
        f.Input.Ledger[0] = Range(BootReservationKind.LoadedImage, 0x100000, 1);
        f.Input.Ledger[1] = Range(BootReservationKind.AllocatorArena, 0x90000000, 2);
        Require(Reserve(f.Input).IsReady, "metadata extents may touch without overlap");
    }

    private static void CheckStorageRejections()
    {
        using Fixture f = new();
        for (int scenario = 0; scenario < 36; scenario++)
        {
            f.Reset();
            Input input = f.Input;
            Status expected = Status.InvalidStorage;
            switch (scenario)
            {
                case 0: input.Allocator = null; break;
                case 1: input.Nodes = null; break;
                case 2: input.Ledger = null; break;
                case 3: input.Heap = null; break;
                case 4: input.Backing = null; break;
                case 5: input.AllocatorCapacity = 0; break;
                case 6: input.AllocatorCapacity = 2; break;
                case 7: input.NodeCount = 0; break;
                case 8: input.NodeCount = 4097; break;
                case 9: input.NodeCapacity = 0; break;
                case 10: input.NodeCapacity = ulong.MaxValue; break;
                case 11: input.ReservationCount = 0; break;
                case 12: input.ReservationCount = 257; break;
                case 13: input.ReservationCapacity = 1; break;
                case 14: input.ReservationCapacity = ulong.MaxValue; break;
                case 15: input.ParentIndex = 2; break;
                case 16: input.HeapCapacity = Heap.RequiredStateBytes - 1; break;
                case 17: input.BackingCapacity = 4095; break;
                case 18: input.Allocator = (BootPageAllocatorState*)((byte*)input.Allocator + 1); break;
                case 19: input.Nodes = (BootPageFreeListNode*)((byte*)input.Nodes + 1); break;
                case 20: input.Ledger = (BootReservedRange*)((byte*)input.Ledger + 1); break;
                case 21: input.Heap = (ManagedHeapPageRangeState*)((byte*)input.Heap + 1); break;
                case 22: input.Backing++; break;
                case 23: input.Allocator = (BootPageAllocatorState*)0xfffffffffffffff8UL; break;
                case 24: input.Nodes = (BootPageFreeListNode*)0xfffffffffffffff8UL; break;
                case 25: input.Ledger = (BootReservedRange*)0xfffffffffffffff8UL; break;
                case 26: input.Heap = (ManagedHeapPageRangeState*)0xfffffffffffffff8UL; break;
                case 27: input.Backing = (byte*)0xfffffffffffff000UL; break;
                case 28: input.BackingCapacity = ulong.MaxValue; break;
                case 29: input.HeapCapacity = ulong.MaxValue; break;
                case 30: input.Nodes = (BootPageFreeListNode*)input.Allocator; expected = Status.StorageOverlap; break;
                case 31: input.Heap = (ManagedHeapPageRangeState*)input.Ledger; expected = Status.StorageOverlap; break;
                case 32: input.Ledger = (BootReservedRange*)(input.Backing + 4096); expected = Status.StorageOverlap; break;
                case 33: input.Heap = (ManagedHeapPageRangeState*)input.Backing; expected = Status.StorageOverlap; break;
                case 34: input.NodeCapacity = 100; expected = Status.StorageOverlap; break;
                case 35: input.HeapCapacity = 0x2001; expected = Status.StorageOverlap; break;
            }
            ExpectUnchanged(f, input, expected, reason: "storage case " + scenario);
            ExpectUnchanged(f, input, expected, retained: true, reason: "retained storage case " + scenario);
        }
        // Every pair of the five supplied storage spans must be disjoint.
        for (int left = 0; left < 5; left++)
        for (int right = left + 1; right < 5; right++)
        {
            f.Reset();
            Input input = f.Input;
            // All addresses are aligned to 4096 for this overlap-only fixture.
            ulong shared = (ulong)f.Input.Backing;
            SetAddress(ref input, left, shared);
            SetAddress(ref input, right, shared);
            ExpectUnchanged(f, input, Status.StorageOverlap, reason: "pairwise metadata/backing alias");
        }
        f.Reset();
        ((byte*)f.Input.Heap)[Heap.RequiredStateBytes - 1] = 1;
        ExpectUnchanged(f, f.Input, Status.AlreadyInitialized, reason: "nondefault slot tail");
    }

    private static void CheckExactCapacityAndPhysicalBoundaries()
    {
        using Fixture f = new();
        f.Input.NodeCapacity = 1;
        f.Input.ReservationCapacity = 2;
        f.Input.BackingCapacity = 4096;
        f.Input.Ledger[1] = Range(BootReservationKind.AllocatorArena, 0x90000000, 1);
        f.Input.Nodes[0] = Node(0x90000000, 1);
        *f.Input.Allocator = State(1, 0);
        Require(Reserve(f.Input).IsReady, "exact one-page backing and metadata capacities");
        CheckAccounting(*f.Input.Allocator, 0, 0, 1, 4096);
        CheckReadOnlyRetention(f, true);

        f.Reset();
        // Physical identity need not fit a host virtual address. This range ends
        // at the final representable page boundary without wrapping.
        f.Input.Ledger[1] = Range(BootReservationKind.AllocatorArena, 0xffffffffffffd000UL, 2);
        f.Input.Nodes[0] = Node(0xffffffffffffd000UL, 2);
        ManagedHeapPageRangeResult result = Reserve(f.Input);
        Require(result.IsReady && result.PhysicalStart == 0xffffffffffffd000UL &&
            result.PhysicalEndExclusive == 0xffffffffffffe000UL && result.MappedStart == (ulong)f.Input.Backing,
            "high synthetic physical address uses mapped offset without signed truncation");
        CheckReadOnlyRetention(f, true);

        for (int scenario = 0; scenario < 5; scenario++)
        {
            f.Reset();
            switch (scenario)
            {
                case 0: f.Input.Nodes[0] = new(0, 0, 0x90000001, 0x90002001, 2, 8192, -1); break;
                case 1: f.Input.Nodes[0] = new(0, 0, 0x90000000, 0x90000000, 0, 0, -1); break;
                case 2: f.Input.Nodes[0] = new(0, -1, 0x90000000, 0x90002000, 2, 8192, -1); break;
                case 3: f.Input.Nodes[0] = new(0, 0, 0x90000000, 0x90002000, ulong.MaxValue, 8192, -1); break;
                case 4: f.Input.Nodes[0] = Node(0x90000000, 1); break;
            }
            ExpectUnchanged(f, f.Input, Status.InvalidAllocator, reason: "node boundary " + scenario);
        }
        f.Reset();
        f.Input.BackingCapacity = 8191;
        ExpectUnchanged(f, f.Input, Status.InvalidParent, reason: "one byte short of whole parent backing");
    }

    private static void CheckParentAndAllocatorRejections()
    {
        using Fixture f = new();
        for (int scenario = 0; scenario < 22; scenario++)
        {
            f.Reset();
            Input input = f.Input;
            Status expected = Status.InvalidAllocator;
            switch (scenario)
            {
                case 0: input.ParentIndex = 0; expected = Status.InvalidParent; break;
                case 1: input.BackingCapacity = 4096; expected = Status.InvalidParent; break;
                case 2: input.Ledger[1] = default; expected = Status.InvalidReservationTable; break;
                case 3: input.Ledger[0] = input.Ledger[1]; expected = Status.InvalidReservationTable; break;
                case 4: input.Ledger[0] = Range(BootReservationKind.LoadedImage, 0x90001000, 1);
                    expected = Status.InvalidReservationTable; break;
                case 5: ((ulong*)(input.Ledger + 1))[1] = ulong.MaxValue; expected = Status.InvalidReservationTable; break;
                case 6: ((ulong*)(input.Ledger + 1))[3] = ulong.MaxValue; expected = Status.InvalidReservationTable; break;
                case 7: *input.Allocator = default; break;
                case 8: *input.Allocator = new(true, 8192, 0, 1, 2, 8192, 0, 0); break;
                case 9: *input.Allocator = new(true, 4096, 0, -1, 2, 8192, 0, 0); break;
                case 10: *input.Allocator = new(true, 4096, 0, 2, 2, 8192, 0, 0); break;
                case 11: *input.Allocator = new(true, 4096, 0, 1, ulong.MaxValue, 8192, 0, 0); break;
                case 12: *input.Allocator = new(true, 4096, 0, 1, 2, 4096, 0, 0); break;
                case 13: *input.Allocator = new(true, 4096, 0, 1, 2, 8192, 1, 4096); break;
                case 14: *input.Allocator = new(true, 4096, -2, 1, 2, 8192, 0, 0); break;
                case 15: *input.Allocator = new(true, 4096, 1, 1, 2, 8192, 0, 0); break;
                case 16: input.Nodes[0] = Node(0x8FFFF000, 2); break;
                case 17: input.Nodes[0] = Node(0x90001000, 2); break;
                case 18: input.Nodes[0] = new(-1, 0, 0x90000000, 0x90002000, 2, 8192, -1); break;
                case 19: input.Nodes[0] = Node(0x90000000, 2, 0); break;
                case 20: input.Nodes[0] = new(0, 0, 0x90000000, 0x90002000, 2, 4096, -1); break;
                case 21: input.Nodes[0] = Node(0x90000000, 2, 2); break;
            }
            ExpectUnchanged(f, input, expected, reason: "parent/state case " + scenario);
        }
        f.Reset();
        *f.Input.Allocator = State(0, 2);
        f.Input.Nodes[0] = default;
        byte[] exhausted = f.Snapshot();
        ManagedHeapPageRangeResult failure = Reserve(f.Input);
        Require(failure.Status == Status.AllocationRejected &&
            failure.AllocationStatus == BootPageAllocatorMutationStatus.NoSuitableRange && !failure.IsReady,
            "actual mutator rejects exhaustion");
        f.AssertUnchanged(exhausted);

        f.Reset();
        // Two disjoint fragments within one four-page parent; preexisting gaps
        // are unavailable and already represented in allocated accounting.
        f.Input.NodeCount = 2;
        f.Input.Ledger[1] = Range(BootReservationKind.AllocatorArena, 0x90000000, 4);
        f.Input.BackingCapacity = 16384;
        *f.Input.Allocator = new(true, 4096, 0, 2, 2, 8192, 2, 8192);
        f.Input.Nodes[0] = Node(0x90000000, 1, 1);
        f.Input.Nodes[1] = Node(0x90002000, 1);
        Require(Reserve(f.Input).IsReady, "bounded fragmented parent");
        CheckReadOnlyRetention(f, true);
        f.Input.Nodes[1] = Node(0x90000000, 1); // Retained page reintroduced as free.
        ExpectUnchanged(f, f.Input, Status.RetainedRangeIsFree, retained: true);

        f.Reset();
        f.Input.NodeCount = 2;
        *f.Input.Allocator = new(true, 4096, 0, 2, 2, 8192, 0, 0);
        f.Input.Nodes[0] = Node(0x90000000, 1, 1);
        f.Input.Nodes[1] = Node(0x90001000, 1, 0);
        ExpectUnchanged(f, f.Input, Status.InvalidAllocator, reason: "bounded two-node cycle");
        f.Input.Nodes[1] = Node(0x90000000, 1);
        ExpectUnchanged(f, f.Input, Status.InvalidAllocator, reason: "overlapping sorted extents");
        f.Input.Nodes[0] = Node(0x90001000, 1, 1);
        ExpectUnchanged(f, f.Input, Status.InvalidAllocator, reason: "descending free extents");
    }

    private static void CheckRetainedRejections()
    {
        using Fixture f = new();
        Require(!default(ManagedHeapPageRangeResult).Succeeded && !default(ManagedHeapPageRangeResult).IsReady,
            "default result never ready");
        ExpectUnchanged(f, f.Input, Status.NotReady, retained: true);
        for (int scenario = 0; scenario < 10; scenario++)
        {
            f.Reset();
            Require(Reserve(f.Input).IsReady, "retained fixture initialized");
            Input input = f.Input;
            switch (scenario)
            {
                case 0: input.Heap = (ManagedHeapPageRangeState*)(f.Storage + 0x2800);
                    *input.Heap = *f.Input.Heap; break;
                case 1: input.Allocator = (BootPageAllocatorState*)(f.Storage + 0x800);
                    *input.Allocator = *f.Input.Allocator; break;
                case 2: input.Nodes = (BootPageFreeListNode*)(f.Storage + 0x900);
                    *input.Nodes = *f.Input.Nodes; break;
                case 3: input.Ledger = (BootReservedRange*)(f.Storage + 0x1800);
                    input.Ledger[0] = f.Input.Ledger[0]; input.Ledger[1] = f.Input.Ledger[1]; break;
                case 4: input.Backing += 4096; break;
                case 5: input.BackingCapacity++; break;
                case 6: input.HeapCapacity++; break;
                case 7: input.NodeCapacity++; break;
                case 8: input.ReservationCapacity++; break;
                case 9: input.Ledger[1] = Range(BootReservationKind.AllocatorArena, 0x90001000, 2); break;
            }
            ExpectUnchanged(f, input, Status.IdentityMismatch, retained: true, reason: "identity case " + scenario);
        }
        f.Reset();
        Require(Reserve(f.Input).IsReady, "retention fixture");
        f.Input.Nodes[0] = Node(0x90000000, 1);
        ExpectUnchanged(f, f.Input, Status.RetainedRangeIsFree, retained: true);
        f.Input.Nodes[0] = Node(0x90001000, 1);
        *f.Input.Allocator = State(2, 0);
        ExpectUnchanged(f, f.Input, Status.InvalidAllocator, retained: true, reason: "stale preallocation accounting");

        f.Reset();
        Require(Reserve(f.Input).IsReady, "reserved-state fixture");
        // Controlled lifecycle corruption represents an already-reserved nonready
        // slot. It does not pretend to inject an actual failed memory write.
        Marshal.WriteByte((nint)f.Input.Heap, Marshal.OffsetOf<ManagedHeapPageRangeState>("LifecycleValue").ToInt32(), 1);
        byte[] before = f.Snapshot();
        ManagedHeapPageRangeResult nonready = Retained(f.Input);
        Require(nonready.Status == Status.NotReady && !nonready.IsReady && nonready.ReadBytes == 0 &&
            nonready.Lifecycle == ManagedHeapPageRangeLifecycle.ReservedNotReady &&
            nonready.PhysicalStart == 0x90000000, "explicit reserved ownership cannot become ready");
        f.AssertUnchanged(before);
        ExpectUnchanged(f, f.Input, Status.AlreadyInitialized);
    }

    private static void CheckPodAndAllocation()
    {
        Require(!RuntimeHelpers.IsReferenceOrContainsReferences<ManagedHeapPageRangeState>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<ManagedHeapPageRangeResult>(), "POD storage/results");
        Require(Heap.RequiredStateBytes == (ulong)sizeof(ManagedHeapPageRangeState) &&
            sizeof(BootPageAllocatorState) == 56 && sizeof(BootPageFreeListNode) == 48, "actual POD sizes");
        using Fixture f = new();
        bool passed = true;
        for (int i = 0; i < 1000; i++) passed &= AllocationIteration(f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) passed &= AllocationIteration(f);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(passed && allocated == 0, "zero managed bytes across warmed reserve/retain/reject calls");
    }

    private static bool AllocationIteration(Fixture f)
    {
        f.Reset();
        ManagedHeapPageRangeResult initial = Reserve(f.Input);
        ManagedHeapPageRangeResult retained = Retained(f.Input);
        ManagedHeapPageRangeResult duplicate = Reserve(f.Input);
        return initial.IsReady && retained.IsReady && retained.ContentsAreZeroNow &&
            duplicate.Status == Status.AlreadyInitialized;
    }

    private static void CheckReadOnlyRetention(Fixture f, bool zero)
    {
        byte[] snapshot = f.Snapshot();
        ManagedHeapPageRangeResult result = Retained(f.Input);
        Require(result.IsReady && result.ReadBytes == 4096 && result.ContentsAreZeroNow == zero,
            "retention and content are separate observations");
        f.AssertUnchanged(snapshot);
    }

    private static void ExpectUnchanged(Fixture f, Input input, Status expected, bool retained = false,
        string reason = "atomic rejection")
    {
        byte[] snapshot = f.Snapshot();
        ManagedHeapPageRangeResult result = retained ? Retained(input) : Reserve(input);
        Require(result.Status == expected && !result.Succeeded && !result.IsReady && !result.ContentsAreZeroNow,
            reason + ": expected " + expected + ", actual " + result.Status);
        f.AssertUnchanged(snapshot);
    }

    private static ManagedHeapPageRangeResult Reserve(Input x) => Heap.ReserveAndInitializeOnce(
        x.Allocator, x.AllocatorCapacity, x.Nodes, x.NodeCount, x.NodeCapacity,
        x.Ledger, x.ReservationCount, x.ReservationCapacity, x.ParentIndex,
        x.Backing, x.BackingCapacity, x.Heap, x.HeapCapacity);

    private static ManagedHeapPageRangeResult Retained(Input x) => Heap.ValidateRetained(
        x.Allocator, x.AllocatorCapacity, x.Nodes, x.NodeCount, x.NodeCapacity,
        x.Ledger, x.ReservationCount, x.ReservationCapacity, x.ParentIndex,
        x.Backing, x.BackingCapacity, x.Heap, x.HeapCapacity);

    private static void SetAddress(ref Input x, int index, ulong address)
    {
        switch (index)
        {
            case 0: x.Allocator = (BootPageAllocatorState*)address; break;
            case 1: x.Nodes = (BootPageFreeListNode*)address; break;
            case 2: x.Ledger = (BootReservedRange*)address; break;
            case 3: x.Heap = (ManagedHeapPageRangeState*)address; break;
            case 4: x.Backing = (byte*)address; break;
        }
    }

    private struct Input
    {
        public BootPageAllocatorState* Allocator;
        public ulong AllocatorCapacity;
        public BootPageFreeListNode* Nodes;
        public ulong NodeCount, NodeCapacity;
        public BootReservedRange* Ledger;
        public ulong ReservationCount, ReservationCapacity, ParentIndex;
        public byte* Backing;
        public ulong BackingCapacity;
        public ManagedHeapPageRangeState* Heap;
        public ulong HeapCapacity;
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly byte* Storage = (byte*)NativeMemory.AlignedAlloc(65536, 4096);
        internal Input Input;
        internal Fixture() { Require(Storage != null, "host buffer allocation"); Reset(); }
        internal void Reset()
        {
            new Span<byte>(Storage, 65536).Fill(0xA5);
            Input = new Input
            {
                Allocator = (BootPageAllocatorState*)Storage, AllocatorCapacity = 1,
                Nodes = (BootPageFreeListNode*)(Storage + 0x100), NodeCount = 1, NodeCapacity = 2,
                Ledger = (BootReservedRange*)(Storage + 0x1000), ReservationCount = 2, ReservationCapacity = 3,
                ParentIndex = 1, Backing = Storage + 0x4000, BackingCapacity = 8192,
                Heap = (ManagedHeapPageRangeState*)(Storage + 0x2000), HeapCapacity = Heap.RequiredStateBytes
            };
            *Input.Allocator = State(2, 0);
            Input.Nodes[0] = Node(0x90000000, 2);
            Input.Nodes[1] = default;
            Input.Ledger[0] = Range(BootReservationKind.LoadedImage, 0x100000, 1);
            Input.Ledger[1] = Range(BootReservationKind.AllocatorArena, 0x90000000, 2);
            new Span<byte>(Input.Heap, (int)Input.HeapCapacity).Clear();
        }
        internal byte[] Snapshot() => new ReadOnlySpan<byte>(Storage, 65536).ToArray();
        internal void AssertUnchanged(byte[] before) => Require(
            new ReadOnlySpan<byte>(Storage, 65536).SequenceEqual(before), "complete caller storage preserved");
        internal void AssertOnlyChanged(byte[] before, params (int Start, int Length)[] changed)
        {
            for (int i = 0; i < before.Length; i++)
            {
                bool allowed = false;
                foreach (var range in changed) allowed |= i >= range.Start && i < range.Start + range.Length;
                if (!allowed) Require(Storage[i] == before[i], "outside byte changed at " + i);
            }
        }
        public void Dispose() => NativeMemory.AlignedFree(Storage);
    }

    private static BootPageAllocatorState State(ulong free, ulong allocated) =>
        new(true, 4096, free == 0 ? -1 : 0, free == 0 ? 0 : 1, free, free * 4096, allocated, allocated * 4096);
    private static BootPageFreeListNode Node(ulong start, ulong pages, int next = -1) =>
        new(0, 0, start, start + pages * 4096, pages, pages * 4096, next);
    private static BootReservedRange Range(BootReservationKind kind, ulong start, ulong pages)
    {
        PhysicalAddress.TryCreateFromUefiBoundary(start, out PhysicalAddress address);
        Require(BootReservedRange.TryCreate(kind, address, pages, out BootReservedRange range) ==
            BootReservedRangeStatus.Success, "fixture range");
        return range;
    }
    private static void CheckAccounting(BootPageAllocatorState state, ulong freePages, ulong freeBytes,
        ulong allocatedPages, ulong allocatedBytes) => Require(state.IsInitialized && state.PageSize == 4096 &&
            state.FreePageCount == freePages && state.FreeByteCount == freeBytes &&
            state.AllocatedPageCount == allocatedPages && state.AllocatedByteCount == allocatedBytes,
            "literal original-authority accounting");
    private static void CheckPage(byte* bytes, byte expected)
    {
        for (int i = 0; i < 4096; i++) Require(bytes[i] == expected, "actual page byte " + i);
    }

    private static void CheckNoStdLibCompilation()
    {
        string root = FindRoot();
        string directory = Path.Combine(root, "artifacts", "m04-006", "nostdlib", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(root, "global.json"), Path.Combine(directory, "global.json"));
        string[] sources =
        [
            "src/OS/Runtime/MinimalRuntime.cs", "src/OS/Runtime/MethodImplAttribute.cs",
            "src/OS.Kernel/Boot/TypedPhysicalVirtualDeviceAddressPODWrappers.cs",
            "src/OS.Kernel/Memory/BootReservedRange.cs", "src/OS.Kernel/Memory/BootReservationTableValidator.cs",
            "src/OS.Kernel/Memory/BootPageAllocatorCoreContracts.cs", "src/OS.Kernel/Memory/BootPageAllocatorMutation.cs",
            "src/OS.Kernel/Runtime/ManagedHeapPageRangeLiveAllocator.cs"
        ];
        string includes = string.Join(Environment.NewLine, sources.Select(p =>
            "<Compile Include=\"" + System.Security.SecurityElement.Escape(Path.Combine(root, p)) + "\" />"));
        File.WriteAllText(Path.Combine(directory, "Compile.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion>
                <OutputType>Library</OutputType><AssemblyName>CLROS.M04.Heap.NoStdLib</AssemblyName>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                <NoStdLib>true</NoStdLib><DefineConstants>NOSTDLIB_EFI</DefineConstants><NoConfig>true</NoConfig>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo><GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles><RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                <ProduceReferenceAssembly>false</ProduceReferenceAssembly><EnableNETAnalyzers>false</EnableNETAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <DebugType>none</DebugType><RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
                <UseSharedCompilation>false</UseSharedCompilation>
              </PropertyGroup>
              <ItemGroup>{{includes}}</ItemGroup>
              <Target Name="RemoveFrameworkReferences" BeforeTargets="BeforeCompile" AfterTargets="FindReferenceAssembliesForReferences">
                <ItemGroup><ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
                  <ReferencePath Remove="@(ReferencePath)" /></ItemGroup>
              </Target>
            </Project>
            """, new UTF8Encoding(false));
        var identities = sources.Select(p => new { Path = p, Sha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, p)))) });
        File.WriteAllText(Path.Combine(directory, "source-identities.json"),
            System.Text.Json.JsonSerializer.Serialize(identities), new UTF8Encoding(false));
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string arg in new[] { "build", "Compile.csproj", "--nologo", "-m:1", "-c", "Release", "-nodeReuse:false" })
            start.ArgumentList.Add(arg);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("no-stdlib compile start");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90000);
        if (!exited) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
        Task.WaitAll(stdout, stderr);
        File.WriteAllText(Path.Combine(directory, "build.stdout.log"), stdout.Result);
        File.WriteAllText(Path.Combine(directory, "build.stderr.log"), stderr.Result);
        Require(exited && process.ExitCode == 0, "actual-source no-stdlib compile: " + directory + " " + stdout.Result + stderr.Result);
        using var stream = File.OpenRead(Path.Combine(directory, "bin", "Release", "net10.0", "CLROS.M04.Heap.NoStdLib.dll"));
        using PEReader image = new(stream);
        MetadataReader metadata = image.GetMetadataReader();
        Require(metadata.AssemblyReferences.Count == 0, "no-stdlib zero assembly references");
        MethodDefinition reader = metadata.MethodDefinitions.Select(metadata.GetMethodDefinition)
            .Single(m => metadata.GetString(m.Name) == "ReadFixedPage");
        Require((reader.ImplAttributes & MethodImplAttributes.NoInlining) != 0 &&
            image.GetMethodBody(reader.RelativeVirtualAddress).GetILBytes()!.Length > 0,
            "actual readback method body retains NoInlining with existing shim");
        Console.WriteLine("M04_HEAP_HOST_NOSTDLIB inputs=8 assemblyReferences=0 readbackNoInlining=true");
    }

    private static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CLROS.slnx"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("CLROS root not found");
    }
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
