using OS.Kernel.Threading;
using OS.Pal;
using M = KernelThreadTableTests.Model;
using S = OS.Kernel.Threading.ThreadLocalStorage.Status;
using R = OS.Kernel.Threading.ThreadLocalStorage.Region;

internal static unsafe class ThreadLocalStorageTests
{
    private const ulong Key = 0x746c736f776e6572;
    private static ThreadLocalStorage* Owner(M m) => (ThreadLocalStorage*)(m.Memory + 0xa000);
    private static ThreadLocalStorage.Slot* Slots(M m) => (ThreadLocalStorage.Slot*)(m.Memory + 0xb000);
    private static ulong Pool(M m) => (ulong)m.Memory + 0x29000;
    private static R Fs(M m, int i = 0) => new(Pool(m) + (uint)i * 0x400, 0x100, Pool(m) + (uint)i * 0x400 + 0x40);
    private static R Gs(M m, int i = 0) => new(Pool(m) + (uint)i * 0x400 + 0x200, 0x100, Pool(m) + (uint)i * 0x400 + 0x240);
    private static S Initialize(M m, uint capacity = 3, uint generation = 1, ulong key = Key) =>
        Owner(m)->Initialize(key, m.Owner, m.Table, KernelThreadTableTests.Key, Slots(m),
            capacity * (uint)sizeof(ThreadLocalStorage.Slot), capacity, Pool(m), 0x2000, generation);
    private static void Setup(M m, uint capacity = 3, uint generation = 1)
    {
        *Owner(m) = default;
        m.Owner.KernelGs = m.Owner.CurrentKernelGs = (ulong)m.Memory + 0x2f000;
        HostCheck.Equal(KernelThreadTable.Status.Ok, m.Bind());
        HostCheck.Equal(S.Ok, Initialize(m, capacity, generation));
    }
    private static OSHostHandle Create(M m, int i, R fs, R gs = default)
    {
        m.Context(i)->FsBase = fs.Base;
        m.Context(i)->UserGsBase = gs.Base;
        for (int j = 160; j < 416; j++)
            m.Context(i)->FxSave[j] = (byte)(0x20 + i);
        var created = m.Create(i);
        HostCheck.True(created.Succeeded);
        return created.Identity;
    }
    private static ThreadLocalStorage.Binding Bind(M m, int i, bool gs = true)
    {
        R f = Fs(m, i), g = gs ? Gs(m, i) : default;
        var thread = Create(m, i, f, g);
        var bound = Owner(m)->Bind(Key, m.Owner, thread, f, g);
        HostCheck.True(bound.Succeeded);
        return bound.Identity;
    }
    private static void Same(M m, byte[] before) => m.Same(before);
    private static bool Zero(R r)
    {
        for (ulong i = 0; i < r.Bytes; i++)
            if (((byte*)r.Start)[i] != 0)
                return false;
        return true;
    }

    [HostTest("thread.tls initialization validates private pool metadata owner and storage atomically")]
    private static void Admission()
    {
        using M m = new(initialize: false);
        *Owner(m) = default;
        HostCheck.Equal(KernelThreadTable.Status.Ok, m.Bind());
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.InvalidCapacity, Initialize(m, 0));
        Same(m, before);
        HostCheck.Equal(S.InvalidCapacity, Initialize(m, generation: 0));
        Same(m, before);
        HostCheck.Equal(S.InvalidOwner, Initialize(m, key: 0));
        Same(m, before);
        var owner = m.Owner;
        owner.Flags |= 0x200;
        HostCheck.Equal(S.InterruptsEnabled, Owner(m)->Initialize(Key, owner, m.Table, KernelThreadTableTests.Key, Slots(m), 1024, 2, Pool(m), 8192));
        Same(m, before);
        HostCheck.Equal(S.InvalidRegion, Owner(m)->Initialize(Key, m.Owner, m.Table, KernelThreadTableTests.Key, Slots(m), 1024, 2, Pool(m) + 1, 8192));
        Same(m, before);
        owner = m.Owner;
        owner.KernelGs = owner.CurrentKernelGs = Pool(m);
        HostCheck.Equal(S.Overlap, Owner(m)->Initialize(Key, owner, m.Table, KernelThreadTableTests.Key, Slots(m), 1024, 2, Pool(m), 8192));
        Same(m, before);
        HostCheck.Equal(S.Overlap, Owner(m)->Initialize(Key, m.Owner, m.Table, KernelThreadTableTests.Key, (ThreadLocalStorage.Slot*)Owner(m), 1024, 2, Pool(m), 8192));
        Same(m, before);
        HostCheck.Equal(S.Ok, Initialize(m));
        before = m.Snapshot();
        HostCheck.Equal(S.AlreadyInitialized, Initialize(m));
        Same(m, before);
        ThreadLocalStorage copy = *Owner(m);
        HostCheck.Equal(S.CopiedOwner, copy.Release(Key, m.Owner));
        Same(m, before);
        HostCheck.True(!default(ThreadLocalStorage.Binding).IsValid && !default(ThreadLocalStorage.Result).Succeeded);
    }

    [HostTest("thread.tls compact exclusive subpool binds four spans without borrowing adjacent bytes")]
    private static void CompactPool()
    {
        using M m = new(initialize: false);
        *Owner(m) = default;
        HostCheck.Equal(KernelThreadTable.Status.Ok, m.Bind());
        ulong start = Pool(m) + 0xf00;
        HostCheck.Equal(S.Ok, Owner(m)->Initialize(Key, m.Owner, m.Table, KernelThreadTableTests.Key, Slots(m),
            2 * (uint)sizeof(ThreadLocalStorage.Slot), 2, start, 256));
        R af = new(start, 64, start), ag = new(start + 64, 64, start + 64),
            bf = new(start + 128, 64, start + 128), bg = new(start + 192, 64, start + 192);
        var a = Create(m, 0, af, ag);
        var b = Create(m, 1, bf, bg);
        var ab = Owner(m)->Bind(Key, m.Owner, a, af, ag);
        var bb = Owner(m)->Bind(Key, m.Owner, b, bf, bg);
        HostCheck.True(ab.Succeeded && bb.Succeeded);
        m.Dispatch();
        m.Exit(a);
        var reaped = Owner(m)->Reap(Key, m.Owner, ab.Identity, m.SupervisorRsp, 0, 0);
        HostCheck.True(reaped.Succeeded && Zero(af) && Zero(ag));
        HostCheck.Equal(128UL, reaped.ReapedBytes);
        HostCheck.True(!Zero(bf) && !Zero(bg));
        HostCheck.Equal((byte)0xa5, ((byte*)start)[-1]);
        HostCheck.Equal((byte)0xa5, ((byte*)start)[256]);
        HostCheck.True(Owner(m)->Inspect(Key, m.Owner, bb.Identity).Succeeded);
    }

    [HostTest("thread.tls distinct FS GS and FP snapshots are admitted without changing bytes")]
    private static void Distinct()
    {
        using M m = new(initialize: false);
        Setup(m);
        var a = Bind(m, 0);
        var b = Bind(m, 1);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.Ok, Owner(m)->Admit(Key, m.Owner, a, b));
        Same(m, before);
        var first = Owner(m)->Inspect(Key, m.Owner, a);
        var second = Owner(m)->Inspect(Key, m.Owner, b);
        HostCheck.Equal(Fs(m).Base, first.Descriptor.Context->FsBase);
        HostCheck.Equal(Gs(m).Base, first.Descriptor.Context->UserGsBase);
        HostCheck.Equal(Fs(m, 1).Base, second.Descriptor.Context->FsBase);
        HostCheck.Equal(Gs(m, 1).Base, second.Descriptor.Context->UserGsBase);
        HostCheck.Equal((byte)0x20, first.Descriptor.Context->FxSave[160]);
        HostCheck.Equal((byte)0x21, second.Descriptor.Context->FxSave[160]);
        HostCheck.Equal(S.Busy, Owner(m)->Release(Key, m.Owner));
        Same(m, before);
    }

    [HostTest("thread.tls bases outside private pool and malformed regions reject unchanged")]
    private static void Regions()
    {
        using M m = new(initialize: false);
        Setup(m);
        var thread = Create(m, 0, Fs(m));
        byte[] before = m.Snapshot();
        R[] invalid = [new(Pool(m) + 1, 256, Fs(m).Base), new(Pool(m), 7, Pool(m)),
            new(Pool(m), 256, Pool(m) + 1), new(Pool(m), ulong.MaxValue, Fs(m).Base),
            new((ulong)m.Supervisor.Context, 256, (ulong)m.Supervisor.Context), new(0, 256, 0),
            new(0x800000000000, 256, 0x800000000000), new(Pool(m), 256, Pool(m) + 256)];
        foreach (R region in invalid)
        {
            HostCheck.Equal(S.InvalidRegion, Owner(m)->Bind(Key, m.Owner, thread, region).Code);
            Same(m, before);
        }
        HostCheck.Equal(S.InvalidBase, Owner(m)->Bind(Key, m.Owner, thread, new(Fs(m).Start, Fs(m).Bytes, Fs(m).Base + 8)).Code);
        Same(m, before);
        HostCheck.Equal(S.ThreadFailure, Owner(m)->Bind(Key, m.Owner, default, Fs(m)).Code);
        Same(m, before);
    }

    [HostTest("thread.tls duplicate full and cross segment aliases cannot borrow another backing")]
    private static void Aliases()
    {
        using M m = new(initialize: false);
        Setup(m, 1);
        var a = Bind(m, 0);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.AlreadyBound, Owner(m)->Bind(Key, m.Owner, a.Thread, Fs(m), Gs(m)).Code);
        Same(m, before);
        var b = Create(m, 1, Fs(m), Gs(m, 1));
        before = m.Snapshot();
        HostCheck.Equal(S.Overlap, Owner(m)->Bind(Key, m.Owner, b, Fs(m), Gs(m, 1)).Code);
        Same(m, before);
        m.Context(1)->FsBase = Fs(m, 1).Base;
        before = m.Snapshot();
        HostCheck.Equal(S.Full, Owner(m)->Bind(Key, m.Owner, b, Fs(m, 1), Gs(m, 1)).Code);
        Same(m, before);
        using M n = new(initialize: false);
        Setup(n);
        R overlap = Fs(n);
        var c = Create(n, 0, overlap, overlap);
        before = n.Snapshot();
        HostCheck.Equal(S.Overlap, Owner(n)->Bind(Key, n.Owner, c, overlap, overlap).Code);
        Same(n, before);
    }

    [HostTest("thread.tls live CPU kernel GS root selector mapping and owner mismatches reject")]
    private static void OwnerState()
    {
        using M m = new(initialize: false);
        Setup(m);
        var a = Bind(m, 0);
        byte[] before = m.Snapshot();
        var owner = m.Owner;
        owner.CurrentKernelGs ^= 8;
        HostCheck.Equal(S.InvalidOwner, Owner(m)->Inspect(Key, owner, a).Code);
        Same(m, before);
        owner = m.Owner;
        owner.KernelGs = owner.CurrentKernelGs = 0;
        HostCheck.Equal(S.InvalidOwner, Owner(m)->Inspect(Key, owner, a).Code);
        Same(m, before);
        owner = m.Owner;
        owner.Root = owner.CurrentRoot = 0x2000;
        HostCheck.Equal(S.InvalidOwner, Owner(m)->Inspect(Key, owner, a).Code);
        Same(m, before);
        owner = m.Owner;
        owner.Fs = 16;
        HostCheck.Equal(S.InvalidOwner, Owner(m)->Inspect(Key, owner, a).Code);
        Same(m, before);
        owner = m.Owner;
        owner.GuardsVerified = false;
        HostCheck.Equal(S.InvalidOwner, Owner(m)->Inspect(Key, owner, a).Code);
        Same(m, before);
        owner = m.Owner;
        owner.MappingsVerified = false;
        HostCheck.Equal(S.InvalidOwner, Owner(m)->Inspect(Key, owner, a).Code);
        Same(m, before);
        owner = m.Owner;
        owner.Flags |= 0x200;
        HostCheck.Equal(S.InterruptsEnabled, Owner(m)->Inspect(Key, owner, a).Code);
        Same(m, before);
        HostCheck.Equal(S.InvalidOwner, Owner(m)->Inspect(Key + 1, m.Owner, a).Code);
        Same(m, before);
    }

    [HostTest("thread.tls saved base FP and context corruption cannot enter transfer admission")]
    private static void ContextState()
    {
        using M m = new(initialize: false);
        Setup(m);
        var a = Bind(m, 0);
        var b = Bind(m, 1);
        m.Context(0)->FsBase = Fs(m, 1).Base;
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.InvalidBase, Owner(m)->Admit(Key, m.Owner, a, b));
        Same(m, before);
        m.Context(0)->FsBase = Fs(m).Base;
        m.Context(0)->FxSave[27] |= 0x80;
        before = m.Snapshot();
        HostCheck.Equal(S.InvalidContext, Owner(m)->Inspect(Key, m.Owner, a).Code);
        Same(m, before);
        m.Context(0)->FxSave[27] = 0;
        before = m.Snapshot();
        HostCheck.Equal(S.InvalidContext, Owner(m)->Admit(Key, m.Owner, a, a));
        Same(m, before);
    }

    [HostTest("thread.tls bind rejects started threads and authenticates captured CALL continuations")]
    private static void Continuations()
    {
        using M m = new(initialize: false);
        Setup(m);
        var a = Bind(m, 0);
        var b = Create(m, 1, Fs(m, 1));
        m.Dispatch();
        HostCheck.True(m.Table->Yield(KernelThreadTableTests.Key, 2, a.Thread, m.Capture(a.Thread)).Succeeded);
        m.Confirm();
        HostCheck.True(Owner(m)->Inspect(Key, m.Owner, a).Succeeded);
        m.Dispatch();
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.WrongState, Owner(m)->Bind(Key, m.Owner, b, Fs(m, 1)).Code);
        Same(m, before);
    }

    [HostTest("thread.tls reap retains bytes for running pinned wrong stack and active segment failures")]
    private static void ReapFailures()
    {
        using M m = new(initialize: false);
        Setup(m);
        var a = Bind(m, 0);
        byte[] before = m.Snapshot();
        var fail = Owner(m)->Reap(Key, m.Owner, a, m.SupervisorRsp, 0, 0);
        HostCheck.Equal(KernelThreadTable.Status.WrongState, fail.ThreadStatus);
        Same(m, before);
        m.Dispatch();
        m.Exit(a.Thread);
        HostCheck.Equal(KernelThreadTable.Status.Ok, m.Table->Pin(KernelThreadTableTests.Key, 2, a.Thread, true));
        before = m.Snapshot();
        HostCheck.Equal(KernelThreadTable.Status.Pinned, Owner(m)->Reap(Key, m.Owner, a, m.SupervisorRsp, 0, 0).ThreadStatus);
        Same(m, before);
        HostCheck.Equal(KernelThreadTable.Status.Ok, m.Table->Pin(KernelThreadTableTests.Key, 2, a.Thread, false));
        before = m.Snapshot();
        HostCheck.Equal(KernelThreadTable.Status.NotSupervisor, Owner(m)->Reap(Key, m.Owner, a, m.Stack(0) + 5000, 0, 0).ThreadStatus);
        Same(m, before);
        HostCheck.Equal(S.Busy, Owner(m)->Reap(Key, m.Owner, a, m.SupervisorRsp, Fs(m).Base, 0).Code);
        Same(m, before);
        HostCheck.Equal(S.Busy, Owner(m)->Reap(Key, m.Owner, a, m.SupervisorRsp, 0, Gs(m).Base).Code);
        Same(m, before);
        HostCheck.Equal(S.InvalidBase, Owner(m)->Reap(Key, m.Owner, a, m.SupervisorRsp, 0x800000000000, 0).Code);
        Same(m, before);
    }

    [HostTest("thread.tls supervisor reap scrubs only private spans and rejects stale exit reuse")]
    private static void Reuse()
    {
        using M m = new(initialize: false);
        Setup(m);
        var a = Bind(m, 0);
        var b = Bind(m, 1);
        m.Dispatch();
        m.Exit(a.Thread);
        HostCheck.True(!Zero(Fs(m)) && !Zero(Gs(m)));
        var reaped = Owner(m)->Reap(Key, m.Owner, a, m.SupervisorRsp, 0, 0);
        HostCheck.True(reaped.Succeeded);
        HostCheck.Equal(512UL, reaped.ReapedBytes);
        HostCheck.True(Zero(Fs(m)) && Zero(Gs(m)));
        HostCheck.True(!Zero(Fs(m, 1)) && !Zero(Gs(m, 1)));
        HostCheck.True(Owner(m)->Inspect(Key, m.Owner, b).Succeeded);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.StaleBinding, Owner(m)->Inspect(Key, m.Owner, a).Code);
        Same(m, before);
        m.Prepare(0);
        var fresh = Bind(m, 0);
        HostCheck.True(a.Thread.Value != fresh.Thread.Value && a.Value != fresh.Value);
        before = m.Snapshot();
        HostCheck.Equal(S.StaleBinding, Owner(m)->Reap(Key, m.Owner, a, m.SupervisorRsp, 0, 0).Code);
        Same(m, before);
        HostCheck.True(Zero(Fs(m)) && Zero(Gs(m)));
        HostCheck.True(Owner(m)->Inspect(Key, m.Owner, fresh).Succeeded);
    }

    [HostTest("thread.tls exhausted slot generations retire and zero segment bindings remain valid")]
    private static void Exhaustion()
    {
        using M m = new(initialize: false);
        Setup(m, 1, uint.MaxValue);
        var thread = m.Create(0).Identity;
        var a = Owner(m)->Bind(Key, m.Owner, thread, default);
        HostCheck.True(a.Succeeded);
        m.Dispatch();
        m.Exit(thread);
        var reaped = Owner(m)->Reap(Key, m.Owner, a.Identity, m.SupervisorRsp, 0, 0);
        HostCheck.True(reaped.Succeeded);
        HostCheck.Equal(0UL, reaped.ReapedBytes);
        m.Prepare(0);
        var next = m.Create(0).Identity;
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.Full, Owner(m)->Bind(Key, m.Owner, next, default).Code);
        Same(m, before);
        HostCheck.Equal(S.Ok, Owner(m)->Release(Key, m.Owner));
    }

    [HostTest("thread.tls foreign bindings and new owner lifetime cannot reuse old authority")]
    private static void OwnerReuse()
    {
        using M m = new(initialize: false);
        using M n = new(initialize: false);
        Setup(m);
        Setup(n);
        var a = Bind(m, 0);
        var foreign = Bind(n, 0);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.InvalidBinding, Owner(m)->Inspect(Key, m.Owner, foreign).Code);
        Same(m, before);
        m.Dispatch();
        m.Exit(a.Thread);
        HostCheck.True(Owner(m)->Reap(Key, m.Owner, a, m.SupervisorRsp, 0, 0).Succeeded);
        HostCheck.Equal(S.Ok, Owner(m)->Release(Key, m.Owner));
        HostCheck.Equal(S.Ok, Initialize(m, key: Key + 1));
        m.Prepare(0);
        var next = Create(m, 0, Fs(m), Gs(m));
        var fresh = Owner(m)->Bind(Key + 1, m.Owner, next, Fs(m), Gs(m));
        HostCheck.True(fresh.Succeeded);
        before = m.Snapshot();
        HostCheck.Equal(S.InvalidBinding, Owner(m)->Inspect(Key + 1, m.Owner, a).Code);
        Same(m, before);
    }

    [HostTest("thread.tls warmed inspection and transfer admission allocate no managed storage")]
    private static void Allocation()
    {
        using M m = new(initialize: false);
        Setup(m);
        var a = Bind(m, 0);
        var b = Bind(m, 1);
        for (int i = 0; i < 100; i++)
            Owner(m)->Admit(Key, m.Owner, a, b);
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool good = true;
        for (int i = 0; i < 1000; i++)
            good &= Owner(m)->Admit(Key, m.Owner, a, b) == S.Ok;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(good);
        HostCheck.Equal(0L, allocated);
    }

    [HostTest("thread.tls all cross thread FS GS region alias combinations reject unchanged")]
    private static void CrossAliases()
    {
        for (int combination = 0; combination < 4; combination++)
        {
            using M m = new(initialize: false);
            Setup(m);
            Bind(m, 0);
            R fs = combination == 0 ? Fs(m) : combination == 1 ? Gs(m) : Fs(m, 1);
            R gs = combination == 2 ? Fs(m) : combination == 3 ? Gs(m) : Gs(m, 1);
            var thread = Create(m, 1, fs, gs);
            byte[] before = m.Snapshot();
            HostCheck.Equal(S.Overlap, Owner(m)->Bind(Key, m.Owner, thread, fs, gs).Code);
            Same(m, before);
        }
    }

    [HostTest("thread.tls bypassed lifecycle reap retains backing rather than falsely releasing TLS")]
    private static void BypassedReap()
    {
        using M m = new(initialize: false);
        Setup(m);
        var a = Bind(m, 0);
        m.Dispatch();
        m.Exit(a.Thread);
        HostCheck.True(m.Table->Reap(KernelThreadTableTests.Key, 2, a.Thread, m.SupervisorRsp).Succeeded);
        byte[] before = m.Snapshot();
        var result = Owner(m)->Reap(Key, m.Owner, a, m.SupervisorRsp, 0, 0);
        HostCheck.Equal(S.ThreadFailure, result.Code);
        HostCheck.Equal(KernelThreadTable.Status.StaleIdentity, result.ThreadStatus);
        Same(m, before);
        HostCheck.True(!Zero(Fs(m)) && !Zero(Gs(m)));
        HostCheck.Equal(S.Busy, Owner(m)->Release(Key, m.Owner));
        Same(m, before);
    }
}
