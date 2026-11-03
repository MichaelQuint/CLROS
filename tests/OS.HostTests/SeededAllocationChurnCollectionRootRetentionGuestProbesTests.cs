using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Runtime;
using OS.Std;
using Fixture = BoundedNonMovingCollectorExplicitRootEnumerationTests.Fixture;
using HeapFixture = BoundedNonMovingCollectorExplicitRootEnumerationTests.HeapFixture;
using Probe = OS.Kernel.Runtime.SeededAllocationChurnCollectionRootRetentionGuestProbes;

internal static unsafe class SeededAllocationChurnCollectionRootRetentionGuestProbesTests
{
    [HostFixture(HostLane.Fast)]
    internal static void Run()
    {
        HostCheck.Run("m04.014", () =>
        {
            try
            {
                uint random = 1;
                foreach (uint expected in new uint[] { 270369, 67634689, 2647435461, 307599695 })
                    Require(DeterministicRandom32.Next(ref random) == expected, "xorshift32 known sequence");
                uint zero = 0, normalized = DeterministicRandom32.DefaultSeed;
                Require(DeterministicRandom32.Next(ref zero) == DeterministicRandom32.Next(ref normalized), "zero seed normalization");
                Require(!default(SeededChurnResult).Succeeded, "default is not success");
                foreach (uint seed in new uint[] { 0, 1, 0xDEADBEEF })
                {
                    using Fixture image = new();
                    using HeapFixture heap = new();
                    image.Type(0x50000000, 24, 0);
                    RhpNewFastResult permanent = heap.Allocate(image);
                    const ulong cookie = 0xFEED1234ABCDEF;
                    *(ulong*)(permanent.ObjectReference + 8) = cookie;
                    SeededChurnResult first = Execute(image, heap, seed, 64, permanent);
                    Require(first.Succeeded && first.CompletedRounds == 64 && first.Collections == 66 &&
                        first.Allocations == first.Reclaimed && first.ReusedAddresses > 0 && first.Fingerprint != 0,
                        "bounded churn completes, reuses and cleans up: " + first.Status);
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    SeededChurnResult second = Execute(image, heap, seed, 64, permanent);
                    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    Require(second.Succeeded && second.Fingerprint == first.Fingerprint &&
                        second.Allocations == first.Allocations && second.Reclaimed == first.Reclaimed && allocated == 0,
                        "repeat schedule is address/identity independent and allocates no host scratch");
                    Require(heap.Valid(permanent) && *(ulong*)(permanent.ObjectReference + 8) == cookie,
                        "external retained root and payload survive both runs");
                    var one = Execute(image, heap, seed, 1, permanent);
                    Require(one.Succeeded && one.CompletedRounds == 1 && one.Allocations == one.Reclaimed, "minimum round count");
                    heap.Guards();
                    image.Guards();
                }
                using (Fixture image = new())
                using (HeapFixture heap = new())
                {
                    image.Type(0x50000000, 24, 0);
                    byte[] before = heap.Snapshot();
                    foreach (uint rounds in new uint[] { 0, 65, uint.MaxValue })
                        Require(Execute(image, heap, 1, rounds).Status == SeededChurnStatus.InvalidRounds, "invalid round bound");
                    image.Type(0x50000000, 32, 0);
                    Require(Execute(image, heap, 1, 2).Status == SeededChurnStatus.TypeRejected, "non-probe layout denied");
                    heap.Unchanged(before);
                    image.Type(0x50000000, 24, 0);
                    var stale = heap.Allocate(image);
                    var cleanup = Execute(image, heap, 1, 2);
                    Require(cleanup.Succeeded && cleanup.InitialReclaimed == 1 && cleanup.InitialReclaimedBytes == 24 &&
                        !heap.Valid(stale), "unrooted initial objects retired and separately counted");
                    before = heap.Snapshot();
                    var rejected = Execute(image, heap, 1, 2, stale);
                    Require(rejected.Status == SeededChurnStatus.CollectionRejected &&
                        rejected.CollectionStatus == BoundedCollectionStatus.InvalidReference, "stale permanent root denied");
                    heap.Unchanged(before);
                }
                using (Fixture image = new())
                using (HeapFixture heap = new(false))
                {
                    image.Type(0x50000000, 24, 0);
                    byte[] before = heap.Snapshot();
                    Require(Execute(image, heap, 1, 2).Status == SeededChurnStatus.CollectionRejected, "noncollecting owner denied");
                    heap.Unchanged(before);
                }
                StaticRoot();
            }
            catch (Exception error)
            {
                Console.WriteLine("TEST_FAIL m04.014 " + error.Message);
                throw;
            }
        });
    }

    private static SeededChurnResult Execute(Fixture image, HeapFixture heap, uint seed, uint rounds,
        RhpNewFastResult permanent = default, CollectorStaticRootAccess retained = default) => Probe.Run(new CollectorModuleAccess
        {
            Capture = image.Capture,
            Mapping = image.Mapping,
            Owner = image.Owner,
            Capacity = 4096,
            Handle = image.Handle,
            Metadata = image.Window
        }, heap.Access, heap.State, HeapFixture.Capacity,
            image.Mt, seed, rounds, permanent, retained);

    private static void StaticRoot()
    {
        using Fixture image = new();
        using HeapFixture heap = new();
        image.Type(0x50000000, 24, 0);
        ulong staticMt = image.Address(0x1800), systemObject = image.Address(0x1180);
        *(uint*)(image.Image + 0x1200) = 0x1000;
        *(ulong*)(image.Image + 0x2200) = unchecked((uint)(0x1800 - 0x2200 + 1));
        *(long*)(staticMt - 24) = -16;
        *(ulong*)(staticMt - 16) = 8;
        *(ulong*)(staticMt - 8) = 1;
        *(uint*)staticMt = 0x01000000;
        *(uint*)(staticMt + 4) = 24;
        *(ulong*)(staticMt + 8) = systemObject;
        *(uint*)systemObject = 0x50000000;
        *(uint*)(systemObject + 4) = 24;
        CollectorStaticRootAccess retained = new()
        {
            Owner = heap.Storage + 0x9000,
            Capacity = GCStaticRootsMalformedMetadata.RequiredRootOwnerBytes,
            Cells = new(image.Address(0x2200), 8),
            SystemObjectMethodTable = systemObject
        };
        NativeMemory.Clear(retained.Owner, (nuint)retained.Capacity);
        var root = GCStaticRootsMalformedMetadata.MaterializeOnce(image.Capture, image.Mapping,
            image.Owner, 4096, image.Handle, image.Window, retained.Cells, systemObject,
            heap.Access, heap.State, HeapFixture.Capacity, retained.Owner, retained.Capacity);
        Require(root.Succeeded, "original static base materialized");
        var child = heap.Allocate(image);
        *(ulong*)(root.ObjectReference + 8) = child.ObjectReference;
        *(ulong*)(child.ObjectReference + 8) = 0xCAFE1234;
        var result = Execute(image, heap, 0xBADF00D, 64, retained: retained);
        Require(result.Succeeded && result.BaselineLiveObjects == 2 && heap.Valid(child) &&
            *(ulong*)(root.ObjectReference + 8) == child.ObjectReference &&
            *(ulong*)(child.ObjectReference + 8) == 0xCAFE1234, "sole static child survives seeded churn and final cleanup");
        var queried = GCStaticRootsMalformedMetadata.TryGetRetainedRoot(image.Capture, image.Mapping,
            image.Owner, 4096, image.Handle, image.Window, retained.Cells, systemObject,
            heap.Access, heap.State, HeapFixture.Capacity, retained.Owner, retained.Capacity);
        Require(queried.Succeeded && queried.ObjectReference == root.ObjectReference, "static owner receipt survives churn");
        heap.Guards();
        image.Guards();
    }

    private static void Require(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }
}
