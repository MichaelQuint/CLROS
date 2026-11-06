using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using Arrays = OS.Kernel.Runtime.GuestTestArrayAllocationLengthOverflowChecks;
using Fixed = OS.Kernel.Runtime.GuestTestRhpNewFast;
using Heap = OS.Kernel.Runtime.ManagedHeapPageRangeLiveAllocator;
using Status = OS.Kernel.Runtime.RhpNewFastStatus;

internal static unsafe partial class GuestTestArrayAllocationLengthOverflowChecksTests
{
    private const ulong Token = 0x1234567800000008;
    private const ulong Token2 = 0xABCD987600000018;
    private const NativeAotLayoutProfile Profile = NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian;
    [HostFixture(HostLane.Native)]
    internal static void Run()
    {
        Console.WriteLine("TEST_START m04.008");
        try
        {
            Group("contract", CheckContract);
            Group("bytes", CheckBytes);
            Group("mixed", CheckMixed);
            Group("domain", CheckDomain);
            Group("lengths", CheckLengths);
            Group("storage", CheckStorage);
            Group("state_authority", CheckStateAndAuthority);
            Group("zero_allocation", CheckZeroAllocation);
            Group("nostdlib", CheckNoStdLibCompilation);
            Console.WriteLine("CLROS_M04_008_HOST_READY pageBytes=4096 stateBytes=80 componentWidths=1,2,4,8" + " sharedFixedCursor=true hostConformance=true guestIntegrated=false compiledArray=false" + " runtimeExport=false referenceStores=false gc=false gate4=Fail");
            Console.WriteLine("TEST_PASS m04.008");
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL m04.008 " + error.GetType().Name + ": " + error.Message);
            throw;
        }
    }

    private static void Group(string name, Action test)
    {
        HostCheck.Run("m04.008." + name, test);
    }

    private static void CheckContract()
    {
        Require(sizeof(RhpNewFastHeapAccess) == 104 && sizeof(RhpNewFastBumpState) == 80 && sizeof(RhpNewFastResult) == 88 && Fixed.RequiredStateBytes == 80, "shared POD sizes");
        Require(!RuntimeHelpers.IsReferenceOrContainsReferences<RhpNewFastHeapAccess>() && !RuntimeHelpers.IsReferenceOrContainsReferences<RhpNewFastBumpState>() && !RuntimeHelpers.IsReferenceOrContainsReferences<RhpNewFastResult>(), "no managed references");
        Require((byte)Status.UnsupportedArrayType == 16 && (byte)Status.InvalidComponentSize == 17 && (byte)Status.InvalidLength == 18 && !default(RhpNewFastResult).Succeeded, "append-only/default contract");
        Require(typeof(Arrays).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Select(m => m.Name).SequenceEqual(["TryAllocate"]), "thin wrapper has no binder/reset/free");
        using Fixture f = new();
        Expect(f, Status.NotInitialized);
        Require(Bind(f).Succeeded, "sole fixed binder");
        Expect(f, Status.UnsupportedProfile, profile: 0, token: 0, flags: 0, length: -1);
        foreach (NativeAotLayoutProfile p in new[]
        {
            NativeAotLayoutProfile.Ilc10_0_12WinX64LittleEndian,
            (NativeAotLayoutProfile)255
        }

        )
            Expect(f, Status.UnsupportedProfile, profile: p);
        Expect(f, Status.InvalidMethodTableToken, token: 0, flags: 0, length: -1);
        byte[] snapshot = f.Snapshot();
        Reject(Bind(f), Status.AlreadyInitialized, operation: RhpNewFastOperation.Bind);
        f.Unchanged(snapshot);
    }

    private static void CheckBytes()
    {
        // Independent literal sizes cover each supported width and alignment edge.
        (uint Width, int Length, uint Size)[] vectors = [(1, 0, 24), (1, 1, 32), (1, 8, 32), (1, 9, 40), (2, 0, 24), (2, 1, 32), (2, 3, 32), (2, 5, 40), (4, 0, 24), (4, 1, 32), (4, 3, 40), (8, 0, 24), (8, 1, 32), (8, 3, 48)];
        using Fixture f = new();
        foreach (var vector in vectors)
        {
            f.Reset();
            Require(Bind(f).Succeeded, "bytes bind");
            new Span<byte>(f.Page, 4096).Fill(0xBC);
            byte[] before = f.Snapshot();
            RhpNewFastResult result = Allocate(f, vector.Width, vector.Length);
            CheckResult(f, result, 0, vector.Size, 1, Token);
            CheckArrayBytes(f.Page, vector.Size, Token, vector.Length);
            f.OnlyChanged(before, (0x3000, 80), (f.PageOffset, (int)vector.Size));
            Require(Retained(f.Access).IsReady && !Retained(f.Access).ContentsAreZeroNow, "nonzero allocated bytes do not revoke retained ownership");
        }

        f.Reset();
        Bind(f);
        CheckResult(f, Allocate(f, 8, 2, 0x01000000), 0, 40, 1, Token);
        CheckArrayBytes(f.Page, 40, Token, 2); // Both pointer-sized slots are zero, never references.
        f.Reset(secondPage: true);
        Bind(f);
        RhpNewFastResult second = Allocate(f, 4, 3);
        CheckResult(f, second, 0, 40, 1, Token);
        Require(second.PhysicalAllocationStart == 0x300001000 && f.PageOffset == 0x6000, "actual mapped page offset");
        f.Reset(physical: 0xFFFFFFFFFFFFD000);
        Bind(f);
        second = Allocate(f, 8, 1, token: 0xFFFFFFFFFFFFFFE0);
        Require(second.AllocationSucceeded && second.PhysicalAllocationEndExclusive == 0xFFFFFFFFFFFFD020, "full-width physical/token arithmetic");
        CheckArrayBytes(f.Page, 32, 0xFFFFFFFFFFFFFFE0, 1);
    }

    private static void CheckMixed()
    {
        using Fixture f = new();
        foreach (bool reverse in new[]
        {
            false,
            true
        }

        )
        {
            f.Reset();
            Bind(f);
            // Explicit sizes/order, not the production evaluator as byte oracle.
            uint[] sizes = reverse ? [40, 32, 32, 24] : [24, 32, 32, 40];
            ulong used = 0;
            for (int i = 0; i < 4; i++)
            {
                int operation = reverse ? 3 - i : i;
                byte[] before = f.Snapshot();
                ulong token = Token + (ulong)i * 0x100;
                RhpNewFastResult r = operation switch
                {
                    0 => Fixed.TryAllocate(f.Access, f.State, f.StateCapacity, Profile, Prefix(0x50000000, 24), token),
                    1 => Allocate(f, 1, 1, token: token),
                    2 => Fixed.TryAllocate(f.Access, f.State, f.StateCapacity, Profile, Prefix(0x50000000, 32), token),
                    _ => Allocate(f, 4, 3, token: token)
                };
                CheckResult(f, r, used, sizes[i], (ulong)i + 1, token);
                if (operation == 1 || operation == 3)
                    CheckArrayBytes(f.Page + used, sizes[i], token, operation == 1 ? 1 : 3);
                else
                    CheckFixedBytes(f.Page + used, sizes[i], token);
                f.OnlyChanged(before, (0x3000, 80), (f.PageOffset + (int)used, (int)sizes[i]));
                f.Page[used + (operation == 1 || operation == 3 ? 24UL : 16UL)] = (byte)(0xD0 + i);
                used += sizes[i];
            }

            Require(f.State->UsedBytes == 128 && f.State->AllocationCount == 4, "one authoritative mixed cursor");
            byte[] snapshot = f.Snapshot();
            Reject(Bind(f), Status.AlreadyInitialized, operation: RhpNewFastOperation.Bind);
            f.Unchanged(snapshot);
        }
    }

    private static void CheckDomain()
    {
        using Fixture f = new();
        Bind(f);
        foreach (uint feature in new uint[]
        {
            0x00080000,
            0x00100000,
            0x00800000,
            0x02000000
        }

        )
            Expect(f, Status.UnsupportedArrayType, flags: 0xE0020001 | feature);
        Expect(f, Status.InvalidMethodTableFlags, flags: 0xE0220001, length: -1);
        foreach (uint extra in new uint[]
        {
            0,
            0x00040000,
            0x00400000,
            0x00440000
        }

        )
        {
            foreach (uint width in new uint[]
            {
                1,
                2,
                4,
                8
            }

            )
            {
                f.Reset();
                Bind(f);
                CheckResult(f, Allocate(f, width, 0, extra), 0, 24, 1, Token);
                CheckArrayBytes(f.Page, 24, Token, 0);
            }
        }

        // All element/kind encodings, including MD, string and fixed shapes.
        for (uint element = 0; element < 32; element++)
            for (uint kind = 0; kind < 4; kind++)
            {
                uint flags = 0x80000001 | (element << 26) | (kind << 16);
                if (element == 24 && kind == 2)
                    continue;
                Status expected = element == 20 && kind == 0 ? Status.InvalidBaseSize : Status.UnsupportedArrayType;
                Expect(f, expected, flags: flags);
            }

        Expect(f, Status.InvalidMethodTableFlags, flags: 0x60020001); // SZ without component flag.
        foreach (uint size in new uint[]
        {
            0,
            16,
            22,
            23,
            25,
            32,
            uint.MaxValue
        }

        )
            Expect(f, Status.InvalidBaseSize, size: size, length: -1);
        foreach (uint width in new uint[]
        {
            0,
            3,
            5,
            7,
            9,
            16,
            255,
            256,
            65535
        }

        )
            Expect(f, Status.InvalidComponentSize, flags: 0xE0020000 | width);
        foreach (uint width in new uint[]
        {
            1,
            2,
            4
        }

        )
            Expect(f, Status.InvalidComponentSize, flags: 0xE1020000 | width);
        Expect(f, Status.UnsupportedArrayType, flags: 0x50000000, length: 0);
        Expect(f, Status.InvalidLength, flags: 0x50000000, length: 1); // Evaluator precedence before non-SZ rejection.
        Expect(f, Status.InvalidBaseSize, flags: 0x50000000, size: 23, length: 1);
        Expect(f, Status.UnsupportedArrayType, flags: 0xD0000002, size: 22, length: 0);
        Expect(f, Status.InvalidLength, flags: 0xD0000002, size: 22, length: -1);
        Expect(f, Status.InvalidComponentSize, flags: 0xD0000001, size: 22, length: -1);
        Expect(f, Status.InvalidLength, flags: 0xE0020003, length: -1); // Nonzero unsupported width after evaluator length.
        Expect(f, Status.InvalidComponentSize, flags: 0xE0020000, length: -1); // Zero component first.
        foreach (uint width in new uint[]
        {
            1,
            2,
            4,
            8
        }

        )
        {
            byte[] before = f.Snapshot();
            Reject(Fixed.TryAllocate(f.Access, f.State, f.StateCapacity, Profile, Prefix(0xE0020000 | width), Token), Status.UnsupportedFixedType);
            f.Unchanged(before);
        }
    }

    private static void CheckLengths()
    {
        using Fixture f = new();
        Bind(f);
        foreach (uint width in new uint[]
        {
            1,
            2,
            4,
            8
        }

        )
        {
            foreach (long length in new[]
            {
                long.MinValue,
                int.MinValue,
                -1L,
                0x7FFFFFC8,
                int.MaxValue,
                0x100000000L,
                0x100000001L,
                long.MaxValue
            }

            )
                Expect(f, Status.InvalidLength, flags: 0xE0020000 | width, length: length);
            foreach (int length in new[]
            {
                0x7FFFFFC6,
                0x7FFFFFC7
            }

            )
                Expect(f, Status.Exhausted, flags: 0xE0020000 | width, length: length);
        }

        Expect(f, Status.Exhausted, flags: 0xE0020008, length: 0x40000000);
        foreach ((uint width, int length) in new (uint, int)[]
        {
            (1, 4072),
            (8, 509)
        }

        )
        {
            f.Reset();
            Bind(f);
            Expect(f, Status.Exhausted, flags: 0xE0020000 | width, length: length + 1);
            byte[] before = f.Snapshot();
            CheckResult(f, Allocate(f, width, length), 0, 4096, 1, Token);
            CheckArrayBytes(f.Page, 4096, Token, length);
            f.OnlyChanged(before, (0x3000, 80), (f.PageOffset, 4096));
            Expect(f, Status.Exhausted, length: 0);
        }

        f.Reset();
        Bind(f);
        for (int i = 0; i < 170; i++)
            Require(Allocate(f, 1, 0).AllocationSucceeded, "zero-length consumes24");
        Require(f.State->UsedBytes == 4080 && f.State->AllocationCount == 170, "zero arrays cannot bypass accounting");
        Expect(f, Status.Exhausted, length: 0);
    }

    private static void CheckStorage()
    {
        using Fixture f = new();
        for (int scenario = 0; scenario < 36; scenario++)
        {
            f.Reset();
            Bind(f);
            RhpNewFastHeapAccess a = f.Access;
            RhpNewFastBumpState* state = f.State;
            ulong capacity = f.StateCapacity;
            Status expected = Status.InvalidStorage;
            switch (scenario)
            {
                case 0:
                    state = null;
                    break;
                case 1:
                    state = (RhpNewFastBumpState*)((byte*)state + 1);
                    break;
                case 2:
                    capacity = 79;
                    break;
                case 3:
                    capacity = ulong.MaxValue;
                    break;
                case 4:
                    state = (RhpNewFastBumpState*)0xFFFFFFFFFFFFFFF8;
                    break;
                case 5:
                    a.Allocator = null;
                    break;
                case 6:
                    a.Nodes = null;
                    break;
                case 7:
                    a.Reservations = null;
                    break;
                case 8:
                    a.Backing = null;
                    break;
                case 9:
                    a.Heap = null;
                    break;
                case 10:
                    a.AllocatorCapacity = 0;
                    break;
                case 11:
                    a.AllocatorCapacity = 2;
                    break;
                case 12:
                    a.NodeCount = 0;
                    break;
                case 13:
                    a.NodeCount = 4097;
                    break;
                case 14:
                    a.NodeCapacity = 0;
                    break;
                case 15:
                    a.NodeCapacity = ulong.MaxValue;
                    break;
                case 16:
                    a.ReservationCount = 0;
                    break;
                case 17:
                    a.ReservationCount = 257;
                    break;
                case 18:
                    a.ReservationCapacity = 1;
                    break;
                case 19:
                    a.ReservationCapacity = ulong.MaxValue;
                    break;
                case 20:
                    a.ParentIndex = 2;
                    break;
                case 21:
                    a.HeapCapacity = 215;
                    break;
                case 22:
                    a.BackingCapacity = 4095;
                    break;
                case 23:
                    a.Allocator = (BootPageAllocatorState*)((byte*)a.Allocator + 1);
                    break;
                case 24:
                    a.Nodes = (BootPageFreeListNode*)((byte*)a.Nodes + 1);
                    break;
                case 25:
                    a.Reservations = (BootReservedRange*)((byte*)a.Reservations + 1);
                    break;
                case 26:
                    a.Heap = (ManagedHeapPageRangeState*)((byte*)a.Heap + 1);
                    break;
                case 27:
                    a.Backing++;
                    break;
                case 28:
                    a.HeapCapacity = ulong.MaxValue;
                    break;
                case 29:
                    a.BackingCapacity = ulong.MaxValue;
                    break;
                case 30:
                    a.Nodes = (BootPageFreeListNode*)0xFFFFFFFFFFFFFFD0;
                    break;
                case 31:
                    a.Reservations = (BootReservedRange*)0xFFFFFFFFFFFFFFD8;
                    break;
                case 32:
                    a.Allocator = (BootPageAllocatorState*)0xFFFFFFFFFFFFFFF8;
                    break;
                case 33:
                    a.Heap = (ManagedHeapPageRangeState*)0xFFFFFFFFFFFFFFF8;
                    break;
                case 34:
                    a.Backing = (byte*)0xFFFFFFFFFFFFF000;
                    break;
                case 35:
                    a.Nodes = (BootPageFreeListNode*)a.Reservations;
                    expected = Status.StorageOverlap;
                    break;
            }

            byte[] before = f.Snapshot();
            Reject(Arrays.TryAllocate(a, state, capacity, 0, Prefix(0), 0, -1), expected);
            f.Unchanged(before);
        }

        foreach (int offset in new[]
        {
            0x100,
            0x300,
            0x330,
            0x1000,
            0x1050,
            0x2000,
            0x20D8,
            0x5000,
            0x7000
        }

        )
        {
            f.Reset();
            Bind(f);
            byte[] before = f.Snapshot();
            Reject(Arrays.TryAllocate(f.Access, (RhpNewFastBumpState*)(f.Storage + offset), 96, Profile, Prefix(0xE0020001), Token, 1), Status.StorageOverlap);
            f.Unchanged(before);
        }

        f.Reset();
        Bind(f);
        byte[] snapshot = f.Snapshot();
        Reject(Arrays.TryAllocate(f.Access, f.State, 0x2001, Profile, Prefix(0xE0020001), Token, 1), Status.StorageOverlap);
        f.Unchanged(snapshot);
        foreach (ulong token in new ulong[]
        {
            0,
            1,
            Token + 1,
            Token + 7,
            ulong.MaxValue,
            0xFFFFFFFFFFFFFFE8,
            (ulong)f.Access.Allocator,
            (ulong)f.Access.Nodes + 48,
            (ulong)f.Access.Reservations + 80,
            (ulong)f.Access.Heap + 216,
            (ulong)f.Access.Backing + 8192,
            (ulong)f.State + 80,
            (ulong)f.State - 16
        }

        )
            Expect(f, Status.InvalidMethodTableToken, token: token);
        Require(Allocate(f, 1, 0, token: (ulong)f.State - 24).AllocationSucceeded, "token exact touching span");
        f.Reset();
        f.State = (RhpNewFastBumpState*)(f.Storage + 0x5000 - 80);
        f.StateCapacity = 80;
        new Span<byte>(f.State, 80).Clear();
        Bind(f);
        CheckResult(f, Allocate(f, 1, 1), 0, 32, 1, Token);
    }

    private static void CheckStateAndAuthority()
    {
        using Fixture f = new();
        for (int slot = 0; slot < 10; slot++)
        {
            f.Reset();
            Bind(f);
            ((ulong*)f.State)[slot] ^= 8;
            Status expected = slot < 3 || slot >= 8 ? Status.InvalidState : Status.HeapBindingMismatch;
            Expect(f, expected, heapStatus: expected == Status.HeapBindingMismatch ? ManagedHeapPageRangeStatus.Success : default);
        }

        foreach ((ulong used, ulong count) in new (ulong, ulong)[]
        {
            (4097, 1),
            (8, 1),
            (24, 0),
            (0, 1),
            (24, 2),
            (4096, 171),
            (4096, ulong.MaxValue),
            (ulong.MaxValue, 1),
            (25, 1)
        }

        )
        {
            f.Reset();
            Bind(f);
            ((ulong*)f.State)[8] = used;
            ((ulong*)f.State)[9] = count;
            Expect(f, Status.InvalidState);
        }

        f.Reset();
        Bind(f);
        RhpNewFastBumpState* copy = (RhpNewFastBumpState*)(f.Storage + 0x4000);
        *copy = *f.State;
        byte[] before = f.Snapshot();
        Reject(Arrays.TryAllocate(f.Access, copy, f.StateCapacity, Profile, Prefix(0xE0020001), Token, 1), Status.InvalidState);
        f.Unchanged(before);
        for (int scenario = 0; scenario < 9; scenario++)
        {
            f.Reset();
            Bind(f);
            Allocate(f, 1, 1);
            RhpNewFastHeapAccess a = f.Access;
            switch (scenario)
            {
                case 0:
                    a.Allocator = (BootPageAllocatorState*)(f.Storage + 0x8000);
                    *a.Allocator = *f.Access.Allocator;
                    break;
                case 1:
                    a.Nodes = (BootPageFreeListNode*)(f.Storage + 0x8000);
                    a.Nodes[0] = f.Access.Nodes[0];
                    break;
                case 2:
                    a.Reservations = (BootReservedRange*)(f.Storage + 0x8000);
                    a.Reservations[0] = f.Access.Reservations[0];
                    a.Reservations[1] = f.Access.Reservations[1];
                    break;
                case 3:
                    a.ParentIndex = 0;
                    break;
                case 4:
                    a.Backing = f.Storage + 0x8000;
                    break;
                case 5:
                    a.Heap = (ManagedHeapPageRangeState*)(f.Storage + 0x8000);
                    *a.Heap = *f.Access.Heap;
                    break;
                case 6:
                    a.NodeCapacity = 1;
                    break;
                case 7:
                    a.BackingCapacity = 8192;
                    break;
                case 8:
                    f.Access.Reservations[1] = Range(BootReservationKind.AllocatorArena, 0x300010000, 2);
                    break;
            }

            before = f.Snapshot();
            Reject(Arrays.TryAllocate(a, f.State, f.StateCapacity, 0, Prefix(0), 0, -1), Status.HeapAuthorityRejected, ManagedHeapPageRangeStatus.IdentityMismatch);
            f.Unchanged(before);
        }

        using Fixture other = new();
        f.Reset();
        Bind(f);
        before = f.Snapshot();
        byte[] otherBefore = other.Snapshot();
        Reject(Arrays.TryAllocate(other.Access, f.State, f.StateCapacity, Profile, Prefix(0xE0020001), Token, 1), Status.HeapBindingMismatch, ManagedHeapPageRangeStatus.Success);
        f.Unchanged(before);
        other.Unchanged(otherBefore);
        f.Reset();
        Bind(f);
        Allocate(f, 1, 1);
        f.Page[24] = 0xE7;
        BootPageAllocatorOnePageResult later = BootPageAllocatorMutator.AllocateOnePage(*f.Access.Allocator, f.Access.Nodes, f.Access.NodeCount, f.Access.NodeCapacity, null, 0, 0, 1, 4096);
        Require(later.Succeeded, "later real allocator allocation");
        *f.Access.Allocator = later.AfterState;
        Require(Allocate(f, 8, 1).AllocationSucceeded && f.Page[24] == 0xE7, "retention after free-chain exhaustion");
        BootPageAllocatorOnePageFreeResult freed = BootPageAllocatorMutator.FreeOnePage(*f.Access.Allocator, f.Access.Nodes, f.Access.NodeCount, f.Access.NodeCapacity, null, 0, 0, later);
        Require(freed.Succeeded, "free only later allocation");
        *f.Access.Allocator = freed.AfterState;
        Require(Allocate(f, 4, 3).AllocationSucceeded, "retention after legal later free");
        *f.Access.Allocator = f.Access.Heap->InitialAllocatorState;
        f.Access.Nodes[0] = Node(0x300000000, 2);
        Expect(f, Status.HeapAuthorityRejected, heapStatus: ManagedHeapPageRangeStatus.RetainedRangeIsFree);
        f.Reset();
        Bind(f);
        *f.Access.Heap = default;
        Expect(f, Status.HeapAuthorityRejected, heapStatus: ManagedHeapPageRangeStatus.NotReady);
    }

    private static void CheckZeroAllocation()
    {
        using Fixture f = new();
        bool passed = true;
        for (int i = 0; i < 32; i++)
            Measure(f, ref passed);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++)
            Measure(f, ref passed);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(passed && allocated == 0, "warmed real mixed/rejected methods allocate zero CLR bytes");
    }

    private static void Measure(Fixture f, ref bool passed)
    {
        f.Reset();
        passed &= Bind(f).Succeeded;
        passed &= Fixed.TryAllocate(f.Access, f.State, f.StateCapacity, Profile, Prefix(0x50000000), Token).AllocationSucceeded;
        passed &= Allocate(f, 1, 0).AllocationSucceeded;
        passed &= Allocate(f, 8, 3).AllocationSucceeded;
        passed &= Allocate(f, 1, -1).Status == Status.InvalidLength;
        passed &= Allocate(f, 8, 0x40000000).Status == Status.Exhausted;
    }

    private static NativeAotMethodTablePrefix Prefix(uint flags, uint size = 24) => new(flags, size, 0xFEDCBA9876543211, ushort.MaxValue, ushort.MaxValue, 0xDEADBEEF);
    private static RhpNewFastResult Bind(Fixture f) => Fixed.InitializeOnce(f.Access, f.State, f.StateCapacity);
    private static RhpNewFastResult Allocate(Fixture f, uint width, long length, uint extra = 0, ulong token = Token) => Arrays.TryAllocate(f.Access, f.State, f.StateCapacity, Profile, Prefix(0xE0020000 | width | extra), token, length);
    private static ManagedHeapPageRangeResult Retained(RhpNewFastHeapAccess a) => Heap.ValidateRetained(a.Allocator, a.AllocatorCapacity, a.Nodes, a.NodeCount, a.NodeCapacity, a.Reservations, a.ReservationCount, a.ReservationCapacity, a.ParentIndex, a.Backing, a.BackingCapacity, a.Heap, a.HeapCapacity);
    private static void Expect(Fixture f, Status expected, uint flags = 0xE0020001, uint size = 24, long length = 0, ulong token = Token, NativeAotLayoutProfile profile = Profile, ManagedHeapPageRangeStatus heapStatus = default)
    {
        byte[] before = f.Snapshot();
        Reject(Arrays.TryAllocate(f.Access, f.State, f.StateCapacity, profile, Prefix(flags, size), token, length), expected, heapStatus);
        f.Unchanged(before);
    }

    private static void Reject(RhpNewFastResult r, Status expected, ManagedHeapPageRangeStatus heapStatus = default, RhpNewFastOperation operation = RhpNewFastOperation.Allocation)
    {
        Require(r.Status == expected && !r.Succeeded && !r.AllocationSucceeded && r.Operation == operation && r.HeapStatus == heapStatus, "exact rejection expected=" + expected + " actual=" + r.Status + "/" + r.HeapStatus);
        Require(r.MappedAllocationStart == 0 && r.MappedAllocationEndExclusive == 0 && r.PhysicalAllocationStart == 0 && r.PhysicalAllocationEndExclusive == 0 && r.ObjectReference == 0 && r.MethodTableToken == 0 && r.BytesConsumed == 0 && r.UsedBefore == 0 && r.UsedAfter == 0 && r.AllocationCount == 0, "no successful rejection payload");
    }

    private static void CheckResult(Fixture f, RhpNewFastResult r, ulong before, uint size, ulong count, ulong token)
    {
        Require(r.AllocationSucceeded && r.Operation == RhpNewFastOperation.Allocation && r.HeapStatus == ManagedHeapPageRangeStatus.Success && r.MappedAllocationStart == (ulong)f.Page + before && r.MappedAllocationEndExclusive == (ulong)f.Page + before + size && r.PhysicalAllocationStart == f.Access.Heap->PhysicalStart + before && r.PhysicalAllocationEndExclusive == f.Access.Heap->PhysicalStart + before + size && r.ObjectReference == (ulong)f.Page + before + 8 && r.MethodTableToken == token && r.BytesConsumed == size && r.UsedBefore == before && r.UsedAfter == before + size && r.AllocationCount == count && f.State->UsedBytes == before + size && f.State->AllocationCount == count, "literal mixed allocation result");
    }

    private static void CheckArrayBytes(byte* origin, uint size, ulong token, int length)
    {
        byte[] expected = new byte[size];
        BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8, 8), token);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(16, 4), length);
        Require(new ReadOnlySpan<byte>(origin, (int)size).SequenceEqual(expected), "independent complete array byte oracle");
    }

    private static void CheckFixedBytes(byte* origin, uint size, ulong token)
    {
        byte[] expected = new byte[size];
        BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8, 8), token);
        Require(new ReadOnlySpan<byte>(origin, (int)size).SequenceEqual(expected), "independent fixed byte oracle");
    }

    private static BootPageFreeListNode Node(ulong start, ulong pages) => new(0, 0, start, start + pages * 4096, pages, pages * 4096, -1);
    private static BootReservedRange Range(BootReservationKind kind, ulong start, ulong pages)
    {
        PhysicalAddress.TryCreateFromUefiBoundary(start, out PhysicalAddress address);
        Require(BootReservedRange.TryCreate(kind, address, pages, out BootReservedRange range) == BootReservedRangeStatus.Success, "fixture reservation");
        return range;
    }

    private static void CheckNoStdLibCompilation()
    {
        string root = FindRoot();
        string directory = Path.Combine(root, "artifacts", "issue77", "nostdlib", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(root, "global.json"), Path.Combine(directory, "global.json"));
        string[] sources = NoStdLibArraySources(root);
        string includes = string.Join(Environment.NewLine, sources.Select(p => "<Compile Include=\"" + System.Security.SecurityElement.Escape(Path.Combine(root, p)) + "\" />"));
        File.WriteAllText(Path.Combine(directory, "Compile.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
                <OutputType>Library</OutputType><AssemblyName>CLROS.M04.Array.NoStdLib</AssemblyName>
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
        var identities = sources.Select(p => new { Path = p, Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, p)))) });
        File.WriteAllText(Path.Combine(directory, "source-identities.json"), System.Text.Json.JsonSerializer.Serialize(identities));
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        string[] arguments = ["build", "Compile.csproj", "--nologo", "-m:1", "-c", "Release", "-nodeReuse:false"];
        foreach (string arg in arguments)
            start.ArgumentList.Add(arg);
        File.WriteAllText(Path.Combine(directory, "command.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            Executable = start.FileName,
            Arguments = arguments,
            WorkingDirectory = directory,
            StartedUtc = DateTime.UtcNow
        }));
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("no-stdlib start");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(), stderr = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        Task.WaitAll(stdout, stderr);
        File.WriteAllText(Path.Combine(directory, "build.stdout.log"), stdout.Result);
        File.WriteAllText(Path.Combine(directory, "build.stderr.log"), stderr.Result);
        File.WriteAllText(Path.Combine(directory, "result.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            Exited = exited,
            NativeExit = process.ExitCode,
            EndedUtc = DateTime.UtcNow
        }));
        Require(exited && process.ExitCode == 0, "actual eleven-source no-stdlib compile: " + directory + " " + stdout.Result + stderr.Result);
        using FileStream stream = File.OpenRead(Path.Combine(directory, "bin", "Release", "net11.0", "CLROS.M04.Array.NoStdLib.dll"));
        using PEReader pe = new(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        Require(metadata.AssemblyReferences.Count == 0, "zero AssemblyRefs");
        foreach ((string type, string method) in new[]
        {
            ("GuestTestArrayAllocationLengthOverflowChecks", "TryAllocate"),
            ("GuestTestRhpNewFast", "TryAllocate"),
            ("GuestTestRhpNewFast", "TryAllocateArray"),
            ("GuestTestRhpNewFast", "AllocateCore")
        }

        )
        {
            TypeDefinition definition = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(t => metadata.GetString(t.Name) == type);
            MethodDefinition entry = definition.GetMethods().Select(metadata.GetMethodDefinition).Single(m => metadata.GetString(m.Name) == method);
            Require(entry.RelativeVirtualAddress != 0 && pe.GetMethodBody(entry.RelativeVirtualAddress).GetILBytes()!.Length > 0, "actual production method body " + type + "." + method);
        }

        Console.WriteLine("M04_ARRAY_HOST_NOSTDLIB inputs=" + sources.Length + " assemblyReferences=0 sharedCore=true");
    }

    private static string FindRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CLROS.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("CLROS root not found");
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
