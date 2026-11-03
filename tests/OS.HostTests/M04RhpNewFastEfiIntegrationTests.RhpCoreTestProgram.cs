using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
#if RHP_CORE_TEST
using OS.Boot;
using OS.Kernel.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using OS.Runtime;

// Compiles the actual private POD core. No CLR object address or privileged path is invoked.
internal static unsafe partial class RhpCoreTestProgram
{

    private static int Main()
    {
        Run("original authority and byte observations", Allocation);
        Run("bounded PE and exact token", ImageBounds);
        Run("storage rejection and all-byte preservation", Rejections);
        Run("actual leaf rejection and exhaustion", Exhaustion);
        Run("POD and zero managed allocation", Pod);
        Console.WriteLine("TEST_PASS rhp.core.tests");
        return 0;
    }
    private static void Run(string name, Action test)
    {
        Console.WriteLine("TEST_START rhp.core " + name);
        try
        {
            test();
            Console.WriteLine("TEST_PASS rhp.core " + name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("TEST_FAIL rhp.core " + name + " " + ex.Message);
            throw;
        }
    }
    private static void Need(bool value, string detail)
    {
        if (!value)
            throw new InvalidOperationException(detail);
    }

    private static void Allocation()
    {
        using F f = new();
        byte[] before = f.Snapshot();
        Need(f.Prepare() == RhpAdmissionStatus.Success, "prepare actual authority");
        f.Only(before, (0x3000, 104), (0x3200, 96), (0x4000, 6672));
        before = f.Snapshot();
        Need(f.Bind().Succeeded, "bind actual allocator");
        f.Only(before, (0x3100, 80));
        Need(f.Bind().Status == RhpNewFastStatus.AlreadyInitialized, "repeat actual bind rejects");
        ulong used = 0;
        for (ulong id = 1; id <= 10; id++)
        {
            uint size = f.Size(id);
            // The actual allocator must clear dirty selected bytes without touching earlier objects.
            new Span<byte>(f.Page + used, (int)size).Fill(0xBC);
            before = f.Snapshot();
            RhpAllocationAttempt r = f.Allocate(id);
            Need(r.Succeeded && r.Leaf.MappedAllocationStart == (ulong)f.Page + used &&
                r.Leaf.ObjectReference == (ulong)f.Page + used + 8 && r.Leaf.BytesConsumed == size &&
                r.Leaf.UsedBefore == used && r.Leaf.UsedAfter == used + size && r.Leaf.AllocationCount == id &&
                r.Observation.ZeroOr == 0 && r.Observation.ZeroBytes == size - 8 &&
                r.Observation.Sync == 0 && r.Observation.ObservedMt == f.Token(id), "independent result and observed bytes id=" + id);
            byte[] expected = new byte[size];
            BinaryPrimitives.WriteUInt64LittleEndian(expected.AsSpan(8, 8), f.Token(id));
            Need(new ReadOnlySpan<byte>(f.Page + used, (int)size).SequenceEqual(expected), "complete independent object byte oracle");
            f.Only(before, (0x3100, 80), (0x3200, 96), (0x4000, 6672), (0x8000 + (int)used, (int)size));
            Need(f.Complete(id, r.Leaf.ObjectReference) == RhpAdmissionStatus.Success, "typed-observation seam complete");
            used += size;
        }
        Need(used == 272 && f.Bump->AllocationCount == 10 && f.Control->Completed == 10, "literal representative fixture extent");
        Need(f.Check() == RhpAdmissionStatus.Success && f.Retained().IsReady && !f.Retained().ContentsAreZeroNow,
            "ownership retained with object content");
        f.Page[16] ^= 1;
        before = f.Snapshot();
        Need(f.Allocate(1).Status == RhpAdmissionStatus.PriorObjectChanged, "prior scalar change rejected before next allocation");
        f.Unchanged(before);
    }

    private static void ImageBounds()
    {
        using F f = new();
        Need(f.Prefix(f.Token(1), f.Token(1)), "valid loaded PE prefix");
        Need(!f.Prefix(ulong.MaxValue - 3, f.Token(1)), "unknown token rejected without dereference");
        Need(!f.Prefix(0, 0), "zero token");
        byte[] pristine = new ReadOnlySpan<byte>(f.Image, 16384).ToArray();
        (int Offset, uint Value)[] invalid = [
            (0,0),(60,0xFFFFFFF0),(0x80,0),(0x84,0x00008664),(0x84,0x00098664),
            (0x84,0x0001014C),(0x94,0),(0x98,0x10B),(0xD0,0x4001),(0xD4,0x100),
            (0x190,0xFFFFF000),(0x194,0x300),(0x194,0x3FF0),(0x198,0x110),
            (0x1AC,0x00000040),(0x1AC,0xC0000040),(0x1AC,0x60000040),(0x1AC,0x42000040)
        ];
        foreach (var mutation in invalid)
        {
            pristine.CopyTo(new Span<byte>(f.Image, 16384));
            W32(f.Image + mutation.Offset, mutation.Value);
            Need(!f.Prefix(f.Token(1), f.Token(1)), "PE mutation rejected at " + mutation.Offset.ToString("X"));
        }
        pristine.CopyTo(new Span<byte>(f.Image, 16384));
        W32(f.Image + 0x198, 0x2000);
        Need(f.Prefix(f.Token(1), f.Token(1)), "raw size may exceed virtual size");
        W32(f.Image + 0x198, 0x800);
        Need(f.Prefix(f.Token(1), f.Token(1)), "virtual size may exceed raw size outside chosen prefix");
        Need(!f.Prefix((ulong)f.Image + 0x17F0, (ulong)f.Image + 0x17F0), "whole prefix must fit initialized raw extent");
        pristine.CopyTo(new Span<byte>(f.Image, 16384));
        W32(f.Image + 0x84, 0x00028664); // Two nonempty sections may not overlap, even away from chosen token.
        new ReadOnlySpan<byte>(f.Image + 0x188, 40).CopyTo(new Span<byte>(f.Image + 0x1B0, 40));
        Need(!f.Prefix(f.Token(1), f.Token(1)), "ambiguous overlapping sections");
        W32(f.Image + 0x1BC, 0x2000);
        Need(f.Prefix(f.Token(1), f.Token(1)), "disjoint second section");
        W32(f.Image + 0x84, 0x00088664);
        Need(f.Prefix(f.Token(1), f.Token(1)), "eight bounded sections including empty sections");
        W32(f.Image + 0x84, 0x00098664);
        Need(!f.Prefix(f.Token(1), f.Token(1)), "ninth section fails closed");
    }

    private static void Rejections()
    {
        using F f = new();
        // Invoke the actual private rejection reader; exercise every bump byte,
        // including authority fields outside UsedBytes/AllocationCount.
        var check = typeof(RhpNewFastAllocationCore).GetMethod("RejectedAllocationUnchanged",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .CreateDelegate<RejectionReadback>();
        byte* bumpBefore = stackalloc byte[80];
        byte* pageBefore = stackalloc byte[4096];
        new ReadOnlySpan<byte>((byte*)f.Bump, 80).CopyTo(new Span<byte>(bumpBefore, 80));
        new ReadOnlySpan<byte>(f.Page, 4096).CopyTo(new Span<byte>(pageBefore, 4096));
        Need(check(f.Bump, bumpBefore, f.Page, pageBefore), "complete rejection snapshot accepts unchanged bytes");
        for (int i = 0; i < 80; i++)
        {
            ((byte*)f.Bump)[i] ^= 1;
            Need(!check(f.Bump, bumpBefore, f.Page, pageBefore), "bump rejection readback byte " + i);
            ((byte*)f.Bump)[i] ^= 1;
        }
        foreach (int i in new[] { 0, 7, 8, 79, 4095 })
        {
            f.Page[i] ^= 1;
            Need(!check(f.Bump, bumpBefore, f.Page, pageBefore), "page rejection readback byte " + i);
            f.Page[i] ^= 1;
        }
        for (int mutation = 0; mutation < 11; mutation++)
        {
            f.Reset();
            RhpNewFastHeapAccess a = f.Original;
            RhpNewFastHeapAccess* access = f.Access;
            RhpNewFastBumpState* bump = f.Bump;
            RhpAllocationControl* control = f.Control;
            RhpObservationStorage* records = f.Records;
            switch (mutation)
            {
                case 0:
                    a.BackingCapacity = 4096;
                    break;
                case 1:
                    a.ReservationCount = 4;
                    break;
                case 2:
                    a.NodeCapacity = 2;
                    break;
                case 3:
                    a.HeapCapacity = 232;
                    break;
                case 4:
                    a.ParentIndex = 5;
                    break;
                case 5:
                    access = (RhpNewFastHeapAccess*)f.Control;
                    break;
                case 6:
                    bump = (RhpNewFastBumpState*)f.Page;
                    break;
                case 7:
                    records = (RhpObservationStorage*)((byte*)f.Control + 8);
                    break;
                case 8:
                    control = (RhpAllocationControl*)((byte*)f.Control + 1);
                    break;
                case 9:
                    a.AllocatorCapacity = 0;
                    break;
                case 10:
                    records = (RhpObservationStorage*)(ulong.MaxValue - 7);
                    break;
            }
            byte[] before = f.Snapshot();
            Need(RhpNewFastAllocationCore.Prepare(a, access, bump, control, records, f.Image, 16384) != RhpAdmissionStatus.Success, "invalid numeric storage " + mutation);
            f.Unchanged(before);
        }
        foreach (int slot in new[] { 0x3000, 0x3100, 0x3200, 0x4000, 0x4000 + 6671 })
        {
            f.Reset();
            f.Storage[slot] = 1;
            byte[] before = f.Snapshot();
            Need(f.Prepare() == RhpAdmissionStatus.AlreadyPrepared, "every scratch prefix must start zero");
            f.Unchanged(before);
        }
        f.Reset();
        f.Page[4095] = 1;
        byte[] dirty = f.Snapshot();
        Need(f.Prepare() == RhpAdmissionStatus.RetainedRejected, "whole-page zero check");
        f.Unchanged(dirty);
        f.Reset();
        Need(f.Prepare() == RhpAdmissionStatus.Success && f.Bind().Succeeded, "prepared rejection fixture");
        for (int mutation = 0; mutation < 5; mutation++)
        {
            ulong oldBefore = f.Control->CanaryBefore, oldAfter = f.Records->CanaryAfter;
            ulong oldHeap = f.Original.Heap->SelfAddress; // No writes to private host leaf fields.
            if (mutation == 0)
                f.Control->InProgress = 1;
            if (mutation == 1)
                f.Control->CanaryBefore ^= 1;
            if (mutation == 2)
                f.Records->CanaryAfter ^= 1;
            if (mutation == 3)
                f.Access->BackingCapacity = 4096;
            if (mutation == 4)
                f.Control->Completed = 11;
            byte[] before = f.Snapshot();
            Need(!f.Allocate(1).Succeeded, "reentry/canary/authority rejection");
            f.Unchanged(before);
            f.Control->InProgress = 0;
            f.Control->CanaryBefore = oldBefore;
            f.Records->CanaryAfter = oldAfter;
            f.Access->BackingCapacity = 8192;
            f.Control->Completed = 0;
            Need(f.Original.Heap->SelfAddress == oldHeap, "original heap never relocated");
        }
        byte[] snapshot = f.Snapshot();
        Need(f.Allocate(1, token: 1).Status == RhpAdmissionStatus.UnknownToken, "finite exact token first");
        f.Unchanged(snapshot);
        Need(f.Allocate(2).Status == RhpAdmissionStatus.InvalidFixture, "fixture order");
        f.Unchanged(snapshot);
        *f.Original.Reservations = default;
        snapshot = f.Snapshot();
        Need(f.Allocate(1).Status == RhpAdmissionStatus.RetainedRejected, "actual live parent ledger revalidation");
        f.Unchanged(snapshot);
    }

    private static void Exhaustion()
    {
        using F f = new();
        Need(f.Prepare() == RhpAdmissionStatus.Success, "before bind setup");
        byte[] page = f.PageSnapshot(), bump = f.BumpSnapshot();
        RhpAllocationAttempt beforeBind = f.Allocate(1);
        Need(beforeBind.Status == RhpAdmissionStatus.AllocationRejected && beforeBind.Leaf.Status == RhpNewFastStatus.NotInitialized,
            "actual frozen leaf status5 before bind");
        f.PageUnchanged(page);
        f.BumpUnchanged(bump);
        Need(f.Bind().Succeeded, "bind after controlled rejected attempt");
        for (ulong id = 1; id <= 10; id++)
        {
            var r = f.Allocate(id);
            Need(r.Succeeded && f.Complete(id, r.Leaf.ObjectReference) == RhpAdmissionStatus.Success, "fixture complete");
        }
        while (24 <= 4096 - f.Bump->UsedBytes)
        {
            var r = f.Allocate(1);
            Need(r.Succeeded, "filler actual allocation");
            W64((byte*)r.Leaf.ObjectReference + 8, 5555);
        }
        page = f.PageSnapshot();
        bump = f.BumpSnapshot();
        var exhausted = f.Allocate(1);
        Need(exhausted.Status == RhpAdmissionStatus.AllocationRejected && exhausted.Leaf.Status == RhpNewFastStatus.Exhausted &&
            f.Bump->UsedBytes == 4088 && f.Bump->AllocationCount == 169, "actual frozen leaf status15 at exact remaining8");
        f.PageUnchanged(page);
        f.BumpUnchanged(bump);
        Need(f.Check() == RhpAdmissionStatus.Success, "ten prior object records remain intact after exhaustion");
    }

    private static void Pod()
    {
        Need(sizeof(RhpNewFastHeapAccess) == 104 && sizeof(RhpNewFastBumpState) == 80 && sizeof(RhpAllocationControl) == 96 &&
            sizeof(RhpObjectObservation) == 256 && sizeof(RhpObservationStorage) == 6672, "exact POD storage sizes");
        Need(!RuntimeHelpers.IsReferenceOrContainsReferences<RhpAllocationControl>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<RhpObservationStorage>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<RhpAllocationAttempt>(), "no hidden managed references");
        using F f = new();
        Need(f.Prepare() == RhpAdmissionStatus.Success && f.Bind().Succeeded, "zero allocation fixture");
        for (int i = 0; i < 20; i++)
            Need(f.Check() == RhpAdmissionStatus.Success, "warm check");
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            if (f.Check() != RhpAdmissionStatus.Success || !f.Prefix(f.Token(1), f.Token(1)))
                throw new InvalidOperationException("unexpected warm failure");
        }
        long after = GC.GetAllocatedBytesForCurrentThread();
        Need(after == before, "zero allocation retained/prefix core");
        before = GC.GetAllocatedBytesForCurrentThread();
        for (ulong id = 1; id <= 10; id++)
        {
            var allocated = f.Allocate(id);
            if (!allocated.Succeeded || f.Complete(id, allocated.Leaf.ObjectReference) != RhpAdmissionStatus.Success)
                throw new InvalidOperationException("unexpected allocation core failure");
        }
        after = GC.GetAllocatedBytesForCurrentThread();
        Need(after == before, "zero managed allocation in actual allocation core");
        Console.WriteLine("RHP_CORE_POD access=104 bump=80 control=96 observation=256 records=6672 allocationDelta=" + (after - before));
    }

    private static void W32(byte* p, uint x) => BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(p, 4), x);
    private static void W64(byte* p, ulong x) => BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(p, 8), x);

}
#else
#endif
