using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Boot;
using OS.Kernel.Memory;
using OS.Kernel.Runtime;
using Core = OS.Kernel.Runtime.CompilerInterfaceDispatch;
using Status = OS.Kernel.Runtime.CompilerInterfaceDispatchStatus;
using Alloc = OS.Kernel.Runtime.GuestTestRhpNewFast;
using Module = OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage;

internal static unsafe partial class CompilerInterfaceDispatchTests
{
    private static void Check(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }

    private static void Expect(Status actual, Status expected) => Check(actual == expected, "expected=" + expected + " actual=" + actual);
    [HostTest("m04.016.interface-dispatch.exact-and-cache", HostLane.Fast, 0)]
    private static void ExactAndCache()
    {
        using var f = new Fixture();
        Check(Core.RequiredStorage(64, 64) <= Fixture.Capacity && Core.RequiredStorage(0, 1) == 0 && Core.RequiredStorage(65, 1) == 0 && Core.RequiredStorage(1, 65) == 0, "finite public sizing");
        var a = f.Allocate(0x2300);
        var b = f.Allocate(0x2400);
        Expect(f.Bind(), Status.Success);
        byte[] image = f.ImageBytes();
        var first = f.Resolve(a, 0);
        Check(first.Target == f.Address(0x11010) && first.DeclaringType == f.Address(0x2300) && first.ReceiverType == a.MethodTableToken && first.RequestedInterface == f.Address(0x1900) && first.ImplementedInterface == first.RequestedInterface && !first.CacheHit && first.AllocationSequence == 1, "first exact dispatch identity");
        Check(f.Resolve(a, 0).CacheHit && !f.Resolve(b, 0).CacheHit && f.Resolve(b, 0).Target == f.Address(0x11020), "one cell alternates genuine receiver types");
        Expect(Core.Observe(f.Runner, Fixture.Capacity, out var progress), Status.Success);
        Check(progress.CellCount == 5 && progress.CacheCount == 2 && progress.CacheHits == 2 && progress.Resolutions == 4 && progress.MetadataWords > 0, "exact cache counters");
        Check(f.ImageBytes().AsSpan().SequenceEqual(image), "no compiler cell or image writes");
        f.Guards();
        Console.WriteLine("M04_DISPATCH_HOST_LAYOUT state=" + Core.StateBytes + " required64=" + Core.RequiredStorage(64, 64));
    }

    [HostTest("m04.016.interface-dispatch.reference-variance", HostLane.Fast, 1)]
    private static void ReferenceVariance()
    {
        using var f = new Fixture();
        var a = f.Allocate(0x2300);
        var b = f.Allocate(0x2400);
        Expect(f.Bind(), Status.Success);
        var covariant = f.Resolve(a, 1);
        Check(covariant.RequestedInterface == f.Address(0x1F00) && covariant.ImplementedInterface == f.Address(0x2000) && covariant.Target == f.Address(0x11030), "Producer<Derived> -> Producer<Base>");
        var contravariant = f.Resolve(a, 2);
        Check(contravariant.RequestedInterface == f.Address(0x2200) && contravariant.ImplementedInterface == f.Address(0x2100) && contravariant.Target == f.Address(0x11040), "Consumer<Base> -> Consumer<Derived>");
        Check(!f.Resolve(b, 1).CacheHit && f.Resolve(b, 1).ImplementedInterface == f.Address(0x1F00) && f.Resolve(b, 1).Target == f.Address(0x11050), "same generic cell does not reuse alternate instantiation");
        f.Reject(b.ObjectReference, 3, Status.IncompatibleInterface);
        f.Reject(b.ObjectReference, 4, Status.IncompatibleInterface);
        Check(f.Resolve(a, 1).CacheHit, "variant hit revalidated");
        // Per-level exact before variance, but a derived compatible implementation
        // wins before an exact implementation belonging to its base class.
        using var levels = new Fixture();
        levels.Type(0x2500, 0x50440000, 24, 0, [0x11070], [0x1F00], 0x3B00, 0x3C00);
        levels.Map(0x3B00, (0, 0, 1));
        levels.Rel(0x3C00, 0x11070);
        levels.Type(0x2600, 0x50440000, 24, 0x2500, [0x11080], [0x2000, 0x1F00], 0x3D00, 0x3E00);
        levels.Map(0x3D00, (0, 0, 1), (1, 0, 2));
        levels.Rel(0x3E00, 0x11080);
        levels.Rel(0x3E04, 0x11090);
        levels.DerivedSealedBytes = 8;
        var derived = levels.Allocate(0x2600);
        Expect(levels.Bind(), Status.Success);
        Check(levels.Resolve(derived, 1).Target == levels.Address(0x11090), "current-owner exact precedes variance");
        using var variantLevel = new Fixture();
        variantLevel.Type(0x2500, 0x50440000, 24, 0, [0x11070], [0x1F00], 0x3B00, 0x3C00);
        variantLevel.Map(0x3B00, (0, 0, 1));
        variantLevel.Rel(0x3C00, 0x11070);
        variantLevel.Type(0x2600, 0x50440000, 24, 0x2500, [0x11080], [0x2000], 0x3D00, 0x3E00);
        variantLevel.Map(0x3D00, (0, 0, 1));
        variantLevel.Rel(0x3E00, 0x11080);
        derived = variantLevel.Allocate(0x2600);
        Expect(variantLevel.Bind(), Status.Success);
        var result = variantLevel.Resolve(derived, 1);
        Check(result.Target == variantLevel.Address(0x11080) && result.DeclaringType == derived.MethodTableToken, "derived variance precedes base exact");
    }

    [HostTest("m04.016.interface-dispatch.declaring-owner", HostLane.Fast, 2)]
    private static void DeclaringOwner()
    {
        using var f = new Fixture();
        var derived = f.Allocate(0x2600);
        Expect(f.Bind(), Status.Success);
        var sealedResult = f.Resolve(derived, 0);
        Check(sealedResult.DeclaringType == f.Address(0x2500) && sealedResult.ImplementationSlot == 1 && sealedResult.Target == f.Address(0x11070) && sealedResult.Target != *(ulong*)(f.Image + 0x2620), "sealed index uses base owner slot count, not derived slot count or interface order");
        using var virtualCase = new Fixture();
        virtualCase.Map(0x3B00, (0, 0, 0));
        derived = virtualCase.Allocate(0x2600);
        Expect(virtualCase.Bind(), Status.Success);
        var virtualResult = virtualCase.Resolve(derived, 0);
        Check(virtualResult.DeclaringType == virtualCase.Address(0x2500) && virtualResult.ImplementationSlot == 0 && virtualResult.Target == virtualCase.Address(0x11080), "true virtual uses original receiver override");
    }

    [HostTest("m04.016.interface-dispatch.receiver-lifetime", HostLane.Fast, 3)]
    private static void ReceiverLifetime()
    {
        using var f = new Fixture();
        var first = f.Allocate(0x2300);
        Expect(f.Bind(), Status.Success);
        f.Resolve(first, 0);
        f.Reject(0, 0, Status.NullReceiver);
        f.Reject(1, 0, Status.ReceiverRejected);
        f.Reject(ulong.MaxValue, 0, Status.ReceiverRejected);
        f.Reject(first.ObjectReference + 8, 0, Status.ReceiverRejected);
        var staticBase = Alloc.TryAllocateStaticBase(f.Heap, f.Allocator, Alloc.RequiredCollectingStateBytes, Fixture.Profile, 0x01000000, 24, f.Address(0x1900));
        Check(staticBase.AllocationSucceeded, "static allocation setup");
        f.Reject(staticBase.ObjectReference, 0, Status.ReceiverRejected);
        using var collected = new Fixture();
        first = collected.Allocate(0x2300);
        Expect(collected.Bind(), Status.Success);
        collected.Resolve(first, 0);
        var sweep = BoundedNonMovingCollectorExplicitRootEnumeration.Collect(Fixture.Profile, new()
        {
            Capture = collected.Capture,
            Mapping = collected.Mapping,
            Owner = collected.Owner,
            Capacity = 4096,
            Handle = collected.Handle,
            Metadata = new(collected.Address(0x1400), 0x3C00)
        }, collected.Heap, collected.Allocator, Alloc.RequiredCollectingStateBytes, null, 0, 0);
        Check(sweep.Succeeded && sweep.ReclaimedObjects == 1, "actual collector reclaimed receiver " + sweep.Status);
        collected.Reject(first.ObjectReference, 0, Status.ReceiverRejected);
        var next = collected.Allocate(0x2400);
        Check(next.ObjectReference == first.ObjectReference && next.AllocationCount != first.AllocationCount, "allocator reuses address with new receipt");
        var result = collected.Resolve(next, 0);
        Check(!result.CacheHit && result.Target == collected.Address(0x11020) && result.AllocationSequence == next.AllocationCount, "old receipt cache cannot serve new lifetime");
    }

    [HostTest("m04.016.interface-dispatch.original-authorities", HostLane.Fast, 4)]
    private static void OriginalAuthorities()
    {
        using var f = new Fixture();
        var receiver = f.Allocate(0x2300);
        Expect(f.Bind(), Status.Success);
        f.Resolve(receiver, 0);
        byte* copied = (byte*)NativeMemory.Alloc((nuint)Fixture.Capacity);
        try
        {
            NativeMemory.Copy(f.Runner, copied, (nuint)Fixture.Capacity);
            Expect(Core.Resolve(copied, Fixture.Capacity, receiver.ObjectReference, f.Cell(0), out var rejected), Status.StateChanged);
            Check(rejected.Target == 0, "copied controller no target");
        }
        finally
        {
            NativeMemory.Free(copied);
        }

        f.Owner[0] ^= 1;
        f.Reject(receiver.ObjectReference, 0, Status.ModuleRejected);
        f.Owner[0] ^= 1;
        ((byte*)f.Allocator)[0] = 0;
        f.Reject(receiver.ObjectReference, 0, Status.AllocatorRejected);
        using var alias = new Fixture();
        Expect(alias.Bind(storage: alias.Owner), Status.StorageRejected);
        using var inline = new Fixture(inline: true);
        receiver = inline.Allocate(0x2300);
        Expect(inline.Bind(), Status.Success);
        inline.Resolve(receiver, 0);
        inline.Guards();
    }

    [HostTest("m04.016.interface-dispatch.cell-encoding", HostLane.Fast, 5)]
    private static void CellEncoding()
    {
        foreach (ulong word in new ulong[]
        {
            0xFF8,
            1,
            2,
            0x100000003
        }

        )
        {
            using var f = new Fixture();
            *(ulong*)(f.Image + 0x5108) = word;
            Expect(f.Bind(), Status.CellRejected);
            Check(f.RunnerBytes().All(b => b == 0), "cell rejection before controller write");
        }

        using (var f = new Fixture())
        {
            Expect(f.Bind(bytes: 16), Status.CellRejected);
            Expect(f.Bind(bytes: 96), Status.CellRejected);
            Expect(f.Bind(start: f.Cell(1), bytes: 64), Status.CellRejected);
            Expect(f.Bind(start: ulong.MaxValue - 15), Status.CellRejected);
            Expect(f.Bind(cells: 4), Status.CellRejected);
        }

        using (var f = new Fixture())
        {
            *(ulong*)(f.Image + 0x5100) = f.Address(0x11000);
            f.Rel(0x5108, 0x1900, 3); // A .NET 10 initial-stub/cache cell is never admitted.
            Expect(f.Bind(), Status.CellRejected);
        }

        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            f.Resolve(receiver, 0);
            f.Rel(0x5108, 0x1A00, 3);
            f.Reject(receiver.ObjectReference, 0, Status.CellChanged);
        }

        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            Expect(Core.Resolve(f.Runner, Fixture.Capacity, receiver.ObjectReference, f.Cell(0) + 8, out _), Status.CellRejected);
        }
    }

    [HostTest("m04.016.interface-dispatch.native-array", HostLane.Fast, 8)]
    private static void NativeArray()
    {
        // Independently authored single sparse entry covers every subsequent
        // zero cell in the same target run, including indices beyond array count.
        using (var f = new Fixture(info: [8, 1, 0, 0, 0]))
        {
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            Check(f.Resolve(receiver, 4).Target == f.Address(0x11010), "sparse preceding target run");
            f.Image[0x1204] = 2;
            f.Reject(receiver.ObjectReference, 4, Status.MetadataChanged);
        }

        foreach (byte[] info in new byte[][]
        {
            [],
            [0],
            [14, 1, 0, 0, 0],
            [8, 255],
            [8, 1, 31],
            [8, 1, 0, 0],
            [8, 1, 8, 0, 0],
            [8, 1, 0, 10, 0],
            [8, 1, 0, 0, 128],
            [8, 1, 0, 15, 255, 255, 255, 255, 0]
        }

        )
        {
            using var f = new Fixture(info: info);
            var status = f.Bind();
            Check(status is Status.MetadataRejected or Status.SlotRejected, "malformed NativeArray rejected: " + status);
            Check(f.RunnerBytes().All(b => b == 0), "malformed metadata leaves owner unbound");
        }

        // All legal root index widths, and all unsigned integer widths, use
        // exactly the same independently known {reference 0, slot 0} tuple.
        foreach (byte[] info in new byte[][]
        {
            [10, 2, 0, 0, 0, 0],
            [12, 4, 0, 0, 0, 0, 0, 0],
            [8, 1, 0, 1, 0, 0],
            [8, 1, 0, 3, 0, 0, 0],
            [8, 1, 0, 7, 0, 0, 0, 0],
            [8, 1, 0, 15, 0, 0, 0, 0, 0]
        }

        )
        {
            using var f = new Fixture(info: info);
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            Check(f.Resolve(receiver, 4).RequestedInterface == f.Address(0x1900), "index and integer widths");
        }

        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            f.Rel(0x1300, 0x1A00);
            f.Reject(receiver.ObjectReference, 0, Status.MetadataChanged);
        }

        foreach (var absent in new[]
        {
            (208U, 210U, 331U, Status.MetadataRejected),
            (209U, 211U, 331U, Status.CellRejected),
            (209U, 210U, 332U, Status.MetadataRejected)
        }

        )
        {
            using var f = new Fixture(infoId: absent.Item1, cellsId: absent.Item2, referencesId: absent.Item3);
            Expect(f.Bind(), absent.Item4);
            Check(f.RunnerBytes().All(b => b == 0), "required ABI section absent before owner publication");
        }
    }

    [HostTest("m04.016.interface-dispatch.metadata-and-targets", HostLane.Fast, 6)]
    private static void MetadataAndTargets()
    {
        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            f.Resolve(receiver, 0);
            *(ulong*)(f.Image + 0x2320) ^= 8;
            f.Reject(receiver.ObjectReference, 0, Status.MetadataChanged);
        }

        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            f.Resolve(receiver, 0);
            // Cache layout starts with Receiver, Cell, then public Resolution.Target.
            *(ulong*)(f.Runner + Core.StateBytes + 8 * 32 + 16) ^= 8;
            f.Reject(receiver.ObjectReference, 0, Status.CacheChanged);
        }

        foreach (int badTarget in new[]
        {
            0x1900,
            0x6000
        }

        )
        {
            using var f = new Fixture();
            f.Rel(0x3900, badTarget);
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            f.Reject(receiver.ObjectReference, 0, Status.TargetRejected);
        }

        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            // The original TypeManager rejects RWX even before target admission.
            *(uint*)(f.Image + 0x188 + 3 * 40 + 36) = 0xE0000020;
            f.Reject(receiver.ObjectReference, 0, Status.ModuleRejected);
        }

        using (var f = new Fixture())
        {
            f.Map(0x3800, (0, 0, 65));
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            f.Reject(receiver.ObjectReference, 0, Status.SlotRejected);
        }

        using (var f = new Fixture())
        {
            // The next RO word deliberately looks like a valid RX relocation.
            // It is outside A's exact three-entry native table contribution.
            f.Rel(0x390C, 0x11020);
            f.Map(0x3800, (0, 0, 4));
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            f.Reject(receiver.ObjectReference, 0, Status.SlotRejected);
        }

        using (var f = new Fixture())
        {
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(admitSealed: false), Status.Success);
            f.Reject(receiver.ObjectReference, 0, Status.SlotRejected);
        }

        using (var f = new Fixture())
        {
            f.Map(0x3800, (0, 0, 1), (0, 0, 1));
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            f.Reject(receiver.ObjectReference, 0, Status.AmbiguousImplementation);
        }

        foreach (int map in new[]
        {
            0x3800,
            0x4FFC
        }

        )
        {
            using var f = new Fixture();
            if (map == 0x3800)
                *(ushort*)(f.Image + map) = 65;
            else
                f.Rel(0x2340, map);
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            f.Reject(receiver.ObjectReference, 0, map == 0x3800 ? Status.MapRejected : Status.MetadataRejected);
        }

        using (var f = new Fixture())
        {
            *(ushort*)(f.Image + 0x3808) = 3;
            var receiver = f.Allocate(0x2300);
            Expect(f.Bind(), Status.Success);
            f.Reject(receiver.ObjectReference, 0, Status.MapRejected);
        }
    }

    [HostTest("m04.016.interface-dispatch.finite-cache", HostLane.Fast, 7)]
    private static void FiniteCache()
    {
        using var f = new Fixture();
        var a = f.Allocate(0x2300);
        var b = f.Allocate(0x2400);
        Expect(f.Bind(cache: 1), Status.Success);
        f.Resolve(a, 0);
        byte[] before = f.RunnerBytes();
        f.Reject(b.ObjectReference, 0, Status.CapacityExceeded);
        Check(f.RunnerBytes().AsSpan().SequenceEqual(before), "capacity rejection rolls back partial metadata and cache");
        Check(f.Resolve(a, 0).CacheHit, "existing entry survives exhaustion");
        f.Guards();
    }
}
