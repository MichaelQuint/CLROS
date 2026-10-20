using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Memory;
using OS.Std.Hash;

internal static unsafe class LinuxStartupServicesTests
{
    [HostTest("linux startup binds configured GC budget to actual pool while retaining physical memory facts and hard commit cap")]
    private static void GcBudgetAdmission()
    {
        using Owner owner = new(new LinuxMemoryServicesTests.Owner(backingPages: 4096));
        var admission = owner.Admission(); admission.GcBudget = LinuxGcBudgetTests.Read("env.DOTNET_GCHeapHardLimit=800000\n", 4096, 4096, 8388608);
        HostCheck.True(admission.GcBudget.Succeeded); HostCheck.True(LinuxStartupServices.Begin(owner.State, admission));
        HostCheck.Equal(8388608UL, owner.State->GcBudget.GcLimitBytes);
        HostCheck.Equal(16777216UL, owner.State->GcBudget.PoolBytes);
        HostCheck.Equal(0L, owner.Call(99, Output));
        HostCheck.Equal(admission.PhysicalBytes, *(ulong*)(owner.Memory.At(Output) + 32));
        HostCheck.Equal(-12L, owner.Call(9, 0, 16777216, 3, 0x22, ulong.MaxValue));
        long mapped = owner.Call(9, 0, 4096, 3, 0x22, ulong.MaxValue); HostCheck.True(mapped > 0);
        HostCheck.Equal(0L, owner.Call(11, (ulong)mapped, 4096)); HostCheck.True(LinuxMemoryServices.Audit(owner.Memory.State));
    }

    [HostTest("linux startup rejects failed and stale-pool GC budgets before heap or entropy ownership changes")]
    private static void RejectedGcBudget()
    {
        using var owner = new Owner();
        foreach (var budget in new[] { LinuxGcBudgetTests.Read(""), LinuxGcBudgetTests.Read("env.DOTNET_GCHeapHardLimit=bad!\n"),
            LinuxGcBudgetTests.Read("env.DOTNET_GCHeapHardLimit=800000\n", 4096, 4096, 8388608) })
        {
            var admission = owner.Admission(); admission.GcBudget = budget; var before = new Snapshot(owner);
            HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission)); before.AssertUnchanged(owner);
            HostCheck.Equal(0UL, owner.State->GcBudget.GcLimitBytes);
        }
        owner.Begin(); HostCheck.True(!owner.State->GcBudget.Succeeded);
    }

    [HostTest("linux startup rejects an initial heap larger than the declared native headroom")]
    private static void GcNativeHeadroom()
    {
        using Owner owner = new(new LinuxMemoryServicesTests.Owner(backingPages: 4096));
        var admission = owner.Admission(); admission.GcBudget = LinuxGcBudgetTests.Read("env.DOTNET_GCHeapHardLimit=800000\n", 4096, 4096, 8388608);
        admission.HeapBytes = 8388608 + 4096; var before = new Snapshot(owner);
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission)); before.AssertUnchanged(owner);
    }

    [HostTest("linux.runtime r13 reservation and sparse commit trace fits retained mappings without false ENOMEM")]
    private static void RuntimeMemoryTrace()
    {
        using Owner owner = new(new LinuxMemoryServicesTests.Owner(tables: X64UserAddressSpace.MaximumTables,
            backingPages: LinuxMemoryServices.DefaultPoolPages, staticRows: 53));
        owner.Begin();
        // Original r13 memory calls after its first page-rounded TLS mmap (the
        // Owner already maps that page). Kernel static addresses are synthetic;
        // their actual count and all reservation/commit sizes are retained.
        (ulong Nr, ulong Address, ulong Bytes, ulong Arg2, ulong Arg3, long Expected)[] calls =
        [
            (12, 0, 0, 0, 0, 17179869184), (12, 17179877376, 0, 0, 0, 17179877376),
            (9, 17179869184, 4096, 0, 50, 17179869184), (9, 0, 4096, 3, 34, 4294971392),
            (9, 0, 1585152, 0, 34, 4294975488), (10, 4294983680, 1576960, 3, 0, 0),
            (9, 0, 535007232, 0, 34, 4296560640), (9, 0, 16384, 3, 34, 4831567872),
            (11, 4296560640, 4096, 0, 0, 0), (28, 4296564736, 535003136, 16, 0, 0),
            (9, 0, 4796416, 0, 34, 4831584256), (28, 4831584256, 4796416, 16, 0, 0),
            (10, 4831584256, 40960, 3, 0, 0), (28, 4831584256, 40960, 17, 0, 0),
            (10, 4831842304, 40960, 3, 0, 0), (28, 4831842304, 40960, 17, 0, 0),
            (10, 4832100352, 20480, 3, 0, 0), (28, 4832100352, 20480, 17, 0, 0),
            (10, 4832227328, 8192, 3, 0, 0), (28, 4832227328, 8192, 17, 0, 0),
            (9, 0, 819220, 3, 34, 4836380672), (9, 0, 8192, 3, 34, 4837203968),
            (9, 0, 4096, 3, 34, 4296560640), (9, 0, 4096, 3, 34, 4837212160),
            (11, 4837212160, 4096, 0, 0, 0), (11, 4296560640, 4096, 0, 0, 0),
            (9, 0, 4096, 3, 34, 4296560640), (10, 4332716032, 4096, 3, 0, 0),
            (28, 4332716032, 4096, 17, 0, 0), (10, 4336910336, 4096, 3, 0, 0),
            (28, 4336910336, 4096, 17, 0, 0), (10, 4341104640, 4096, 3, 0, 0)
        ];
        for (int i = 0; i < calls.Length; i++)
        {
            var c = calls[i];
            HostCheck.True(owner.Memory.State->BackedPages + 1 < owner.Memory.Memory.PoolPages);
            HostCheck.True(owner.Memory.State->Root.UsedTables < owner.Memory.Memory.TablePages / 2);
            long value = owner.Call(c.Nr, c.Address, c.Bytes, c.Arg2, c.Arg3, c.Nr == 9 ? ulong.MaxValue : 0);
            HostCheck.Equal(c.Expected, value, "recorded memory call " + i);
            HostCheck.True(LinuxMemoryServices.Audit(owner.Memory.State));
        }
        HostCheck.Equal(7UL, owner.Memory.Leaf(4341104640) & 7);
    }


    [HostTest("linux.runtime r17 first four MiB commit exhausts retained backing atomically before guard")]
    private static void RuntimeBackingExhaustion()
    {
        using Owner owner = new(new LinuxMemoryServicesTests.Owner(tables: X64UserAddressSpace.MaximumTables,
            backingPages: LinuxMemoryServices.DefaultPoolPages, staticRows: 53, runtimeImage: true));
        owner.Begin();
        // Literal r17 memory mutations in serial order, after Owner's first TLS
        // mmap and before main identity 4294967297 ordinal 592. Static placements
        // are synthetic; row count, cookie page and initial stack are retained.
        (ulong Nr, ulong Address, ulong Bytes, ulong Arg2, ulong Arg3, long Expected)[] calls =
        [
            (12, 0, 0, 0, 0, 17179869184),
            (12, 17179877376, 0, 0, 0, 17179877376),
            (9, 17179869184, 4096, 0, 50, 17179869184),
            (9, 0, 4096, 3, 34, 4294971392),
            (9, 0, 1585152, 0, 34, 4294975488),
            (10, 4294983680, 1576960, 3, 0, 0),
            (9, 0, 535007232, 0, 34, 4296560640),
            (9, 0, 16384, 3, 34, 4831567872), // Thread 2, ordinal 6.
            (11, 4296560640, 4096, 0, 0, 0),
            (28, 4296564736, 535003136, 16, 0, 0),
            (9, 0, 4796416, 0, 34, 4831584256),
            (28, 4831584256, 4796416, 16, 0, 0),
            (10, 4831584256, 40960, 3, 0, 0),
            (28, 4831584256, 40960, 17, 0, 0),
            (10, 4831842304, 40960, 3, 0, 0),
            (28, 4831842304, 40960, 17, 0, 0),
            (10, 4832100352, 20480, 3, 0, 0),
            (28, 4832100352, 20480, 17, 0, 0),
            (10, 4832227328, 8192, 3, 0, 0),
            (28, 4832227328, 8192, 17, 0, 0),
            (9, 0, 819220, 3, 34, 4836380672),
            (9, 0, 8192, 3, 34, 4837203968),
            (9, 0, 4096, 3, 34, 4296560640),
            (9, 0, 4096, 3, 34, 4837212160),
            (11, 4837212160, 4096, 0, 0, 0),
            (11, 4296560640, 4096, 0, 0, 0),
            (9, 0, 4096, 3, 34, 4296560640),
            (10, 4332716032, 4096, 3, 0, 0),
            (28, 4332716032, 4096, 17, 0, 0),
            (10, 4336910336, 4096, 3, 0, 0),
            (28, 4336910336, 4096, 17, 0, 0),
            (10, 4341104640, 4096, 3, 0, 0),
            (28, 4341104640, 4096, 17, 0, 0),
            (10, 4345298944, 4096, 3, 0, 0),
            (28, 4345298944, 4096, 17, 0, 0),
            (10, 4299161600, 4096, 3, 0, 0),
            (28, 4299161600, 4096, 17, 0, 0),
            (9, 0, 147476, 3, 34, 4837212160),
            (9, 0, 28672, 3, 34, 4837363712),
            (9, 0, 4096, 3, 34, 4837392384),
            (9, 0, 16384, 3, 34, 4837396480),
            (9, 0, 126976, 0, 34, 4837412864),
            (11, 4837412864, 61440, 0, 0, 0),
            (28, 4837474304, 65536, 16, 0, 0),
            (10, 4837474304, 4096, 3, 0, 0),
            (28, 4837474304, 4096, 17, 0, 0),
            (10, 5451776, 4096, 3, 0, 0),
            (10, 5451776, 4096, 1, 0, 0),
            (10, 4299165696, 65536, 3, 0, 0),
            (28, 4299165696, 65536, 17, 0, 0),
            (10, 4837478400, 4096, 3, 0, 0),
            (28, 4837478400, 4096, 17, 0, 0),
            (10, 4341108736, 65536, 3, 0, 0),
            (28, 4341108736, 65536, 17, 0, 0),
            (9, 0, 32768, 3, 34, 4837412864),
            (9, 0, 1585152, 0, 34, 4837539840),
            (10, 4837548032, 1576960, 3, 0, 0),
            (9, 0, 1585152, 0, 34, 4839124992),
            (10, 4839133184, 1576960, 3, 0, 0),
            (10, 4345303040, 4194304, 3, 0, 0), // Thread 4, ordinal 4.
            (28, 4345303040, 4194304, 17, 0, 0), // Thread 4, ordinal 5.
            (10, 4831625216, 40960, 3, 0, 0),
            (28, 4831625216, 40960, 17, 0, 0),
            (10, 4831883264, 40960, 3, 0, 0),
            (28, 4831883264, 40960, 17, 0, 0),
            (10, 4832120832, 20480, 3, 0, 0),
            (28, 4832120832, 20480, 17, 0, 0),
            (10, 4832235520, 4096, 3, 0, 0),
            (28, 4832235520, 4096, 17, 0, 0),
            (10, 4378853376, 4096, 3, 0, 0),
            (28, 4378853376, 4096, 17, 0, 0),
            (9, 0, 4096, 3, 34, 4837445632),
            (11, 4837445632, 4096, 0, 0, 0),
            (9, 0, 1585152, 0, 34, 4840710144),
            (10, 4840718336, 1576960, 3, 0, 0),
            (10, 4832579584, 32768, 3, 0, 0),
            (28, 4832579584, 32768, 17, 0, 0),
            (10, 4832546816, 32768, 3, 0, 0),
            (28, 4832546816, 32768, 17, 0, 0),
            (10, 4832514048, 32768, 3, 0, 0),
            (28, 4832514048, 32768, 17, 0, 0),
            (10, 4832612352, 262144, 3, 0, 0),
            (28, 4832612352, 262144, 17, 0, 0),
            (10, 4832251904, 262144, 3, 0, 0),
            (28, 4832251904, 262144, 17, 0, 0),
            (9, 4378853376, 4096, 0, 50, 4378853376), // Thread 5, ordinal 11.
            (28, 4378853376, 4096, 16, 0, 0), // Thread 5, ordinal 12.
        ];
        for (int i = 0; i < calls.Length; i++)
        {
            var c = calls[i];
            HostCheck.Equal(c.Expected, owner.Call(c.Nr, c.Address, c.Bytes, c.Arg2, c.Arg3,
                c.Nr == 9 ? ulong.MaxValue : 0), "r17 memory call " + i);
            HostCheck.True(LinuxMemoryServices.Audit(owner.Memory.State));
            if (c.Nr == 10 && c.Address == 0x533000 && c.Arg2 == 1)
            {
                // The intervening musl stack probes must not change ownership.
                var before = new Snapshot(owner);
                for (uint page = 0; page < 384; page++)
                    HostCheck.Equal(page == 383 ? -14L : -12L,
                        owner.Call(25, 1879040000 - page * 4096UL, 4096, 8192));
                before.AssertUnchanged(owner);
            }
        }
        HostCheck.True(!owner.Memory.State->HasImageProtections && !owner.Memory.State->HasMutationGuard);
        var stats = RuntimeMemoryStatistics(owner.Memory);
        const uint requestedPages = 4194304 / 4096;
        Console.WriteLine($"HOST_MEMORY_DIAGNOSTIC r17 ordinal=592 regions={stats.Regions} backed={owner.Memory.State->BackedPages} " +
            $"free={stats.FreePages} largest={stats.LargestFreeRun} requested={requestedPages} " +
            $"tables={owner.Memory.State->Root.UsedTables}/{owner.Memory.Memory.TablePages} " +
            $"rows={stats.MappingRows}/{X64UserAddressSpace.MaximumRanges}");
        HostCheck.Equal(51U, stats.Regions);
        HostCheck.Equal(3076U, owner.Memory.State->BackedPages);
        HostCheck.Equal(1020U, stats.FreePages);
        HostCheck.Equal(1019U, stats.LargestFreeRun);
        HostCheck.Equal(86U, stats.MappingRows);
        HostCheck.True(stats.Regions + 2 <= LinuxMemoryServices.MaximumRegions);
        HostCheck.True(stats.MappingRows + 2 <= X64UserAddressSpace.MaximumRanges);
        HostCheck.True(owner.Memory.State->Root.UsedTables < owner.Memory.Memory.TablePages / 2);
        HostCheck.True(owner.Memory.State->ReservedBytes < owner.Memory.Memory.ReservationBytes);
        HostCheck.True(stats.FreePages < requestedPages && stats.LargestFreeRun < requestedPages);

        var rejected = new Snapshot(owner);
        HostCheck.Equal(-12L, owner.Call(10, 4349497344, 4194304, 3));
        rejected.AssertUnchanged(owner);
        HostCheck.True(LinuxMemoryServices.Audit(owner.Memory.State));

        // A guard is unnecessary to reproduce ENOMEM; even an allowing guard
        // cannot run because allocation rejects before candidate publication.
        ulong guardCalls = 0;
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(owner.Memory.State, &guardCalls, &AllowRuntimeMutation));
        rejected = new Snapshot(owner);
        HostCheck.Equal(-12L, owner.Call(10, 4349497344, 4194304, 3));
        HostCheck.Equal(0UL, guardCalls);
        rejected.AssertUnchanged(owner);
        HostCheck.True(LinuxMemoryServices.DetachMutationGuard(owner.Memory.State, &guardCalls, &AllowRuntimeMutation));
        HostCheck.Equal(1UL, guardCalls);
        HostCheck.True(LinuxMemoryServices.Audit(owner.Memory.State));

        // Replay the same exact successful prefix in the separately admitted
        // application pool; only its physical capacity differs.
        using Owner application = new(new LinuxMemoryServicesTests.Owner(tables: X64UserAddressSpace.MaximumTables,
            backingPages: LinuxMemoryServices.MaximumPoolPages, staticRows: 53, runtimeImage: true));
        application.Begin();
        foreach (var c in calls)
            HostCheck.Equal(c.Expected, application.Call(c.Nr, c.Address, c.Bytes, c.Arg2, c.Arg3,
                c.Nr == 9 ? ulong.MaxValue : 0));
        HostCheck.Equal(3076U, application.Memory.State->BackedPages);
        HostCheck.Equal(0L, application.Call(10, 4349497344, 4194304, 3));
        HostCheck.Equal(4100U, application.Memory.State->BackedPages);
        HostCheck.Equal(7UL, application.Memory.Leaf(4349497344) & 7);
        HostCheck.True(LinuxMemoryServices.Audit(application.Memory.State));
    }

    private static (uint Regions, uint MappingRows, uint FreePages, uint LargestFreeRun)
        RuntimeMemoryStatistics(LinuxMemoryServicesTests.Owner owner)
    {
        // Read a boxed snapshot of the private inventory for host diagnostics;
        // no production visibility or managed/native State layout is changed.
        const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object state = *owner.State;
        uint count = (uint)typeof(LinuxMemoryServices.State).GetField("Count", fields)!.GetValue(state)!;
        object regions = typeof(LinuxMemoryServices.State).GetField("Regions", fields)!.GetValue(state)!;
        HostCheck.True(count <= LinuxMemoryServices.MaximumRegions);
        var pinned = GCHandle.Alloc(regions, GCHandleType.Pinned);
        var used = new bool[owner.Memory.PoolPages];
        uint mapped = owner.Memory.StaticCount, free = 0, run = 0, largest = 0;
        try
        {
            ulong* words = (ulong*)pinned.AddrOfPinnedObject();
            for (uint i = 0; i < count; i++)
            {
                ulong bytes = words[i * 4 + 1], backing = words[i * 4 + 2];
                if ((words[i * 4 + 3] & 7) != 0) mapped++;
                if (backing == 0) continue;
                HostCheck.True(backing >= owner.Memory.PoolPhysical && bytes <= owner.Memory.PoolPages * 4096UL &&
                    backing - owner.Memory.PoolPhysical <= owner.Memory.PoolPages * 4096UL - bytes);
                uint first = checked((uint)((backing - owner.Memory.PoolPhysical) / 4096));
                for (uint page = first; page < first + bytes / 4096; page++)
                {
                    HostCheck.True(!used[page]); used[page] = true;
                }
            }
        }
        finally { pinned.Free(); }
        foreach (bool occupied in used)
        {
            if (occupied) run = 0;
            else { free++; run++; largest = Math.Max(largest, run); }
        }
        HostCheck.Equal(owner.State->BackedPages, owner.Memory.PoolPages - free);
        return (count, mapped, free, largest);
    }

    [UnmanagedCallersOnly]
    private static byte AllowRuntimeMutation(void* context, ulong address, ulong bytes)
    { (*(ulong*)context)++; return 1; }

    private const ulong Output = 0x100000000, Heap = 0x400000000;
    private struct Model
    {
        public LinuxMemoryServices.Admission Memory;
        public ulong Ticks;
        public uint Reads, Copies, Fences, PartialCopyBytes;
        public bool FailCopy, FailClock, Regress;
    }
    private sealed class Owner : IDisposable
    {
        public readonly LinuxMemoryServicesTests.Owner Memory;
        public readonly LinuxStartupServices.State* State;
        public readonly Model* Context;
        public readonly byte* Entropy;
        public readonly uint EntropyBytes;
        public Owner() : this(new LinuxMemoryServicesTests.Owner()) { }
        public Owner(uint entropyBytes) : this(new LinuxMemoryServicesTests.Owner(), entropyBytes) { }
        public Owner(LinuxMemoryServicesTests.Owner memory, uint entropyBytes = 32)
        {
            Memory = memory;
            Memory.Begin();
            HostCheck.Equal((long)Output, LinuxMemoryServices.Map(Memory.State, Output, 4096, 3, 0x100022, ulong.MaxValue, 0));
            State = (LinuxStartupServices.State*)NativeMemory.AlignedAlloc((nuint)sizeof(LinuxStartupServices.State), 16); *State = default;
            Context = (Model*)NativeMemory.AlignedAlloc((nuint)sizeof(Model), 16); *Context = new() { Memory = Memory.Memory, Ticks = 10000 };
            EntropyBytes = entropyBytes;
            Entropy = (byte*)NativeMemory.Alloc(entropyBytes); for (uint i = 0; i < entropyBytes; i++) Entropy[i] = (byte)(i % 251 + 1);
        }
        public LinuxStartupServices.Admission Admission() => new()
        {
            Memory = Memory.State, User = new() { Context = Context, PageAccess = &Permissions },
            CopyToUser = &Copy, ReadClock = &Clock, MemoryBarrier = &Fence, FrequencyHz = 100000000,
            PhysicalBytes = 256 * 1024 * 1024, AvailableBytes = 64 * 1024 * 1024,
            HeapStart = Heap, HeapBytes = 2 * 1024 * 1024, StackBytes = 65536,
            ProcessId = 1, Entropy = Entropy, CapturedEntropyBytes = EntropyBytes, SingleCpuValidated = true, FirmwareExited = true
        };
        public void Begin() => HostCheck.True(LinuxStartupServices.Begin(State, Admission()));
        public long Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0) =>
            LinuxStartupServices.Dispatch(State, new() { Number = number, Argument0 = a0, Argument1 = a1, Argument2 = a2,
                Argument3 = a3, Argument4 = a4, Argument5 = a5 });
        public void Dispose() { Memory.Dispose(); NativeMemory.AlignedFree(State); NativeMemory.AlignedFree(Context); NativeMemory.Free(Entropy); }
    }
    private sealed class Snapshot
    {
        private readonly byte[] tables, memory, startup, pool, staticRows;
        public Snapshot(Owner owner)
        {
            tables = owner.Memory.Tables();
            memory = new ReadOnlySpan<byte>(owner.Memory.State, sizeof(LinuxMemoryServices.State)).ToArray();
            startup = new ReadOnlySpan<byte>(owner.State, sizeof(LinuxStartupServices.State)).ToArray();
            pool = new ReadOnlySpan<byte>(owner.Memory.Memory.Pool, checked((int)owner.Memory.Memory.PoolPages * 4096)).ToArray();
            staticRows = new ReadOnlySpan<byte>(owner.Memory.Memory.StaticRanges,
                checked((int)owner.Memory.Memory.StaticCount * sizeof(X64UserMappingRange))).ToArray();
        }
        public void AssertUnchanged(Owner owner) => HostCheck.True(tables.AsSpan().SequenceEqual(owner.Memory.Tables()) &&
            memory.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.State, sizeof(LinuxMemoryServices.State))) &&
            startup.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.State, sizeof(LinuxStartupServices.State))) &&
            pool.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.Memory.Pool, pool.Length)) &&
            staticRows.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(owner.Memory.Memory.StaticRanges, staticRows.Length)));
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
        if (model->FailCopy && model->PartialCopyBytes == 0) return 0;
        if (model->FailCopy && bytes > model->PartialCopyBytes) bytes = model->PartialCopyBytes;
        for (uint i = 0; i < bytes; i++)
        {
            var m = model->Memory;
            if (!X64UserAddressSpace.TryReadLeaf(m.Tables, m.RootPhysical, m.TablePages, address + i, out ulong leaf) || (leaf & 7) != 7) return 0;
            ulong physical = (leaf & 0x000ffffffffff000) + ((address + i) & 4095);
            if (physical < m.PoolPhysical || physical >= m.PoolPhysical + m.PoolPages * 4096UL) return 0;
            m.Pool[physical - m.PoolPhysical] = source[i];
        }
        return model->FailCopy ? (byte)0 : (byte)1;
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

    [HostTest("linux.startup required realtime rejects missing unavailable and mismatched clocks before heap reservation")]
    private static void RealtimeAdmission()
    {
        using var owner = new Owner(); var admission = owner.Admission(); admission.RequireRealtime = true;
        var before = new Snapshot(owner);
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission)); before.AssertUnchanged(owner);
        OS.Kernel.Platform.OwnedRealtimeClock.State clock = default; admission.Realtime = &clock;
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission)); before.AssertUnchanged(owner);
        HostCheck.True(OS.Kernel.Platform.OwnedRealtimeClock.Begin(ref clock, 1800000000, 0, 10000, 1000000, 60));
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission)); before.AssertUnchanged(owner);
        clock = default;
        HostCheck.True(OS.Kernel.Platform.OwnedRealtimeClock.Begin(ref clock, 1800000000, 0, 10000, admission.FrequencyHz, 60));
        HostCheck.True(LinuxStartupServices.Begin(owner.State, admission));
    }

    [HostTest("linux.startup owned realtime uses x64 timespec fractional carry while preserving monotonic epoch")]
    private static void RealtimeTimespec()
    {
        using var owner = new Owner(); var admission = owner.Admission();
        OS.Kernel.Platform.OwnedRealtimeClock.State clock = default;
        HostCheck.True(OS.Kernel.Platform.OwnedRealtimeClock.Begin(ref clock, 1800000000, 999000000,
            10000, admission.FrequencyHz, 60));
        admission.Realtime = &clock; admission.RequireRealtime = true;
        HostCheck.True(LinuxStartupServices.Begin(owner.State, admission));
        HostCheck.Equal(0L, owner.Call(228, 0, Output));
        byte* time = owner.Memory.At(Output);
        HostCheck.Equal(1800000001L, *(long*)time); HostCheck.Equal(1000000UL, *(ulong*)(time + 8));
        HostCheck.Equal(0L, owner.Call(228, 1, Output));
        HostCheck.Equal(0UL, *(ulong*)time); HostCheck.Equal(3100000UL, *(ulong*)(time + 8));
        // This PAL build measures clock_gettime; an unmeasured gettimeofday ABI stays unsupported.
        HostCheck.Equal(-38L, owner.Call(96, Output));
    }

    [HostTest("linux.startup realtime checks user faults copy failures and finite expiry")]
    private static void RealtimeOutputFailures()
    {
        using var owner = new Owner(); var admission = owner.Admission();
        OS.Kernel.Platform.OwnedRealtimeClock.State clock = default;
        HostCheck.True(OS.Kernel.Platform.OwnedRealtimeClock.Begin(ref clock, 1800000000, 0,
            10000, admission.FrequencyHz, 1));
        admission.Realtime = &clock; admission.RequireRealtime = true;
        HostCheck.True(LinuxStartupServices.Begin(owner.State, admission));
        new Span<byte>(owner.Memory.At(Output), 16).Fill(0xa5);
        ulong ticks = owner.Context->Ticks;
        HostCheck.Equal(-14L, owner.Call(228, 0, Output + 4090)); HostCheck.Equal(ticks, owner.Context->Ticks);
        owner.Context->FailCopy = true; HostCheck.Equal(-14L, owner.Call(228, 0, Output));
        owner.Context->FailCopy = false; owner.Context->Ticks = 10000 + admission.FrequencyHz;
        HostCheck.Equal(-5L, owner.Call(228, 0, Output)); HostCheck.True(!clock.IsAvailable);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Output), 16).IndexOfAnyExcept((byte)0xa5) == -1);
    }

    [HostTest("linux.startup coarse monotonic clock uses live epoch with millisecond resolution and checked output")]
    private static void CoarseClock()
    {
        using var owner = new Owner(); owner.Begin();
        owner.Context->Ticks = 123456789;
        HostCheck.Equal(0L, owner.Call(228, 6, Output));
        byte* time = owner.Memory.At(Output);
        HostCheck.Equal(1UL, *(ulong*)time); HostCheck.Equal(235000000UL, *(ulong*)(time + 8));
        HostCheck.Equal(0L, owner.Call(228, 1, Output));
        HostCheck.Equal(1UL, *(ulong*)time); HostCheck.Equal(236567890UL, *(ulong*)(time + 8));
        owner.Context->Ticks = 199999999;
        HostCheck.Equal(0L, owner.Call(228, 6, Output));
        HostCheck.Equal(2UL, *(ulong*)time); HostCheck.Equal(0UL, *(ulong*)(time + 8));
        byte[] before = new ReadOnlySpan<byte>(time, 16).ToArray();
        foreach (ulong id in new ulong[] { 0, 5, 7, ulong.MaxValue })
            HostCheck.Equal(-22L, owner.Call(228, id, Output));
        ulong ticks = owner.Context->Ticks;
        HostCheck.Equal(-14L, owner.Call(228, 6, Output + 4090)); HostCheck.Equal(ticks, owner.Context->Ticks);
        owner.Context->Regress = true; HostCheck.Equal(-5L, owner.Call(228, 6, Output));
        owner.Context->Regress = false; owner.Context->FailClock = true; HostCheck.Equal(-5L, owner.Call(228, 6, Output));
        owner.Context->FailClock = false; owner.Context->FailCopy = true; HostCheck.Equal(-14L, owner.Call(228, 6, Output));
        HostCheck.True(before.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(time, 16)));
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

    private static LinuxStartupServices.Admission ServiceAdmission(Owner owner, HmacSha256Drbg.State* generator)
    {
        var admission = owner.Admission();
        admission.Entropy = null; admission.CapturedEntropyBytes = 0;
        admission.Generator = generator; admission.ContinuingRandom = true; admission.User.UserInterrupts = true;
        return admission;
    }

    private static void SeedService(HmacSha256Drbg.State* generator)
    {
        // Deterministic host-test input only; the native owner must acquire
        // fresh independent firmware entropy before admitting this capability.
        byte* seed = stackalloc byte[48]; for (uint i = 0; i < 48; i++) seed[i] = (byte)(i + 1);
        HostCheck.True(HmacSha256Drbg.Instantiate(generator, seed, seed + 32, null, 0));
    }

    // CLR field access, not marshalled offsets: Admission contains bool fields
    // whose marshalled layout differs from the real unsafe owner layout.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "GeneratedRequests")]
    private static extern ref ulong ServiceRequestCounter(ref LinuxStartupServices.State state);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "GeneratedBytes")]
    private static extern ref ulong ServiceByteCounter(ref LinuxStartupServices.State state);

    [HostTest("continuing getrandom returns bounded prefixes beyond the finite pool with separate generator ownership")]
    private static void ContinuingRandom()
    {
        using var owner = new Owner(); HmacSha256Drbg.State generator = default, oracle = default;
        SeedService(&generator); SeedService(&oracle);
        HostCheck.Equal((long)(Output + 4096), LinuxMemoryServices.Map(owner.Memory.State, Output + 4096, 4096, 3, 0x100022, ulong.MaxValue, 0));
        HostCheck.True(LinuxStartupServices.Begin(owner.State, ServiceAdmission(owner, &generator)));
        HostCheck.Equal(0L, owner.Call(318, ulong.MaxValue, 0));
        HostCheck.Equal(-22L, owner.Call(318, Output, 1, 4));
        HostCheck.Equal(8192L, owner.Call(318, Output, 9000));
        byte* expected = stackalloc byte[8192]; HostCheck.True(HmacSha256Drbg.Generate(&oracle, expected, 8192));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Output), 4096).SequenceEqual(new ReadOnlySpan<byte>(expected, 4096)));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Output + 4096), 4096).SequenceEqual(new ReadOnlySpan<byte>(expected + 4096, 4096)));
        HostCheck.Equal(256L, owner.Call(318, Output, 256));
        HostCheck.True(HmacSha256Drbg.Generate(&oracle, expected, 256));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Output), 256).SequenceEqual(new ReadOnlySpan<byte>(expected, 256)));
        for (uint i = 0; i < 64; i++) HostCheck.Equal(128L, owner.Call(318, Output, 128));
        foreach (ulong flags in new ulong[] { 1, 2, 3 }) HostCheck.Equal(64L, owner.Call(318, Output, 64, flags));
        HostCheck.Equal(69UL, owner.State->RandomRequests); HostCheck.Equal(16832UL, owner.State->RandomBytes);
        HostCheck.Equal(69UL, generator.RequestsSinceReseed); HostCheck.Equal(0U, owner.State->RemainingEntropy);
        for (uint i = 0; i < owner.EntropyBytes; i++) HostCheck.Equal((byte)(i + 1), owner.Entropy[i]);
        long allocations = GC.GetAllocatedBytesForCurrentThread(); long result = owner.Call(318, Output, 64);
        HostCheck.Equal(allocations, GC.GetAllocatedBytesForCurrentThread()); HostCheck.Equal(64L, result);
        HostCheck.True(LinuxStartupServices.DiscardEntropy(owner.State));
        HostCheck.True(new ReadOnlySpan<byte>(&generator, sizeof(HmacSha256Drbg.State)).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal(-11L, owner.Call(318, Output, 8, 1)); HostCheck.Equal(-38L, owner.Call(318, Output, 8));
        HostCheck.True(LinuxStartupServices.DiscardEntropy(owner.State));
        HostCheck.Equal(1L, owner.Call(39)); HostCheck.True(HmacSha256Drbg.Erase(&oracle));
    }

    [HostTest("continuing entropy admission rejects finite mixing missing preemption aliases and copied generators unchanged")]
    private static void ContinuingRandomAdmission()
    {
        using var owner = new Owner(); HmacSha256Drbg.State generator = default;
        var before = new Snapshot(owner);
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, ServiceAdmission(owner, &generator)));
        SeedService(&generator);
        var finite = owner.Admission(); finite.Generator = &generator;
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, finite));
        var admission = ServiceAdmission(owner, &generator); admission.User.UserInterrupts = false;
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission));
        admission = ServiceAdmission(owner, &generator); admission.Entropy = owner.Entropy;
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission));
        admission = ServiceAdmission(owner, &generator); admission.CapturedEntropyBytes = 32;
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission));
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, ServiceAdmission(owner, null)));
        foreach (ulong source in new ulong[] { (ulong)owner.State, (ulong)owner.Memory.State, (ulong)owner.Memory.Memory.Pool,
            (ulong)owner.Memory.Memory.Tables, (ulong)owner.Memory.Memory.CandidateTables, (ulong)owner.Memory.Memory.StaticRanges,
            (ulong)&generator + 1, ulong.MaxValue - 7 })
            HostCheck.True(!LinuxStartupServices.Begin(owner.State, ServiceAdmission(owner, (HmacSha256Drbg.State*)source)));
        var copy = generator;
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, ServiceAdmission(owner, &copy)));
        before.AssertUnchanged(owner); HostCheck.Equal(0U, owner.Context->Reads);
        HostCheck.True(HmacSha256Drbg.Erase(&generator));
    }

    [HostTest("continuing getrandom preflight consumes nothing and a failed partial copy burns the complete generated request")]
    private static void ContinuingRandomCopy()
    {
        using var owner = new Owner(); HmacSha256Drbg.State generator = default, oracle = default;
        SeedService(&generator); SeedService(&oracle);
        HostCheck.True(LinuxStartupServices.Begin(owner.State, ServiceAdmission(owner, &generator)));
        HostCheck.Equal(-14L, owner.Call(318, Output, 8192)); // Second page is not mapped.
        HostCheck.Equal(-14L, owner.Call(318, ulong.MaxValue, 32));
        HostCheck.Equal(0UL, generator.RequestsSinceReseed); HostCheck.Equal(0U, owner.Context->Copies);
        owner.Context->FailCopy = true; owner.Context->PartialCopyBytes = 7;
        HostCheck.Equal(-14L, owner.Call(318, Output, 32));
        byte* expected = stackalloc byte[32]; HostCheck.True(HmacSha256Drbg.Generate(&oracle, expected, 32));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Output), 7).SequenceEqual(new ReadOnlySpan<byte>(expected, 7)));
        HostCheck.Equal(1UL, owner.State->RandomRequests); HostCheck.Equal(32UL, owner.State->RandomBytes);
        owner.Context->FailCopy = false;
        HostCheck.Equal(32L, owner.Call(318, Output, 32)); HostCheck.True(HmacSha256Drbg.Generate(&oracle, expected, 32));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Output), 32).SequenceEqual(new ReadOnlySpan<byte>(expected, 32)));
        HostCheck.Equal(2UL, generator.RequestsSinceReseed);
        HostCheck.True(LinuxStartupServices.DiscardEntropy(owner.State) && HmacSha256Drbg.Erase(&oracle));
    }

    [HostTest("continuing getrandom source exhaustion reseed failure and lifetime overflow never substitute output")]
    private static void ContinuingRandomUnavailable()
    {
        using var owner = new Owner(); HmacSha256Drbg.State generator = default; SeedService(&generator);
        HostCheck.True(LinuxStartupServices.Begin(owner.State, ServiceAdmission(owner, &generator)));
        ulong* counter = (ulong*)((byte*)&generator + Marshal.OffsetOf<HmacSha256Drbg.State>("Counter").ToInt32());
        *counter = HmacSha256Drbg.ReseedInterval + 1;
        HostCheck.Equal(-11L, owner.Call(318, Output, 16, 1)); HostCheck.Equal(-38L, owner.Call(318, Output, 16));
        HostCheck.True(!HmacSha256Drbg.Reseed(&generator, null));
        HostCheck.Equal(-11L, owner.Call(318, Output, 16, 3)); HostCheck.Equal(0U, owner.Context->Copies);
        byte* entropy = stackalloc byte[32]; new Span<byte>(entropy, 32).Fill(0x73);
        HostCheck.True(HmacSha256Drbg.Reseed(&generator, entropy)); HostCheck.Equal(16L, owner.Call(318, Output, 16));
        ref ulong requests = ref ServiceRequestCounter(ref *owner.State);
        requests = ulong.MaxValue; HostCheck.Equal(-11L, owner.Call(318, Output, 8, 1)); requests = 1;
        ref ulong bytes = ref ServiceByteCounter(ref *owner.State);
        bytes = ulong.MaxValue - 7; HostCheck.Equal(-38L, owner.Call(318, Output, 8)); bytes = 16;
        HostCheck.Equal(1UL, generator.RequestsSinceReseed); HostCheck.Equal(1U, owner.Context->Copies);
        HostCheck.True(LinuxStartupServices.DiscardEntropy(owner.State));
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

    [HostTest("linux.startup runtime entropy serves observed 136 and 200 byte lifetimes without reuse")]
    private static void RuntimeEntropy()
    {
        foreach (uint total in new uint[] { 136, 200, 256 })
        {
            using var owner = new Owner(256); owner.Begin();
            uint used = 0;
            while (used < total)
            {
                uint count = Math.Min(8U, total - used);
                long allocations = GC.GetAllocatedBytesForCurrentThread();
                long result = owner.Call(318, Output + used, count);
                allocations = GC.GetAllocatedBytesForCurrentThread() - allocations;
                HostCheck.Equal((long)count, result); HostCheck.Equal(0L, allocations);
                used += count;
                HostCheck.Equal(256U - used, owner.State->RemainingEntropy);
            }
            for (uint i = 0; i < total; i++) HostCheck.Equal((byte)(i % 251 + 1), owner.Memory.At(Output)[i]);
            HostCheck.True(new ReadOnlySpan<byte>(owner.Entropy, (int)total).IndexOfAnyExcept((byte)0) == -1);
            for (uint i = total; i < 256; i++) HostCheck.Equal((byte)(i % 251 + 1), owner.Entropy[i]);
            HostCheck.True(!LinuxStartupServices.Begin(owner.State, owner.Admission()));
        }
    }

    [HostTest("linux.startup runtime entropy rejects bad ranges and burns partial failed copies before exhaustion")]
    private static void RuntimeEntropyFailures()
    {
        using var owner = new Owner(256); owner.Begin();
        HostCheck.Equal(-14L, owner.Call(318, Output + 4090, 16));
        HostCheck.Equal(-22L, owner.Call(318, Output, 33));
        HostCheck.Equal(-22L, owner.Call(318, Output, 8, 4));
        HostCheck.Equal(0L, owner.Call(318, 0, 0));
        HostCheck.Equal(256U, owner.State->RemainingEntropy); HostCheck.Equal(0U, owner.Context->Copies);
        for (int i = 0; i < 7; i++) HostCheck.Equal(32L, owner.Call(318, Output + (uint)i * 32, 32));
        owner.Context->FailCopy = true;
        owner.Context->PartialCopyBytes = 7;
        HostCheck.Equal(-14L, owner.Call(318, Output + 224, 24));
        for (uint i = 224; i < 231; i++) HostCheck.Equal((byte)(i % 251 + 1), owner.Memory.At(Output)[i]);
        HostCheck.Equal((byte)0, owner.Memory.At(Output)[231]);
        HostCheck.Equal(8U, owner.State->RemainingEntropy);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Entropy, 248).IndexOfAnyExcept((byte)0) == -1);
        owner.Context->FailCopy = false;
        uint copies = owner.Context->Copies;
        HostCheck.Equal(-38L, owner.Call(318, Output + 248, 16));
        HostCheck.Equal(copies, owner.Context->Copies); HostCheck.Equal(8U, owner.State->RemainingEntropy);
        HostCheck.Equal(8L, owner.Call(318, Output + 248, 16, 1));
        for (uint i = 248; i < 256; i++) HostCheck.Equal((byte)(i % 251 + 1), owner.Memory.At(Output)[i]);
        HostCheck.Equal(0U, owner.State->RemainingEntropy);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Entropy, 256).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal(-11L, owner.Call(318, Output, 1, 1)); HostCheck.Equal(-38L, owner.Call(318, Output, 1));
    }

    [HostTest("linux.startup explicit web entropy serves its complete finite lifetime without reuse")]
    private static void WebEntropy()
    {
        HostCheck.Equal(32U, LinuxStartupServices.EntropyBytes);
        HostCheck.Equal(256U, LinuxStartupServices.RuntimeEntropyBytes);
        HostCheck.Equal(4096U, LinuxStartupServices.WebEntropyBytes);
        using var owner = new Owner(LinuxStartupServices.WebEntropyBytes); owner.Begin();
        uint used = 0;
        while (used < 4096)
        {
            // Include a 16-byte request after 244 bytes have already been used.
            uint count = used == 224 ? 20U : used == 244 ? 16U : Math.Min(32U, 4096 - used);
            long allocations = GC.GetAllocatedBytesForCurrentThread();
            long result = owner.Call(318, Output + used, count);
            allocations = GC.GetAllocatedBytesForCurrentThread() - allocations;
            HostCheck.Equal((long)count, result); HostCheck.Equal(0L, allocations);
            used += count; HostCheck.Equal(4096U - used, owner.State->RemainingEntropy);
        }
        for (uint i = 0; i < 4096; i++) HostCheck.Equal((byte)(i % 251 + 1), owner.Memory.At(Output)[i]);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Entropy, 4096).IndexOfAnyExcept((byte)0) == -1);
        uint copies = owner.Context->Copies;
        HostCheck.Equal(-38L, owner.Call(318, Output, 16)); HostCheck.Equal(-11L, owner.Call(318, Output, 16, 1));
        HostCheck.Equal(copies, owner.Context->Copies);
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, owner.Admission()));
        HostCheck.True(LinuxStartupServices.DiscardEntropy(owner.State));
        HostCheck.Equal(0U, owner.State->RemainingEntropy);
    }

    [HostTest("linux.startup web entropy keeps request bounds burns attempted copies and discards without refill")]
    private static void WebEntropyFailures()
    {
        using var owner = new Owner(LinuxStartupServices.WebEntropyBytes); owner.Begin();
        HostCheck.Equal(-14L, owner.Call(318, Output + 4090, 16));
        HostCheck.Equal(-22L, owner.Call(318, Output, 33)); HostCheck.Equal(-22L, owner.Call(318, Output, 8, 4));
        HostCheck.Equal(0L, owner.Call(318, 0, 0));
        HostCheck.Equal(4096U, owner.State->RemainingEntropy); HostCheck.Equal(0U, owner.Context->Copies);
        new Span<byte>(owner.Memory.At(Output), 32).Fill(0xa5);
        owner.Context->FailCopy = true; owner.Context->PartialCopyBytes = 7;
        HostCheck.Equal(-14L, owner.Call(318, Output, 32));
        HostCheck.Equal(4064U, owner.State->RemainingEntropy);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Entropy, 32).IndexOfAnyExcept((byte)0) == -1);
        for (int i = 0; i < 7; i++) HostCheck.Equal((byte)(i + 1), owner.Memory.At(Output)[i]);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Output) + 7, 25).IndexOfAnyExcept((byte)0xa5) == -1);
        owner.Context->FailCopy = false;
        HostCheck.Equal(16L, owner.Call(318, Output + 32, 16));
        for (int i = 0; i < 16; i++) HostCheck.Equal((byte)(i + 33), owner.Memory.At(Output + 32)[i]);
        HostCheck.Equal(4048U, owner.State->RemainingEntropy);
        HostCheck.True(LinuxStartupServices.DiscardEntropy(owner.State));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Entropy, 4096).IndexOfAnyExcept((byte)0) == -1);
        HostCheck.Equal(0U, owner.State->RemainingEntropy);
        HostCheck.Equal(-38L, owner.Call(318, Output, 8)); HostCheck.Equal(-11L, owner.Call(318, Output, 8, 1));
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, owner.Admission()));
    }

    [HostTest("linux.startup explicit web entropy admits only its full exclusive captured extent")]
    private static void WebEntropyAdmission()
    {
        using var owner = new Owner(LinuxStartupServices.WebEntropyBytes); var before = new Snapshot(owner);
        foreach (uint count in new uint[] { 512, 4095, 4097, 8192, uint.MaxValue })
        {
            var admission = owner.Admission(); admission.CapturedEntropyBytes = count;
            HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission)); before.AssertUnchanged(owner);
        }
        ulong[] protectedStarts = [(ulong)owner.State, (ulong)owner.Memory.State, (ulong)owner.Memory.Memory.Pool,
            (ulong)owner.Memory.Memory.Tables, (ulong)owner.Memory.Memory.CandidateTables, (ulong)owner.Memory.Memory.StaticRanges];
        foreach (ulong start in protectedStarts)
        {
            var admission = owner.Admission(); admission.Entropy = (byte*)(start - 2048);
            HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission)); before.AssertUnchanged(owner);
        }
        var overflow = owner.Admission(); overflow.Entropy = (byte*)(ulong.MaxValue - 2047);
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, overflow)); before.AssertUnchanged(owner);
        new Span<byte>(owner.Entropy, 4096).Clear();
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, owner.Admission())); before.AssertUnchanged(owner);
        HostCheck.Equal(0U, owner.Context->Reads);
        owner.Entropy[4095] = 0x71; owner.Begin(); HostCheck.Equal(4096U, owner.State->RemainingEntropy);
    }

    [HostTest("linux.startup full runtime entropy extent rejects suffix aliases and overflow before any read or reservation")]
    private static void RuntimeEntropyAdmission()
    {
        using var owner = new Owner(256); var before = new Snapshot(owner);
        foreach (uint count in new uint[] { 0, 31, 33, 255, 257, uint.MaxValue })
        {
            var admission = owner.Admission(); admission.CapturedEntropyBytes = count;
            HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission)); before.AssertUnchanged(owner);
        }
        // The first 32 bytes are outside each protected range; the later bytes
        // overlap it. Reject numerically before dereferencing the candidate.
        ulong[] protectedStarts = [(ulong)owner.State, (ulong)owner.Memory.State, (ulong)owner.Memory.Memory.Pool,
            (ulong)owner.Memory.Memory.Tables, (ulong)owner.Memory.Memory.CandidateTables, (ulong)owner.Memory.Memory.StaticRanges];
        foreach (ulong start in protectedStarts)
        {
            var admission = owner.Admission(); admission.Entropy = (byte*)(start - 128);
            HostCheck.True(!LinuxStartupServices.Begin(owner.State, admission)); before.AssertUnchanged(owner);
        }
        var overflow = owner.Admission(); overflow.Entropy = (byte*)(ulong.MaxValue - 127);
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, overflow)); before.AssertUnchanged(owner);
        HostCheck.Equal(0U, owner.Context->Reads);
        new Span<byte>(owner.Entropy, 256).Clear();
        HostCheck.True(!LinuxStartupServices.Begin(owner.State, owner.Admission())); before.AssertUnchanged(owner);
        owner.Entropy[255] = 0x71; owner.Begin(); HostCheck.Equal(256U, owner.State->RemainingEntropy);
    }

    [HostTest("linux.startup entropy discard scrubs the full lifetime and cannot replenish it")]
    private static void EntropyDiscard()
    {
        foreach (uint bytes in new uint[] { 32, 256 })
        {
            using var owner = new Owner(bytes);
            HostCheck.True(!LinuxStartupServices.DiscardEntropy(owner.State)); owner.Begin();
            HostCheck.Equal(8L, owner.Call(318, Output, 8));
            HostCheck.True(LinuxStartupServices.DiscardEntropy(owner.State));
            HostCheck.Equal(0U, owner.State->RemainingEntropy);
            HostCheck.True(new ReadOnlySpan<byte>(owner.Entropy, (int)bytes).IndexOfAnyExcept((byte)0) == -1);
            HostCheck.Equal(-11L, owner.Call(318, Output, 8, 1));
            HostCheck.True(LinuxStartupServices.DiscardEntropy(owner.State));
            HostCheck.True(!LinuxStartupServices.Begin(owner.State, owner.Admission()));
        }
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

    [HostTest("linux.startup fragmented physical heap growth preserves independent maps and zeroes released brk pages")]
    private static void FragmentedHeap()
    {
        using var owner = new Owner(new LinuxMemoryServicesTests.Owner(backingPages: 17)); owner.Begin();
        for (uint block = 0; block < 4; block++)
        {
            ulong address = Output + 0x10000 + block * 0x10000;
            HostCheck.Equal((long)address, owner.Call(9, address, 4 * 4096, 3, 0x100022, ulong.MaxValue));
            for (uint page = 0; page < 4; page++) *owner.Memory.At(address + page * 4096) = (byte)(block * 4 + page + 1);
        }
        HostCheck.Equal(0L, owner.Call(11, Output + 0x10000, 4 * 4096));
        HostCheck.Equal(0L, owner.Call(11, Output + 0x30000, 4 * 4096));
        HostCheck.Equal((long)Heap + 6 * 4096, owner.Call(12, Heap + 6 * 4096));
        for (uint page = 0; page < 6; page++)
        {
            HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Heap + page * 4096), 4096).IndexOfAnyExcept((byte)0) == -1);
            *owner.Memory.At(Heap + page * 4096) = (byte)(100 + page);
        }
        BrkRejected(owner, Heap + 9 * 4096);
        HostCheck.Equal((long)Heap + 2 * 4096, owner.Call(12, Heap + 2 * 4096));
        HostCheck.Equal((long)Heap + 6 * 4096, owner.Call(12, Heap + 6 * 4096));
        HostCheck.Equal((byte)100, *owner.Memory.At(Heap)); HostCheck.Equal((byte)101, *owner.Memory.At(Heap + 4096));
        for (uint page = 2; page < 6; page++)
            HostCheck.True(new ReadOnlySpan<byte>(owner.Memory.At(Heap + page * 4096), 4096).IndexOfAnyExcept((byte)0) == -1);
        for (uint block = 1; block < 4; block += 2)
            for (uint page = 0; page < 4; page++)
                HostCheck.Equal((byte)(block * 4 + page + 1), *owner.Memory.At(Output + 0x10000 + block * 0x10000 + page * 4096));
        HostCheck.Equal((long)Heap, owner.Call(12, Heap)); HostCheck.True(LinuxMemoryServices.Audit(owner.Memory.State));
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
