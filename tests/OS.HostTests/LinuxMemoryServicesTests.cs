using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;

internal static unsafe class LinuxMemoryServicesTests
{
    private const ulong Root = 0x1000000, Pool = 0x2000000, Start = 0x100000000, Reservation = 8234266624;
    private const uint TablePages = 16, PoolPages = 16;
    internal sealed class Owner : IDisposable
    {
        public readonly LinuxMemoryServices.State* State;
        public readonly LinuxMemoryServices.Admission Memory;
        public Owner(uint tables = TablePages, ulong quota = 0x400000000)
        {
            State = (LinuxMemoryServices.State*)NativeMemory.AlignedAlloc((nuint)sizeof(LinuxMemoryServices.State), 16);
            *State = default;
            byte* pool = (byte*)NativeMemory.AlignedAlloc(PoolPages * 4096, 4096);
            ulong* live = (ulong*)NativeMemory.AlignedAlloc(TablePages * 4096, 4096);
            ulong* candidate = (ulong*)NativeMemory.AlignedAlloc(TablePages * 4096, 4096);
            var ranges = (X64UserMappingRange*)NativeMemory.AlignedAlloc((nuint)(2 * sizeof(X64UserMappingRange)), 16);
            ranges[0] = new() { VirtualStart = 0x400000, PhysicalStart = 0x300000, Pages = 1,
                Protection = PageProtection.Read | PageProtection.Execute, Cache = PageCachePolicy.WriteBack, User = true };
            ranges[1] = new() { VirtualStart = 0x100000, PhysicalStart = 0x100000, Pages = 1,
                Protection = PageProtection.Read | PageProtection.Write, Cache = PageCachePolicy.WriteBack };
            new Span<byte>(pool, (int)PoolPages * 4096).Fill(0xa5);
            new Span<ulong>(live, (int)TablePages * 512).Clear();
            new Span<ulong>(candidate, (int)TablePages * 512).Clear();
            HostCheck.Equal(X64PageTableEntryStatus.Success, X64PatMappingToken.TryCreate(0x0007040600070406, 0, 3, 255, out var pat));
            Memory = new() { Pool = pool, PoolPhysical = Pool, PoolPages = PoolPages, RootPhysical = Root,
                TablePages = tables, Tables = live, CandidateTables = candidate, StaticRanges = ranges, StaticCount = 2,
                ReservationBytes = quota, Conditions = X64PageTableEncodingPreconditions.CreateObserved(48, true, true, false, false, pat) };
        }
        public void Begin() => HostCheck.True(LinuxMemoryServices.Begin(State, Memory));
        public ulong Leaf(ulong va)
        {
            HostCheck.True(X64UserAddressSpace.TryReadLeaf(Memory.Tables, Root, Memory.TablePages, va, out ulong leaf));
            return leaf;
        }
        public byte* At(ulong va)
        {
            ulong leaf = Leaf(va), physical = leaf & 0x000ffffffffff000;
            HostCheck.True((leaf & 5) == 5 && physical >= Pool && physical < Pool + PoolPages * 4096);
            return Memory.Pool + physical - Pool + (va & 4095);
        }
        public byte[] Tables() => new ReadOnlySpan<byte>(Memory.Tables, (int)Memory.TablePages * 4096).ToArray();
        public void Dispose()
        {
            NativeMemory.AlignedFree(State); NativeMemory.AlignedFree(Memory.Pool); NativeMemory.AlignedFree(Memory.Tables);
            NativeMemory.AlignedFree(Memory.CandidateTables); NativeMemory.AlignedFree(Memory.StaticRanges);
        }
    }
    private static long Map(Owner owner, ulong address, ulong bytes, uint protection, ulong flags = 0x100022) =>
        LinuxMemoryServices.Map(owner.State, address, bytes, protection, flags, ulong.MaxValue, 0);

    private struct GuardOwner { internal ulong Pin, LastStart, LastBytes; internal uint Calls; internal bool Deny; }

    [HostTest("linux.memory terminal window unmap scrubs dynamic backing after guard release and preserves static mappings")]
    private static void TerminalWindowUnmap()
    {
        using var owner = new Owner(); owner.Begin();
        ulong originalCode = owner.Leaf(0x400000);
        HostCheck.Equal((long)Start, Map(owner, Start, 3 * 4096, 3));
        *owner.At(Start) = 0x61; *owner.At(Start + 8192) = 0x62;
        HostCheck.Equal((long)(Start + 4096), Map(owner, Start + 4096, 4096, 0, 0x32));
        ulong sparse = Start + 0x200000;
        HostCheck.Equal((long)sparse, Map(owner, sparse, Reservation, 0));
        ulong high = sparse + 0x100000000;
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, high, 4096, 3));
        *owner.At(high) = 0x73;
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, high, 4096, 1));
        ulong reserved = owner.State->ReservedBytes; uint backed = owner.State->BackedPages;
        byte[] tables = owner.Tables(), pool = new ReadOnlySpan<byte>(owner.Memory.Pool, (int)PoolPages * 4096).ToArray();
        GuardOwner guard = new() { Pin = Start };
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(owner.State, &guard, &Guard));
        const ulong window = LinuxSyscallGateway.UserEnd - LinuxMemoryServices.DynamicStart;
        HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(owner.State, LinuxMemoryServices.DynamicStart, window));
        HostCheck.True(guard.Calls > 0 && owner.State->ReservedBytes == reserved && owner.State->BackedPages == backed);
        HostCheck.True(tables.AsSpan().SequenceEqual(owner.Tables()) && pool.AsSpan().SequenceEqual(
            new ReadOnlySpan<byte>(owner.Memory.Pool, (int)PoolPages * 4096)) && LinuxMemoryServices.Audit(owner.State));
        HostCheck.True(!LinuxMemoryServices.DetachMutationGuard(owner.State, &guard, &Guard));
        guard.Pin = 0; // The retained word borrow must drain before detach can succeed.
        HostCheck.True(LinuxMemoryServices.DetachMutationGuard(owner.State, &guard, &Guard));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, LinuxMemoryServices.DynamicStart, window));
        HostCheck.True(owner.State->ReservedBytes == 0 && owner.State->BackedPages == 0 && LinuxMemoryServices.Audit(owner.State));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.Pool, (int)PoolPages * 4096).IndexOfAnyExcept((byte)0) < 0);
        foreach (ulong address in new[] { Start, Start + 8192, high })
            HostCheck.True(!X64UserAddressSpace.TryReadLeaf(owner.Memory.Tables, Root, TablePages, address, out ulong leaf) || (leaf & 1) == 0);
        HostCheck.Equal(originalCode, owner.Leaf(0x400000));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, LinuxMemoryServices.DynamicStart, window));
        HostCheck.True(LinuxMemoryServices.Audit(owner.State) && owner.State->ReservedBytes == 0 && owner.State->BackedPages == 0);
    }

    [HostTest("linux.memory fault codes distinguish reservation permissions holes static user ranges and supervisor addresses")]
    private static void PageFaultCodes()
    {
        using var owner = new Owner(); owner.Begin();
        HostCheck.Equal((long)Start, Map(owner,Start,3 * 4096,0));
        foreach (ulong address in new ulong[] {Start,Start+12287,0x400123,LinuxSyscallGateway.UserEnd,ulong.MaxValue})
        { HostCheck.True(LinuxMemoryServices.TryPageFaultCode(owner.State,address,out int code)); HostCheck.Equal(2,code); }
        foreach (ulong address in new ulong[] {0,0x100123,Start-1,Start+12288})
        { HostCheck.True(LinuxMemoryServices.TryPageFaultCode(owner.State,address,out int code)); HostCheck.Equal(1,code); }
        HostCheck.Equal(0L,LinuxMemoryServices.Unmap(owner.State,Start+4096,4096));
        HostCheck.True(LinuxMemoryServices.TryPageFaultCode(owner.State,Start+4096,out int hole) && hole == 1);
        HostCheck.True(LinuxMemoryServices.TryPageFaultCode(owner.State,Start+8192,out int reserved) && reserved == 2);
        HostCheck.True(!LinuxMemoryServices.TryPageFaultCode(null,0,out int invalid) && invalid == 0);
        owner.Memory.StaticRanges[0].Pages++;
        HostCheck.True(!LinuxMemoryServices.TryPageFaultCode(owner.State,Start,out invalid) && invalid == 0);
        owner.Memory.StaticRanges[0].Pages--;
    }
    [UnmanagedCallersOnly]
    private static byte Guard(void* context, ulong address, ulong bytes)
    {
        var owner = (GuardOwner*)context; owner->Calls++; owner->LastStart = address; owner->LastBytes = bytes;
        return owner->Deny || owner->Pin != 0 && address < owner->Pin + 4 && owner->Pin < address + bytes ? (byte)0 : (byte)1;
    }

    [HostTest("linux.memory guard rejects retained backing changes atomically and permits exact unrelated intervals")]
    private static void MutationGuard()
    {
        using var owner = new Owner(); owner.Begin(); HostCheck.Equal((long)Start, Map(owner, Start, 4 * 4096, 3));
        *owner.At(Start + 4096) = 0x7b; GuardOwner guard = new() { Pin = Start + 4096 + 128 };
        HostCheck.True(!LinuxMemoryServices.AttachMutationGuard(owner.State, null, &Guard));
        HostCheck.True(!LinuxMemoryServices.AttachMutationGuard(owner.State, owner.State, &Guard));
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(owner.State, &guard, &Guard));
        HostCheck.True(!LinuxMemoryServices.AttachMutationGuard(owner.State, &guard, &Guard));
        HostCheck.True(owner.State->HasMutationGuard); var copy = *owner.State;
        HostCheck.True(!LinuxMemoryServices.Audit(&copy));
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start, 4 * 4096, 3));
        HostCheck.Equal(0L, LinuxMemoryServices.Advise(owner.State, Start + 4096, 4096, 16)); HostCheck.Equal(0U, guard.Calls);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + 2 * 4096, 4096, 1));
        HostCheck.Equal(Start + 2 * 4096, guard.LastStart); HostCheck.Equal(4096UL, guard.LastBytes);
        byte[] tables = owner.Tables(), pool = new ReadOnlySpan<byte>(owner.Memory.Pool, (int)PoolPages * 4096).ToArray();
        byte[] state = new ReadOnlySpan<byte>(owner.State, sizeof(LinuxMemoryServices.State)).ToArray();
        foreach (int operation in new[] { 0, 1, 2, 3 })
        {
            long result = operation switch
            {
                0 => LinuxMemoryServices.Protect(owner.State, Start + 4096, 4096, 1),
                1 => LinuxMemoryServices.Unmap(owner.State, Start + 4096, 4096),
                2 => LinuxMemoryServices.Advise(owner.State, Start + 4096, 4096, 4),
                _ => Map(owner, Start + 4096, 4096, 3, 0x32)
            };
            HostCheck.Equal(-12L, result);
            HostCheck.True(tables.AsSpan().SequenceEqual(owner.Tables()) && pool.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.Pool, pool.Length)) &&
                state.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.State, state.Length)) && LinuxMemoryServices.Audit(owner.State));
        }
        HostCheck.True(!LinuxMemoryServices.DetachMutationGuard(owner.State, &guard, &Guard));
        GuardOwner other = default; HostCheck.True(!LinuxMemoryServices.DetachMutationGuard(owner.State, &other, &Guard));
        guard.Pin = 0; guard.Deny = true;
        HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(owner.State, Start + 3 * 4096, 4096));
        guard.Deny = false; HostCheck.True(LinuxMemoryServices.DetachMutationGuard(owner.State, &guard, &Guard));
        HostCheck.True(!owner.State->HasMutationGuard); HostCheck.Equal(4096UL, guard.LastStart);
        HostCheck.Equal(OS.Kernel.Memory.X64UserAddressSpace.UserEnd - 4096, guard.LastBytes);
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start + 4096, 4096));
    }

    [HostTest("m16-006 measured 8234266624-byte sparse reservation bounded commit and high leaves", HostLane.Fast, 0)]
    private static void SparseReservation()
    {
        using var owner = new Owner(); owner.Begin();
        uint baselineTables = owner.State->Root.UsedTables;
        ulong baselineLeaves = owner.State->Root.LeafPages;
        byte[] original = owner.Tables();
        long before = GC.GetAllocatedBytesForCurrentThread();
        long reserved = Map(owner, 0, Reservation, 0, 0x22);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.Equal((long)Start, reserved);
        HostCheck.True(allocated == 0 && owner.State->ReservedBytes == Reservation && owner.State->BackedPages == 0 &&
            owner.State->Root.UsedTables == baselineTables && owner.State->Root.LeafPages == baselineLeaves);
        HostCheck.True(original.AsSpan().SequenceEqual(owner.Tables()));
        HostCheck.Equal(0UL, owner.Leaf(Start)); HostCheck.Equal(0UL, owner.Leaf(Start + Reservation - 4096));
        // Replay the trace's page-sized alignment-prefix release, then commit
        // distant islands. Neither the intervening hole nor reservation consumes a leaf.
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start, 4096));
        HostCheck.Equal(Reservation - 4096, owner.State->ReservedBytes);
        ulong high = Start + Reservation - 8192;
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + 4096, 8192, 3));
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, high, 4096, 3));
        HostCheck.True(owner.State->BackedPages == 3 && owner.State->Root.LeafPages == baselineLeaves + 3 &&
            owner.State->Root.UsedTables < TablePages);
        HostCheck.Equal(0x8000000002000007UL, owner.Leaf(Start + 4096));
        HostCheck.Equal(0x8000000002002007UL, owner.Leaf(high));
        HostCheck.True(new ReadOnlySpan<byte>(owner.At(high), 4096).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal(0UL, owner.Leaf(Start + 0x80000000));
        HostCheck.True(LinuxMemoryServices.Audit(owner.State));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start, Reservation));
        HostCheck.True(owner.State->ReservedBytes == 0 && owner.State->BackedPages == 0 &&
            original.AsSpan().SequenceEqual(owner.Tables()));
    }

    [HostTest("m16-006 protection retains content fixed decommit and reuse zero real backing", HostLane.Fast, 1)]
    private static void DecommitAndReuse()
    {
        using var owner = new Owner(); owner.Begin();
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
        HostCheck.Equal((byte)0x6d, *owner.At(Start)); HostCheck.Equal((byte)0x6d, *owner.At(Start + 8192));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(owner.State, Start + 4096, 4096));
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(owner.State, Start, 3 * 4096, 1));
        HostCheck.Equal((long)Start + 4096, Map(owner, Start + 4096, 4096, 3));
        HostCheck.True(new ReadOnlySpan<byte>(owner.At(Start + 4096), 4096).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.True(LinuxMemoryServices.Audit(owner.State));
    }

    [HostTest("m16-006 independent VA backing and table exhaustion roll back all live state", HostLane.Fast, 2)]
    private static void Exhaustion()
    {
        using var owner = new Owner(quota: Reservation); owner.Begin();
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
        using var tight = new Owner(tables: 6); tight.Begin();
        byte[] baseline = tight.Tables();
        HostCheck.Equal((long)Start, Map(tight, Start, Reservation, 0));
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(tight.State, Start, 4096, 3));
        HostCheck.True(tight.State->BackedPages == 0 && baseline.AsSpan().SequenceEqual(tight.Tables()));
    }

    [HostTest("m16-006 malformed unsupported alias stale and W-X inputs preserve ownership", HostLane.Fast, 3)]
    private static void Rejections()
    {
        using var owner = new Owner(); owner.Begin();
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
        byte[] committed = owner.Tables(); *owner.At(Start) = 0x61;
        HostCheck.Equal(-17L, Map(owner, Start, 4096, 0));
        HostCheck.Equal(-22L, LinuxMemoryServices.Protect(owner.State, Start, 4096, 7));
        HostCheck.Equal(-22L, LinuxMemoryServices.Advise(owner.State, Start, 4096, 999));
        HostCheck.True(committed.AsSpan().SequenceEqual(owner.Tables()) && *owner.At(Start) == 0x61);
        owner.Memory.Tables[0] ^= 4; // Stale/untrusted live root.
        byte[] stale = owner.Tables();
        HostCheck.Equal(-14L, LinuxMemoryServices.Unmap(owner.State, Start, 4096));
        HostCheck.True(stale.AsSpan().SequenceEqual(owner.Tables()) && owner.State->ReservedBytes == 4096);
        using var other = new Owner(); var admission = other.Memory;
        admission.CandidateTables = admission.Tables;
        HostCheck.True(!LinuxMemoryServices.Begin(other.State, admission));
        HostCheck.True(other.State->Root.Status == X64UserAddressSpaceStatus.Uninitialized &&
            new ReadOnlySpan<byte>(other.Memory.Pool, (int)PoolPages * 4096).IndexOfAnyExcept((byte)0xa5) == -1);
    }

    [HostTest("m16-006 partial anonymous discard distinguishes retained accessible backing and NONE release", HostLane.Fast, 4)]
    private static void Discard()
    {
        using var owner = new Owner(); owner.Begin();
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
        using var owner = new Owner(); owner.Begin();
        for (uint i = 0; i < LinuxMemoryServices.MaximumRegions; i++)
            HostCheck.Equal((long)(Start + i * 8192), Map(owner, Start + i * 8192, 1, 0));
        byte[] tables = owner.Tables();
        HostCheck.Equal(-12L, Map(owner, Start + 0x1000000, 4096, 0));
        HostCheck.True(owner.State->ReservedBytes == LinuxMemoryServices.MaximumRegions * 4096 &&
            owner.State->BackedPages == 0 && tables.AsSpan().SequenceEqual(owner.Tables()));
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

    [HostTest("m16-006 repeated identical protection and contiguous commits avoid metadata fragmentation", HostLane.Fast, 6)]
    private static void RepeatedProtection()
    {
        using var owner = new Owner(); owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 128 * 4096, 0));
        for (uint i = 1; i < 128; i += 2)
            HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + i * 4096, 4096, 0));
        for (uint i = 0; i < PoolPages; i++)
        {
            HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + i * 4096, 4096, 3));
            *owner.At(Start + i * 4096) = (byte)(i + 1);
        }
        HostCheck.True(owner.State->ReservedBytes == 128 * 4096 && owner.State->BackedPages == PoolPages);
        for (uint i = 0; i < PoolPages; i++) HostCheck.Equal((byte)(i + 1), *owner.At(Start + i * 4096));
        HostCheck.True(LinuxMemoryServices.Audit(owner.State));
    }

    [HostTest("m16-006 mremap preserves old bytes zeroes growth and distinguishes guards from stack probes", HostLane.Fast, 7)]
    private static void Remapping()
    {
        using var owner = new Owner(); owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 4096, 3));
        *owner.At(Start) = 0x51;
        new Span<byte>(owner.Memory.Pool + 4096, 4096).Fill(0x7b);
        HostCheck.Equal((long)Start, LinuxMemoryServices.Remap(owner.State, Start, 4096, 8192, 0));
        HostCheck.True(*owner.At(Start) == 0x51 && owner.State->BackedPages == 2 &&
            new ReadOnlySpan<byte>(owner.At(Start + 4096), 4096).IndexOfAnyExcept((byte)0) == -1);
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
        using var owner = new Owner(); owner.Begin();
        HostCheck.Equal((long)Start, Map(owner, Start, 3 * 4096, 3)); *owner.At(Start + 4096) = 0x62;
        HostCheck.True(!LinuxMemoryServices.IsDumpExcluded(owner.State, Start, 3 * 4096));
        HostCheck.Equal(0L, LinuxMemoryServices.Advise(owner.State, Start + 4096, 4096, 16));
        HostCheck.True(LinuxMemoryServices.IsDumpExcluded(owner.State, Start + 4096, 4096) &&
            !LinuxMemoryServices.IsDumpExcluded(owner.State, Start, 3 * 4096) && *owner.At(Start + 4096) == 0x62);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(owner.State, Start + 4096, 4096, 1));
        HostCheck.True(LinuxMemoryServices.IsDumpExcluded(owner.State, Start + 4096, 4096));
        HostCheck.Equal(0L, LinuxMemoryServices.Advise(owner.State, Start + 4096, 4096, 17));
        HostCheck.True(!LinuxMemoryServices.IsDumpExcluded(owner.State, Start + 4096, 4096) && *owner.At(Start + 4096) == 0x62);
    }

    [HostTest("m16-006 static root adoption rejects stale root before touching pool or state", HostLane.Fast, 9)]
    private static void Adoption()
    {
        using var owner = new Owner();
        var root = X64UserAddressSpace.Build(owner.Memory.StaticRanges, owner.Memory.StaticCount, owner.Memory.Tables,
            owner.Memory.RootPhysical, owner.Memory.TablePages, owner.Memory.Conditions);
        HostCheck.True(root.Succeeded);
        owner.Memory.Tables[0] ^= 4;
        HostCheck.True(!LinuxMemoryServices.Adopt(owner.State, owner.Memory, root));
        HostCheck.True(owner.State->Root.Status == X64UserAddressSpaceStatus.Uninitialized &&
            new ReadOnlySpan<byte>(owner.Memory.Pool, (int)PoolPages * 4096).IndexOfAnyExcept((byte)0xa5) == -1);
        owner.Memory.Tables[0] ^= 4;
        HostCheck.True(LinuxMemoryServices.Adopt(owner.State, owner.Memory, root) && LinuxMemoryServices.Audit(owner.State));
    }
}
