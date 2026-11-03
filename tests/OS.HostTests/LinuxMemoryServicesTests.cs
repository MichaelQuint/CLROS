using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;

internal static unsafe partial class LinuxMemoryServicesTests
{
    private const ulong Root = 0x1000000, Pool = 0x2000000, Start = 0x100000000, Reservation = 8234266624;
    private const uint TablePages = 16, PoolPages = 16;
    private static long Map(Owner owner, ulong address, ulong bytes, uint protection, ulong flags = 0x100022) => LinuxMemoryServices.Map(owner.State, address, bytes, protection, flags, ulong.MaxValue, 0);
    [HostTest("linux.memory complete mixed backing demand rejects atomically before allocation and preserves retained none pages")]
    private static void MixedBackingCapacity()
    {
        using var owner = new Owner(backingPages: 4);
        owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 2 * 4096, 3));
        *owner.At(Start) = 0x73;
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start, 4096, 0));
        HostCheck.Equal((long)Start + 8192, Map(owner, Start + 8192, 3 * 4096, 0));
        var before = Snapshot(owner);
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(owner.State, Start, 5 * 4096, 3));
        Same(owner, before);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start, 4 * 4096, 3));
        HostCheck.Equal(4U, owner.State->BackedPages);
        HostCheck.Equal((byte)0x73, *owner.At(Start));
        HostCheck.True(new ReadOnlySpan<byte>(owner.At(Start + 8192), 8192).IndexOfAnyExcept((byte)0) < 0);
    }

    [HostTest("linux.memory full pool fixed replacement reuses eligible backing without double charging")]
    private static void ReplacementBackingCapacity()
    {
        using var owner = new Owner(backingPages: 4);
        owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 4 * 4096, 3));
        *owner.At(Start) = 0x41;
        *owner.At(Start + 3 * 4096) = 0x63;
        HostCheck.Equal((long)Start + 4096, Map(owner, Start + 4096, 2 * 4096, 3, 0x32));
        HostCheck.Equal(4U, owner.State->BackedPages);
        HostCheck.Equal((byte)0x41, *owner.At(Start));
        HostCheck.Equal((byte)0x63, *owner.At(Start + 3 * 4096));
        HostCheck.True(new ReadOnlySpan<byte>(owner.At(Start + 4096), 8192).IndexOfAnyExcept((byte)0) < 0);
        var before = Snapshot(owner);
        HostCheck.Equal(-12L, Map(owner, Start + 4096, 4 * 4096, 3, 0x32));
        Same(owner, before);
    }

    [HostTest("linux.memory backing high-water counts published commits and survives replacement release and cleanup")]
    private static void BackingHighWater()
    {
        using var owner = new Owner();
        owner.Begin();
        HostCheck.Equal(0U, owner.State->PeakBackedPages);
        HostCheck.Equal((long)Start, Map(owner, Start, 2 * 4096, 3));
        HostCheck.Equal(2U, owner.State->PeakBackedPages);
        HostCheck.Equal((long)(Start + 0x10000), Map(owner, Start + 0x10000, 3 * 4096, 0));
        HostCheck.Equal(2U, owner.State->BackedPages);
        HostCheck.Equal(2U, owner.State->PeakBackedPages);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + 0x10000, 3 * 4096, 3));
        HostCheck.Equal(5U, owner.State->BackedPages);
        HostCheck.Equal(5U, owner.State->PeakBackedPages);
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start, 2 * 4096));
        HostCheck.Equal(3U, owner.State->BackedPages);
        HostCheck.Equal(5U, owner.State->PeakBackedPages);
        HostCheck.Equal((long)Start, Map(owner, Start, 4 * 4096, 3));
        HostCheck.Equal(7U, owner.State->PeakBackedPages);
        HostCheck.Equal((long)Start, Map(owner, Start, 4096, 3, 0x32));
        HostCheck.Equal(7U, owner.State->BackedPages);
        HostCheck.Equal(7U, owner.State->PeakBackedPages);
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start, 0x20000));
        HostCheck.Equal(0U, owner.State->BackedPages);
        HostCheck.Equal(7U, owner.State->PeakBackedPages);
        HostCheck.True(LinuxMemoryServices.Audit(owner.State));
    }

    [HostTest("linux.memory rejected pool and table commits leave backing high-water and published ownership unchanged")]
    private static void RejectedBackingHighWater()
    {
        using var owner = new Owner(backingPages: 4);
        owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 2 * 4096, 3));
        var before = Snapshot(owner);
        HostCheck.Equal(-12L, Map(owner, Start + 0x10000, 3 * 4096, 3));
        Same(owner, before);
        HostCheck.Equal(2U, owner.State->PeakBackedPages);
        using var tight = new Owner(tables: 6);
        tight.Begin();
        before = Snapshot(tight);
        HostCheck.Equal(-12L, Map(tight, Start, 4096, 3));
        Same(tight, before);
        HostCheck.Equal(0U, tight.State->PeakBackedPages);
    }

    private static LinuxMemoryServices.Admission ImageAdmission(Owner owner)
    {
        var memory = owner.Memory;
        memory.StaticRanges[2] = new()
        {
            VirtualStart = 0x401000,
            PhysicalStart = 0x301000,
            Pages = 3,
            Protection = PageProtection.Read,
            Cache = PageCachePolicy.WriteBack,
            User = true
        };
        memory.StaticRanges[3] = new()
        {
            VirtualStart = 0x404000,
            PhysicalStart = 0x304000,
            Pages = 1,
            Protection = PageProtection.Read | PageProtection.Write,
            Cache = PageCachePolicy.WriteBack,
            User = true
        };
        memory.StaticRanges[4] = new()
        {
            VirtualStart = 0x6ffff000,
            PhysicalStart = 0x306000,
            Pages = 1,
            Protection = PageProtection.Read | PageProtection.Write,
            Cache = PageCachePolicy.WriteBack,
            User = true
        };
        memory.ImageVirtualStart = 0x400000;
        memory.ImagePhysicalStart = 0x300000;
        memory.ImageBytes = 0x6000;
        return memory;
    }

    private static (byte[] State, byte[] Tables, byte[] Pool, byte[] Static) Snapshot(Owner owner) => (new ReadOnlySpan<byte>(owner.State, sizeof(LinuxMemoryServices.State)).ToArray(), owner.Tables(), new ReadOnlySpan<byte>(owner.Memory.Pool, (int)owner.Memory.PoolPages * 4096).ToArray(), new ReadOnlySpan<byte>(owner.Memory.StaticRanges, (int)owner.Memory.StaticCount * sizeof(X64UserMappingRange)).ToArray());
    private static void Same(Owner owner, (byte[] State, byte[] Tables, byte[] Pool, byte[] Static) before)
    {
        HostCheck.True(before.State.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.State, before.State.Length)));
        HostCheck.True(before.Tables.AsSpan().SequenceEqual(owner.Tables()));
        HostCheck.True(before.Pool.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.Pool, before.Pool.Length)));
        HostCheck.True(before.Static.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.StaticRanges, before.Static.Length)));
    }

    private static LinuxMemoryServices.Observation ObserveUnchanged(Owner owner, uint backed, uint runs, uint largest, uint regions)
    {
        var before = Snapshot(owner);
        byte[] candidate = new ReadOnlySpan<byte>(owner.Memory.CandidateTables, (int)owner.Memory.TablePages * 4096).ToArray();
        HostCheck.True(LinuxMemoryServices.TryObserve(owner.State, out var observed));
        Same(owner, before);
        HostCheck.True(candidate.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.CandidateTables, candidate.Length)));
        HostCheck.Equal(owner.Memory.PoolPages, observed.PoolPages);
        HostCheck.Equal(backed, observed.BackedPages);
        HostCheck.Equal(owner.Memory.PoolPages - backed, observed.FreePages);
        HostCheck.Equal(runs, observed.FreeRuns);
        HostCheck.Equal(largest, observed.LargestFreeRunPages);
        HostCheck.Equal(regions, observed.DynamicRegions);
        HostCheck.Equal(LinuxMemoryServices.MaximumRegions, observed.RegionCapacity);
        HostCheck.Equal(owner.State->Root.UsedTables, observed.UsedTables);
        HostCheck.Equal(owner.Memory.TablePages, observed.TableCapacity);
        return observed;
    }

    [HostTest("linux.memory observation counts physical holes independently of virtual and physical extent order")]
    private static void ObservationPhysicalRuns()
    {
        using var empty = new Owner();
        empty.Begin();
        ObserveUnchanged(empty, 0, 1, PoolPages, 0);
        using var owner = FragmentedPool();
        ObserveUnchanged(owner, 8, 2, 4, 2);
        // Two physical holes back one new virtual interval beyond the old owners.
        HostCheck.Equal((long)(Start + 0x100000), Map(owner, Start + 0x100000, 6 * 4096, 3));
        ObserveUnchanged(owner, 14, 1, 2, 4);
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start + 0x10000, 4 * 4096));
        ObserveUnchanged(owner, 10, 2, 4, 3);
    }

    [HostTest("linux.memory observation retains none backing excludes virtual-only reservations and reports current tables")]
    private static void ObservationRetainedNone()
    {
        using var owner = new Owner(backingPages: 4);
        owner.Begin();
        uint staticTables = ObserveUnchanged(owner, 0, 1, 4, 0).UsedTables;
        HostCheck.Equal((long)Start, Map(owner, Start, 4 * 4096, 3));
        HostCheck.True(ObserveUnchanged(owner, 4, 0, 0, 1).UsedTables > staticTables);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start, 4 * 4096, 0));
        HostCheck.Equal(staticTables, ObserveUnchanged(owner, 4, 0, 0, 1).UsedTables);
        HostCheck.Equal((long)(Start + 0x10000), Map(owner, Start + 0x10000, 2 * 4096, 0));
        HostCheck.Equal(staticTables, ObserveUnchanged(owner, 4, 0, 0, 2).UsedTables);
        HostCheck.Equal(0L, LinuxMemoryServices.Advise(owner.State, Start, 4 * 4096, 4));
        HostCheck.Equal(staticTables, ObserveUnchanged(owner, 0, 1, 4, 2).UsedTables);
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start, 0x20000));
        HostCheck.Equal(staticTables, ObserveUnchanged(owner, 0, 1, 4, 0).UsedTables);
    }

    [HostTest("linux.memory observation reflects only published fixed replacement backing and metadata")]
    private static void ObservationFixedReplacement()
    {
        using var owner = new Owner(backingPages: 8);
        owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 6 * 4096, 3));
        HostCheck.Equal((long)(Start + 0x10000), Map(owner, Start + 0x10000, 2 * 4096, 3));
        ObserveUnchanged(owner, 8, 0, 0, 2);
        HostCheck.Equal((long)(Start + 2 * 4096), Map(owner, Start + 2 * 4096, 2 * 4096, 0, 0x32));
        ObserveUnchanged(owner, 6, 1, 2, 4);
        HostCheck.Equal((long)Start, Map(owner, Start, 6 * 4096, 0, 0x32));
        ObserveUnchanged(owner, 2, 1, 6, 2);
        HostCheck.Equal((long)Start, Map(owner, Start, 6 * 4096, 3, 0x32));
        ObserveUnchanged(owner, 8, 0, 0, 2);
    }

    [HostTest("linux.memory observation preserves physical and metadata rollback and rejects invalid state without mutation")]
    private static void ObservationRollback()
    {
        using var tight = FragmentedPool(tables: 8);
        ObserveUnchanged(tight, 8, 2, 4, 2);
        var before = Snapshot(tight);
        HostCheck.Equal(-12L, Map(tight, Start + 0x40000000, 6 * 4096, 3));
        Same(tight, before);
        ObserveUnchanged(tight, 8, 2, 4, 2);
        using var owner = FragmentedPool();
        before = Snapshot(owner);
        HostCheck.Equal(-12L, Map(owner, Start + 0x100000, 9 * 4096, 3));
        Same(owner, before);
        ObserveUnchanged(owner, 8, 2, 4, 2);
        for (uint i = 0; i < LinuxMemoryServices.MaximumRegions - 3; i++)
            HostCheck.Equal((long)(Start + 0x200000 + i * 8192), Map(owner, Start + 0x200000 + i * 8192, 4096, 0));
        before = Snapshot(owner);
        HostCheck.Equal(-12L, Map(owner, Start + 0x100000, 6 * 4096, 3));
        Same(owner, before);
        var observed = ObserveUnchanged(owner, 8, 2, 4, LinuxMemoryServices.MaximumRegions - 1);
        HostCheck.True(!LinuxMemoryServices.TryObserve(null, out observed));
        EmptyObservation(observed);
        using var invalid = new Owner();
        before = Snapshot(invalid);
        HostCheck.True(!LinuxMemoryServices.TryObserve(invalid.State, out observed));
        EmptyObservation(observed);
        Same(invalid, before);
        // Altered admitted tables cannot be copied as an accepted observation.
        owner.Memory.Tables[0] ^= 4;
        before = Snapshot(owner);
        HostCheck.True(!LinuxMemoryServices.TryObserve(owner.State, out observed));
        EmptyObservation(observed);
        Same(owner, before);
        owner.Memory.Tables[0] ^= 4;
        HostCheck.True(LinuxMemoryServices.Audit(owner.State));
    }

    private static void EmptyObservation(LinuxMemoryServices.Observation observed) => HostCheck.True(observed.PoolPages == 0 && observed.BackedPages == 0 && observed.FreePages == 0 && observed.FreeRuns == 0 && observed.LargestFreeRunPages == 0 && observed.DynamicRegions == 0 && observed.RegionCapacity == 0 && observed.UsedTables == 0 && observed.TableCapacity == 0);
    [HostTest("linux.memory owned image cookie permissions survive mmap and restore original leaf identity")]
    private static void ImageRoundTrip()
    {
        using var owner = new Owner(staticRows: 5);
        var memory = ImageAdmission(owner);
        HostCheck.True(LinuxMemoryServices.Begin(owner.State, memory));
        byte[] original = owner.Tables(), staticRows = Snapshot(owner).Static;
        ulong code = owner.Leaf(0x400000), left = owner.Leaf(0x401000), word = owner.Leaf(0x402000), right = owner.Leaf(0x403000);
        HostCheck.Equal(0x8000000000302005UL, word);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x402000, 4096, 1));
        HostCheck.True(!owner.State->HasImageProtections && original.AsSpan().SequenceEqual(owner.Tables()));
        HostCheck.Equal((long)Start, Map(owner, Start, 4096, 3));
        *owner.At(Start) = 0x5a;
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x402000, 1, 3));
        HostCheck.True(owner.State->HasImageProtections && LinuxMemoryServices.Audit(owner.State));
        HostCheck.Equal(word | 2, owner.Leaf(0x402000));
        HostCheck.Equal(left, owner.Leaf(0x401000));
        HostCheck.Equal(right, owner.Leaf(0x403000));
        HostCheck.Equal(code, owner.Leaf(0x400000));
        HostCheck.Equal((long)(Start + 8192), Map(owner, Start + 8192, 4096, 3));
        HostCheck.Equal(word | 2, owner.Leaf(0x402000));
        HostCheck.True(*owner.At(Start) == 0x5a);
        HostCheck.Equal(8192UL, owner.State->ReservedBytes);
        HostCheck.Equal(2U, owner.State->BackedPages);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x402000, 4096, 1));
        HostCheck.True(!owner.State->HasImageProtections);
        HostCheck.Equal(word, owner.Leaf(0x402000));
        // Whole-row and originally writable data use the same owned R/RW rule.
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x401000, 3 * 4096, 3));
        HostCheck.Equal(left | 2, owner.Leaf(0x401000));
        HostCheck.Equal(right | 2, owner.Leaf(0x403000));
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x401000, 3 * 4096, 1));
        ulong writable = owner.Leaf(0x404000);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x404000, 4096, 1));
        HostCheck.Equal(writable & ~2UL, owner.Leaf(0x404000));
        HostCheck.Equal(0L, LinuxMemoryServices.RestoreImageProtections(owner.State));
        HostCheck.Equal(writable, owner.Leaf(0x404000));
        HostCheck.True(*owner.At(Start) == 0x5a);
        HostCheck.Equal(8192UL, owner.State->ReservedBytes);
        HostCheck.Equal(2U, owner.State->BackedPages);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x402000, 4096, 3));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start, X64UserAddressSpace.UserEnd - Start));
        HostCheck.True(owner.State->HasImageProtections && LinuxMemoryServices.Audit(owner.State));
        HostCheck.True(!X64UserAddressSpace.Audit(memory.StaticRanges, memory.StaticCount, memory.Tables, Root, memory.TablePages, memory.Conditions, owner.State->Root));
        HostCheck.Equal(0L, LinuxMemoryServices.RestoreImageProtections(owner.State));
        HostCheck.True(!owner.State->HasImageProtections && owner.State->ReservedBytes == 0 && owner.State->BackedPages == 0);
        HostCheck.True(original.AsSpan().SequenceEqual(owner.Tables()) && LinuxMemoryServices.Audit(owner.State));
        HostCheck.True(X64UserAddressSpace.Audit(memory.StaticRanges, memory.StaticCount, memory.Tables, Root, memory.TablePages, memory.Conditions, owner.State->Root));
        HostCheck.True(staticRows.AsSpan().SequenceEqual(Snapshot(owner).Static));
        HostCheck.Equal(0L, LinuxMemoryServices.RestoreImageProtections(owner.State));
    }

    [HostTest("linux.memory image authority rejects invalid envelopes code stack gaps and competing windows atomically")]
    private static void ImageAuthority()
    {
        using var owner = new Owner(staticRows: 5);
        var memory = ImageAdmission(owner);
        var empty = Snapshot(owner);
        for (int field = 0; field < 13; field++)
        {
            var bad = memory;
            switch (field)
            {
                case 0:
                    bad.ImageVirtualStart++;
                    break;
                case 1:
                    bad.ImagePhysicalStart++;
                    break;
                case 2:
                    bad.ImageBytes++;
                    break;
                case 3:
                    bad.ImageVirtualStart = 0;
                    break;
                case 4:
                    bad.ImagePhysicalStart = 0;
                    break;
                case 5:
                    bad.ImageBytes = 0;
                    break;
                case 6:
                    bad.ImageVirtualStart = LinuxMemoryServices.DynamicStart - 4096;
                    break;
                case 7:
                    bad.ImagePhysicalStart = X64UserAddressSpace.PhysicalEnd - 4096;
                    break;
                case 8:
                    bad.ImagePhysicalStart = Pool;
                    break;
                case 9:
                    bad.ImagePhysicalStart = Root;
                    break;
                case 10:
                    bad.ImagePhysicalStart += 4096;
                    break;
                case 11:
                    bad.ImageBytes = 8192;
                    break; // Cuts an original data row.
                case 12:
                    bad.ImageVirtualStart = ulong.MaxValue - 4095;
                    break;
            }

            HostCheck.True(!LinuxMemoryServices.Begin(owner.State, bad));
            Same(owner, empty);
        }

        var row = memory.StaticRanges[2];
        memory.StaticRanges[2].User = false;
        HostCheck.True(!LinuxMemoryServices.Begin(owner.State, memory));
        memory.StaticRanges[2] = row;
        Same(owner, empty);
        memory.StaticRanges[2].PhysicalStart += 4096;
        HostCheck.True(!LinuxMemoryServices.Begin(owner.State, memory));
        memory.StaticRanges[2] = row;
        Same(owner, empty);
        HostCheck.True(LinuxMemoryServices.Begin(owner.State, memory));
        var before = Snapshot(owner);
        (ulong Address, ulong Bytes, uint Protection)[] denied = [(0x400000, 4096, 3), (0x100000, 4096, 3), (Root, 4096, 3), (0x6ffff000, 4096, 1), (0x405000, 4096, 3), (0x406000, 4096, 3), (0x403000, 8192, 3), (0x400000, 8192, 1), (0, 4096, 3), (0x402001, 4096, 3), (0x402000, 0, 3), (0x402000, ulong.MaxValue, 3), (0x402000, 4096, 0), (0x402000, 4096, 2), (0x402000, 4096, 5), (0x402000, 4096, 7)];
        foreach (var request in denied)
        {
            HostCheck.Equal(-22L, LinuxMemoryServices.Protect(owner.State, request.Address, request.Bytes, request.Protection));
            Same(owner, before);
            HostCheck.True(LinuxMemoryServices.Audit(owner.State));
        }

        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x401000, 8192, 3));
        before = Snapshot(owner);
        foreach (var request in new[]
        {
            (0x401000UL, 4096UL),
            (0x402000UL, 4096UL),
            (0x401000UL, 12288UL),
            (0x404000UL, 4096UL)
        }

        )
        {
            HostCheck.Equal(-12L, LinuxMemoryServices.Protect(owner.State, request.Item1, request.Item2, 1));
            Same(owner, before);
        }

        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x401000, 8192, 3));
        Same(owner, before);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x401000, 8192, 1));
        using var disabled = new Owner(staticRows: 5);
        var defaults = ImageAdmission(disabled);
        defaults.ImageVirtualStart = defaults.ImagePhysicalStart = defaults.ImageBytes = 0;
        HostCheck.True(LinuxMemoryServices.Begin(disabled.State, defaults));
        var unchanged = Snapshot(disabled);
        HostCheck.Equal(-22L, LinuxMemoryServices.Protect(disabled.State, 0x402000, 4096, 3));
        Same(disabled, unchanged);
        HostCheck.Equal(0L, LinuxMemoryServices.RestoreImageProtections(disabled.State));
        Same(disabled, unchanged);
    }

    [HostTest("linux.memory image permission changes honor retained borrows and require guard detachment for reset")]
    private static void ImageGuards()
    {
        using var owner = new Owner(staticRows: 5);
        var memory = ImageAdmission(owner);
        HostCheck.True(LinuxMemoryServices.Begin(owner.State, memory));
        GuardOwner guard = new()
        {
            Pin = 0x402080
        };
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(owner.State, &guard, &Guard));
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x402000, 4096, 1));
        HostCheck.Equal(0U, guard.Calls);
        var before = Snapshot(owner);
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(owner.State, 0x402000, 4096, 3));
        Same(owner, before);
        HostCheck.Equal(0x402000UL, guard.LastStart);
        HostCheck.Equal(4096UL, guard.LastBytes);
        guard.Pin = 0;
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x402000, 4096, 3));
        before = Snapshot(owner);
        uint calls = guard.Calls;
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x402000, 4096, 3));
        HostCheck.Equal(calls, guard.Calls);
        Same(owner, before);
        HostCheck.Equal(-12L, LinuxMemoryServices.RestoreImageProtections(owner.State));
        Same(owner, before);
        guard.Pin = 0x402080;
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(owner.State, 0x402000, 4096, 1));
        Same(owner, before);
        HostCheck.True(!LinuxMemoryServices.DetachMutationGuard(owner.State, &guard, &Guard));
        guard.Pin = 0;
        HostCheck.True(LinuxMemoryServices.DetachMutationGuard(owner.State, &guard, &Guard));
        HostCheck.Equal(0L, LinuxMemoryServices.RestoreImageProtections(owner.State));
        HostCheck.True(!owner.State->HasImageProtections && LinuxMemoryServices.Audit(owner.State));
    }

    [HostTest("linux.memory image fragments and later mmap share bounded inventory with atomic exhaustion")]
    private static void ImageCapacity()
    {
        using var owner = new Owner(tables: 32, backingPages: 64, staticRows: 63);
        var memory = ImageAdmission(owner);
        HostCheck.True(LinuxMemoryServices.Begin(owner.State, memory));
        for (uint i = 0; i < LinuxMemoryServices.MaximumRegions; i++)
            HostCheck.Equal((long)(Start + i * 8192), Map(owner, Start + i * 8192, 4096, 3));
        *owner.At(Start) = 0x3d;
        var before = Snapshot(owner);
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(owner.State, 0x402000, 4096, 3));
        Same(owner, before);
        ulong last = Start + (LinuxMemoryServices.MaximumRegions - 1) * 8192UL;
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, last, 4096));
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, 0x402000, 4096, 3));
        before = Snapshot(owner);
        HostCheck.Equal(-12L, Map(owner, last, 4096, 3));
        Same(owner, before);
        HostCheck.True(owner.State->HasImageProtections && *owner.At(Start) == 0x3d && LinuxMemoryServices.Audit(owner.State));
        HostCheck.Equal(0L, LinuxMemoryServices.RestoreImageProtections(owner.State));
        HostCheck.Equal((long)last, Map(owner, last, 4096, 3));
        HostCheck.Equal(64U, owner.State->BackedPages);
        HostCheck.True(*owner.At(Start) == 0x3d);
    }

    [HostTest("linux.memory terminal window unmap scrubs dynamic backing after guard release and preserves static mappings")]
    private static void TerminalWindowUnmap()
    {
        using var owner = new Owner();
        owner.Begin();
        ulong originalCode = owner.Leaf(0x400000);
        HostCheck.Equal((long)Start, Map(owner, Start, 3 * 4096, 3));
        *owner.At(Start) = 0x61;
        *owner.At(Start + 8192) = 0x62;
        HostCheck.Equal((long)(Start + 4096), Map(owner, Start + 4096, 4096, 0, 0x32));
        ulong sparse = Start + 0x200000;
        HostCheck.Equal((long)sparse, Map(owner, sparse, Reservation, 0));
        ulong high = sparse + 0x100000000;
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, high, 4096, 3));
        *owner.At(high) = 0x73;
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, high, 4096, 1));
        ulong reserved = owner.State->ReservedBytes;
        uint backed = owner.State->BackedPages;
        byte[] tables = owner.Tables(), pool = new ReadOnlySpan<byte>(owner.Memory.Pool, (int)PoolPages * 4096).ToArray();
        GuardOwner guard = new()
        {
            Pin = Start
        };
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(owner.State, &guard, &Guard));
        const ulong window = LinuxSyscallGateway.UserEnd - LinuxMemoryServices.DynamicStart;
        HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(owner.State, LinuxMemoryServices.DynamicStart, window));
        HostCheck.True(guard.Calls > 0 && owner.State->ReservedBytes == reserved && owner.State->BackedPages == backed);
        HostCheck.True(tables.AsSpan().SequenceEqual(owner.Tables()) && pool.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.Pool, (int)PoolPages * 4096)) && LinuxMemoryServices.Audit(owner.State));
        HostCheck.True(!LinuxMemoryServices.DetachMutationGuard(owner.State, &guard, &Guard));
        guard.Pin = 0; // The retained word borrow must drain before detach can succeed.
        HostCheck.True(LinuxMemoryServices.DetachMutationGuard(owner.State, &guard, &Guard));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, LinuxMemoryServices.DynamicStart, window));
        HostCheck.True(owner.State->ReservedBytes == 0 && owner.State->BackedPages == 0 && LinuxMemoryServices.Audit(owner.State));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.Pool, (int)PoolPages * 4096).IndexOfAnyExcept((byte)0) < 0);
        foreach (ulong address in new[]
        {
            Start,
            Start + 8192,
            high
        }

        )
            HostCheck.True(!X64UserAddressSpace.TryReadLeaf(owner.Memory.Tables, Root, TablePages, address, out ulong leaf) || (leaf & 1) == 0);
        HostCheck.Equal(originalCode, owner.Leaf(0x400000));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, LinuxMemoryServices.DynamicStart, window));
        HostCheck.True(LinuxMemoryServices.Audit(owner.State) && owner.State->ReservedBytes == 0 && owner.State->BackedPages == 0);
    }

    [HostTest("linux.memory fault codes distinguish reservation permissions holes static user ranges and supervisor addresses")]
    private static void PageFaultCodes()
    {
        using var owner = new Owner();
        owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 3 * 4096, 0));
        foreach (ulong address in new ulong[]
        {
            Start,
            Start + 12287,
            0x400123,
            LinuxSyscallGateway.UserEnd,
            ulong.MaxValue
        }

        )
        {
            HostCheck.True(LinuxMemoryServices.TryPageFaultCode(owner.State, address, out int code));
            HostCheck.Equal(2, code);
        }

        foreach (ulong address in new ulong[]
        {
            0,
            0x100123,
            Start - 1,
            Start + 12288
        }

        )
        {
            HostCheck.True(LinuxMemoryServices.TryPageFaultCode(owner.State, address, out int code));
            HostCheck.Equal(1, code);
        }

        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start + 4096, 4096));
        HostCheck.True(LinuxMemoryServices.TryPageFaultCode(owner.State, Start + 4096, out int hole) && hole == 1);
        HostCheck.True(LinuxMemoryServices.TryPageFaultCode(owner.State, Start + 8192, out int reserved) && reserved == 2);
        HostCheck.True(!LinuxMemoryServices.TryPageFaultCode(null, 0, out int invalid) && invalid == 0);
        owner.Memory.StaticRanges[0].Pages++;
        HostCheck.True(!LinuxMemoryServices.TryPageFaultCode(owner.State, Start, out invalid) && invalid == 0);
        owner.Memory.StaticRanges[0].Pages--;
    }

    [UnmanagedCallersOnly]
    private static byte Guard(void* context, ulong address, ulong bytes)
    {
        var owner = (GuardOwner*)context;
        owner->Calls++;
        owner->LastStart = address;
        owner->LastBytes = bytes;
        return owner->Deny || owner->Pin != 0 && address < owner->Pin + 4 && owner->Pin < address + bytes ? (byte)0 : (byte)1;
    }

    [HostTest("linux.memory guard rejects retained backing changes atomically and permits exact unrelated intervals")]
    private static void MutationGuard()
    {
        using var owner = new Owner();
        owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 4 * 4096, 3));
        *owner.At(Start + 4096) = 0x7b;
        GuardOwner guard = new()
        {
            Pin = Start + 4096 + 128
        };
        HostCheck.True(!LinuxMemoryServices.AttachMutationGuard(owner.State, null, &Guard));
        HostCheck.True(!LinuxMemoryServices.AttachMutationGuard(owner.State, owner.State, &Guard));
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(owner.State, &guard, &Guard));
        HostCheck.True(!LinuxMemoryServices.AttachMutationGuard(owner.State, &guard, &Guard));
        HostCheck.True(owner.State->HasMutationGuard);
        var copy = *owner.State;
        HostCheck.True(!LinuxMemoryServices.Audit(&copy));
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start, 4 * 4096, 3));
        HostCheck.Equal(0L, LinuxMemoryServices.Advise(owner.State, Start + 4096, 4096, 16));
        HostCheck.Equal(0U, guard.Calls);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + 2 * 4096, 4096, 1));
        HostCheck.Equal(Start + 2 * 4096, guard.LastStart);
        HostCheck.Equal(4096UL, guard.LastBytes);
        byte[] tables = owner.Tables(), pool = new ReadOnlySpan<byte>(owner.Memory.Pool, (int)PoolPages * 4096).ToArray();
        byte[] state = new ReadOnlySpan<byte>(owner.State, sizeof(LinuxMemoryServices.State)).ToArray();
        foreach (int operation in new[]
        {
            0,
            1,
            2,
            3
        }

        )
        {
            long result = operation switch
            {
                0 => LinuxMemoryServices.Protect(owner.State, Start + 4096, 4096, 1),
                1 => LinuxMemoryServices.Unmap(owner.State, Start + 4096, 4096),
                2 => LinuxMemoryServices.Advise(owner.State, Start + 4096, 4096, 4),
                _ => Map(owner, Start + 4096, 4096, 3, 0x32)
            };
            HostCheck.Equal(-12L, result);
            HostCheck.True(tables.AsSpan().SequenceEqual(owner.Tables()) && pool.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.Pool, pool.Length)) && state.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.State, state.Length)) && LinuxMemoryServices.Audit(owner.State));
        }

        HostCheck.True(!LinuxMemoryServices.DetachMutationGuard(owner.State, &guard, &Guard));
        GuardOwner other = default;
        HostCheck.True(!LinuxMemoryServices.DetachMutationGuard(owner.State, &other, &Guard));
        guard.Pin = 0;
        guard.Deny = true;
        HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(owner.State, Start + 3 * 4096, 4096));
        guard.Deny = false;
        HostCheck.True(LinuxMemoryServices.DetachMutationGuard(owner.State, &guard, &Guard));
        HostCheck.True(!owner.State->HasMutationGuard);
        HostCheck.Equal(4096UL, guard.LastStart);
        HostCheck.Equal(OS.Kernel.Memory.X64UserAddressSpace.UserEnd - 4096, guard.LastBytes);
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start + 4096, 4096));
    }

    [HostTest("m16-006 measured 8234266624-byte sparse reservation bounded commit and high leaves", HostLane.Fast, 0)]
    private static void SparseReservation()
    {
        using var owner = new Owner();
        owner.Begin();
        uint baselineTables = owner.State->Root.UsedTables;
        ulong baselineLeaves = owner.State->Root.LeafPages;
        byte[] original = owner.Tables();
        long before = GC.GetAllocatedBytesForCurrentThread();
        long reserved = Map(owner, 0, Reservation, 0, 0x22);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.Equal((long)Start, reserved);
        HostCheck.True(allocated == 0 && owner.State->ReservedBytes == Reservation && owner.State->BackedPages == 0 && owner.State->Root.UsedTables == baselineTables && owner.State->Root.LeafPages == baselineLeaves);
        HostCheck.True(original.AsSpan().SequenceEqual(owner.Tables()));
        HostCheck.Equal(0UL, owner.Leaf(Start));
        HostCheck.Equal(0UL, owner.Leaf(Start + Reservation - 4096));
        // Replay the trace's page-sized alignment-prefix release, then commit
        // distant islands. Neither the intervening hole nor reservation consumes a leaf.
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start, 4096));
        HostCheck.Equal(Reservation - 4096, owner.State->ReservedBytes);
        ulong high = Start + Reservation - 8192;
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + 4096, 8192, 3));
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, high, 4096, 3));
        HostCheck.True(owner.State->BackedPages == 3 && owner.State->Root.LeafPages == baselineLeaves + 3 && owner.State->Root.UsedTables < TablePages);
        HostCheck.Equal(0x8000000002000007UL, owner.Leaf(Start + 4096));
        HostCheck.Equal(0x8000000002002007UL, owner.Leaf(high));
        HostCheck.True(new ReadOnlySpan<byte>(owner.At(high), 4096).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal(0UL, owner.Leaf(Start + 0x80000000));
        HostCheck.True(LinuxMemoryServices.Audit(owner.State));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start, Reservation));
        HostCheck.True(owner.State->ReservedBytes == 0 && owner.State->BackedPages == 0 && original.AsSpan().SequenceEqual(owner.Tables()));
    }

    [HostTest("m16-006 protection retains content fixed decommit and reuse zero real backing", HostLane.Fast, 1)]
    private static void DecommitAndReuse()
    {
        using var owner = new Owner();
        owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 3 * 4096, 3));
        new Span<byte>(owner.At(Start), 3 * 4096).Fill(0x6d);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + 4096, 4096, 0));
        HostCheck.True(owner.State->BackedPages == 3 && owner.Leaf(Start + 4096) == 0);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + 4096, 4096, 1));
        HostCheck.Equal((byte)0x6d, *owner.At(Start + 4096));
        HostCheck.Equal(0x8000000002001005UL, owner.Leaf(Start + 4096));
        // GC's actual decommit shape replaces only the middle page with NONE.
        HostCheck.Equal((long)Start + 4096, Map(owner, Start + 4096, 4096, 0, 0x32));
        HostCheck.True(owner.State->BackedPages == 2 && owner.State->ReservedBytes == 3 * 4096 && owner.Leaf(Start + 4096) == 0);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.Pool + 4096, 4096).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + 4096, 4096, 3));
        HostCheck.True(new ReadOnlySpan<byte>(owner.At(Start + 4096), 4096).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal((byte)0x6d, *owner.At(Start));
        HostCheck.Equal((byte)0x6d, *owner.At(Start + 8192));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start + 4096, 4096));
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(owner.State, Start, 3 * 4096, 1));
        HostCheck.Equal((long)Start + 4096, Map(owner, Start + 4096, 4096, 3));
        HostCheck.True(new ReadOnlySpan<byte>(owner.At(Start + 4096), 4096).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.True(LinuxMemoryServices.Audit(owner.State));
    }

    [HostTest("linux.memory large-pool policy changes preserve live bytes and release reuse zeroing above the old pool bound")]
    private static void LargePoolZeroing()
    {
        using var owner = new Owner(tables: X64UserAddressSpace.MaximumTables, backingPages: LinuxMemoryServices.MaximumPoolPages);
        owner.Begin();
        const ulong pages = 4097, bytes = pages * 4096, tail = Start + 4095 * 4096UL;
        HostCheck.Equal((long)Start, Map(owner, Start, bytes, 3));
        new Span<byte>(owner.At(Start), (int)bytes).Fill(0x6d);
        HostCheck.Equal(0L, LinuxMemoryServices.Advise(owner.State, Start, bytes, 17));
        HostCheck.Equal((long)(Start + 0x4000000), Map(owner, Start + 0x4000000, 0x100000, 0));
        HostCheck.True(new ReadOnlySpan<byte>(owner.At(Start), (int)bytes).IndexOfAnyExcept((byte)0x6d) == -1);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, tail + 4096, 4096, 0));
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, tail + 4096, 4096, 3));
        HostCheck.Equal((byte)0x6d, *owner.At(tail + 4096));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, tail, 8192));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.Pool + 4095 * 4096UL, 8192).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal((long)(Start + 0x4200000), Map(owner, Start + 0x4200000, 8192, 3));
        HostCheck.True(new ReadOnlySpan<byte>(owner.At(Start + 0x4200000), 8192).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal((byte)0x6d, *owner.At(Start));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.Pool + bytes, (int)(owner.Memory.PoolPages * 4096UL - bytes)).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start, X64UserAddressSpace.UserEnd - Start));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.Pool, (int)owner.Memory.PoolPages * 4096).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.True(LinuxMemoryServices.Audit(owner.State));
    }

    [HostTest("m16-006 independent VA backing and table exhaustion roll back all live state", HostLane.Fast, 2)]
    private static void Exhaustion()
    {
        using var owner = new Owner(quota: Reservation);
        owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, Reservation, 0));
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start, PoolPages * 4096, 3));
        new Span<byte>(owner.At(Start), (int)PoolPages * 4096).Fill(0x42);
        byte[] tables = owner.Tables(), pool = new ReadOnlySpan<byte>(owner.Memory.Pool, (int)PoolPages * 4096).ToArray();
        uint backed = owner.State->BackedPages, used = owner.State->Root.UsedTables;
        HostCheck.Equal(-12L, Map(owner, Start + Reservation, 4096, 0)); // Virtual quota.
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(owner.State, Start + PoolPages * 4096, 4096, 3)); // Physical quota.
        HostCheck.True(tables.AsSpan().SequenceEqual(owner.Tables()) && pool.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.Pool, pool.Length)));
        HostCheck.True(owner.State->ReservedBytes == Reservation && owner.State->BackedPages == backed && owner.State->Root.UsedTables == used);
        // Baseline consumes six table pages; a distant high leaf needs two more.
        using var tight = new Owner(tables: 6);
        tight.Begin();
        byte[] baseline = tight.Tables();
        HostCheck.Equal((long)Start, Map(tight, Start, Reservation, 0));
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(tight.State, Start, 4096, 3));
        HostCheck.True(tight.State->BackedPages == 0 && baseline.AsSpan().SequenceEqual(tight.Tables()));
    }

    [HostTest("m16-006 malformed unsupported alias stale and W-X inputs preserve ownership", HostLane.Fast, 3)]
    private static void Rejections()
    {
        using var owner = new Owner();
        owner.Begin();
        byte[] baseline = owner.Tables();
        HostCheck.True(!LinuxMemoryServices.Begin(owner.State, owner.Memory));
        HostCheck.Equal(-22L, Map(owner, Start + 1, 4096, 0));
        HostCheck.Equal(-22L, Map(owner, Start, ulong.MaxValue, 0));
        HostCheck.Equal(-22L, Map(owner, X64UserAddressSpace.UserEnd - 4096, 8192, 0));
        HostCheck.Equal(-22L, Map(owner, 0x400000, 4096, 0, 0x32));
        HostCheck.Equal(-22L, Map(owner, Start, 4096, 7));
        HostCheck.Equal(-22L, Map(owner, Start, 4096, 3, 0x23));
        HostCheck.Equal(-22L, LinuxMemoryServices.Map(owner.State, Start, 4096, 3, 0x22, 4, 0));
        HostCheck.Equal(-22L, LinuxMemoryServices.Map(owner.State, Start, 4096, 3, 0x22, ulong.MaxValue, 4096));
        HostCheck.True(owner.State->ReservedBytes == 0 && owner.State->BackedPages == 0 && baseline.AsSpan().SequenceEqual(owner.Tables()));
        HostCheck.Equal((long)Start, Map(owner, Start, 4096, 3));
        byte[] committed = owner.Tables();
        *owner.At(Start) = 0x61;
        HostCheck.Equal(-17L, Map(owner, Start, 4096, 0));
        HostCheck.Equal(-22L, LinuxMemoryServices.Protect(owner.State, Start, 4096, 7));
        HostCheck.Equal(-22L, LinuxMemoryServices.Advise(owner.State, Start, 4096, 999));
        HostCheck.True(committed.AsSpan().SequenceEqual(owner.Tables()) && *owner.At(Start) == 0x61);
        owner.Memory.Tables[0] ^= 4; // Stale/untrusted live root.
        byte[] stale = owner.Tables();
        HostCheck.Equal(-14L, LinuxMemoryServices.Unmap(owner.State, Start, 4096));
        HostCheck.True(stale.AsSpan().SequenceEqual(owner.Tables()) && owner.State->ReservedBytes == 4096);
        using var other = new Owner();
        var admission = other.Memory;
        admission.CandidateTables = admission.Tables;
        HostCheck.True(!LinuxMemoryServices.Begin(other.State, admission));
        HostCheck.True(other.State->Root.Status == X64UserAddressSpaceStatus.Uninitialized && new ReadOnlySpan<byte>(other.Memory.Pool, (int)PoolPages * 4096).IndexOfAnyExcept((byte)0xa5) == -1);
    }

    [HostTest("m16-006 partial anonymous discard distinguishes retained accessible backing and NONE release", HostLane.Fast, 4)]
    private static void Discard()
    {
        using var owner = new Owner();
        owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 8192, 3));
        new Span<byte>(owner.At(Start), 8192).Fill(0x39);
        HostCheck.Equal(0L, LinuxMemoryServices.Advise(owner.State, Start + 4096, 4096, 4));
        HostCheck.True(owner.State->BackedPages == 2 && *owner.At(Start) == 0x39 && *owner.At(Start + 4096) == 0);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start, 4096, 0));
        HostCheck.Equal(0L, LinuxMemoryServices.Advise(owner.State, Start, 4096, 4));
        HostCheck.True(owner.State->BackedPages == 1 && owner.State->ReservedBytes == 8192 && owner.Leaf(Start) == 0);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start, 4096, 3));
        HostCheck.True(owner.State->BackedPages == 2 && *owner.At(Start) == 0 && LinuxMemoryServices.Audit(owner.State));
    }

    [HostTest("m16-006 descriptor exhaustion honest hints and page rounding preserve separate sparse owners", HostLane.Fast, 5)]
    private static void MetadataAndHints()
    {
        using var owner = new Owner();
        owner.Begin();
        for (uint i = 0; i < LinuxMemoryServices.MaximumRegions; i++)
            HostCheck.Equal((long)(Start + i * 8192), Map(owner, Start + i * 8192, 1, 0));
        byte[] tables = owner.Tables();
        HostCheck.Equal(-12L, Map(owner, Start + 0x1000000, 4096, 0));
        HostCheck.True(owner.State->ReservedBytes == LinuxMemoryServices.MaximumRegions * 4096 && owner.State->BackedPages == 0 && tables.AsSpan().SequenceEqual(owner.Tables()));
        // Filling this hole transiently creates 65 regions, but the admitted
        // result coalesces to 63. It must not fail the final ownership quota.
        HostCheck.Equal((long)Start + 4096, Map(owner, Start + 4096, 4096, 0));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start + 2 * 8192, 1));
        // An occupied non-fixed hint selects the earliest actual hole.
        HostCheck.Equal((long)Start + 3 * 4096, Map(owner, Start, 4096, 0, 0x22));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start, 0x2000000));
        HostCheck.True(owner.State->ReservedBytes == 0 && LinuxMemoryServices.Audit(owner.State));
        for (uint i = 0; i < LinuxMemoryServices.MaximumRegions; i++)
            HostCheck.Equal((long)(Start + i * 0x10000), Map(owner, Start + i * 0x10000, 3 * 4096, 0));
        // Advice temporarily splits one owner into three, then merges it back.
        HostCheck.Equal(0L, LinuxMemoryServices.Advise(owner.State, Start + 4096, 4096, 4));
        byte[] sparse = owner.Tables();
        // A real protection change would leave 66 owners, so rejection is honest
        // and even its provisional backing allocation must not be published.
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(owner.State, Start + 4096, 4096, 3));
        HostCheck.True(owner.State->BackedPages == 0 && sparse.AsSpan().SequenceEqual(owner.Tables()) && LinuxMemoryServices.Audit(owner.State));
    }

    private static Owner FragmentedPool(uint tables = TablePages)
    {
        var owner = new Owner(tables: tables);
        owner.Begin();
        for (uint block = 0; block < 4; block++)
        {
            ulong address = Start + block * 0x10000;
            HostCheck.Equal((long)address, Map(owner, address, 4 * 4096, 3));
            for (uint page = 0; page < 4; page++)
                *owner.At(address + page * 4096) = (byte)(block * 4 + page + 1);
        }

        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start, 4 * 4096));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start + 0x20000, 4 * 4096));
        return owner;
    }

    [HostTest("linux.memory fragmented pool backs contiguous mmap and sparse protection without aliasing or stale bytes")]
    private static void FragmentedBacking()
    {
        for (uint mode = 0; mode < 3; mode++)
        {
            using var owner = FragmentedPool();
            const ulong address = Start + 0x100000;
            HostCheck.Equal((long)address, Map(owner, address, 6 * 4096, mode == 0 ? 3U : 0U));
            if (mode == 1)
                HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, address, 6 * 4096, 3));
            if (mode == 2)
                HostCheck.Equal((long)address, Map(owner, address, 6 * 4096, 3, 0x32));
            for (uint page = 0; page < 6; page++)
            {
                ulong physical = owner.Leaf(address + page * 4096) & 0x000ffffffffff000;
                HostCheck.Equal(Pool + (page < 4 ? page : page + 4) * 4096UL, physical);
                HostCheck.True(new ReadOnlySpan<byte>(owner.At(address + page * 4096), 4096).IndexOfAnyExcept((byte)0) == -1);
                *owner.At(address + page * 4096) = (byte)(100 + page);
            }

            HostCheck.True(owner.State->BackedPages == 14 && LinuxMemoryServices.Audit(owner.State));
            for (uint block = 1; block < 4; block += 2)
                for (uint page = 0; page < 4; page++)
                    HostCheck.Equal((byte)(block * 4 + page + 1), *owner.At(Start + block * 0x10000 + page * 4096));
            HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, address, 6 * 4096, 0));
            HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, address, 6 * 4096, 3));
            for (uint page = 0; page < 6; page++)
                HostCheck.Equal((byte)(100 + page), *owner.At(address + page * 4096));
            HostCheck.Equal(0L, LinuxMemoryServices.Advise(owner.State, address + 3 * 4096, 2 * 4096, 4));
            HostCheck.True(new ReadOnlySpan<byte>(owner.At(address + 3 * 4096), 4096).IndexOfAnyExcept((byte)0) == -1);
            HostCheck.True(new ReadOnlySpan<byte>(owner.At(address + 4 * 4096), 4096).IndexOfAnyExcept((byte)0) == -1);
            HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, address + 3 * 4096, 2 * 4096));
            HostCheck.Equal((byte)102, *owner.At(address + 2 * 4096));
            HostCheck.Equal((byte)105, *owner.At(address + 5 * 4096));
            HostCheck.True(LinuxMemoryServices.Audit(owner.State));
        }
    }

    [HostTest("linux.memory fragmented allocation capacity and metadata failures preserve live tables owners and bytes")]
    private static void FragmentedRollback()
    {
        using var tight = FragmentedPool(tables: 8);
        var tightBefore = Snapshot(tight);
        HostCheck.Equal(-12L, Map(tight, Start + 0x40000000, 6 * 4096, 3));
        Same(tight, tightBefore);
        using var owner = FragmentedPool();
        var before = Snapshot(owner);
        HostCheck.Equal(-12L, Map(owner, Start + 0x100000, 9 * 4096, 3));
        Same(owner, before);
        for (uint i = 0; i < LinuxMemoryServices.MaximumRegions - 3; i++)
            HostCheck.Equal((long)(Start + 0x200000 + i * 8192), Map(owner, Start + 0x200000 + i * 8192, 4096, 0));
        before = Snapshot(owner);
        HostCheck.Equal(-12L, Map(owner, Start + 0x100000, 6 * 4096, 3));
        Same(owner, before);
        HostCheck.True(LinuxMemoryServices.Audit(owner.State));
    }

    [HostTest("linux.memory fragmented backing retains actual futex leases through wake until completion consumption")]
    private static void FragmentedFutexLease()
    {
        using var owner = FragmentedPool();
        const ulong address = Start + 0x100000, word = address + 4 * 4096 + 128;
        HostCheck.Equal((long)address, Map(owner, address, 6 * 4096, 3));
        *(uint*)owner.At(word) = 42;
        using LinuxThreadServicesTests.Model worker = new(futex: true);
        worker.UseMemory(owner.Memory);
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(owner.State, worker.Service, &LinuxFutexTests.MemoryGuard));
        var parent = worker.Start();
        worker.Kernel.Dispatch();
        var child = worker.Clone();
        HostCheck.Equal(LinuxSyscallAction.Blocked, worker.Call(202, word, 128, 42).Action);
        LinuxFutexTests.Park(worker, parent);
        worker.Kernel.Dispatch();
        var before = Snapshot(owner);
        HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(owner.State, address + 3 * 4096, 2 * 4096));
        Same(owner, before);
        HostCheck.Equal(-12L, Map(owner, address + 3 * 4096, 2 * 4096, 3, 0x32));
        Same(owner, before);
        HostCheck.Equal(-12L, LinuxMemoryServices.Advise(owner.State, address + 3 * 4096, 2 * 4096, 4));
        Same(owner, before);
        HostCheck.Equal(1L, worker.Call(202, word, 129, 1).Value);
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(owner.State, address + 3 * 4096, 2 * 4096, 1));
        Same(owner, before);
        worker.Yield(child);
        HostCheck.Equal(parent.Value, worker.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, worker.Service->CompleteFutexWait(LinuxThreadServicesTests.Key, 2).Value);
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, address + 3 * 4096, 2 * 4096));
        HostCheck.Equal((byte)0, owner.Memory.Pool[3 * 4096]);
        HostCheck.Equal((byte)0, owner.Memory.Pool[8 * 4096]);
        LinuxFutexTests.Drain(worker, owner.State);
        HostCheck.True(LinuxMemoryServices.Audit(owner.State));
    }

    [HostTest("m16-006 repeated identical protection and contiguous commits avoid metadata fragmentation", HostLane.Fast, 6)]
    private static void RepeatedProtection()
    {
        using var owner = new Owner();
        owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 128 * 4096, 0));
        for (uint i = 1; i < 128; i += 2)
            HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + i * 4096, 4096, 0));
        for (uint i = 0; i < PoolPages; i++)
        {
            HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + i * 4096, 4096, 3));
            *owner.At(Start + i * 4096) = (byte)(i + 1);
        }

        HostCheck.True(owner.State->ReservedBytes == 128 * 4096 && owner.State->BackedPages == PoolPages);
        for (uint i = 0; i < PoolPages; i++)
            HostCheck.Equal((byte)(i + 1), *owner.At(Start + i * 4096));
        HostCheck.True(LinuxMemoryServices.Audit(owner.State));
    }

    [HostTest("m16-006 mremap preserves old bytes zeroes growth and distinguishes guards from stack probes", HostLane.Fast, 7)]
    private static void Remapping()
    {
        using var owner = new Owner();
        owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 4096, 3));
        *owner.At(Start) = 0x51;
        new Span<byte>(owner.Memory.Pool + 4096, 4096).Fill(0x7b);
        HostCheck.Equal((long)Start, LinuxMemoryServices.Remap(owner.State, Start, 4096, 8192, 0));
        HostCheck.True(*owner.At(Start) == 0x51 && owner.State->BackedPages == 2 && new ReadOnlySpan<byte>(owner.At(Start + 4096), 4096).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal((long)Start, LinuxMemoryServices.Remap(owner.State, Start, 8192, 4096, 0));
        HostCheck.True(owner.State->BackedPages == 1 && owner.Leaf(Start + 4096) == 0 && *owner.At(Start) == 0x51);
        HostCheck.Equal((long)Start + 4096, Map(owner, Start + 4096, 4096, 0));
        byte[] tables = owner.Tables();
        HostCheck.Equal(-12L, LinuxMemoryServices.Remap(owner.State, Start, 4096, 8192, 0));
        HostCheck.Equal(-14L, LinuxMemoryServices.Remap(owner.State, Start + 0x10000, 4096, 8192, 0));
        HostCheck.Equal(-12L, LinuxMemoryServices.Remap(owner.State, 0x400000, 4096, 8192, 0));
        HostCheck.Equal(-14L, LinuxMemoryServices.Remap(owner.State, 0x3ff000, 4096, 8192, 0));
        HostCheck.Equal(-22L, LinuxMemoryServices.Remap(owner.State, Start, 4096, 8192, 1));
        HostCheck.True(tables.AsSpan().SequenceEqual(owner.Tables()) && *owner.At(Start) == 0x51);
    }

    [HostTest("m16-006 dump exclusion splits and persists across protection with real policy state", HostLane.Fast, 8)]
    private static void DumpPolicy()
    {
        using var owner = new Owner();
        owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 3 * 4096, 3));
        *owner.At(Start + 4096) = 0x62;
        HostCheck.True(!LinuxMemoryServices.IsDumpExcluded(owner.State, Start, 3 * 4096));
        HostCheck.Equal(0L, LinuxMemoryServices.Advise(owner.State, Start + 4096, 4096, 16));
        HostCheck.True(LinuxMemoryServices.IsDumpExcluded(owner.State, Start + 4096, 4096) && !LinuxMemoryServices.IsDumpExcluded(owner.State, Start, 3 * 4096) && *owner.At(Start + 4096) == 0x62);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + 4096, 4096, 1));
        HostCheck.True(LinuxMemoryServices.IsDumpExcluded(owner.State, Start + 4096, 4096));
        HostCheck.Equal(0L, LinuxMemoryServices.Advise(owner.State, Start + 4096, 4096, 17));
        HostCheck.True(!LinuxMemoryServices.IsDumpExcluded(owner.State, Start + 4096, 4096) && *owner.At(Start + 4096) == 0x62);
    }

    [HostTest("m16-006 static root adoption rejects stale root before touching pool or state", HostLane.Fast, 9)]
    private static void Adoption()
    {
        using var owner = new Owner();
        var root = X64UserAddressSpace.Build(owner.Memory.StaticRanges, owner.Memory.StaticCount, owner.Memory.Tables, owner.Memory.RootPhysical, owner.Memory.TablePages, owner.Memory.Conditions);
        HostCheck.True(root.Succeeded);
        owner.Memory.Tables[0] ^= 4;
        HostCheck.True(!LinuxMemoryServices.Adopt(owner.State, owner.Memory, root));
        HostCheck.True(owner.State->Root.Status == X64UserAddressSpaceStatus.Uninitialized && new ReadOnlySpan<byte>(owner.Memory.Pool, (int)PoolPages * 4096).IndexOfAnyExcept((byte)0xa5) == -1);
        owner.Memory.Tables[0] ^= 4;
        HostCheck.True(LinuxMemoryServices.Adopt(owner.State, owner.Memory, root) && LinuxMemoryServices.Audit(owner.State));
    }

    [HostTest("socket memory retains high UC MMIO outside its real reservation across adoption and mmap rebuild", HostLane.Fast, 9)]
    private static void DeviceAdoption()
    {
        const ulong device = 0x800004000;
        foreach (bool adopt in new[]
        {
            false,
            true
        }

        )
            foreach (int variant in Enumerable.Range(0, 3))
            {
                using var owner = new Owner(quota: variant == 1 ? X64UserAddressSpace.UserEnd - Start : 0x400000000, staticRows: 3);
                var memory = owner.Memory;
                memory.StaticRanges[2] = new()
                {
                    VirtualStart = device,
                    PhysicalStart = variant == 2 ? 0x500000UL : device,
                    Pages = 1,
                    Protection = PageProtection.Read | PageProtection.Write,
                    Cache = variant == 2 ? PageCachePolicy.WriteBack : PageCachePolicy.Uncached,
                    User = variant == 2
                };
                var root = adopt ? X64UserAddressSpace.Build(memory.StaticRanges, memory.StaticCount, memory.Tables, memory.RootPhysical, memory.TablePages, memory.Conditions) : default;
                if (adopt)
                    HostCheck.True(root.Succeeded);
                byte[] before = owner.Tables();
                bool success = adopt ? LinuxMemoryServices.Adopt(owner.State, memory, root) : LinuxMemoryServices.Begin(owner.State, memory);
                HostCheck.Equal(variant == 0, success);
                if (!success)
                {
                    HostCheck.True(before.SequenceEqual(owner.Tables()) && owner.State->Root.Status == X64UserAddressSpaceStatus.Uninitialized && new ReadOnlySpan<byte>(memory.Pool, (int)PoolPages * 4096).IndexOfAnyExcept((byte)0xa5) < 0);
                    continue;
                }

                ulong entry = owner.Leaf(device);
                HostCheck.True((entry & 4) == 0 && (entry & (1UL << 63)) != 0);
                HostCheck.Equal((long)Start, Map(owner, Start, 4096, 3));
                HostCheck.True(Map(owner, device, 4096, 3) < 0 && owner.Leaf(device) == entry && LinuxMemoryServices.Audit(owner.State));
                HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start, 4096));
                HostCheck.True(owner.Leaf(device) == entry && LinuxMemoryServices.Audit(owner.State));
            }
    }
}
