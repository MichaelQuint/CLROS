using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Runtime;
using OS.Kernel.Memory;
using Alloc = OS.Kernel.Runtime.GuestTestRhpNewFast;
using Core = OS.Kernel.Runtime.BoundedNonMovingCollectorExplicitRootEnumeration;
using Status = OS.Kernel.Runtime.CollectorTypeMapStatus;

internal static unsafe class BoundedNonMovingCollectorExplicitRootEnumerationTests
{
    [HostFixture(HostLane.Fast)]
    internal static void Run()
    {
        HostCheck.Run("m04.013.reference_maps", () =>
        {
            using Fixture f = new();
            Require(!default(CollectorTypeMapResult).Succeeded &&
                !default(CollectorTypeMapResult).TryGetSeries(0, out _), "default denied");
            f.Type(0x50000000, 24, 0);
            Require(f.Inspect().SeriesCount == 0 && f.Inspect().Succeeded, "pointer-free fixed class");
            f.Type(0x51000000, 24, 1);
            f.Series(0, -16, 8);
            CollectorTypeMapResult one = f.Inspect();
            Require(one.Succeeded && one.SeriesCount == 1 && one.Prefix.BaseSize == 24 &&
                one.MethodTable == f.Mt && one.TryGetSeries(0, out var series) &&
                series.OffsetFromReference == 8 && series.Bytes == 8 && !one.TryGetSeries(1, out _),
                "ordinary compiler-format reference map");

            f.Type(0x01000000, 24, 1); f.Series(0, -16, 8);
            var abbreviated = f.Inspect(true, new(f.Mt - 24, 40));
            Require(abbreviated.Succeeded && abbreviated.SeriesCount == 1, "exact sixteen-byte static MT window");
            Require(f.Inspect(false, new(f.Mt - 24, 40)).Status == Status.InvalidMethodTable,
                "ordinary prefix cannot read beyond the caller window");

            f.Type(0x51000000, 80, 4);
            ulong[] offsets = [56, 8, 40, 24];
            for (int i = 0; i < 4; i++) f.Series(i, -72, offsets[i]);
            CollectorTypeMapResult four = f.Inspect();
            Require(four.Succeeded && four.SeriesCount == 4, "four disjoint series in metadata order");
            for (uint i = 0; i < 4; i++)
                Require(four.TryGetSeries(i, out series) && series.OffsetFromReference == offsets[i] &&
                    series.Bytes == 8, "every series decoded");
            f.Series(3, -72, 40); f.Expect(Status.OverlappingSeries);

            foreach (long count in new long[] { 0, -1, long.MinValue, 5, long.MaxValue })
            { f.Type(0x51000000, 24, count); f.Expect(Status.UnsupportedSeriesCount); }
            foreach ((long adjusted, ulong offset) in new (long, ulong)[]
            { (long.MinValue, 8), (long.MaxValue, 8), (-24, 8), (-15, 8), (-17, 8), (-16, 0), (-16, 9), (-16, 16), (-16, ulong.MaxValue) })
            { f.Type(0x51000000, 24, 1); f.Series(0, adjusted, offset); f.Expect(Status.InvalidDescriptor); }
            foreach (uint flags in new uint[] { 0x51100000, 0xD1000008, 0x61000008, 0x51200000 })
            { f.Type(flags, 24, 1); f.Series(0, -16, 8); f.Expect(Status.UnsupportedType); }
            foreach (uint size in new uint[] { 0, 16, 25, 4104, uint.MaxValue })
            { f.Type(0x51000000, size, 1); f.Expect(Status.UnsupportedType); }

            f.Type(0x51000000, 24, 1); f.Series(0, -16, 8);
            Require(f.Inspect(false, new(f.Mt - 8, 32)).Status == Status.InvalidDescriptor,
                "complete descriptor extent required");
            Require(f.Inspect(false, new(ulong.MaxValue - 7, 16)).Status == Status.InvalidWindow,
                "window overflow denied before dereference");
            Require(f.Inspect(false, default, f.Mt + 1).Status == Status.InvalidMethodTable, "unaligned MT");
            Require(f.Inspect(false, default, 0x1000).Status == Status.InvalidMethodTable, "foreign MT");
            Require(f.Inspect(false, default, 0, f.Handle ^ 8).Status == Status.ModuleRejected, "wrong module handle");
            Require(Core.InspectType(default, f.Capture, f.Mapping, f.Owner, 4096, f.Handle,
                f.Window, f.Mt).Status == Status.UnsupportedProfile, "unpinned ABI denied");

            // A syntactically valid MT in writable data is not immutable metadata.
            NativeMemory.Copy((void*)(f.Mt - 24), f.Image + 0x2400, 48);
            Require(f.Inspect(false, new(f.Address(0x2400), 48), f.Address(0x2418)).Status == Status.ModuleRejected,
                "registered PE permissions required");
            byte[] before = f.Snapshot();
            for (int i = 0; i < 20; i++) Require(f.Inspect().Succeeded, "warmup");
            long start = GC.GetAllocatedBytesForCurrentThread();
            bool valid = true;
            for (int i = 0; i < 1000; i++) valid &= f.Inspect().Succeeded;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - start;
            Require(valid && bytes == 0 && f.Snapshot().AsSpan().SequenceEqual(before),
                "repeated inspection allocates and changes no caller bytes");
            f.Guards();
        });
    }

    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    [HostFixture(HostLane.Fast)]
    internal static void Collection()
    {
        HostCheck.Run("m04.013", () =>
        {
            try
            {
                Enablement(); GraphAndReuse(); RejectionIsAtomic(); StaticLifetime(); LedgerBounds();
            }
            catch (Exception error)
            { Console.WriteLine("TEST_FAIL m04.013 " + error.Message); throw; }
        });
    }

    private static void Enablement()
    {
        using Fixture image = new(); image.Type(0x50000000, 24, 0);
        using HeapFixture small = new(false, 80);
        byte[] before = small.Snapshot();
        Require(Alloc.EnableCollection(small.Access, small.State, 80) == RhpNewFastStatus.InvalidStorage,
            "legacy owner capacity cannot contain records"); small.Unchanged(before);
        using HeapFixture plain = new(false);
        before = plain.Snapshot();
        Require(Collect(image, plain, null, 0).OwnerStatus == RhpNewFastStatus.CollectionNotEnabled,
            "collection requires explicit opt-in"); plain.Unchanged(before);
        ((byte*)plain.State)[80] = 1; before = plain.Snapshot();
        Require(Alloc.EnableCollection(plain.Access, plain.State, HeapFixture.Capacity) == RhpNewFastStatus.InvalidAllocationLedger,
            "records must start zero"); plain.Unchanged(before); ((byte*)plain.State)[80] = 0;
        plain.Allocate(image); before = plain.Snapshot();
        Require(Alloc.EnableCollection(plain.Access, plain.State, HeapFixture.Capacity) == RhpNewFastStatus.AlreadyInitialized,
            "no adoption after untracked allocations"); plain.Unchanged(before);
        using HeapFixture tracked = new();
        Require(Collect(image, tracked, null, 0, limit: 0).Succeeded, "empty heap needs no work slots");
        tracked.Allocate(image);
        ((ulong*)tracked.State)[9] = ulong.MaxValue; before = tracked.Snapshot();
        Require(Alloc.TryAllocate(tracked.Access, tracked.State, HeapFixture.Capacity, Profile,
            new(0x50000000, 24, 0, 0, 0, 0), image.Mt).Status == RhpNewFastStatus.AllocationIdentityExhausted,
            "identity never wraps"); tracked.Unchanged(before);
    }

    private static void GraphAndReuse()
    {
        using Fixture image = new(); using HeapFixture heap = new();
        image.Type(0x51000000, 24, 1); image.Series(0, -16, 8);
        RhpNewFastResult a = heap.Allocate(image), b = heap.Allocate(image), c = heap.Allocate(image),
            d = heap.Allocate(image), e = heap.Allocate(image), shared = heap.Allocate(image), dead = heap.Allocate(image);
        Link(a, b); Link(b, c); Link(c, a); Link(d, e); Link(e, d); Link(shared, b);
        RhpNewFastResult* roots = stackalloc RhpNewFastResult[4] { a, shared, a, default };
        var result = Collect(image, heap, roots, 4);
        Require(result.Succeeded && result.LiveObjects == 4 && result.ReclaimedObjects == 3 &&
            result.ReclaimedBytes == 72, "chains, sharing, duplicate/null roots and live/dead cycles");
        Require(heap.Valid(a) && heap.Valid(b) && heap.Valid(c) && heap.Valid(shared) &&
            !heap.Valid(d) && !heap.Valid(e) && !heap.Valid(dead), "sweep retires only dead receipts");
        RhpNewFastResult reused = heap.Allocate(image);
        Require(reused.ObjectReference == d.ObjectReference && reused.AllocationCount > dead.AllocationCount &&
            !heap.Valid(d) && heap.Valid(reused) && *(ulong*)(reused.ObjectReference + 8) == 0 &&
            heap.State->UsedBytes == 168, "real first-fit reuse with fresh identity and unchanged high water");
        result = Collect(image, heap, roots, 4);
        Require(result.Succeeded && result.ReclaimedObjects == 1 && result.LiveObjects == 4, "repeat collection");
        result = Collect(image, heap, null, 0);
        Require(result.Succeeded && result.LiveObjects == 0 && result.ReclaimedObjects == 4, "zero roots reclaim all");
        RhpNewFastResult replacement = heap.Allocate(image);
        Require(replacement.ObjectReference == a.ObjectReference && !heap.Valid(a), "same address and MT reject stale identity");
        byte[] before = heap.Snapshot();
        Require(Collect(image, heap, &a, 1).Status == BoundedCollectionStatus.InvalidReference, "stale explicit root denied");
        heap.Unchanged(before);
        Require(Collect(image, heap, &replacement, 1).Succeeded, "current receipt retained");
        long allocated = GC.GetAllocatedBytesForCurrentThread(); bool valid = true;
        for (int i = 0; i < 100; i++) valid &= Collect(image, heap, &replacement, 1).Succeeded;
        Require(valid && GC.GetAllocatedBytesForCurrentThread() == allocated, "collection allocates no managed scratch");
        heap.Guards();
    }

    private static void RejectionIsAtomic()
    {
        using Fixture image = new(); using HeapFixture heap = new(); using HeapFixture other = new();
        image.Type(0x51000000, 24, 1); image.Series(0, -16, 8);
        RhpNewFastResult a = heap.Allocate(image), b = heap.Allocate(image), dead = heap.Allocate(image);
        Link(a, b);
        void Rejected(BoundedCollectionStatus expected, ulong child, uint limit = 170)
        {
            *(ulong*)(a.ObjectReference + 8) = child;
            byte[] before = heap.Snapshot(), metadata = image.Snapshot();
            RhpNewFastResult root = a;
            Require(Collect(image, heap, &root, 1, limit: limit).Status == expected, "expected " + expected);
            heap.Unchanged(before); Require(image.Snapshot().AsSpan().SequenceEqual(metadata), "metadata unchanged");
        }
        Rejected(BoundedCollectionStatus.WorkLimitExceeded, b.ObjectReference, 1);
        Rejected(BoundedCollectionStatus.WorkLimitExceeded, b.ObjectReference, 0);
        Rejected(BoundedCollectionStatus.InvalidReference, b.ObjectReference + 8);
        Rejected(BoundedCollectionStatus.InvalidReference, heap.State->MappedStart + 3000);
        RhpNewFastResult foreign = other.Allocate(image);
        Rejected(BoundedCollectionStatus.InvalidReference, foreign.ObjectReference);
        Link(a, b); image.Series(0, -24, 8);
        Rejected(BoundedCollectionStatus.MetadataRejected, b.ObjectReference);
        image.Series(0, -16, 8);
        byte[] bytes = heap.Snapshot();
        Require(Collect(image, heap, &foreign, 1).Status == BoundedCollectionStatus.InvalidReference, "foreign receipt");
        heap.Unchanged(bytes);
        Require(Collect(image, heap, (RhpNewFastResult*)heap.State, 1).Status == BoundedCollectionStatus.StorageOverlap,
            "roots cannot alias owner"); heap.Unchanged(bytes);
        RhpNewFastResult validRoot = a;
        Require(Collect(image, heap, &validRoot, 1).Succeeded && !heap.Valid(dead), "valid graph recovers after rejections");
        Rejected(BoundedCollectionStatus.InvalidReference, dead.ObjectReference);
        Link(a, b);
        // Unreachable malformed fields must also reject before any sweep.
        RhpNewFastResult unreachable = heap.Allocate(image);
        *(ulong*)(unreachable.ObjectReference + 8) = 1;
        Rejected(BoundedCollectionStatus.InvalidReference, b.ObjectReference);
    }

    private static void StaticLifetime()
    {
        using Fixture image = new(); using HeapFixture heap = new();
        image.Type(0x51000000, 24, 1); image.Series(0, -16, 8);
        ulong staticMt = image.Address(0x1800), systemObject = image.Address(0x1180);
        W32(image.Image + 0x1200, 0x1000); // Entry1200 -> Cell2200.
        W64(image.Image + 0x2200, unchecked((uint)(0x1800 - 0x2200 + 1)));
        W64((byte*)staticMt - 24, unchecked((ulong)-16L)); W64((byte*)staticMt - 16, 8);
        W64((byte*)staticMt - 8, 1); W32((byte*)staticMt, 0x01000000); W32((byte*)staticMt + 4, 24);
        W64((byte*)staticMt + 8, systemObject);
        W32((byte*)systemObject, 0x50000000); W32((byte*)systemObject + 4, 24);
        CollectorStaticRootAccess retained = new() { Owner = heap.Storage + 0x9000,
            Capacity = GCStaticRootsMalformedMetadata.RequiredRootOwnerBytes,
            Cells = new(image.Address(0x2200), 8), SystemObjectMethodTable = systemObject };
        NativeMemory.Clear(retained.Owner, (nuint)retained.Capacity);
        GCStaticRootResult root = GCStaticRootsMalformedMetadata.MaterializeOnce(image.Capture, image.Mapping,
            image.Owner, 4096, image.Handle, image.Window, retained.Cells, systemObject,
            heap.Access, heap.State, HeapFixture.Capacity, retained.Owner, retained.Capacity);
        Require(root.Succeeded, "materialize through collecting owner: " + root.Status + "/" + root.HeapStatus);
        RhpNewFastResult child = heap.Allocate(image), dead = heap.Allocate(image);
        *(ulong*)(root.ObjectReference + 8) = child.ObjectReference;
        byte[] before = heap.Snapshot();
        Require(Collect(image, heap, null, 0).Status == BoundedCollectionStatus.StaticRootRequired, "static base cannot be silently omitted");
        heap.Unchanged(before);
        var result = Collect(image, heap, null, 0, retained);
        Require(result.Succeeded && result.LiveObjects == 2 && result.ReclaimedObjects == 1 && heap.Valid(child),
            "sole child survives through retained static root");
        RhpNewFastResult reused = heap.Allocate(image);
        Require(reused.ObjectReference == dead.ObjectReference && !heap.Valid(dead), "static owner allows same-owner reuse");
        *(ulong*)(root.ObjectReference + 8) = 0;
        result = Collect(image, heap, null, 0, retained);
        Require(result.Succeeded && result.LiveObjects == 1 && result.ReclaimedObjects == 2 && !heap.Valid(child),
            "cleared static field permits reclaim");
        var queried = GCStaticRootsMalformedMetadata.TryGetRetainedRoot(image.Capture, image.Mapping,
            image.Owner, 4096, image.Handle, image.Window, retained.Cells, systemObject,
            heap.Access, heap.State, HeapFixture.Capacity, retained.Owner, retained.Capacity);
        Require(queried.Succeeded && queried.ObjectReference == root.ObjectReference, "retained query survives sweep and reuse");
        retained.Owner[0] ^= 1; before = heap.Snapshot();
        Require(Collect(image, heap, null, 0, retained).Status == BoundedCollectionStatus.StaticRootRejected, "corrupt root owner");
        heap.Unchanged(before);
    }

    private static void LedgerBounds()
    {
        using Fixture image = new(); using HeapFixture heap = new();
        image.Type(0x50000000, 24, 0);
        RhpNewFastResult* roots = stackalloc RhpNewFastResult[170];
        for (int i = 0; i < 170; i++) roots[i] = heap.Allocate(image);
        Require(Alloc.TryAllocate(heap.Access, heap.State, HeapFixture.Capacity, Profile,
            new(0x50000000, 24, 0, 0, 0, 0), image.Mt).Status == RhpNewFastStatus.Exhausted, "full page bound");
        var result = Collect(image, heap, roots, 170);
        Require(result.Succeeded && result.LiveObjects == 170 && result.ReclaimedObjects == 0, "all three mark words");
        byte* records = (byte*)heap.State + 80;
        foreach ((int offset, ulong value) in new (int, ulong)[] { (0, 1), (8, 17), (16, 0), (24, ulong.MaxValue),
            (32, 0), (56, 1) })
        {
            ulong old = *(ulong*)(records + offset); *(ulong*)(records + offset) = value;
            byte[] before = heap.Snapshot();
            Require(Collect(image, heap, roots, 170).Status == BoundedCollectionStatus.OwnerRejected, "corrupt ledger rejection");
            heap.Unchanged(before); *(ulong*)(records + offset) = old;
        }
        Require(Collect(image, heap, null, 0).ReclaimedObjects == 170, "full-page reclamation");
        RhpNewFastResult fresh = heap.Allocate(image);
        Require(fresh.UsedBefore == 0 && fresh.AllocationCount == 171 && !heap.Valid(roots[0]), "identity exceeds former bump count bound");
        Require(Alloc.EnableCollection(heap.Access, heap.State, HeapFixture.Capacity) == RhpNewFastStatus.AlreadyInitialized,
            "collection cannot reset owner"); heap.Guards();
    }

    private const NativeAotLayoutProfile Profile = NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian;
    private static void Link(RhpNewFastResult parent, RhpNewFastResult child) =>
        *(ulong*)(parent.ObjectReference + 8) = child.ObjectReference;
    private static BoundedCollectionResult Collect(Fixture image, HeapFixture heap, RhpNewFastResult* roots,
        uint count, CollectorStaticRootAccess retained = default, uint limit = 170) =>
        Core.Collect(Profile, new() { Capture = image.Capture, Mapping = image.Mapping, Owner = image.Owner,
            Capacity = 4096, Handle = image.Handle, Metadata = image.Window }, heap.Access, heap.State,
            HeapFixture.Capacity, roots, count, count, retained, limit);

    internal sealed class HeapFixture : IDisposable
    {
        internal const ulong Capacity = 5520;
        internal byte* Storage = (byte*)NativeMemory.AlignedAlloc(65536, 4096);
        internal RhpNewFastHeapAccess Access;
        internal RhpNewFastBumpState* State => (RhpNewFastBumpState*)(Storage + 0x3000);
        internal HeapFixture(bool collecting = true, ulong stateCapacity = Capacity)
        {
            Require(Storage != null && Alloc.RequiredCollectingStateBytes == Capacity && sizeof(RhpNewFastBumpState) == 80, "owner extents");
            new Span<byte>(Storage, 65536).Fill(0xA5);
            Access = new() { Allocator = (BootPageAllocatorState*)(Storage + 0x100), AllocatorCapacity = 1,
                Nodes = (BootPageFreeListNode*)(Storage + 0x300), NodeCount = 1, NodeCapacity = 2,
                Reservations = (BootReservedRange*)(Storage + 0x1000), ReservationCount = 2, ReservationCapacity = 3,
                ParentIndex = 1, Heap = (ManagedHeapPageRangeState*)(Storage + 0x2000), HeapCapacity = 232,
                Backing = Storage + 0x5000, BackingCapacity = 8208 };
            *Access.Allocator = new(true, 4096, 0, 1, 2, 8192, 0, 0);
            Access.Nodes[0] = new(0, 0, 0x300000000, 0x300002000, 2, 8192, -1);
            PhysicalAddress.TryCreateFromUefiBoundary(0x100000, out var image);
            PhysicalAddress.TryCreateFromUefiBoundary(0x300000000, out var arena);
            BootReservedRange.TryCreate(BootReservationKind.LoadedImage, image, 1, out Access.Reservations[0]);
            BootReservedRange.TryCreate(BootReservationKind.AllocatorArena, arena, 2, out Access.Reservations[1]);
            NativeMemory.Clear(Access.Heap, 216); NativeMemory.Clear(State, (nuint)Capacity);
            var result = ManagedHeapPageRangeLiveAllocator.ReserveAndInitializeOnce(Access.Allocator, 1,
                Access.Nodes, 1, 2, Access.Reservations, 2, 3, 1, Access.Backing, Access.BackingCapacity, Access.Heap, 232);
            Require(result.IsReady && Alloc.InitializeOnce(Access, State, stateCapacity).Succeeded &&
                (!collecting || Alloc.EnableCollection(Access, State, stateCapacity) == RhpNewFastStatus.Success),
                "original retained owner initialization");
        }
        internal RhpNewFastResult Allocate(Fixture image)
        {
            var map = image.Inspect(); Require(map.Succeeded, "type fixture");
            var result = Alloc.TryAllocate(Access, State, Capacity, Profile, map.Prefix, image.Mt);
            Require(result.AllocationSucceeded, "same-owner allocation: " + result.Status); return result;
        }
        internal bool Valid(RhpNewFastResult receipt) => Alloc.TryValidateRetainedAllocation(Access, State, Capacity, receipt) == RhpNewFastStatus.Success;
        internal byte[] Snapshot() => new ReadOnlySpan<byte>(Storage, 65536).ToArray();
        internal void Unchanged(byte[] before) => Require(new ReadOnlySpan<byte>(Storage, 65536).SequenceEqual(before), "all owner, page and canary bytes preserved");
        internal void Guards()
        {
            foreach (int offset in new[] { 0, 0x2F00, 0x4600, 0x8000, 0xA000 })
                for (int i = 0; i < 64; i++) Require(Storage[offset + i] == 0xA5, "heap outer canary");
        }
        public void Dispose() => NativeMemory.AlignedFree(Storage);
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly byte* storage;
        internal byte* Image => storage + 4096;
        internal byte* Owner => storage + 0xB000;
        internal readonly EfiLoadedImageRangeCapturePlan Capture;
        internal readonly M01LoadedImageVirtualMappingEvidence Mapping;
        internal readonly ulong Handle;
        internal ulong Mt => Address(0x1400);
        internal GCStaticReadableWindow Window => new(Address(0x1100), 0xF00);

        internal Fixture()
        {
            storage = (byte*)NativeMemory.AlignedAlloc(0xD000, 4096);
            if (storage == null) throw new OutOfMemoryException();
            new Span<byte>(storage, 0xD000).Fill(0xA5);
            NativeMemory.Clear(Image, 0x8000); NativeMemory.Clear(Owner, 4096);
            W16(Image, 0x5A4D); W32(Image + 60, 0x80); W32(Image + 0x80, 0x4550);
            W16(Image + 0x84, 0x8664); W16(Image + 0x86, 4); W16(Image + 0x94, 0xF0);
            W16(Image + 0x98, 0x20B); W32(Image + 0xD0, 0x8000); W32(Image + 0xD4, 0x400);
            Section(0, ".rdata", 0x1000, 0x1000, 0x40000040);
            Section(1, ".data", 0x2000, 0x2000, 0xC0000040);
            Section(2, ".modules", 0x5000, 8, 0x40000040);
            Section(3, ".text", 0x6000, 0x1000, 0x60000020);
            W64(Image + 0x5000, Address(0x1000)); W32(Image + 0x1000, 0x00525452);
            W16(Image + 0x1004, 26); W16(Image + 0x1006, 1); W16(Image + 0x100C, 4); Image[0x100E] = 16; Image[0x100F] = 1;
            Row(0, 201, 4, 0x1200); Row(1, 202, 8, 0x2100);
            Row(2, 204, 16, 0x2000); Row(3, 213, 0, 0x1210);
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(
                0, 0x1000, 0x2000, (ulong)Image, 0x8000);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
            var module = TypeManagerModuleMetadataLiveNativeAOTImage.RegisterOnce(Capture, Mapping, Owner, 4096);
            Require(module.Succeeded, "original TypeManager registration " + module.Status);
            Handle = module.Handle;
        }

        internal ulong Address(int offset) => (ulong)(Image + offset);
        internal void Type(uint flags, uint size, long count)
        {
            W32((byte*)Mt, flags); W32((byte*)Mt + 4, size); W64((byte*)Mt + 8, 0);
            W64((byte*)Mt + 16, 0); W64((byte*)Mt - 8, unchecked((ulong)count));
        }
        internal void Series(int index, long adjusted, ulong offset)
        { byte* p = (byte*)Mt - 24 - index * 16; W64(p, unchecked((ulong)adjusted)); W64(p + 8, offset); }
        internal CollectorTypeMapResult Inspect(bool abbreviated = false,
            GCStaticReadableWindow window = default, ulong mt = 0, ulong handle = 0) =>
            Core.InspectType(NativeAotLayoutProfile.Ilc11_0_0Rc2WinX64LittleEndian,
                Capture, Mapping, Owner, 4096, handle == 0 ? Handle : handle,
                window.Bytes == 0 ? Window : window, mt == 0 ? Mt : mt, abbreviated);
        internal void Expect(Status expected)
        {
            byte[] before = Snapshot(); var result = Inspect();
            Require(result.Status == expected && !result.Succeeded &&
                result.MethodTable == 0 && result.SeriesCount == 0 && !result.TryGetSeries(0, out _),
                "expected " + expected + ", got " + result.Status);
            Require(Snapshot().AsSpan().SequenceEqual(before), "failure preserves complete input span");
        }
        internal byte[] Snapshot() => new ReadOnlySpan<byte>(storage, 0xD000).ToArray();
        internal void Guards()
        {
            foreach (int offset in new[] { 0, 0x9000, 0xA000, 0xC000 })
                for (int i = 0; i < 4096; i++) Require(storage[offset + i] == 0xA5, "outer canary");
        }
        private void Row(int index, uint id, uint length, int start)
        { byte* p = Image + 0x1010 + index * 16; W32(p, id); W32(p + 4, length); W64(p + 8, Address(start)); }
        private void Section(int index, string name, uint start, uint size, uint flags)
        { byte* p = Image + 0x188 + index * 40; for (int i = 0; i < name.Length; i++) p[i] = (byte)name[i]; W32(p + 8, size); W32(p + 12, start); W32(p + 36, flags); }
        public void Dispose() => NativeMemory.AlignedFree(storage);
    }

    private static void W16(byte* p, ushort v) { for (int i = 0; i < 2; i++) p[i] = (byte)(v >> (i * 8)); }
    private static void W32(byte* p, uint v) { for (int i = 0; i < 4; i++) p[i] = (byte)(v >> (i * 8)); }
    private static void W64(byte* p, ulong v) { for (int i = 0; i < 8; i++) p[i] = (byte)(v >> (i * 8)); }
}
