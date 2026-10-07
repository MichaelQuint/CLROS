using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.CompilerInterfaceDispatch;
using Status = OS.Kernel.Runtime.CompilerInterfaceDispatchStatus;
using Alloc = OS.Kernel.Runtime.GuestTestRhpNewFast;
using Module = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;

internal static unsafe class CompilerInterfaceDispatchTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;
        Run("exact-and-cache", ExactAndCache, ref failures);
        Run("reference-variance", ReferenceVariance, ref failures);
        Run("declaring-owner", DeclaringOwner, ref failures);
        Run("receiver-lifetime", ReceiverLifetime, ref failures);
        Run("original-authorities", OriginalAuthorities, ref failures);
        Run("cell-encoding", CellEncoding, ref failures);
        Run("metadata-and-targets", MetadataAndTargets, ref failures);
        Run("finite-cache", FiniteCache, ref failures);
        if (failures != 0) throw new InvalidOperationException("compiler interface dispatch failures=" + failures);
    }

    private static void Run(string name, Action action, ref int failures)
    {
        string marker = "m04.016.interface-dispatch." + name;
        Console.WriteLine("TEST_START " + marker);
        try { action(); Console.WriteLine("TEST_PASS " + marker); }
        catch (Exception error) { failures++; Console.WriteLine("TEST_FAIL " + marker + " " + error.Message); }
    }
    private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static void Expect(Status actual, Status expected) => Check(actual == expected, "expected=" + expected + " actual=" + actual);

    private static void ExactAndCache()
    {
        using var f = new Fixture();
        Check(Core.RequiredStorage(64, 64) <= Fixture.Capacity && Core.RequiredStorage(0, 1) == 0 &&
            Core.RequiredStorage(65, 1) == 0 && Core.RequiredStorage(1, 65) == 0, "finite public sizing");
        var a = f.Allocate(0x2300); var b = f.Allocate(0x2400);
        Expect(f.Bind(), Status.Success);
        byte[] image = f.ImageBytes();
        var first = f.Resolve(a, 0);
        Check(first.Target == f.Address(0x11010) && first.DeclaringType == f.Address(0x2300) &&
            first.ReceiverType == a.MethodTableToken && first.RequestedInterface == f.Address(0x1900) &&
            first.ImplementedInterface == first.RequestedInterface && !first.CacheHit && first.AllocationSequence == 1,
            "first exact dispatch identity");
        Check(f.Resolve(a, 0).CacheHit && !f.Resolve(b, 0).CacheHit &&
            f.Resolve(b, 0).Target == f.Address(0x11020), "one cell alternates genuine receiver types");
        Expect(Core.Observe(f.Runner, Fixture.Capacity, out var progress), Status.Success);
        Check(progress.CellCount == 5 && progress.CacheCount == 2 && progress.CacheHits == 2 &&
            progress.Resolutions == 4 && progress.MetadataWords > 0, "exact cache counters");
        Check(f.ImageBytes().AsSpan().SequenceEqual(image), "no compiler cell or image writes");
        f.Guards();
        Console.WriteLine("M04_DISPATCH_HOST_LAYOUT state=" + Core.StateBytes + " required64=" + Core.RequiredStorage(64, 64));
    }

    private static void ReferenceVariance()
    {
        using var f = new Fixture();
        var a = f.Allocate(0x2300); var b = f.Allocate(0x2400);
        Expect(f.Bind(), Status.Success);
        var covariant = f.Resolve(a, 1);
        Check(covariant.RequestedInterface == f.Address(0x1F00) && covariant.ImplementedInterface == f.Address(0x2000) &&
            covariant.Target == f.Address(0x11030), "Producer<Derived> -> Producer<Base>");
        var contravariant = f.Resolve(a, 2);
        Check(contravariant.RequestedInterface == f.Address(0x2200) && contravariant.ImplementedInterface == f.Address(0x2100) &&
            contravariant.Target == f.Address(0x11040), "Consumer<Base> -> Consumer<Derived>");
        Check(!f.Resolve(b, 1).CacheHit && f.Resolve(b, 1).ImplementedInterface == f.Address(0x1F00) &&
            f.Resolve(b, 1).Target == f.Address(0x11050), "same generic cell does not reuse alternate instantiation");
        f.Reject(b.ObjectReference, 3, Status.IncompatibleInterface);
        f.Reject(b.ObjectReference, 4, Status.IncompatibleInterface);
        Check(f.Resolve(a, 1).CacheHit, "variant hit revalidated");

        // Per-level exact before variance, but a derived compatible implementation
        // wins before an exact implementation belonging to its base class.
        using var levels = new Fixture();
        levels.Type(0x2500, 0x50440000, 24, 0, [0x11070], [0x1F00], 0x3B00, 0x3C00);
        levels.Map(0x3B00, (0, 0, 1)); levels.Rel(0x3C00, 0x11070);
        levels.Type(0x2600, 0x50440000, 24, 0x2500, [0x11080], [0x2000, 0x1F00], 0x3D00, 0x3E00);
        levels.Map(0x3D00, (0, 0, 1), (1, 0, 2)); levels.Rel(0x3E00, 0x11080); levels.Rel(0x3E04, 0x11090);
        levels.DerivedSealedBytes = 8;
        var derived = levels.Allocate(0x2600); Expect(levels.Bind(), Status.Success);
        Check(levels.Resolve(derived, 1).Target == levels.Address(0x11090), "current-owner exact precedes variance");
        using var variantLevel = new Fixture();
        variantLevel.Type(0x2500, 0x50440000, 24, 0, [0x11070], [0x1F00], 0x3B00, 0x3C00);
        variantLevel.Map(0x3B00, (0, 0, 1)); variantLevel.Rel(0x3C00, 0x11070);
        variantLevel.Type(0x2600, 0x50440000, 24, 0x2500, [0x11080], [0x2000], 0x3D00, 0x3E00);
        variantLevel.Map(0x3D00, (0, 0, 1)); variantLevel.Rel(0x3E00, 0x11080);
        derived = variantLevel.Allocate(0x2600); Expect(variantLevel.Bind(), Status.Success);
        var result = variantLevel.Resolve(derived, 1);
        Check(result.Target == variantLevel.Address(0x11080) && result.DeclaringType == derived.MethodTableToken,
            "derived variance precedes base exact");
    }

    private static void DeclaringOwner()
    {
        using var f = new Fixture();
        var derived = f.Allocate(0x2600); Expect(f.Bind(), Status.Success);
        var sealedResult = f.Resolve(derived, 0);
        Check(sealedResult.DeclaringType == f.Address(0x2500) && sealedResult.ImplementationSlot == 1 &&
            sealedResult.Target == f.Address(0x11070) && sealedResult.Target != *(ulong*)(f.Image + 0x2620),
            "sealed index uses base owner slot count, not derived slot count or interface order");
        using var virtualCase = new Fixture();
        virtualCase.Map(0x3B00, (0, 0, 0));
        derived = virtualCase.Allocate(0x2600); Expect(virtualCase.Bind(), Status.Success);
        var virtualResult = virtualCase.Resolve(derived, 0);
        Check(virtualResult.DeclaringType == virtualCase.Address(0x2500) && virtualResult.ImplementationSlot == 0 &&
            virtualResult.Target == virtualCase.Address(0x11080), "true virtual uses original receiver override");
    }

    private static void ReceiverLifetime()
    {
        using var f = new Fixture();
        var first = f.Allocate(0x2300); Expect(f.Bind(), Status.Success); f.Resolve(first, 0);
        f.Reject(0, 0, Status.NullReceiver);
        f.Reject(1, 0, Status.ReceiverRejected);
        f.Reject(ulong.MaxValue, 0, Status.ReceiverRejected);
        f.Reject(first.ObjectReference + 8, 0, Status.ReceiverRejected);
        var staticBase = Alloc.TryAllocateStaticBase(f.Heap, f.Allocator, Alloc.RequiredCollectingStateBytes,
            Fixture.Profile, 0x01000000, 24, f.Address(0x1900));
        Check(staticBase.AllocationSucceeded, "static allocation setup");
        f.Reject(staticBase.ObjectReference, 0, Status.ReceiverRejected);

        using var collected = new Fixture();
        first = collected.Allocate(0x2300); Expect(collected.Bind(), Status.Success); collected.Resolve(first, 0);
        var sweep = BoundedNonMovingCollectorExplicitRootEnumeration.Collect(Fixture.Profile,
            new() { Capture = collected.Capture, Mapping = collected.Mapping, Owner = collected.Owner,
                Capacity = 4096, Handle = collected.Handle, Metadata = new(collected.Address(0x1400), 0x3C00) },
            collected.Heap, collected.Allocator, Alloc.RequiredCollectingStateBytes, null, 0, 0);
        Check(sweep.Succeeded && sweep.ReclaimedObjects == 1, "actual collector reclaimed receiver " + sweep.Status);
        collected.Reject(first.ObjectReference, 0, Status.ReceiverRejected);
        var next = collected.Allocate(0x2400);
        Check(next.ObjectReference == first.ObjectReference && next.AllocationCount != first.AllocationCount, "allocator reuses address with new receipt");
        var result = collected.Resolve(next, 0);
        Check(!result.CacheHit && result.Target == collected.Address(0x11020) && result.AllocationSequence == next.AllocationCount,
            "old receipt cache cannot serve new lifetime");
    }

    private static void OriginalAuthorities()
    {
        using var f = new Fixture();
        var receiver = f.Allocate(0x2300); Expect(f.Bind(), Status.Success); f.Resolve(receiver, 0);
        byte* copied = (byte*)NativeMemory.Alloc((nuint)Fixture.Capacity);
        try
        {
            NativeMemory.Copy(f.Runner, copied, (nuint)Fixture.Capacity);
            Expect(Core.Resolve(copied, Fixture.Capacity, receiver.ObjectReference, f.Cell(0), out var rejected), Status.StateChanged);
            Check(rejected.Target == 0, "copied controller no target");
        }
        finally { NativeMemory.Free(copied); }
        f.Owner[0] ^= 1;
        f.Reject(receiver.ObjectReference, 0, Status.ModuleRejected);
        f.Owner[0] ^= 1;
        ((byte*)f.Allocator)[0] = 0;
        f.Reject(receiver.ObjectReference, 0, Status.AllocatorRejected);
        using var alias = new Fixture();
        Expect(alias.Bind(storage: alias.Owner), Status.StorageRejected);
        using var inline = new Fixture(inline: true);
        receiver = inline.Allocate(0x2300); Expect(inline.Bind(), Status.Success); inline.Resolve(receiver, 0); inline.Guards();
    }

    private static void CellEncoding()
    {
        foreach (ulong word in new ulong[] { 0, 0xFF8, 1, 2, 0x100000003 })
        {
            using var f = new Fixture();
            *(ulong*)(f.Image + 0x5108) = word;
            Expect(f.Bind(), Status.UnsupportedCell);
            Check(f.RunnerBytes().All(b => b == 0), "cell rejection before controller write");
        }
        using (var f = new Fixture())
        {
            Expect(f.Bind(bytes: 16), Status.CellRejected);
            Expect(f.Bind(start: ulong.MaxValue - 15), Status.CellRejected);
            *(ulong*)(f.Image + 0x5118) = 0x10000;
            Expect(f.Bind(), Status.CellRejected);
        }
        using (var f = new Fixture())
        {
            for (int i = 0; i < 33; i++) f.CellRecord(0x5100 + i * 16, 0x1900);
            *(ulong*)(f.Image + 0x5310) = 0; *(ulong*)(f.Image + 0x5318) = 0;
            Expect(f.Bind(bytes: 34 * 16, cells: 64), Status.CellRejected);
        }
        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300); Expect(f.Bind(), Status.Success); f.Resolve(receiver, 0);
            f.Rel(0x5108, 0x1A00, 3);
            f.Reject(receiver.ObjectReference, 0, Status.CellChanged);
        }
        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300); Expect(f.Bind(), Status.Success);
            Expect(Core.Resolve(f.Runner, Fixture.Capacity, receiver.ObjectReference, f.Cell(0) + 8, out _), Status.CellRejected);
        }
    }

    private static void MetadataAndTargets()
    {
        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300); Expect(f.Bind(), Status.Success); f.Resolve(receiver, 0);
            *(ulong*)(f.Image + 0x2320) ^= 8;
            f.Reject(receiver.ObjectReference, 0, Status.MetadataChanged);
        }
        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300); Expect(f.Bind(), Status.Success); f.Resolve(receiver, 0);
            // Cache layout starts with Receiver, Cell, then public Resolution.Target.
            *(ulong*)(f.Runner + Core.StateBytes + 8 * 32 + 16) ^= 8;
            f.Reject(receiver.ObjectReference, 0, Status.CacheChanged);
        }
        foreach (int badTarget in new[] { 0x1900, 0x6000 })
        {
            using var f = new Fixture(); f.Rel(0x3900, badTarget);
            var receiver = f.Allocate(0x2300); Expect(f.Bind(), Status.Success);
            f.Reject(receiver.ObjectReference, 0, Status.TargetRejected);
        }
        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300); Expect(f.Bind(), Status.Success);
            // The original TypeManager rejects RWX even before target admission.
            *(uint*)(f.Image + 0x188 + 3 * 40 + 36) = 0xE0000020;
            f.Reject(receiver.ObjectReference, 0, Status.ModuleRejected);
        }
        using (var f = new Fixture())
        {
            f.Map(0x3800, (0, 0, 65)); var receiver = f.Allocate(0x2300); Expect(f.Bind(), Status.Success);
            f.Reject(receiver.ObjectReference, 0, Status.SlotRejected);
        }
        using (var f = new Fixture())
        {
            // The next RO word deliberately looks like a valid RX relocation.
            // It is outside A's exact three-entry native table contribution.
            f.Rel(0x390C, 0x11020); f.Map(0x3800, (0, 0, 4));
            var receiver = f.Allocate(0x2300); Expect(f.Bind(), Status.Success);
            f.Reject(receiver.ObjectReference, 0, Status.SlotRejected);
        }
        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300); Expect(f.Bind(admitSealed: false), Status.Success);
            f.Reject(receiver.ObjectReference, 0, Status.SlotRejected);
        }
        using (var f = new Fixture())
        {
            f.Map(0x3800, (0, 0, 1), (0, 0, 1)); var receiver = f.Allocate(0x2300); Expect(f.Bind(), Status.Success);
            f.Reject(receiver.ObjectReference, 0, Status.AmbiguousImplementation);
        }
        foreach (int map in new[] { 0x3800, 0x4FFC })
        {
            using var f = new Fixture();
            if (map == 0x3800) *(ushort*)(f.Image + map) = 65;
            else f.Rel(0x2340, map);
            var receiver = f.Allocate(0x2300); Expect(f.Bind(), Status.Success);
            f.Reject(receiver.ObjectReference, 0, map == 0x3800 ? Status.MapRejected : Status.MetadataRejected);
        }
        using (var f = new Fixture())
        {
            *(ushort*)(f.Image + 0x3808) = 3; var receiver = f.Allocate(0x2300); Expect(f.Bind(), Status.Success);
            f.Reject(receiver.ObjectReference, 0, Status.MapRejected);
        }
    }

    private static void FiniteCache()
    {
        using var f = new Fixture(); var a = f.Allocate(0x2300); var b = f.Allocate(0x2400);
        Expect(f.Bind(cache: 1), Status.Success); f.Resolve(a, 0);
        byte[] before = f.RunnerBytes(); f.Reject(b.ObjectReference, 0, Status.CapacityExceeded);
        Check(f.RunnerBytes().AsSpan().SequenceEqual(before), "capacity rejection rolls back partial metadata and cache");
        Check(f.Resolve(a, 0).CacheHit, "existing entry survives exhaustion");
        f.Guards();
    }

    // Synthetic compiler-layout bytes with genuine original module registration
    // and genuine heap/collecting receipts. Returned RX addresses are compared,
    // never executed. Native callsites and argument preservation are guest proof.
    private sealed class Fixture : IDisposable
    {
        internal const uint ImageSize = 0x18000;
        internal const ulong Capacity = 32768;
        internal const NativeAotLayoutProfile Profile = NativeAotLayoutProfile.Ilc10_0_8WinX64LittleEndian;
        internal readonly byte* Image = (byte*)NativeMemory.AlignedAlloc(ImageSize, 4096);
        internal readonly byte* Owner = (byte*)NativeMemory.AllocZeroed(4096);
        private readonly byte* runnerAllocation = (byte*)NativeMemory.AllocZeroed((nuint)Capacity + 16);
        private readonly byte* heapAllocation = (byte*)NativeMemory.AlignedAlloc(65536, 4096);
        internal readonly byte* Runner;
        internal RhpNewFastHeapAccess Heap;
        internal RhpNewFastBumpState* Allocator => (RhpNewFastBumpState*)(heapAllocation + 0x3000);
        internal readonly EfiLoadedImageRangeCapturePlan Capture;
        internal readonly M01LoadedImageVirtualMappingEvidence Mapping;
        internal readonly ulong Handle;
        internal ulong DerivedSealedBytes = 4;

        internal Fixture(bool inline = false, uint textFlags = 0x60000020)
        {
            Check(Image != null && Owner != null && runnerAllocation != null && heapAllocation != null, "fixture allocation");
            NativeMemory.Clear(Image, ImageSize); NativeMemory.Clear(heapAllocation, 65536);
            Runner = inline ? Image + 0x6000 : runnerAllocation + 8;
            *(ulong*)(Runner - 8) = 0x0123456789ABCDEF; *(ulong*)(Runner + Capacity) = 0xFEDCBA9876543210;
            *(ushort*)Image = 0x5A4D; *(uint*)(Image + 60) = 0x80; *(uint*)(Image + 0x80) = 0x4550;
            *(ushort*)(Image + 0x84) = 0x8664; *(ushort*)(Image + 0x86) = 4; *(ushort*)(Image + 0x94) = 0xF0;
            *(ushort*)(Image + 0x98) = 0x20B; *(uint*)(Image + 0xD0) = ImageSize; *(uint*)(Image + 0xD4) = 0x400;
            Section(0, ".rdata", 0x1000, 0x4000, 0x40000040);
            Section(1, ".data", 0x5000, 0xA000, 0xC0000040);
            Section(2, ".modules", 0x10000, 16, 0x40000040);
            Section(3, ".text", 0x11000, 0x1000, textFlags);
            *(ulong*)(Image + 0x10000) = Address(0x1000);
            *(uint*)(Image + 0x1000) = 0x00525452; *(ushort*)(Image + 0x1004) = 16;
            *(ushort*)(Image + 0x100C) = 1; Image[0x100E] = 24; Image[0x100F] = 1;
            *(uint*)(Image + 0x1010) = 204; *(ulong*)(Image + 0x1018) = Address(0x5000);
            var observation = EfiLoadedImageProtocolObservation.CaptureFromUefiBoundary(0, 0x1000, 0x2000, (ulong)Image, ImageSize);
            Capture = EfiLoadedImageRangeCapturePlan.Create(&observation, 1, 1, 4096);
            Mapping = M01LoadedImageVirtualMappingEvidence.CaptureCallerAttestedIdentity(Capture);
            var registration = Module.RegisterOnce(Capture, Mapping, Owner, 4096);
            Check(registration.Succeeded, "registered fixture module " + registration.Status); Handle = registration.Handle;
            Type(0x1800, 0x50000000, 24, 0, [], []);
            Type(0x1900, 0x54000000, 0, 0, [], []); Type(0x1A00, 0x54000000, 0, 0, [], []);
            Type(0x1B00, 0x54830000, 1, 0, [], [], composition: 0x4200); Image[0x4200] = 1;
            Type(0x1C00, 0x54830000, 1, 0, [], [], composition: 0x4210); Image[0x4210] = 2;
            Type(0x1D00, 0x50000000, 24, 0x1800, [], []); Type(0x1E00, 0x50000000, 24, 0x1D00, [], []);
            Type(0x1F00, 0x56800000, 0, 0, [], [], definition: 0x1B00, composition: 0x1D00);
            Type(0x2000, 0x56800000, 0, 0, [], [], definition: 0x1B00, composition: 0x1E00);
            Type(0x2100, 0x56800000, 0, 0, [], [], definition: 0x1C00, composition: 0x1D00);
            Type(0x2200, 0x56800000, 0, 0, [], [], definition: 0x1C00, composition: 0x1E00);
            Type(0x2300, 0x50440000, 24, 0x1800, [0x11000], [0x1900, 0x2000, 0x2100], 0x3800, 0x3900);
            Map(0x3800, (0, 0, 1), (1, 0, 2), (2, 0, 3)); Rel(0x3900, 0x11010); Rel(0x3904, 0x11030); Rel(0x3908, 0x11040);
            Type(0x2400, 0x50440000, 24, 0x1800, [0x11000], [0x1900, 0x1F00, 0x2200], 0x3A00, 0x3A80);
            Map(0x3A00, (0, 0, 1), (1, 0, 2), (2, 0, 3)); Rel(0x3A80, 0x11020); Rel(0x3A84, 0x11050); Rel(0x3A88, 0x11060);
            Type(0x2500, 0x50440000, 24, 0x1800, [0x11000], [0x1900], 0x3B00, 0x3C00);
            Map(0x3B00, (0, 0, 1)); Rel(0x3C00, 0x11070);
            Type(0x2600, 0x50000000, 24, 0x2500, [0x11080, 0x11090, 0x110A0], [0x1A00, 0x1900]);
            int[] requested = [0x1900, 0x1F00, 0x2200, 0x2000, 0x2100];
            for (int i = 0; i < requested.Length; i++) CellRecord(0x5100 + i * 32, requested[i]);
            Heap = new() { Allocator = (BootPageAllocatorState*)(heapAllocation + 0x100), AllocatorCapacity = 1,
                Nodes = (BootPageFreeListNode*)(heapAllocation + 0x300), NodeCount = 1, NodeCapacity = 2,
                Reservations = (BootReservedRange*)(heapAllocation + 0x1000), ReservationCount = 2, ReservationCapacity = 3,
                ParentIndex = 1, Heap = (ManagedHeapPageRangeState*)(heapAllocation + 0x2000), HeapCapacity = 232,
                Backing = heapAllocation + 0x5000, BackingCapacity = 8208 };
            *Heap.Allocator = new(true, 4096, 0, 1, 2, 8192, 0, 0);
            Heap.Nodes[0] = new(0, 0, 0x300000000, 0x300002000, 2, 8192, -1);
            PhysicalAddress.TryCreateFromUefiBoundary(0x100000, out var image);
            PhysicalAddress.TryCreateFromUefiBoundary(0x300000000, out var arena);
            BootReservedRange.TryCreate(BootReservationKind.LoadedImage, image, 1, out Heap.Reservations[0]);
            BootReservedRange.TryCreate(BootReservationKind.AllocatorArena, arena, 2, out Heap.Reservations[1]);
            var heap = ManagedHeapPageRangeLiveAllocator.ReserveAndInitializeOnce(Heap.Allocator, 1, Heap.Nodes, 1, 2,
                Heap.Reservations, 2, 3, 1, Heap.Backing, Heap.BackingCapacity, Heap.Heap, 232);
            Check(heap.IsReady && Alloc.InitializeOnce(Heap, Allocator, Alloc.RequiredCollectingStateBytes).Succeeded &&
                Alloc.EnableCollection(Heap, Allocator, Alloc.RequiredCollectingStateBytes) == RhpNewFastStatus.Success, "original collecting owner");
        }

        internal Status Bind(ulong bytes = 160, uint cells = 8, uint cache = 8, byte* storage = null, ulong start = 0, bool admitSealed = true)
        {
            CompilerInterfaceDispatchSealedTable* tables = stackalloc CompilerInterfaceDispatchSealedTable[4];
            tables[0] = new() { DeclaringType = Address(0x2300), Table = Address(0x3900), Bytes = 12 };
            tables[1] = new() { DeclaringType = Address(0x2400), Table = Address(0x3A80), Bytes = 12 };
            tables[2] = new() { DeclaringType = Address(0x2500), Table = Address(0x3C00), Bytes = 4 };
            tables[3] = new() { DeclaringType = Address(0x2600), Table = Address(0x3E00), Bytes = DerivedSealedBytes };
            uint count = (*(uint*)(Image + 0x2600) & 0x00400000) != 0 ? 4U : 3U;
            return Core.Bind(Capture, Mapping, Owner, 4096, Handle, Heap, Allocator, Alloc.RequiredCollectingStateBytes,
                storage == null ? Runner : storage, Capacity, Address(0x11000), start == 0 ? Address(0x5100) : start, bytes, cells, cache,
                admitSealed ? tables : null, admitSealed ? count : 0);
        }
        internal RhpNewFastResult Allocate(int offset)
        {
            byte* p = Image + offset;
            var result = Alloc.TryAllocate(Heap, Allocator, Alloc.RequiredCollectingStateBytes, Profile,
                new(*(uint*)p, *(uint*)(p + 4), *(ulong*)(p + 8), *(ushort*)(p + 16), *(ushort*)(p + 18), *(uint*)(p + 20)), Address(offset));
            Check(result.AllocationSucceeded, "receiver allocation " + result.Status); return result;
        }
        internal CompilerInterfaceDispatchResolution Resolve(RhpNewFastResult receiver, int cell)
        {
            Expect(Core.Resolve(Runner, Capacity, receiver.ObjectReference, Cell(cell), out var result), Status.Success); return result;
        }
        internal void Reject(ulong receiver, int cell, Status expected)
        {
            Expect(Core.Resolve(Runner, Capacity, receiver, Cell(cell), out var result), expected);
            Check(result.Target == 0 && result.ReceiverType == 0 && result.AllocationSequence == 0 && !result.CacheHit, "rejection has no dispatch payload");
        }
        internal ulong Address(int offset) => (ulong)Image + (uint)offset;
        internal ulong Cell(int index) => Address(0x5100 + index * 32);
        internal void CellRecord(int offset, int type) { *(ulong*)(Image + offset) = Address(0x11000); Rel(offset + 8, type, 3); }
        internal void Rel(int at, int to, int tag = 0) => *(int*)(Image + at) = to - at + tag;
        internal void Type(int at, uint flags, uint size, int parent, int[] slots, int[] interfaces,
            int map = 0, int sealedTable = 0, int definition = 0, int composition = 0)
        {
            NativeMemory.Clear(Image + at, 0x100);
            *(uint*)(Image + at) = flags; *(uint*)(Image + at + 4) = size;
            *(ulong*)(Image + at + 8) = parent == 0 ? 0 : Address(parent);
            *(ushort*)(Image + at + 16) = (ushort)slots.Length; *(ushort*)(Image + at + 18) = (ushort)interfaces.Length;
            int tail = at + 24;
            foreach (int slot in slots) { *(ulong*)(Image + tail) = Address(slot); tail += 8; }
            foreach (int type in interfaces) { *(ulong*)(Image + tail) = Address(type); tail += 8; }
            Rel(tail, 0x5000); tail += 8;
            if ((flags & 0x00040000) != 0) { Rel(tail, map); tail += 4; }
            if ((flags & 0x00400000) != 0) { Rel(tail, sealedTable); tail += 4; }
            if ((flags & 0x02000000) != 0) { Rel(tail, definition); tail += 4; }
            if (composition != 0) Rel(tail, composition);
        }
        internal void Map(int at, params (ushort Index, ushort Slot, ushort Implementation)[] rows)
        {
            NativeMemory.Clear(Image + at, 0x60); *(ushort*)(Image + at) = (ushort)rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                byte* p = Image + at + 8 + i * 6;
                *(ushort*)p = rows[i].Index; *(ushort*)(p + 2) = rows[i].Slot; *(ushort*)(p + 4) = rows[i].Implementation;
            }
        }
        internal byte[] RunnerBytes() => new ReadOnlySpan<byte>(Runner, (int)Capacity).ToArray();
        internal byte[] ImageBytes() => new ReadOnlySpan<byte>(Image, (int)ImageSize).ToArray();
        internal void Guards() => Check(*(ulong*)(Runner - 8) == 0x0123456789ABCDEF && *(ulong*)(Runner + Capacity) == 0xFEDCBA9876543210, "controller canaries");
        private void Section(int i, string name, uint start, uint bytes, uint flags)
        {
            byte* p = Image + 0x188 + i * 40;
            for (int n = 0; n < name.Length; n++) p[n] = (byte)name[n];
            *(uint*)(p + 8) = bytes; *(uint*)(p + 12) = start; *(uint*)(p + 36) = flags;
        }
        public void Dispose()
        { NativeMemory.AlignedFree(Image); NativeMemory.Free(Owner); NativeMemory.Free(runnerAllocation); NativeMemory.AlignedFree(heapAllocation); }
    }
}
