using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using Alloc = OS.Kernel.Runtime.GuestTestRhpNewFast;
using Heap = OS.Kernel.Runtime.ManagedHeapPageRangeLiveAllocator;
using Status = OS.Kernel.Runtime.RhpNewFastStatus;

internal static unsafe class GuestTestRhpNewFastTests
{
    private const ulong Token = 0x1234567800000008;
    private const ulong Token2 = 0xABCD987600000018;
    private const NativeAotLayoutProfile Profile = NativeAotLayoutProfile.Ilc10_0_8WinX64LittleEndian;

    [ModuleInitializer]
    internal static void Run()
    {
        Console.WriteLine("TEST_START m04.007");
        try
        {
            Group("bind", CheckBinding);
            Group("bytes", CheckObjectBytes);
            Group("fixed_domain", CheckFixedDomain);
            Group("admission", CheckAdmission);
            Group("exhaustion", CheckExhaustion);
            Group("storage", CheckStorage);
            Group("state_authority", CheckStateAndAuthority);
            Group("zero_allocation", CheckPodAndAllocation);
            Group("nostdlib", CheckNoStdLibCompilation);
            Console.WriteLine("CLROS_M04_007_HOST_READY pageBytes=4096 minimumObjectBytes=24"
                + " stateBytes=80 hostConformance=true guestIntegrated=false compiledNew=false"
                + " runtimeExport=false gc=false gate4=Fail");
            Console.WriteLine("TEST_PASS m04.007");
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL m04.007 " + error.GetType().Name + ": " + error.Message);
            throw;
        }
    }

    private static void Group(string name, Action test)
    {
        Console.WriteLine("TEST_START m04.007." + name);
        try { test(); Console.WriteLine("TEST_PASS m04.007." + name); }
        catch (Exception error) { Console.WriteLine("TEST_FAIL m04.007." + name + " " + error.GetType().Name); throw; }
    }

    private static void CheckBinding()
    {
        Require(!default(RhpNewFastResult).Succeeded && !default(RhpNewFastResult).AllocationSucceeded &&
            !default(RhpNewFastBumpState).IsInitialized, "defaults fail closed");
        using Fixture f = new();
        Expect(f, Status.NotInitialized);
        byte[] before = f.Snapshot();
        RhpNewFastResult bound = Bind(f);
        Require(bound.Succeeded && !bound.AllocationSucceeded && bound.Operation == RhpNewFastOperation.Bind &&
            bound.HeapStatus == ManagedHeapPageRangeStatus.Success, "bind is not allocation");
        ZeroPayload(bound);
        Require(f.State->IsInitialized && f.State->SelfAddress == (ulong)f.State &&
            f.State->HeapAddress == (ulong)f.Access.Heap && f.State->MappedStart == (ulong)f.Page &&
            f.State->PhysicalStart == 0x300000000 && f.State->PhysicalEndExclusive == 0x300001000 &&
            f.State->UsedBytes == 0 && f.State->AllocationCount == 0, "bound original full-width identities");
        f.OnlyChanged(before, (0x3000, 80));
        ExpectBind(f, Status.AlreadyInitialized);
        Require(Allocate(f).AllocationSucceeded, "first allocation");
        f.Page[16] = 0xA3;
        ExpectBind(f, Status.AlreadyInitialized);
        Require(f.Page[16] == 0xA3, "repeat bind does not erase objects");

        for (int offset = 0; offset < 4096; offset += 4095)
        {
            f.Reset(); f.Page[offset] = 1;
            ExpectBind(f, Status.InitialPageNotPristine, ManagedHeapPageRangeStatus.Success);
        }
        // All 80 state bytes, not just the initialization flag, must start at zero.
        for (int i = 0; i < 80; i++)
        {
            f.Reset(); ((byte*)f.State)[i] = 1;
            ExpectBind(f, Status.AlreadyInitialized);
        }
    }

    private static void CheckObjectBytes()
    {
        using Fixture f = new();
        Require(Bind(f).Succeeded, "bind bytes fixture");
        byte[] before = f.Snapshot();
        RhpNewFastResult first = Allocate(f, size: 24);
        CheckResult(f, first, 0, 24, 1, Token);
        CheckBytes(f.Page, 24, Token);
        f.OnlyChanged(before, (0x3000, 80), (f.PageOffset, 24));
        // An actual nonzero previous payload and a dirty future interval make
        // preservation and selected-interval zeroing independently observable.
        f.Page[16] = 0xE7;
        new Span<byte>(f.Page + 24, 32).Fill(0xBC);
        before = f.Snapshot();
        RhpNewFastResult second = Allocate(f, size: 32, token: Token2);
        CheckResult(f, second, 24, 32, 2, Token2);
        CheckBytes(f.Page + 24, 32, Token2);
        Require(f.Page[16] == 0xE7 && first.ObjectReference == (ulong)f.Page + 8 &&
            second.ObjectReference == (ulong)f.Page + 32, "independent reference origins and prior data");
        f.OnlyChanged(before, (0x3000, 80), (f.PageOffset + 24, 32));
        Require(Retained(f.Access).IsReady && !Retained(f.Access).ContentsAreZeroNow,
            "actual retained authority accepts nonzero objects");

        f.Reset(secondPage: true);
        before = f.Snapshot();
        Require(Bind(f).Succeeded, "bind mapped second page");
        first = Allocate(f);
        CheckResult(f, first, 0, 24, 1, Token);
        Require(first.PhysicalAllocationStart == 0x300001000 && f.PageOffset == 0x6000,
            "retained physical-to-mapped offset used");
        f.OnlyChanged(before, (0x3000, 80), (0x6000, 24));

        f.Reset(physical: 0xFFFFFFFFFFFFD000);
        Require(Bind(f).Succeeded, "high physical interval");
        first = Allocate(f, token: 0xFFFFFFFFFFFFFFE0);
        Require(first.AllocationSucceeded && first.PhysicalAllocationStart == 0xFFFFFFFFFFFFD000 &&
            first.PhysicalAllocationEndExclusive == 0xFFFFFFFFFFFFD018,
            "no physical/token truncation; numeric token is never dereferenced");
        CheckBytes(f.Page, 24, 0xFFFFFFFFFFFFFFE0);

        // A real array route shares this exact binder/controller. Fixed-only
        // admission and byte writes must remain unchanged after either route.
        f.Reset(); Require(Bind(f).Succeeded, "mixed fixed regression bind");
        CheckResult(f, Allocate(f), 0, 24, 1, Token);
        NativeAotMethodTablePrefix arrayPrefix = Prefix(0xE0020001, 24);
        RhpNewFastResult array = GuestTestArrayAllocationLengthOverflowChecks.TryAllocate(
            f.Access, f.State, f.StateCapacity, Profile, arrayPrefix, Token2, 1);
        CheckResult(f, array, 24, 32, 2, Token2);
        before = f.Snapshot();
        Rejected(Alloc.TryAllocate(f.Access, f.State, f.StateCapacity, Profile, arrayPrefix, Token),
            Status.UnsupportedFixedType);
        f.Unchanged(before);
        CheckResult(f, Allocate(f, size: 32), 56, 32, 3, Token);
        CheckBytes(f.Page + 56, 32, Token);
        f.OnlyChanged(before, (0x3000, 80), (f.PageOffset + 56, 32));
    }

    private static void CheckFixedDomain()
    {
        using Fixture f = new();
        int admitted = 0;
        for (uint element = 0; element < 32; element++)
        {
            bool valid = (element >= 2 && element <= 16) || element == 18 || element == 20;
            if (!valid) { f.Reset(); Bind(f); Expect(f, Status.UnsupportedFixedType, flags: element << 26); continue; }
            admitted++;
            foreach (uint observation in new uint[] { 0, 0x01000000, 0x02000000, 0x03000000 })
            {
                f.Reset(); Require(Bind(f).Succeeded, "fixed-domain bind");
                // Explicitly retain inert dispatch/sealed/variance/interface observations.
                uint flags = (element << 26) | observation | 0x00C40008;
                byte[] before = f.Snapshot();
                RhpNewFastResult result = Allocate(f, flags: flags, size: 40);
                CheckResult(f, result, 0, 40, 1, Token);
                CheckBytes(f.Page, 40, Token);
                Require(Read64(f.Page + 16) == 0 && Read64(f.Page + 24) == 0 && Read64(f.Page + 32) == 0,
                    "initial reference-sized slots are zero; no host references created");
                f.OnlyChanged(before, (0x3000, 80), (f.PageOffset, 40));
            }
        }
        Require(admitted == 17, "literal fixed domain cardinality");
        for (uint element = 2; element <= 18; element++)
        {
            if (element == 17) continue;
            for (uint padding = 0; padding < 8; padding++)
            {
                uint lastOffset = element == 18 ? 7U : 0U;
                for (uint nullableOffset = 0; nullableOffset <= lastOffset; nullableOffset++)
                {
                    f.Reset(); Bind(f);
                    RhpNewFastResult result = Allocate(f, flags: (element << 26) | (padding << 5) |
                        (nullableOffset << 8) | 0x03000000, size: 24);
                    CheckResult(f, result, 0, 24, 1, Token);
                    CheckBytes(f.Page, 24, Token);
                }
            }
        }
        // Literal primitive/enum/padded struct/reference/generic/nullable scalar
        // descriptions. These are not compiled boxes or emitted field layouts.
        uint[] mixedFlags = [0x080000E0, 0x20000080, 0x28000000, 0x20000080,
            0x40000060, 0x43000000, 0x50000000, 0x51000000, 0x52000000, 0x53000000, 0x4B000240];
        uint[] mixedSizes = [24, 24, 24, 24, 32, 56, 24, 40, 32, 48, 32];
        f.Reset(); Bind(f); ulong used = 0;
        for (int i = 0; i < mixedFlags.Length; i++)
        {
            byte[] before = f.Snapshot();
            ulong token = Token + (ulong)i * 0x100;
            RhpNewFastResult result = Allocate(f, mixedFlags[i], mixedSizes[i], token);
            CheckResult(f, result, used, mixedSizes[i], (ulong)i + 1, token);
            CheckBytes(f.Page + used, mixedSizes[i], token);
            f.OnlyChanged(before, (0x3000, 80), (f.PageOffset + (int)used, (int)mixedSizes[i]));
            f.Page[used + 16] = (byte)(i + 1);
            used += mixedSizes[i];
        }
        Require(!ObjectArrayStringEETypeMethodTableLayoutContracts.Evaluate(Profile,
            Prefix(0x53000000, 24), 0, 4096).Succeeded,
            "fixed allocator domain deliberately exceeds frozen scalar layout evaluator");
    }

    private static void CheckAdmission()
    {
        using Fixture f = new(); Bind(f);
        foreach (NativeAotLayoutProfile profile in new[] { (NativeAotLayoutProfile)0, (NativeAotLayoutProfile)2, (NativeAotLayoutProfile)255 })
            Expect(f, Status.UnsupportedProfile, profile: profile);
        foreach (ulong token in new ulong[] { 0, 1, 7, Token + 1, Token + 2, Token + 3, Token + 4,
            Token + 5, Token + 6, Token + 7, ulong.MaxValue, 0xFFFFFFFFFFFFFFF8, 0xFFFFFFFFFFFFFFF0, 0xFFFFFFFFFFFFFFE8 })
            Expect(f, Status.InvalidMethodTableToken, token: token);
        ulong[] aliases = [(ulong)f.Access.Allocator, (ulong)f.Access.Nodes + 48,
            (ulong)f.Access.Reservations + 80, (ulong)f.Access.Heap + 216,
            (ulong)f.Access.Backing + 8192, (ulong)f.State + 80, (ulong)f.State - 16];
        foreach (ulong token in aliases) Expect(f, Status.InvalidMethodTableToken, token: token);
        Require(Allocate(f, token: (ulong)f.State - 24).AllocationSucceeded,
            "exactly touching metadata declaration is numeric non-overlap");
        Require(Allocate(f, token: (ulong)f.State + f.StateCapacity).AllocationSucceeded,
            "declared prefix immediately after state capacity");

        foreach (uint bit in new uint[] { 0x00200000, 0x0800, 0x2000, 0x4000, 0x8000 })
            Expect(f, Status.InvalidMethodTableFlags, flags: 0x50000000 | bit);
        foreach (uint bit in new uint[] { 0x80000000, 0x00010000, 0x00020000, 0x00030000,
            0x00080000, 0x00100000, 1, 2, 4, 0x10, 0x1000 })
            Expect(f, Status.UnsupportedFixedType, flags: 0x50000000 | bit);
        for (uint padding = 1; padding < 8; padding++)
            Expect(f, Status.InvalidMethodTableFlags, flags: 0x50000000 | padding << 5);
        for (uint offset = 1; offset < 8; offset++)
            foreach (uint element in new uint[] { 2, 8, 16, 20 })
                Expect(f, Status.InvalidMethodTableFlags, flags: (element << 26) | offset << 8);
        foreach (uint element in new uint[] { 2, 16, 18, 20 })
            Expect(f, Status.UnsupportedFixedType, flags: (element << 26) | 0x10);
        foreach (uint size in new uint[] { 0, 1, 8, 16, 23, 25, 31, uint.MaxValue })
            Expect(f, Status.InvalidBaseSize, size: size);
        foreach (uint size in new uint[] { 4104, 0x80000000, 0xFFFFFFF8 })
            Expect(f, Status.Exhausted, size: size);
    }

    private static void CheckExhaustion()
    {
        using Fixture f = new(); Bind(f);
        for (ulong i = 0; i < 128; i++)
        {
            byte[] before = f.Snapshot();
            RhpNewFastResult result = Allocate(f, size: 32, token: Token + i * 8);
            CheckResult(f, result, i * 32, 32, i + 1, Token + i * 8);
            CheckBytes(f.Page + i * 32, 32, Token + i * 8);
            f.OnlyChanged(before, (0x3000, 80), (f.PageOffset + (int)i * 32, 32));
        }
        Require(f.State->UsedBytes == 4096 && f.State->AllocationCount == 128, "exact end cursor stays offset");
        Expect(f, Status.Exhausted);
        f.Reset(); Bind(f);
        for (int i = 0; i < 170; i++) Require(Allocate(f).AllocationSucceeded, "170 minimum allocations");
        Require(f.State->UsedBytes == 4080 && f.State->AllocationCount == 170, "16-byte unusable suffix");
        Expect(f, Status.Exhausted);
        f.Reset(); Bind(f);
        CheckResult(f, Allocate(f, size: 4096), 0, 4096, 1, Token);
        CheckBytes(f.Page, 4096, Token);
        Expect(f, Status.Exhausted);
    }

    private static void CheckStorage()
    {
        using Fixture f = new();
        for (int scenario = 0; scenario < 36; scenario++)
        {
            f.Reset(); Bind(f);
            RhpNewFastHeapAccess a = f.Access; RhpNewFastBumpState* state = f.State;
            ulong capacity = f.StateCapacity;
            Status expected = Status.InvalidStorage;
            switch (scenario)
            {
                case 0: state = null; break;
                case 1: state = (RhpNewFastBumpState*)((byte*)state + 1); break;
                case 2: capacity = 79; break;
                case 3: capacity = ulong.MaxValue; break;
                case 4: state = (RhpNewFastBumpState*)0xFFFFFFFFFFFFFFF8; break;
                case 5: a.Allocator = null; break;
                case 6: a.Nodes = null; break;
                case 7: a.Reservations = null; break;
                case 8: a.Backing = null; break;
                case 9: a.Heap = null; break;
                case 10: a.AllocatorCapacity = 0; break;
                case 11: a.AllocatorCapacity = 2; break;
                case 12: a.NodeCount = 0; break;
                case 13: a.NodeCount = 4097; break;
                case 14: a.NodeCapacity = 0; break;
                case 15: a.NodeCapacity = ulong.MaxValue; break;
                case 16: a.ReservationCount = 0; break;
                case 17: a.ReservationCount = 257; break;
                case 18: a.ReservationCapacity = 1; break;
                case 19: a.ReservationCapacity = ulong.MaxValue; break;
                case 20: a.ParentIndex = 2; break;
                case 21: a.HeapCapacity = 215; break;
                case 22: a.BackingCapacity = 4095; break;
                case 23: a.Allocator = (BootPageAllocatorState*)((byte*)a.Allocator + 1); break;
                case 24: a.Nodes = (BootPageFreeListNode*)((byte*)a.Nodes + 1); break;
                case 25: a.Reservations = (BootReservedRange*)((byte*)a.Reservations + 1); break;
                case 26: a.Heap = (ManagedHeapPageRangeState*)((byte*)a.Heap + 1); break;
                case 27: a.Backing++; break;
                case 28: a.HeapCapacity = ulong.MaxValue; break;
                case 29: a.BackingCapacity = ulong.MaxValue; break;
                case 30: a.Nodes = (BootPageFreeListNode*)0xFFFFFFFFFFFFFFD0; break;
                case 31: a.Reservations = (BootReservedRange*)0xFFFFFFFFFFFFFFD8; break;
                case 32: a.Allocator = (BootPageAllocatorState*)0xFFFFFFFFFFFFFFF8; break;
                case 33: a.Heap = (ManagedHeapPageRangeState*)0xFFFFFFFFFFFFFFF8; break;
                case 34: a.Backing = (byte*)0xFFFFFFFFFFFFF000; break;
                case 35: a.Nodes = (BootPageFreeListNode*)a.Reservations; expected = Status.StorageOverlap; break;
            }
            byte[] before = f.Snapshot();
            Rejected(Alloc.TryAllocate(a, state, capacity, Profile, Prefix(), Token), expected);
            Rejected(Alloc.InitializeOnce(a, state, capacity), expected, operation: RhpNewFastOperation.Bind);
            f.Unchanged(before);
        }
        foreach (int offset in new[] { 0x100, 0x300, 0x330, 0x1000, 0x1050, 0x2000, 0x20D8, 0x5000, 0x7000 })
        {
            f.Reset(); byte[] before = f.Snapshot();
            Rejected(Alloc.InitializeOnce(f.Access, (RhpNewFastBumpState*)(f.Storage + offset), 96),
                Status.StorageOverlap, operation: RhpNewFastOperation.Bind);
            f.Unchanged(before);
        }
        f.Reset();
        // Full state capacity reaches another declared buffer even though its
        // 80-byte required prefix is disjoint.
        byte[] snapshot = f.Snapshot();
        Rejected(Alloc.InitializeOnce(f.Access, f.State, 0x2001), Status.StorageOverlap, operation: RhpNewFastOperation.Bind);
        f.Unchanged(snapshot);
        // Exact touching endpoints, exact prefix capacity and preserved tail.
        f.Reset(); f.State = (RhpNewFastBumpState*)(f.Storage + 0x5000 - 80); f.StateCapacity = 80;
        new Span<byte>(f.State, 80).Clear(); snapshot = f.Snapshot();
        Require(Bind(f).Succeeded, "state ends exactly at backing start");
        f.OnlyChanged(snapshot, (0x5000 - 80, 80));
    }

    private static void CheckStateAndAuthority()
    {
        using Fixture f = new();
        for (int slot = 0; slot < 10; slot++)
        {
            f.Reset(); Bind(f); ((ulong*)f.State)[slot] ^= 8;
            Status expected = slot < 3 || slot >= 8 ? Status.InvalidState : Status.HeapBindingMismatch;
            Expect(f, expected, heapStatus: expected == Status.HeapBindingMismatch ? ManagedHeapPageRangeStatus.Success : default);
        }
        foreach ((ulong used, ulong count) in new (ulong, ulong)[] { (4097, 1), (8, 1), (24, 0), (0, 1),
            (24, 2), (4096, 171), (4096, ulong.MaxValue), (ulong.MaxValue, 1), (25, 1) })
        {
            f.Reset(); Bind(f); ((ulong*)f.State)[8] = used; ((ulong*)f.State)[9] = count;
            Expect(f, Status.InvalidState);
        }
        f.Reset(); Bind(f); byte[] before = f.Snapshot();
        Rejected(Alloc.TryAllocate(f.Access, f.State, 104, Profile, Prefix(), Token), Status.InvalidState);
        f.Unchanged(before);
        RhpNewFastBumpState* copy = (RhpNewFastBumpState*)(f.Storage + 0x4000);
        *copy = *f.State; before = f.Snapshot();
        Rejected(Alloc.TryAllocate(f.Access, copy, f.StateCapacity, Profile, Prefix(), Token), Status.InvalidState);
        f.Unchanged(before);

        for (int scenario = 0; scenario < 9; scenario++)
        {
            f.Reset(); Bind(f); Require(Allocate(f).AllocationSucceeded, "authority fixture has object");
            RhpNewFastHeapAccess a = f.Access;
            ManagedHeapPageRangeStatus expected = ManagedHeapPageRangeStatus.IdentityMismatch;
            switch (scenario)
            {
                case 0: a.Allocator = (BootPageAllocatorState*)(f.Storage + 0x8000); *a.Allocator = *f.Access.Allocator; break;
                case 1: a.Nodes = (BootPageFreeListNode*)(f.Storage + 0x8000); a.Nodes[0] = f.Access.Nodes[0]; break;
                case 2: a.Reservations = (BootReservedRange*)(f.Storage + 0x8000); a.Reservations[0] = f.Access.Reservations[0]; a.Reservations[1] = f.Access.Reservations[1]; break;
                case 3: a.ParentIndex = 0; break;
                case 4: a.Backing = f.Storage + 0x8000; break;
                case 5: a.Heap = (ManagedHeapPageRangeState*)(f.Storage + 0x8000); *a.Heap = *f.Access.Heap; break;
                case 6: a.NodeCapacity = 1; break;
                case 7: a.BackingCapacity = 8192; break;
                case 8: f.Access.Reservations[1] = Range(BootReservationKind.AllocatorArena, 0x300010000, 2); break;
            }
            before = f.Snapshot();
            Rejected(Alloc.TryAllocate(a, f.State, f.StateCapacity, Profile, Prefix(), Token), Status.HeapAuthorityRejected, expected);
            f.Unchanged(before);
        }
        using Fixture other = new();
        f.Reset(); Bind(f); before = f.Snapshot(); byte[] otherBefore = other.Snapshot();
        Rejected(Alloc.TryAllocate(other.Access, f.State, f.StateCapacity, Profile, Prefix(), Token),
            Status.HeapBindingMismatch, ManagedHeapPageRangeStatus.Success);
        f.Unchanged(before); other.Unchanged(otherBefore);

        f.Reset(); Bind(f); Allocate(f); f.Page[16] = 42;
        BootPageAllocatorOnePageResult later = BootPageAllocatorMutator.AllocateOnePage(*f.Access.Allocator,
            f.Access.Nodes, f.Access.NodeCount, f.Access.NodeCapacity, null, 0, 0, 1, 4096);
        Require(later.Succeeded, "original allocator can independently consume its other page");
        *f.Access.Allocator = later.AfterState;
        Require(Retained(f.Access).IsReady && !Retained(f.Access).ContentsAreZeroNow && Allocate(f).AllocationSucceeded,
            "nonzero heap plus valid exhausted external free list remains valid");
        Require(f.Page[16] == 42, "later allocation preserves earlier payload");
        BootPageAllocatorOnePageFreeResult freed = BootPageAllocatorMutator.FreeOnePage(*f.Access.Allocator,
            f.Access.Nodes, f.Access.NodeCount, f.Access.NodeCapacity, null, 0, 0, later);
        Require(freed.Succeeded, "free only other page"); *f.Access.Allocator = freed.AfterState;
        Require(Allocate(f).AllocationSucceeded, "retained owner survives unrelated free");
        *f.Access.Allocator = f.Access.Heap->InitialAllocatorState;
        f.Access.Nodes[0] = Node(0x300000000, 2);
        Expect(f, Status.HeapAuthorityRejected, heapStatus: ManagedHeapPageRangeStatus.RetainedRangeIsFree);

        f.Reset(); f.Access.Heap[0] = default;
        ExpectBind(f, Status.HeapAuthorityRejected, ManagedHeapPageRangeStatus.NotReady);
        f.Reset(); Bind(f); f.Access.Heap[0] = default;
        Expect(f, Status.HeapAuthorityRejected, heapStatus: ManagedHeapPageRangeStatus.NotReady);
    }

    private static void CheckPodAndAllocation()
    {
        Require(sizeof(RhpNewFastHeapAccess) == 104 && sizeof(RhpNewFastBumpState) == 80 &&
            sizeof(RhpNewFastResult) == 88 && Alloc.RequiredStateBytes == 80 && Heap.RequiredStateBytes == 216,
            "frozen x64 POD sizes");
        Require(!RuntimeHelpers.IsReferenceOrContainsReferences<RhpNewFastHeapAccess>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<RhpNewFastBumpState>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<RhpNewFastResult>(), "POD contains no CLR references");
        string[] statuses = ["Uninitialized", "Success", "InvalidStorage", "StorageOverlap", "AlreadyInitialized",
            "NotInitialized", "InvalidState", "HeapAuthorityRejected", "HeapBindingMismatch", "InitialPageNotPristine",
            "UnsupportedProfile", "InvalidMethodTableToken", "InvalidMethodTableFlags", "UnsupportedFixedType",
            "InvalidBaseSize", "Exhausted", "UnsupportedArrayType", "InvalidComponentSize", "InvalidLength"];
        Require(Enum.GetValues<Status>().Length == statuses.Length, "append-only status count");
        for (int i = 0; i < statuses.Length; i++) Require(((Status)i).ToString() == statuses[i], "stable status values");
        string[] fields = ["InitializedValue", "SelfAddressValue", "CapacityValue", "HeapAddressValue",
            "HeapCapacityValue", "MappedStartValue", "PhysicalStartValue", "PhysicalEndValue", "UsedValue", "CountValue"];
        for (int i = 0; i < fields.Length; i++) Require((int)Marshal.OffsetOf<RhpNewFastBumpState>(fields[i]) == i * 8,
            "independent corruption fixture field offset");
        using Fixture f = new();
        bool passed = true;
        for (int i = 0; i < 32; i++) MeasureIteration(f, ref passed);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 256; i++) MeasureIteration(f, ref passed);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(passed && allocated == 0, "warm bind/allocation/rejection paths allocate zero CLR bytes");
    }

    private static void MeasureIteration(Fixture f, ref bool passed)
    {
        f.Reset();
        passed &= Bind(f).Succeeded;
        passed &= Allocate(f).AllocationSucceeded;
        passed &= Allocate(f, token: 0).Status == Status.InvalidMethodTableToken;
        passed &= Allocate(f, size: 4096).Status == Status.Exhausted;
        passed &= Bind(f).Status == Status.AlreadyInitialized;
    }

    private static NativeAotMethodTablePrefix Prefix(uint flags = 0x50000000, uint size = 24) =>
        new(flags, size, 0xFEDCBA9876543211, ushort.MaxValue, ushort.MaxValue, 0xDEADBEEF);
    private static RhpNewFastResult Bind(Fixture f) => Alloc.InitializeOnce(f.Access, f.State, f.StateCapacity);
    private static RhpNewFastResult Allocate(Fixture f, uint flags = 0x50000000, uint size = 24, ulong token = Token) =>
        Alloc.TryAllocate(f.Access, f.State, f.StateCapacity, Profile, Prefix(flags, size), token);
    private static ManagedHeapPageRangeResult Retained(RhpNewFastHeapAccess a) => Heap.ValidateRetained(a.Allocator,
        a.AllocatorCapacity, a.Nodes, a.NodeCount, a.NodeCapacity, a.Reservations, a.ReservationCount,
        a.ReservationCapacity, a.ParentIndex, a.Backing, a.BackingCapacity, a.Heap, a.HeapCapacity);
    private static void Expect(Fixture f, Status expected, uint flags = 0x50000000, uint size = 24,
        ulong token = Token, NativeAotLayoutProfile profile = Profile, ManagedHeapPageRangeStatus heapStatus = default)
    {
        byte[] before = f.Snapshot();
        Rejected(Alloc.TryAllocate(f.Access, f.State, f.StateCapacity, profile, Prefix(flags, size), token), expected, heapStatus);
        f.Unchanged(before);
    }
    private static void ExpectBind(Fixture f, Status expected, ManagedHeapPageRangeStatus heapStatus = default)
    {
        byte[] before = f.Snapshot();
        Rejected(Bind(f), expected, heapStatus, RhpNewFastOperation.Bind); f.Unchanged(before);
    }
    private static void Rejected(RhpNewFastResult r, Status expected, ManagedHeapPageRangeStatus heapStatus = default,
        RhpNewFastOperation operation = RhpNewFastOperation.Allocation)
    {
        Require(r.Status == expected && !r.Succeeded && !r.AllocationSucceeded && r.Operation == operation &&
            r.HeapStatus == heapStatus, "exact rejection expected=" + expected + " actual=" + r.Status + "/" + r.HeapStatus);
        ZeroPayload(r);
    }
    private static void ZeroPayload(RhpNewFastResult r) => Require(r.MappedAllocationStart == 0 &&
        r.MappedAllocationEndExclusive == 0 && r.PhysicalAllocationStart == 0 && r.PhysicalAllocationEndExclusive == 0 &&
        r.ObjectReference == 0 && r.MethodTableToken == 0 && r.BytesConsumed == 0 && r.UsedBefore == 0 &&
        r.UsedAfter == 0 && r.AllocationCount == 0, "no address/size/counter success payload on rejection or bind");
    private static void CheckResult(Fixture f, RhpNewFastResult r, ulong before, uint size, ulong count, ulong token)
    {
        Require(r.AllocationSucceeded && r.Status == Status.Success && r.Operation == RhpNewFastOperation.Allocation &&
            r.HeapStatus == ManagedHeapPageRangeStatus.Success && r.MappedAllocationStart == (ulong)f.Page + before &&
            r.MappedAllocationEndExclusive == (ulong)f.Page + before + size &&
            r.PhysicalAllocationStart == f.Access.Heap->PhysicalStart + before &&
            r.PhysicalAllocationEndExclusive == f.Access.Heap->PhysicalStart + before + size &&
            r.ObjectReference == (ulong)f.Page + before + 8 && r.MethodTableToken == token && r.BytesConsumed == size &&
            r.UsedBefore == before && r.UsedAfter == before + size && r.AllocationCount == count &&
            f.State->UsedBytes == before + size && f.State->AllocationCount == count, "literal allocation result");
    }
    private static void CheckBytes(byte* origin, uint size, ulong token)
    {
        byte[] expected = new byte[size];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8, 8), token);
        Require(new ReadOnlySpan<byte>(origin, (int)size).SequenceEqual(expected), "independent complete byte oracle");
    }
    private static ulong Read64(byte* pointer) => System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(pointer, 8));

    private sealed class Fixture : IDisposable
    {
        internal readonly byte* Storage = (byte*)NativeMemory.AlignedAlloc(65536, 4096);
        internal RhpNewFastHeapAccess Access;
        internal RhpNewFastBumpState* State;
        internal ulong StateCapacity;
        internal byte* Page;
        internal int PageOffset => (int)(Page - Storage);
        internal Fixture() { Require(Storage != null, "native fixture"); Reset(); }
        internal void Reset(bool secondPage = false, ulong physical = 0x300000000)
        {
            // Fixture reset creates a fresh synthetic authority lifetime; no
            // production reset operation or second controller is being exercised.
            new Span<byte>(Storage, 65536).Fill(0xA5);
            Access = new RhpNewFastHeapAccess
            {
                Allocator = (BootPageAllocatorState*)(Storage + 0x100), AllocatorCapacity = 1,
                Nodes = (BootPageFreeListNode*)(Storage + 0x300), NodeCount = 1, NodeCapacity = 2,
                Reservations = (BootReservedRange*)(Storage + 0x1000), ReservationCount = 2, ReservationCapacity = 3,
                ParentIndex = 1, Heap = (ManagedHeapPageRangeState*)(Storage + 0x2000), HeapCapacity = 232,
                Backing = Storage + 0x5000, BackingCapacity = 8208
            };
            State = (RhpNewFastBumpState*)(Storage + 0x3000); StateCapacity = 96;
            *Access.Allocator = new BootPageAllocatorState(true, 4096, 0, 1, secondPage ? 1UL : 2UL,
                secondPage ? 4096UL : 8192UL, secondPage ? 1UL : 0UL, secondPage ? 4096UL : 0UL);
            Access.Nodes[0] = Node(physical + (secondPage ? 4096UL : 0UL), secondPage ? 1UL : 2UL);
            Access.Reservations[0] = Range(BootReservationKind.LoadedImage, 0x100000, 1);
            Access.Reservations[1] = Range(BootReservationKind.AllocatorArena, physical, 2);
            new Span<byte>(Access.Heap, 216).Clear(); new Span<byte>(State, 80).Clear();
            ManagedHeapPageRangeResult heap = Heap.ReserveAndInitializeOnce(Access.Allocator, Access.AllocatorCapacity,
                Access.Nodes, Access.NodeCount, Access.NodeCapacity, Access.Reservations, Access.ReservationCount,
                Access.ReservationCapacity, Access.ParentIndex, Access.Backing, Access.BackingCapacity, Access.Heap, Access.HeapCapacity);
            Require(heap.IsReady, "actual #75 reserve fixture"); Page = (byte*)heap.MappedStart;
        }
        internal byte[] Snapshot() => new ReadOnlySpan<byte>(Storage, 65536).ToArray();
        internal void Unchanged(byte[] before) => Require(new ReadOnlySpan<byte>(Storage, 65536).SequenceEqual(before), "all caller storage/canaries unchanged");
        internal void OnlyChanged(byte[] before, params (int Start, int Length)[] intervals)
        {
            for (int i = 0; i < before.Length; i++)
            {
                bool inside = false;
                foreach (var interval in intervals) inside |= i >= interval.Start && i < interval.Start + interval.Length;
                if (!inside && Storage[i] != before[i]) throw new InvalidOperationException("changed outside interval at " + i);
            }
        }
        public void Dispose() => NativeMemory.AlignedFree(Storage);
    }
    private static BootPageFreeListNode Node(ulong start, ulong pages) => new(0, 0, start, start + pages * 4096, pages, pages * 4096, -1);
    private static BootReservedRange Range(BootReservationKind kind, ulong start, ulong pages)
    {
        PhysicalAddress.TryCreateFromUefiBoundary(start, out PhysicalAddress address);
        Require(BootReservedRange.TryCreate(kind, address, pages, out BootReservedRange range) == BootReservedRangeStatus.Success, "fixture range");
        return range;
    }

    private static void CheckNoStdLibCompilation()
    {
        string root = FindRoot();
        string directory = Path.Combine(root, "artifacts", "issue76", "nostdlib", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(root, "global.json"), Path.Combine(directory, "global.json"));
        string[] sources =
        [
            "src/OS/Runtime/MinimalRuntime.cs", "src/OS/Runtime/MethodImplAttribute.cs",
            "src/OS.Kernel/Boot/TypedPhysicalVirtualDeviceAddressPODWrappers.cs",
            "src/OS.Kernel/Memory/BootReservedRange.cs", "src/OS.Kernel/Memory/BootReservationTableValidator.cs",
            "src/OS.Kernel/Memory/BootPageAllocatorCoreContracts.cs", "src/OS.Kernel/Memory/BootPageAllocatorMutation.cs",
            "src/OS.Kernel/Runtime/ManagedHeapPageRangeLiveAllocator.cs",
            "src/OS.Kernel/Runtime/ObjectArrayStringEETypeMethodTableLayoutContracts.cs",
            "src/OS.Kernel/Runtime/GuestTestRhpNewFast.cs"
        ];
        string includes = string.Join(Environment.NewLine, sources.Select(p =>
            "<Compile Include=\"" + System.Security.SecurityElement.Escape(Path.Combine(root, p)) + "\" />"));
        File.WriteAllText(Path.Combine(directory, "Compile.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework><LangVersion>14.0</LangVersion>
                <OutputType>Library</OutputType><AssemblyName>CLROS.M04.NewFast.NoStdLib</AssemblyName>
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
        foreach (string arg in new[] { "build", "Compile.csproj", "--nologo", "-m:1", "-c", "Release", "-nodeReuse:false" }) start.ArgumentList.Add(arg);
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("no-stdlib compile start");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(); Task<string> stderr = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(90000);
        if (!exited) { process.Kill(entireProcessTree: true); process.WaitForExit(); }
        Task.WaitAll(stdout, stderr);
        File.WriteAllText(Path.Combine(directory, "build.stdout.log"), stdout.Result);
        File.WriteAllText(Path.Combine(directory, "build.stderr.log"), stderr.Result);
        File.WriteAllText(Path.Combine(directory, "build.exitcode.txt"), process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Require(exited && process.ExitCode == 0, "actual-source no-stdlib compile: " + directory + " " + stdout.Result + stderr.Result);
        using var stream = File.OpenRead(Path.Combine(directory, "bin", "Release", "net10.0", "CLROS.M04.NewFast.NoStdLib.dll"));
        using PEReader image = new(stream); MetadataReader metadata = image.GetMetadataReader();
        Require(metadata.AssemblyReferences.Count == 0, "actual ten-source closure has zero assembly references");
        Require(metadata.MethodDefinitions.Select(metadata.GetMethodDefinition).Any(m => metadata.GetString(m.Name) == "TryAllocate" && m.RelativeVirtualAddress != 0),
            "actual allocation method is present");
        TypeDefinition allocator = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition)
            .Single(t => metadata.GetString(t.Name) == "GuestTestRhpNewFast");
        foreach (string name in new[] { "TryAllocate", "TryAllocateArray", "AllocateCore" })
        {
            MethodDefinition entry = allocator.GetMethods().Select(metadata.GetMethodDefinition)
                .Single(m => metadata.GetString(m.Name) == name);
            Require(entry.RelativeVirtualAddress != 0 && image.GetMethodBody(entry.RelativeVirtualAddress).GetILBytes()!.Length > 0,
                "actual fixed closure entry/core body " + name);
        }
        Require(!metadata.TypeDefinitions.Select(metadata.GetTypeDefinition)
            .Any(t => metadata.GetString(t.Name) == "GuestTestArrayAllocationLengthOverflowChecks"),
            "fixed ten-source closure does not depend on the array wrapper");
        Console.WriteLine("M04_NEWFAST_HOST_NOSTDLIB inputs=10 assemblyReferences=0");
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
