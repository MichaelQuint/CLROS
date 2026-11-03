using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using Leaf = OS.Kernel.Runtime.CheckedReferenceBulkBarriersIncludingDotNetN10Bulk;
using Heap = OS.Kernel.Runtime.ManagedHeapPageRangeLiveAllocator;
using Status = OS.Kernel.Runtime.CheckedReferenceBulkStatus;

internal static unsafe partial class CheckedReferenceBulkBarriersIncludingDotNetN10BulkTests
{
    private const CheckedReferenceBulkProfile Profile = CheckedReferenceBulkProfile.NonCollectingExclusiveRetainedPage;
    [HostFixture(HostLane.Native)]
    internal static void Run()
    {
        Console.WriteLine("TEST_START m04.010");
        try
        {
            Group("contract", CheckContract);
            Group("stores", CheckStores);
            Group("moves", CheckMoves);
            Group("bounds", CheckBounds);
            Group("storage", CheckStorage);
            Group("identity", CheckIdentity);
            Group("live_authority", CheckLiveAuthority);
            Group("mixed", CheckMixed);
            Group("zero_allocation", CheckZeroAllocation);
            Group("nostdlib", CheckNoStdLibCompilation);
            Console.WriteLine("CLROS_M04_010_READY pageBytes=4096 wordBytes=8 hostOnly=true noncollecting=true" + " guestIntegrated=false runtimeExports=false gc=false gate4=Fail");
            Console.WriteLine("TEST_PASS m04.010");
        }
        catch (Exception error)
        {
            Console.WriteLine("TEST_FAIL m04.010 " + error.GetType().Name + ": " + error.Message);
            throw;
        }
    }

    private static void Group(string name, Action test)
    {
        HostCheck.Run("m04.010." + name, test);
    }

    private static void CheckContract()
    {
        Require(!default(CheckedReferenceBulkResult).Succeeded && default(CheckedReferenceBulkResult).BytesWritten == 0, "default result is not success");
        Require(sizeof(RhpNewFastHeapAccess) == 104 && sizeof(RhpNewFastBumpState) == 80 && sizeof(ManagedHeapPageRangeState) == 216 && sizeof(CheckedReferenceBulkResult) == 16, "literal POD sizes and unchanged original authority");
        Require(!RuntimeHelpers.IsReferenceOrContainsReferences<CheckedReferenceBulkResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<RhpNewFastHeapAccess>(), "no managed references in POD");
        using Fixture f = new();
        f.Pattern();
        byte[] before = f.Snapshot();
        Reject(Leaf.TryMove(default, default, ulong.MaxValue, 3, 1), Status.HeapAuthorityRejected, ManagedHeapPageRangeStatus.InvalidStorage);
        f.Unchanged(before);
        Reject(Leaf.TryStore(default, Profile, 0, 0), Status.HeapAuthorityRejected, ManagedHeapPageRangeStatus.InvalidStorage);
        f.Unchanged(before);
        Reject(Leaf.TryMove(default, Profile, 0, 0, 0), Status.HeapAuthorityRejected, ManagedHeapPageRangeStatus.InvalidStorage);
        f.Unchanged(before);
        foreach (CheckedReferenceBulkProfile profile in new[]
        {
            default(CheckedReferenceBulkProfile),
            (CheckedReferenceBulkProfile)2,
            (CheckedReferenceBulkProfile)255
        }

        )
        {
            Reject(Leaf.TryStore(f.Access, profile, 3, 0), Status.UnsupportedProfile);
            f.Unchanged(before);
            Reject(Leaf.TryMove(f.Access, profile, 3, 1, ulong.MaxValue), Status.UnsupportedProfile);
            f.Unchanged(before);
            Reject(Leaf.TryMove(f.Access, profile, 0, 0, 0), Status.UnsupportedProfile);
            f.Unchanged(before);
        }

        f.Unchanged(before);
    }

    private static void CheckStores()
    {
        using Fixture f = new();
        (ulong Word, byte[] Bytes)[] words = [(0UL, [0, 0, 0, 0, 0, 0, 0, 0]), (0x0123456789ABCDEFUL, [0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01]), (0x8000000000000000UL, [0, 0, 0, 0, 0, 0, 0, 0x80]), (ulong.MaxValue, [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]), (1UL, [1, 0, 0, 0, 0, 0, 0, 0])];
        foreach (bool secondPage in new[]
        {
            false,
            true
        }

        )
            foreach (ulong offset in new ulong[]
            {
                0,
                8,
                2040,
                4088
            }

            )
                foreach (var word in words)
                {
                    f.Reset(secondPage);
                    f.Pattern();
                    Require(f.PageOffset == (secondPage ? 0x6000 : 0x5000), "mapped page offset is real, not presumed backing start");
                    byte[] expected = f.Snapshot();
                    word.Bytes.CopyTo(expected, f.PageOffset + (int)offset);
                    Success(Leaf.TryStore(f.Access, Profile, offset, word.Word), 8);
                    f.Unchanged(expected);
                }
    }

    private static void CheckMoves()
    {
        using Fixture f = new();
        (ulong Source, ulong Destination, ulong Count)[] moves = [(0, 0, 0), (4096, 4096, 0), (4096, 0, 0), (0, 4096, 0), (0, 0, 4096), (8, 8, 4088), (4088, 4088, 8), (0, 8, 8), (8, 0, 8), (0, 16, 16), (16, 0, 16), (0, 8, 16), (8, 0, 16), (0, 8, 24), (8, 0, 24), (0, 8, 4088), (8, 0, 4088), (0, 2048, 2048), (2048, 0, 2048), (24, 2048, 1024), (2048, 24, 1024), (0, 4088, 8), (4088, 0, 8)];
        foreach (bool secondPage in new[]
        {
            false,
            true
        }

        )
            foreach (var move in moves)
            {
                f.Reset(secondPage);
                f.Pattern();
                byte[] before = f.Snapshot();
                byte[] expected = (byte[])before.Clone();
                // Independent immutable source bytes: no use of the leaf's word loop/direction.
                for (ulong i = 0; i < move.Count; i++)
                    expected[f.PageOffset + (int)(move.Destination + i)] = before[f.PageOffset + (int)(move.Source + i)];
                Success(Leaf.TryMove(f.Access, Profile, move.Source, move.Destination, move.Count), move.Source == move.Destination ? 0 : move.Count);
                f.Unchanged(expected);
            }
    }

    private static void CheckBounds()
    {
        using Fixture f = new();
        f.Pattern();
        byte[] before = f.Snapshot();
        foreach (ulong offset in new ulong[]
        {
            1,
            7,
            4095,
            4097,
            ulong.MaxValue
        }

        )
        {
            Reject(Leaf.TryStore(f.Access, Profile, offset, 1), Status.InvalidAlignment);
            f.Unchanged(before);
        }

        foreach (ulong offset in new ulong[]
        {
            4096,
            4104,
            0x100000000,
            ulong.MaxValue - 7
        }

        )
        {
            Reject(Leaf.TryStore(f.Access, Profile, offset, 1), Status.InvalidRange);
            f.Unchanged(before);
        }

        foreach (ulong offset in new ulong[]
        {
            1,
            7,
            4095,
            4097,
            ulong.MaxValue
        }

        )
        {
            Reject(Leaf.TryMove(f.Access, Profile, offset, 0, 1), Status.InvalidAlignment);
            f.Unchanged(before);
            Reject(Leaf.TryMove(f.Access, Profile, 0, offset, 0), Status.InvalidAlignment);
            f.Unchanged(before);
        }

        foreach (ulong length in new ulong[]
        {
            1,
            7,
            9,
            4095,
            4097,
            ulong.MaxValue
        }

        )
        {
            Reject(Leaf.TryMove(f.Access, Profile, 0, 0, length), Status.InvalidLength);
            f.Unchanged(before);
        }

        foreach (ulong length in new ulong[]
        {
            4104,
            0x100000000,
            0x8000000000000000,
            ulong.MaxValue - 7
        }

        )
        {
            Reject(Leaf.TryMove(f.Access, Profile, 0, 0, length), Status.InvalidRange);
            f.Unchanged(before);
        }

        foreach (ulong offset in new ulong[]
        {
            4104,
            0x100000000,
            0x8000000000000000,
            ulong.MaxValue - 7
        }

        )
        {
            Reject(Leaf.TryMove(f.Access, Profile, offset, 0, 0), Status.InvalidRange);
            f.Unchanged(before);
            Reject(Leaf.TryMove(f.Access, Profile, 0, offset, 0), Status.InvalidRange);
            f.Unchanged(before);
            Reject(Leaf.TryMove(f.Access, Profile, offset, offset, 8), Status.InvalidRange);
            f.Unchanged(before);
        }

        foreach (var move in new (ulong S, ulong D, ulong N)[]
        {
            (0, 8, 4096),
            (8, 0, 4096),
            (4088, 0, 16),
            (0, 4088, 16),
            (4096, 0, 8),
            (0, 4096, 8)
        }

        )
        {
            Reject(Leaf.TryMove(f.Access, Profile, move.S, move.D, move.N), Status.InvalidRange);
            f.Unchanged(before);
        }

        f.Unchanged(before);
    }

    private static void CheckStorage()
    {
        using Fixture f = new();
        for (int scenario = 0; scenario < 30; scenario++)
        {
            f.Reset();
            RhpNewFastHeapAccess a = f.Access;
            // Synthetic addresses are deliberately structurally invalid: original
            // full-span preflight must reject before dereferencing any metadata/page.
            switch (scenario)
            {
                case 0:
                    a.Allocator = null;
                    break;
                case 1:
                    a.Nodes = null;
                    break;
                case 2:
                    a.Reservations = null;
                    break;
                case 3:
                    a.Heap = null;
                    break;
                case 4:
                    a.Backing = null;
                    break;
                case 5:
                    a.AllocatorCapacity = 0;
                    break;
                case 6:
                    a.AllocatorCapacity = 2;
                    break;
                case 7:
                    a.NodeCount = 0;
                    break;
                case 8:
                    a.NodeCount = 3;
                    break;
                case 9:
                    a.NodeCapacity = ulong.MaxValue;
                    break;
                case 10:
                    a.ReservationCount = 0;
                    break;
                case 11:
                    a.ReservationCount = 4;
                    break;
                case 12:
                    a.ReservationCapacity = ulong.MaxValue;
                    break;
                case 13:
                    a.ParentIndex = 2;
                    break;
                case 14:
                    a.BackingCapacity = 4095;
                    break;
                case 15:
                    a.HeapCapacity = 215;
                    break;
                case 16:
                    a.Allocator = (BootPageAllocatorState*)((byte*)a.Allocator + 1);
                    break;
                case 17:
                    a.Nodes = (BootPageFreeListNode*)((byte*)a.Nodes + 1);
                    break;
                case 18:
                    a.Reservations = (BootReservedRange*)((byte*)a.Reservations + 1);
                    break;
                case 19:
                    a.Heap = (ManagedHeapPageRangeState*)((byte*)a.Heap + 1);
                    break;
                case 20:
                    a.Backing++;
                    break;
                case 21:
                    a.Allocator = (BootPageAllocatorState*)(ulong.MaxValue - 7);
                    break;
                case 22:
                    a.Nodes = (BootPageFreeListNode*)(ulong.MaxValue - 7);
                    break;
                case 23:
                    a.Reservations = (BootReservedRange*)(ulong.MaxValue - 7);
                    break;
                case 24:
                    a.Heap = (ManagedHeapPageRangeState*)(ulong.MaxValue - 7);
                    break;
                case 25:
                    a.Backing = (byte*)(ulong.MaxValue - 4095);
                    break;
                case 26:
                    a.BackingCapacity = ulong.MaxValue;
                    break;
                case 27:
                    a.HeapCapacity = ulong.MaxValue;
                    break;
                case 28:
                    a.NodeCapacity = 0;
                    break;
                case 29:
                    a.ReservationCapacity = 0;
                    break;
            }

            RejectBoth(f, a, ManagedHeapPageRangeStatus.InvalidStorage);
        }

        for (int scenario = 0; scenario < 6; scenario++)
        {
            f.Reset();
            RhpNewFastHeapAccess a = f.Access;
            switch (scenario)
            {
                case 0:
                    a.Allocator = (BootPageAllocatorState*)a.Nodes;
                    break;
                case 1:
                    a.Nodes = (BootPageFreeListNode*)a.Reservations;
                    break;
                case 2:
                    a.Reservations = (BootReservedRange*)a.Heap;
                    break;
                case 3:
                    a.Heap = (ManagedHeapPageRangeState*)a.Backing;
                    break;
                case 4:
                    a.NodeCapacity = 71;
                    break; // Full capacity crosses ledger despite only one live node.
                case 5:
                    a.HeapCapacity = 0x3001;
                    break; // Full capacity crosses backing, not just state bytes.
            }

            RejectBoth(f, a, ManagedHeapPageRangeStatus.StorageOverlap);
        }
    }

    private static void CheckIdentity()
    {
        using Fixture f = new();
        for (int scenario = 0; scenario < 12; scenario++)
        {
            f.Reset();
            RhpNewFastHeapAccess a = f.Access;
            switch (scenario)
            {
                case 0:
                    a.Allocator = (BootPageAllocatorState*)(f.Storage + 0x8000);
                    *a.Allocator = *f.Access.Allocator;
                    break;
                case 1:
                    a.Nodes = (BootPageFreeListNode*)(f.Storage + 0x8000);
                    *a.Nodes = *f.Access.Nodes;
                    break;
                case 2:
                    a.NodeCount = 2;
                    break;
                case 3:
                    a.NodeCapacity = 3;
                    break;
                case 4:
                    a.Reservations = (BootReservedRange*)(f.Storage + 0x8000);
                    a.Reservations[0] = f.Access.Reservations[0];
                    a.Reservations[1] = f.Access.Reservations[1];
                    break;
                case 5:
                    a.ReservationCount = 3;
                    break;
                case 6:
                    a.ReservationCapacity = 4;
                    break;
                case 7:
                    a.ParentIndex = 0;
                    break;
                case 8:
                    a.Backing = f.Storage + 0x8000;
                    break;
                case 9:
                    a.BackingCapacity = 8209;
                    break;
                case 10:
                    a.Heap = (ManagedHeapPageRangeState*)(f.Storage + 0x8000);
                    *a.Heap = *f.Access.Heap;
                    break;
                case 11:
                    a.HeapCapacity = 233;
                    break;
            }

            RejectBoth(f, a, ManagedHeapPageRangeStatus.IdentityMismatch);
        }
    // The thirteenth field (AllocatorCapacity) accepts exactly one; its
    // changed values are rejected by storage preflight above.
    }

    private static void CheckLiveAuthority()
    {
        using Fixture f = new();
        Success(Leaf.TryStore(f.Access, Profile, 0, 1), 8);
        f.Access.Heap[0] = default;
        RejectBoth(f, f.Access, ManagedHeapPageRangeStatus.NotReady);
        f.Reset();
        f.Access.Reservations[1] = Range(BootReservationKind.AllocatorArena, 0x300010000, 2);
        RejectBoth(f, f.Access, ManagedHeapPageRangeStatus.IdentityMismatch);
        f.Reset();
        *f.Access.Allocator = default;
        RejectBoth(f, f.Access, ManagedHeapPageRangeStatus.InvalidAllocator);
        f.Reset();
        f.Access.Nodes[0] = default;
        RejectBoth(f, f.Access, ManagedHeapPageRangeStatus.InvalidAllocator);
        f.Reset();
        *f.Access.Allocator = f.Access.Heap->InitialAllocatorState;
        f.Access.Nodes[0] = Node(0x300000000, 2);
        RejectBoth(f, f.Access, ManagedHeapPageRangeStatus.RetainedRangeIsFree);
        f.Reset();
        BootPageAllocatorOnePageResult other = BootPageAllocatorMutator.AllocateOnePage(*f.Access.Allocator, f.Access.Nodes, f.Access.NodeCount, f.Access.NodeCapacity, null, 0, 0, 1, 4096);
        Require(other.Succeeded, "real allocator consumes unrelated free page");
        *f.Access.Allocator = other.AfterState;
        byte[] expected = f.Snapshot();
        expected[f.PageOffset] = 1;
        Success(Leaf.TryStore(f.Access, Profile, 0, 1), 8);
        f.Unchanged(expected);
        BootPageAllocatorOnePageFreeResult freed = BootPageAllocatorMutator.FreeOnePage(*f.Access.Allocator, f.Access.Nodes, f.Access.NodeCount, f.Access.NodeCapacity, null, 0, 0, other);
        Require(freed.Succeeded, "real allocator releases only unrelated page");
        *f.Access.Allocator = freed.AfterState;
        expected = f.Snapshot();
        expected[f.PageOffset + 8] = 1;
        Success(Leaf.TryMove(f.Access, Profile, 0, 8, 8), 8);
        f.Unchanged(expected);
    }

    private static void CheckMixed()
    {
        using Fixture f = new();
        const NativeAotLayoutProfile layout = NativeAotLayoutProfile.Ilc11_0_0Rc3WinX64LittleEndian;
        Require(GuestTestRhpNewFast.InitializeOnce(f.Access, f.State, 96).Succeeded, "bind real sole cursor");
        for (int round = 0; round < 8; round++)
        {
            ulong start = (ulong)round * 112;
            NativeAotMethodTablePrefix fixedType = new(0x50000000, 32, 0, 0, 0, 0);
            NativeAotMethodTablePrefix arrayType = new(0xE0020001, 24, 0, 0, 0, 0);
            NativeAotMethodTablePrefix stringType = new(0xD0000002, 22, 0, 0, 0, 0);
            RhpNewFastResult one = GuestTestRhpNewFast.TryAllocate(f.Access, f.State, 96, layout, fixedType, 0x1234567800000008);
            RhpNewFastResult two = GuestTestArrayAllocationLengthOverflowChecks.TryAllocate(f.Access, f.State, 96, layout, arrayType, 0x1234567800000108, 16);
            RhpNewFastResult three = GuestTestStringAllocationCharacterStorage.TryAllocate(f.Access, f.State, 96, layout, stringType, 0x1234567800000208, 6);
            Require(one.AllocationSucceeded && two.AllocationSucceeded && three.AllocationSucceeded && one.BytesConsumed == 32 && two.BytesConsumed == 40 && three.BytesConsumed == 40 && f.State->UsedBytes == start + 112 && f.State->AllocationCount == (ulong)(round + 1) * 3, "literal mixed cursor increments");
            byte[] expected = f.Snapshot();
            byte[] word = [0xFE, 0xDC, 0xBA, 0x98, 0x76, 0x54, 0x32, 0x10];
            word.CopyTo(expected, f.PageOffset + (int)start + 16);
            Success(Leaf.TryStore(f.Access, Profile, start + 16, 0x1032547698BADCFE), 8);
            f.Unchanged(expected);
            word.CopyTo(expected, f.PageOffset + (int)start + 56);
            Success(Leaf.TryMove(f.Access, Profile, start + 16, start + 56, 8), 8);
            f.Unchanged(expected); // Includes original cursor/count and complete String bytes/terminator.
            Success(Leaf.TryMove(f.Access, Profile, 0, 0, 4096), 0);
            f.Unchanged(expected);
        }
    }

    private static void CheckZeroAllocation()
    {
        using Fixture f = new();
        bool okay = true;
        for (int i = 0; i < 64; i++)
        {
            Leaf.TryStore(f.Access, Profile, 0, 1);
            Leaf.TryMove(f.Access, Profile, 0, 8, 16);
            Leaf.TryMove(f.Access, Profile, 8, 0, 16);
            Leaf.TryMove(default, Profile, 0, 0, 0);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; i++)
        {
            okay &= Leaf.TryStore(f.Access, Profile, 0, (ulong)i).Succeeded;
            okay &= Leaf.TryMove(f.Access, Profile, 0, 8, 16).Succeeded;
            okay &= Leaf.TryMove(f.Access, Profile, 8, 0, 16).Succeeded;
            okay &= Leaf.TryMove(f.Access, Profile, 4096, 0, 0).Succeeded;
            okay &= Leaf.TryMove(f.Access, Profile, 0, 0, 4096).BytesWritten == 0;
            okay &= !Leaf.TryMove(default, Profile, 0, 0, 0).Succeeded;
            okay &= !Leaf.TryStore(f.Access, Profile, ulong.MaxValue - 7, 0).Succeeded;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Require(okay && allocated == 0, "warmed successful and rejected leaf operations allocate zero CLR bytes");
    }

    private static void Success(CheckedReferenceBulkResult result, ulong bytes) => Require(result.Succeeded && result.Status == Status.Success && result.HeapStatus == ManagedHeapPageRangeStatus.Success && result.BytesWritten == bytes, "exact success result");
    private static void Reject(CheckedReferenceBulkResult result, Status status, ManagedHeapPageRangeStatus heap = default) => Require(!result.Succeeded && result.Status == status && result.HeapStatus == heap && result.BytesWritten == 0, "rejection expected " + status + "/" + heap + " observed " + result.Status + "/" + result.HeapStatus);
    private static void RejectBoth(Fixture f, RhpNewFastHeapAccess access, ManagedHeapPageRangeStatus heap)
    {
        f.Pattern(); // The real fixture page stays addressable even when its synthetic authority is invalid.
        byte[] before = f.Snapshot();
        Reject(Leaf.TryStore(access, Profile, 0, 0xFEDCBA9876543210), Status.HeapAuthorityRejected, heap);
        f.Unchanged(before);
        Reject(Leaf.TryMove(access, Profile, 0, 8, 8), Status.HeapAuthorityRejected, heap);
        f.Unchanged(before);
        Reject(Leaf.TryMove(access, Profile, 0, 0, 0), Status.HeapAuthorityRejected, heap);
        f.Unchanged(before);
        Reject(Leaf.TryMove(access, default, 3, 1, ulong.MaxValue), Status.HeapAuthorityRejected, heap);
        f.Unchanged(before);
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
        string directory = Path.Combine(root, "artifacts", "issue79", "nostdlib", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(root, "global.json"), Path.Combine(directory, "global.json"));
        string[] sources = ["src/OS/Runtime/MethodTable.cs",
            "src/OS/Runtime/RuntimeFailFast.cs",
            "src/OS/Runtime/Object.cs",
            "src/OS/Runtime/Void.cs",
            "src/OS/Runtime/Boolean.cs",
            "src/OS/Runtime/Char.cs",
            "src/OS/Runtime/SByte.cs",
            "src/OS/Runtime/Byte.cs",
            "src/OS/Runtime/Int16.cs",
            "src/OS/Runtime/UInt16.cs",
            "src/OS/Runtime/Int32.cs",
            "src/OS/Runtime/UInt32.cs",
            "src/OS/Runtime/Int64.cs",
            "src/OS/Runtime/UInt64.cs",
            "src/OS/Runtime/IntPtr.cs",
            "src/OS/Runtime/UIntPtr.cs",
            "src/OS/Runtime/Single.cs",
            "src/OS/Runtime/Double.cs",
            "src/OS/Runtime/ValueType.cs",
            "src/OS/Runtime/Enum.cs",
            "src/OS/Runtime/Type.cs",
            "src/OS/Runtime/RuntimeType.cs",
            "src/OS/Runtime/Array.cs",
            "src/OS/Runtime/Delegate.cs",
            "src/OS/Runtime/MulticastDelegate.cs",
            "src/OS/Runtime/String.cs",
            "src/OS/Runtime/Attribute.cs",
            "src/OS/Runtime/Buffer.cs",
            "src/OS/Runtime/SpanHelpers.cs",
            "src/OS/Runtime/AttributeTargets.cs",
            "src/OS/Runtime/AttributeUsageAttribute.cs",
            "src/OS/Runtime/RuntimeTypeHandle.cs",
            "src/OS/Runtime/RuntimeMethodHandle.cs",
            "src/OS/Runtime/RuntimeFieldHandle.cs",
            "src/OS/Runtime/RuntimeExportAttribute.cs",
            "src/OS/Runtime/X64StackProbeRuntime.cs",
            "src/OS/Runtime/TypeCast.cs",
            "src/OS/Runtime/RuntimeFeature.cs",
            "src/OS/Runtime/RuntimeHelpers.cs",
            "src/OS/Runtime/InAttribute.cs",
            "src/OS/Runtime/UnmanagedType.cs",
            "src/OS/Runtime/UnmanagedCallersOnlyAttribute.cs",
            "src/OS/Runtime/IDynamicInterfaceCastable.cs",
            "src/OS/Runtime/ThrowHelpers.cs",
            "src/OS/Runtime/StartupCodeHelpers.cs",
            "src/OS/Runtime/IntrinsicAttribute.cs",
            "src/OS/Runtime/Unsafe.cs",
            "src/OS/Runtime/MinimalRuntime.Array.cs",
            "src/OS/Runtime/Nullable.cs",
            "src/OS/Runtime/RhpNewFastRuntime.cs",
            "src/OS/Runtime/DefaultMemberAttribute.cs",
            "src/OS/Runtime/IndexerNameAttribute.cs",
            "src/OS/Runtime/Exception.cs",
            "src/OS/Runtime/RuntimeImports.cs", "src/OS/Runtime/MethodImplAttribute.MethodImplOptions.cs",
            "src/OS/Runtime/MethodImplAttribute.cs", "src/OS.Kernel/Boot/AddressValueStatus.cs",
            "src/OS.Kernel/Boot/PhysicalAddress.cs",
            "src/OS.Kernel/Boot/VirtualAddress.cs",
            "src/OS.Kernel/Boot/DeviceAddress.cs",
            "src/OS.Kernel/Boot/AddressValueOperations.cs", "src/OS.Kernel/Memory/BootReservedRange.BootReservationKind.cs",
            "src/OS.Kernel/Memory/BootReservedRange.BootReservedRangeStatus.cs",
            "src/OS.Kernel/Memory/BootReservedRange.cs", "src/OS.Kernel/Memory/BootReservationTableValidator.BootReservationTableStatus.cs",
            "src/OS.Kernel/Memory/BootReservationTableValidator.BootReservationTableValidationResult.cs",
            "src/OS.Kernel/Memory/BootReservationTableValidator.cs",
            "src/OS.Kernel/Memory/BootReservationTableValidator.ValidationState.cs", "src/OS.Kernel/Memory/BootPageFreeListNode.cs",
            "src/OS.Kernel/Memory/BootPageAllocatorCoreContracts.BootPageAllocatorState.cs", "src/OS.Kernel/Memory/BootPageAllocatorMutationStatus.cs",
            "src/OS.Kernel/Memory/BootPageAllocatorOnePageRestoreReceipt.cs",
            "src/OS.Kernel/Memory/BootPageAllocatorOnePageCoalescingKind.cs",
            "src/OS.Kernel/Memory/BootPageAllocatorOnePageResult.cs",
            "src/OS.Kernel/Memory/BootPageAllocatorOnePageFreeResult.cs",
            "src/OS.Kernel/Memory/BootPageAllocatorMutationResult.cs",
            "src/OS.Kernel/Memory/BootPageAllocatorMutator.cs", "src/OS.Kernel/Runtime/ManagedHeapPageRangeLiveAllocator.ManagedHeapPageRangeStatus.cs",
            "src/OS.Kernel/Runtime/ManagedHeapPageRangeLiveAllocator.ManagedHeapPageRangeLifecycle.cs",
            "src/OS.Kernel/Runtime/ManagedHeapPageRangeLiveAllocator.ManagedHeapPageRangeState.cs",
            "src/OS.Kernel/Runtime/ManagedHeapPageRangeLiveAllocator.ManagedHeapPageRangeResult.cs",
            "src/OS.Kernel/Runtime/ManagedHeapPageRangeLiveAllocator.cs", "src/OS.Kernel/Runtime/ObjectArrayStringEETypeMethodTableLayoutContracts.NativeAotLayoutProfile.cs",
            "src/OS.Kernel/Runtime/ObjectArrayStringEETypeMethodTableLayoutContracts.NativeAotLayoutShape.cs",
            "src/OS.Kernel/Runtime/ObjectArrayStringEETypeMethodTableLayoutContracts.NativeAotLayoutStatus.cs",
            "src/OS.Kernel/Runtime/ObjectArrayStringEETypeMethodTableLayoutContracts.NativeAotMethodTablePrefix.cs",
            "src/OS.Kernel/Runtime/ObjectArrayStringEETypeMethodTableLayoutContracts.NativeAotLayoutResult.cs",
            "src/OS.Kernel/Runtime/ObjectArrayStringEETypeMethodTableLayoutContracts.cs", "src/OS.Kernel/Runtime/GuestTestRhpNewFast.RhpNewFastHeapAccess.cs",
            "src/OS.Kernel/Runtime/GuestTestRhpNewFast.RhpNewFastBumpState.cs",
            "src/OS.Kernel/Runtime/GuestTestRhpNewFast.RhpNewFastStatus.cs",
            "src/OS.Kernel/Runtime/GuestTestRhpNewFast.RhpNewFastOperation.cs",
            "src/OS.Kernel/Runtime/GuestTestRhpNewFast.RhpNewFastResult.cs",
            "src/OS.Kernel/Runtime/GuestTestRhpNewFast.cs",
            "src/OS.Kernel/Runtime/GuestTestRhpNewFast.AllocationRecord.cs", "src/OS.Kernel/Runtime/CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.CheckedReferenceBulkProfile.cs",
            "src/OS.Kernel/Runtime/CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.CheckedReferenceBulkStatus.cs",
            "src/OS.Kernel/Runtime/CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.CheckedReferenceBulkResult.cs",
            "src/OS.Kernel/Runtime/CheckedReferenceBulkBarriersIncludingDotNetN10Bulk.cs"];
        string includes = string.Join(Environment.NewLine, sources.Select(p => "<Compile Include=\"" + System.Security.SecurityElement.Escape(Path.Combine(root, p)) + "\" />"));
        File.WriteAllText(Path.Combine(directory, "Compile.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net11.0</TargetFramework><LangVersion>14.0</LangVersion>
                <OutputType>Library</OutputType><AssemblyName>CLROS.M04.CheckedReference.NoStdLib</AssemblyName>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems><AllowUnsafeBlocks>true</AllowUnsafeBlocks>
                <NoStdLib>true</NoStdLib><DefineConstants>NOSTDLIB_EFI</DefineConstants><NoConfig>true</NoConfig>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo><GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
                <ComputeNETCoreBuildOutputFiles>false</ComputeNETCoreBuildOutputFiles><RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
                <ProduceReferenceAssembly>false</ProduceReferenceAssembly><EnableNETAnalyzers>false</EnableNETAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild><TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <DebugType>none</DebugType><RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources><UseSharedCompilation>false</UseSharedCompilation>
              </PropertyGroup>
              <ItemGroup>{{includes}}</ItemGroup>
              <Target Name="RemoveFrameworkReferences" BeforeTargets="BeforeCompile" AfterTargets="FindReferenceAssembliesForReferences">
                <ItemGroup><ReferencePathWithRefAssemblies Remove="@(ReferencePathWithRefAssemblies)" />
                  <ReferencePath Remove="@(ReferencePath)" /></ItemGroup>
              </Target>
            </Project>
            """, new UTF8Encoding(false));
        var identities = sources.Select(p => new { Path = p, Bytes = new FileInfo(Path.Combine(root, p)).Length, Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, p)))) }).ToArray();
        File.WriteAllText(Path.Combine(directory, "source-identities.json"), System.Text.Json.JsonSerializer.Serialize(identities), new UTF8Encoding(false));
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        string[] args = ["build", "Compile.csproj", "--nologo", "-m:1", "-c", "Release", "-nodeReuse:false"];
        foreach (string arg in args)
            start.ArgumentList.Add(arg);
        File.WriteAllText(Path.Combine(directory, "command.json"), System.Text.Json.JsonSerializer.Serialize(new { FileName = "dotnet", Arguments = args, WorkingDirectory = directory }), new UTF8Encoding(false));
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("no-stdlib compile start");
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
        File.WriteAllText(Path.Combine(directory, "build.exitcode.txt"), process.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Require(exited && process.ExitCode == 0, "actual-source no-stdlib compile: " + directory + " " + stdout.Result + stderr.Result);
        Require(stderr.Result.Length == 0 && System.Text.RegularExpressions.Regex.Matches(stdout.Result, @"(?m)^\s*0 Warning\(s\)\s*$").Count == 1 && System.Text.RegularExpressions.Regex.Matches(stdout.Result, @"(?m)^\s*0 Error\(s\)\s*$").Count == 1, "exact clean compiler summaries");
        foreach (var input in identities)
            Require(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root, input.Path)))) == input.Sha256, "no-stdlib source unchanged: " + input.Path);
        using var stream = File.OpenRead(Path.Combine(directory, "bin", "Release", "net11.0", "CLROS.M04.CheckedReference.NoStdLib.dll"));
        using PEReader image = new(stream);
        MetadataReader metadata = image.GetMetadataReader();
        Require(metadata.AssemblyReferences.Count == 0, "actual eleven-source closure has zero AssemblyRefs");
        TypeDefinition leaf = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition).Single(t => metadata.GetString(t.Name) == "CheckedReferenceBulkBarriersIncludingDotNetN10Bulk");
        foreach (string name in new[]
        {
            "TryStore",
            "TryMove",
            "Retained",
            "WithinPage"
        }

        )
        {
            MethodDefinition method = leaf.GetMethods().Select(metadata.GetMethodDefinition).Single(m => metadata.GetString(m.Name) == name);
            Require(method.RelativeVirtualAddress != 0 && image.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!.Length > 0, "actual leaf body " + name);
        }

        File.WriteAllText(Path.Combine(directory, "metadata.json"), "{\"Sources\":11,\"AssemblyReferences\":0,\"ActualLeafBodies\":4}", new UTF8Encoding(false));
        Console.WriteLine("M04_CHECKED_REFERENCE_HOST_NOSTDLIB inputs=11 assemblyReferences=0");
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
