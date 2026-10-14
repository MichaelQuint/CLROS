using System.Runtime.InteropServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;

internal static unsafe class LinuxStartupServicesTests
{
    private const ulong Output = 0x100000000, Heap = 0x400000000;
    private struct Model
    {
        public LinuxMemoryServices.Admission Memory;
        public ulong Ticks;
        public uint Reads, Copies, Fences;
        public bool FailCopy, FailClock, Regress;
    }
    private sealed class Owner : IDisposable
    {
        public readonly LinuxMemoryServicesTests.Owner Memory;
        public readonly LinuxStartupServices.State* State;
        public readonly Model* Context;
        public readonly byte* Entropy;
        public Owner() : this(new LinuxMemoryServicesTests.Owner()) { }
        public Owner(LinuxMemoryServicesTests.Owner memory)
        {
            Memory = memory;
            Memory.Begin();
            HostCheck.Equal((long)Output, LinuxMemoryServices.Map(Memory.State, Output, 4096, 3, 0x100022, ulong.MaxValue, 0));
            State = (LinuxStartupServices.State*)NativeMemory.AlignedAlloc((nuint)sizeof(LinuxStartupServices.State), 16); *State = default;
            Context = (Model*)NativeMemory.AlignedAlloc((nuint)sizeof(Model), 16); *Context = new() { Memory = Memory.Memory, Ticks = 10000 };
            Entropy = (byte*)NativeMemory.Alloc(32); for (uint i = 0; i < 32; i++) Entropy[i] = (byte)(i + 1);
        }
        public LinuxStartupServices.Admission Admission() => new()
        {
            Memory = Memory.State, User = new() { Context = Context, PageAccess = &Permissions },
            CopyToUser = &Copy, ReadClock = &Clock, MemoryBarrier = &Fence, FrequencyHz = 100000000,
            PhysicalBytes = 256 * 1024 * 1024, AvailableBytes = 64 * 1024 * 1024,
            HeapStart = Heap, HeapBytes = 2 * 1024 * 1024, StackBytes = 65536,
            ProcessId = 1, Entropy = Entropy, CapturedEntropyBytes = 32, SingleCpuValidated = true, FirmwareExited = true
        };
        public void Begin() => HostCheck.True(LinuxStartupServices.Begin(State, Admission()));
        public long Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0) =>
            LinuxStartupServices.Dispatch(State, new() { Number = number, Argument0 = a0, Argument1 = a1, Argument2 = a2,
                Argument3 = a3, Argument4 = a4, Argument5 = a5 });
        public void Dispose() { Memory.Dispose(); NativeMemory.AlignedFree(State); NativeMemory.AlignedFree(Context); NativeMemory.Free(Entropy); }
    }
    private sealed class Snapshot
    {
        private readonly byte[] tables, memory, startup, pool;
        public Snapshot(Owner owner)
        {
            tables = owner.Memory.Tables();
            memory = new ReadOnlySpan<byte>(owner.Memory.State, sizeof(LinuxMemoryServices.State)).ToArray();
            startup = new ReadOnlySpan<byte>(owner.State, sizeof(LinuxStartupServices.State)).ToArray();
            pool = new ReadOnlySpan<byte>(owner.Memory.Memory.Pool, checked((int)owner.Memory.Memory.PoolPages * 4096)).ToArray();
        }
        public void AssertUnchanged(Owner owner) => HostCheck.True(tables.AsSpan().SequenceEqual(owner.Memory.Tables()) &&
            memory.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.State, sizeof(LinuxMemoryServices.State))) &&
            startup.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.State, sizeof(LinuxStartupServices.State))) &&
            pool.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.Memory.Pool, pool.Length)));
    }
    private static void BrkRejected(Owner owner, ulong requested)
    {
        var snapshot = new Snapshot(owner); ulong current = owner.State->Break;
        long before = GC.GetAllocatedBytesForCurrentThread();
        long result = owner.Call(12, requested), allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.Equal((long)current, result); HostCheck.Equal(0L, allocated);
        snapshot.AssertUnchanged(owner); HostCheck.True(LinuxMemoryServices.Audit(owner.Memory.State));
    }
    [UnmanagedCallersOnly]
    private static byte Permissions(void* context, ulong page)
    {
        var m = ((Model*)context)->Memory;
        if (!X64UserAddressSpace.TryReadLeaf(m.Tables, m.RootPhysical, m.TablePages, page, out ulong leaf) || (leaf & 5) != 5) return 0;
        return (byte)(1 | ((leaf & 2) != 0 ? 2 : 0) | ((leaf & (1UL << 63)) == 0 ? 4 : 0));
    }
    [UnmanagedCallersOnly]
    private static byte Copy(void* context, ulong address, byte* source, uint bytes)
    {
        Model* model = (Model*)context; model->Copies++;
        if (model->FailCopy) return 0;
        for (uint i = 0; i < bytes; i++)
        {
            var m = model->Memory;
            if (!X64UserAddressSpace.TryReadLeaf(m.Tables, m.RootPhysical, m.TablePages, address + i, out ulong leaf) || (leaf & 7) != 7) return 0;
            ulong physical = (leaf & 0x000ffffffffff000) + ((address + i) & 4095);
            if (physical < m.PoolPhysical || physical >= m.PoolPhysical + m.PoolPages * 4096UL) return 0;
            m.Pool[physical - m.PoolPhysical] = source[i];
        }
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Clock(void* context, ulong* ticks)
    {
        Model* model = (Model*)context; model->Reads++;
        if (model->FailClock) return 0;
        model->Ticks += 100000; *ticks = model->Regress ? 0 : model->Ticks; return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Fence(void* context)
    { System.Threading.Thread.MemoryBarrier(); ((Model*)context)->Fences++; return 1; }

    [HostTest("m16-006 real brk growth shrink and quota failure retain actual break", HostLane.Fast, 0)]
    private static void HeapLifecycle()
    {
        using var owner = new Owner(); owner.Begin();
        HostCheck.Equal((long)Heap, owner.Call(12));
        HostCheck.Equal((long)Heap + 8192, owner.Call(12, Heap + 8192));
        HostCheck.Equal((long)Heap + 8192, owner.Call(12));
        HostCheck.True(owner.Memory.State->BackedPages == 3);
        *owner.Memory.At(Heap) = 0x49;
        HostCheck.Equal((long)Heap + 8192, owner.Call(12, Heap + 3 * 1024 * 1024));
        HostCheck.Equal((long)Heap + 8192, owner.Call(12, Heap + 2 * 1024 * 1024));
        HostCheck.Equal((long)Heap, owner.Call(12, Heap));
        HostCheck.True(owner.Memory.State->BackedPages == 1 && owner.Memory.Leaf(Heap) == 0);
        HostCheck.Equal((long)Heap + 8192, owner.Call(12, Heap + 8192));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Heap), 8192).IndexOfAnyExcept((byte)0) == -1);
    }

    [HostTest("linux.startup brk shrink preserves retained futex backing and exact raw break until completion drains")]
    private static void GuardedHeap()
    {
        using var owner = new Owner(); owner.Begin(); HostCheck.Equal((long)Heap + 3 * 4096, owner.Call(12, Heap + 3 * 4096));
        const ulong word = Heap + 4096 + 128, key = LinuxThreadServicesTests.Key;
        *(uint*)owner.Memory.At(word) = 42;
        using LinuxThreadServicesTests.Model threads = new(futex: true); threads.UseMemory(owner.Memory.Memory);
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(owner.Memory.State, threads.Service, &LinuxFutexTests.MemoryGuard));
        var parent = threads.Start(); threads.Kernel.Dispatch(); var child = threads.Clone();
        HostCheck.Equal(LinuxSyscallAction.Blocked, threads.Call(202, word, 128, 42).Action);
        LinuxFutexTests.Park(threads, parent); threads.Kernel.Dispatch();
        BrkRejected(owner, Heap + 4096); HostCheck.Equal(42U, *(uint*)owner.Memory.At(word));
        HostCheck.Equal((long)Heap + 4 * 4096, owner.Call(12, Heap + 4 * 4096));
        HostCheck.Equal((long)Heap + 3 * 4096, owner.Call(12, Heap + 3 * 4096));
        HostCheck.Equal(1L, threads.Call(202, word, 129, 1).Value); BrkRejected(owner, Heap + 4096);
        threads.Yield(child); threads.Kernel.Dispatch(); HostCheck.Equal(0L, threads.Service->CompleteFutexWait(key, 2).Value);
        HostCheck.Equal((long)Heap + 4096, owner.Call(12, Heap + 4096)); HostCheck.Equal(0UL, owner.Memory.Leaf(word));
        LinuxFutexTests.Drain(threads, owner.Memory.State);
    }

    [HostTest("m16-006 startup queries use x64 layouts actual memory and admitted CPU facts", HostLane.Fast, 1)]
    private static void Queries()
    {
        using var owner = new Owner(); owner.Begin();
        HostCheck.Equal(8L, owner.Call(204, 0, 8, Output));
        HostCheck.Equal(1UL, *(ulong*)owner.Memory.At(Output));
        HostCheck.Equal(0L, owner.Call(99, Output));
        byte* info = owner.Memory.At(Output);
        HostCheck.Equal(256UL * 1024 * 1024, *(ulong*)(info + 32));
        HostCheck.Equal(64UL * 1024 * 1024 - 4096, *(ulong*)(info + 40));
        HostCheck.Equal((ushort)1, *(ushort*)(info + 80)); HostCheck.Equal(1U, *(uint*)(info + 104));
        HostCheck.Equal(0L, owner.Call(97, 3, Output + 128));
        HostCheck.Equal(65536UL, *(ulong*)(info + 128)); HostCheck.Equal(65536UL, *(ulong*)(info + 136));
        HostCheck.Equal(1L, owner.Call(39)); HostCheck.Equal(1L, owner.Call(186));
        HostCheck.Equal(-3L, owner.Call(204, 2, 8, Output)); HostCheck.Equal(-22L, owner.Call(204, 0, 4, Output));
        HostCheck.Equal(-14L, owner.Call(97, 3, 0)); HostCheck.Equal(-22L, owner.Call(97, 999, Output));
        HostCheck.Equal(-1L, owner.Call(302, 0, 3, Output, Output + 128));
        HostCheck.Equal(0L, owner.Call(302, 0, 3, 0, 0));
        HostCheck.Equal(-38L, owner.Call(158)); // No fake TLS/thread setup.
        HostCheck.Equal(24L, owner.Call(324)); HostCheck.Equal(-1L, owner.Call(324, 8));
        HostCheck.Equal(0L, owner.Call(324, 16)); HostCheck.Equal(0L, owner.Call(324, 8));
        HostCheck.True(owner.Context->Fences == 1); HostCheck.Equal(-22L, owner.Call(324, 1));
    }

    [HostTest("m16-006 clock conversion regression and copy failures never invent time", HostLane.Fast, 2)]
    private static void Time()
    {
        using var owner = new Owner(); owner.Begin();
        HostCheck.Equal(0L, owner.Call(228, 1, Output));
        ulong nanos = *(ulong*)(owner.Memory.At(Output) + 8);
        HostCheck.True(nanos > 0 && nanos < 1000000000 && *(ulong*)owner.Memory.At(Output) == 0);
        HostCheck.Equal(0L, owner.Call(228, 1, Output));
        HostCheck.True(*(ulong*)(owner.Memory.At(Output) + 8) > nanos);
        byte[] before = new ReadOnlySpan<byte>(owner.Memory.At(Output), 16).ToArray();
        owner.Context->Regress = true; HostCheck.Equal(-5L, owner.Call(228, 1, Output));
        HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.At(Output), 16)));
        owner.Context->Regress = false; owner.Context->FailClock = true; HostCheck.Equal(-5L, owner.Call(99, Output));
        HostCheck.Equal(-22L, owner.Call(228, 0, Output));
        HostCheck.Equal(-14L, owner.Call(228, 1, ulong.MaxValue));
    }

    [HostTest("m16-006 fresh entropy is consumed once with genuine exhaustion and burn on attempted copy", HostLane.Fast, 3)]
    private static void Random()
    {
        using var owner = new Owner(); owner.Begin();
        HostCheck.Equal(-14L, owner.Call(318, 0, 16)); HostCheck.Equal(32U, owner.State->RemainingEntropy);
        HostCheck.Equal(16L, owner.Call(318, Output, 16));
        for (int i = 0; i < 16; i++) HostCheck.Equal((byte)(i + 1), owner.Memory.At(Output)[i]);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Entropy, 16).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal(-38L, owner.Call(318, Output + 64, 32)); HostCheck.Equal(16U, owner.State->RemainingEntropy);
        owner.Context->FailCopy = true;
        HostCheck.Equal(-14L, owner.Call(318, Output + 64, 16)); HostCheck.Equal(0U, owner.State->RemainingEntropy);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Entropy, 32).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal(-11L, owner.Call(318, Output, 16, 1)); HostCheck.Equal(-38L, owner.Call(318, Output, 16));
        HostCheck.Equal(-22L, owner.Call(318, Output, 16, 4));
    }

    [HostTest("m16-006 startup admission aliases stale ownership and clock failure reject without reserving heap", HostLane.Fast, 4)]
    private static void AdmissionFailures()
    {
        using var owner = new Owner(); ulong reserved = owner.Memory.State->ReservedBytes;
        var admission = owner.Admission(); admission.SingleCpuValidated = false;
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission));
        admission = owner.Admission(); admission.Entropy = (byte*)owner.State;
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission));
        admission = owner.Admission(); admission.CapturedEntropyBytes = 31;
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission));
        owner.Context->FailClock = true; HostCheck.True(!LinuxStartupServices.Begin(owner.State, owner.Admission()));
        HostCheck.True(owner.Memory.State->ReservedBytes == reserved && owner.State->RemainingEntropy == 0);
        owner.Context->FailClock = false; owner.Begin();
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, owner.Admission()));
    }

    [HostTest("m16-006 brk rejects separately owned fixed mapping without mutation", HostLane.Fast, 5)]
    private static void HeapFixedReplacement()
    {
        using var owner = new Owner(); owner.Begin();
        ulong initial = (ulong)owner.Call(12), page = initial + 4096;
        HostCheck.Equal((long)Heap, (long)initial);
        HostCheck.Equal((long)page, owner.Call(9, page, 4096, 3, 0x32, ulong.MaxValue));
        *owner.Memory.At(page) = 0x5a;
        HostCheck.Equal(0L, owner.Call(10, page, 4096, 1));
        ulong leaf = owner.Memory.Leaf(page);
        BrkRejected(owner, initial + 8192);
        HostCheck.Equal(leaf, owner.Memory.Leaf(page));
        HostCheck.Equal((byte)0x5a, *owner.Memory.At(page));
    }

    [HostTest("m16-006 partial fixed overlap poisons active heap prefix but noreplace is transactional", HostLane.Fast, 6)]
    private static void HeapPartialReplacement()
    {
        using var owner = new Owner(); owner.Begin();
        var before = new Snapshot(owner);
        HostCheck.Equal(-17L, owner.Call(9, Heap, 4096, 3, 0x100022, ulong.MaxValue));
        before.AssertUnchanged(owner);
        HostCheck.Equal((long)Heap + 8192, owner.Call(12, Heap + 8192));
        *owner.Memory.At(Heap + 4096) = 0x66;
        HostCheck.Equal((long)Heap - 4096, owner.Call(9, Heap - 4096, 8192, 3, 0x32, ulong.MaxValue));
        *owner.Memory.At(Heap - 4096) = 0x71; *owner.Memory.At(Heap) = 0x72;
        HostCheck.Equal(0L, owner.Call(10, Heap - 4096, 8192, 1));
        BrkRejected(owner, Heap + 12288); BrkRejected(owner, Heap + 4096); BrkRejected(owner, Heap);
        HostCheck.Equal((byte)0x71, *owner.Memory.At(Heap - 4096));
        HostCheck.Equal((byte)0x72, *owner.Memory.At(Heap));
        HostCheck.Equal((byte)0x66, *owner.Memory.At(Heap + 4096));
    }

    [HostTest("m16-006 heap holes and independent none replacements never regain brk ownership", HostLane.Fast, 7)]
    private static void HeapHoles()
    {
        using var owner = new Owner(); owner.Begin();
        HostCheck.Equal(0L, owner.Call(11, Heap + 4096, 4096));
        HostCheck.Equal((long)Heap + 4096, owner.Call(12, Heap + 4096));
        BrkRejected(owner, Heap + 8192);
        HostCheck.Equal((long)Heap + 4096, owner.Call(9, Heap + 4096, 4096, 0, 0x100022, ulong.MaxValue));
        BrkRejected(owner, Heap + 8192);
        HostCheck.Equal(0L, owner.Call(10, Heap + 4096, 4096, 3));
        *owner.Memory.At(Heap + 4096) = 0x51;
        HostCheck.Equal(0L, owner.Call(10, Heap + 4096, 4096, 1));
        HostCheck.Equal((long)Heap, owner.Call(12, Heap));
        HostCheck.Equal((long)Heap + 4096, owner.Call(12, Heap + 4096));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Heap), 4096).IndexOfAnyExcept((byte)0) == -1);
        BrkRejected(owner, Heap + 8192);
        HostCheck.Equal((byte)0x51, *owner.Memory.At(Heap + 4096));
    }

    [HostTest("m16-006 owned protected heap shrink releases only complete pages and regrows zero", HostLane.Fast, 8)]
    private static void HeapProtectedShrink()
    {
        using var owner = new Owner(); owner.Begin();
        HostCheck.Equal((long)Heap + 12288, owner.Call(12, Heap + 12288));
        new Span<byte>(owner.Memory.At(Heap), 12288).Fill(0x4f);
        HostCheck.Equal(0L, owner.Call(10, Heap, 12288, 0));
        HostCheck.Equal((long)Heap + 4096, owner.Call(12, Heap + 4096));
        HostCheck.Equal(2U, owner.Memory.State->BackedPages); // Output plus the retained first heap page.
        HostCheck.Equal(0L, owner.Call(10, Heap, 4096, 1));
        HostCheck.Equal((byte)0x4f, *owner.Memory.At(Heap));
        ulong first = owner.Memory.Leaf(Heap);
        HostCheck.Equal((long)Heap + 12288, owner.Call(12, Heap + 12288));
        HostCheck.Equal(first, owner.Memory.Leaf(Heap));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Heap + 4096), 8192).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal((long)Heap, owner.Call(12, Heap));
        HostCheck.Equal((long)Heap + 1, owner.Call(12, Heap + 1));
        HostCheck.Equal(0L, owner.Call(10, Heap, 4096, 1)); first = owner.Memory.Leaf(Heap);
        HostCheck.Equal((long)Heap + 2, owner.Call(12, Heap + 2)); HostCheck.Equal(first, owner.Memory.Leaf(Heap));
        HostCheck.Equal((long)Heap, owner.Call(9, Heap, 4096, 1, 0x32, ulong.MaxValue));
        BrkRejected(owner, Heap + 3); BrkRejected(owner, Heap);
    }

    [HostTest("m16-006 heap ownership survives dump advice and never coalesces independent neighbor", HostLane.Fast, 9)]
    private static void HeapPolicyFlags()
    {
        using var owner = new Owner(); owner.Begin();
        HostCheck.Equal((long)Heap - 4096, owner.Call(9, Heap - 4096, 4096, 0, 0x100022, ulong.MaxValue));
        HostCheck.Equal((long)Heap + 1, owner.Call(12, Heap + 1));
        HostCheck.Equal(0L, owner.Call(28, Heap, 4096, 16));
        HostCheck.Equal(0L, owner.Call(10, Heap, 4096, 0));
        HostCheck.Equal(0L, owner.Call(28, Heap, 4096, 4));
        HostCheck.Equal((long)Heap + 8192, owner.Call(12, Heap + 8192));
        HostCheck.Equal(0L, owner.Call(10, Heap, 4096, 1));
        HostCheck.True(LinuxMemoryServices.IsDumpExcluded(owner.Memory.State, Heap, 4096));
        HostCheck.Equal((long)Heap, owner.Call(12, Heap));
        HostCheck.Equal((long)Heap + 8192, owner.Call(12, Heap + 8192));
        HostCheck.True(LinuxMemoryServices.IsDumpExcluded(owner.Memory.State, Heap, 4096));
        HostCheck.True(!LinuxMemoryServices.IsDumpExcluded(owner.Memory.State, Heap + 4096, 4096));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Heap), 8192).IndexOfAnyExcept((byte)0) == -1);
        BrkRejected(owner, ulong.MaxValue); BrkRejected(owner, Heap - 1); BrkRejected(owner, Heap + 2 * 1024 * 1024 + 1);
    }

    [HostTest("m16-006 brk end-of-user-window arithmetic and byte changes retain owned pages", HostLane.Fast, 10)]
    private static void HeapEndOfWindow()
    {
        using var owner = new Owner(); var admission = owner.Admission();
        ulong end = LinuxSyscallGateway.UserEnd, start = end - 8192;
        admission.HeapStart = start; admission.HeapBytes = 8192;
        HostCheck.True(LinuxStartupServices.Begin(owner.State, admission));
        HostCheck.Equal((long)end - 1, owner.Call(12, end - 1));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(start), 8192).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal((long)end, owner.Call(12, end)); BrkRejected(owner, end + 1);
        HostCheck.Equal((long)start + 1, owner.Call(12, start + 1));
        *owner.Memory.At(start) = 0x65; ulong leaf = owner.Memory.Leaf(start);
        HostCheck.Equal((long)start + 2, owner.Call(12, start + 2));
        HostCheck.Equal(leaf, owner.Memory.Leaf(start)); HostCheck.Equal((byte)0x65, *owner.Memory.At(start));
        HostCheck.True(LinuxMemoryServices.Audit(owner.Memory.State));
    }

    [HostTest("m16-006 brk table region and reservation quota failures publish no partial ownership", HostLane.Fast, 11)]
    private static void HeapPublicationFailures()
    {
        using var baseline = new Owner();
        using var tableLimited = new Owner(new LinuxMemoryServicesTests.Owner(baseline.Memory.State->Root.UsedTables));
        tableLimited.Begin(); BrkRejected(tableLimited, Heap + 4096);
        using var physical = new Owner(); physical.Begin();
        HostCheck.Equal(0L, physical.Call(28, Heap + 4096, 4096, 16));
        // Distinct dump-policy fragments allocate candidate backing before the
        // final fragment exhausts the pool. No partial allocation may publish.
        BrkRejected(physical, Heap + physical.Memory.Memory.PoolPages * 4096UL);
        using var owner = new Owner(); owner.Begin();
        for (uint i = 0; i < LinuxMemoryServices.MaximumRegions - 2; i++)
        {
            ulong address = Output + 0x1000000 + i * 8192;
            HostCheck.Equal((long)address, owner.Call(9, address, 4096, 0, 0x100022, ulong.MaxValue));
        }
        var full = new Snapshot(owner);
        HostCheck.Equal(-12L, owner.Call(9, Output + 0x2000000, 4096, 0, 0x100022, ulong.MaxValue));
        full.AssertUnchanged(owner); BrkRejected(owner, Heap + 4096);
        using var quota = new Owner(new LinuxMemoryServicesTests.Owner(quota: 4096));
        var empty = new Snapshot(quota);
        HostCheck.True(!LinuxStartupServices.Begin(quota.State, quota.Admission()));
        empty.AssertUnchanged(quota);
        using var conflict = new Owner();
        HostCheck.Equal((long)Heap, LinuxMemoryServices.Map(conflict.Memory.State, Heap, 4096, 0, 0x100022, ulong.MaxValue, 0));
        var foreign = new Snapshot(conflict);
        HostCheck.True(!LinuxStartupServices.Begin(conflict.State, conflict.Admission()));
        foreign.AssertUnchanged(conflict);
    }
}
