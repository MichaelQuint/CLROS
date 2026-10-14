using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Threading;
using OS.Pal;
using Timer = OS.Kernel.Threading.SchedulerTimer;

internal static unsafe class SchedulerTimerTests
{
    private const ulong Key = 0x4d3039303035, Apic = 0xfee00000;
    private const uint Reload = 12345;
    private static OSHostHandle Id(uint slot, uint generation = 1) => new(((ulong)generation << 32) | slot);

    // Register model independently applies one-shot expiration and EOI effects.
    // It deliberately permits a callback failure AFTER a write has taken effect.
    private struct Model
    {
        public fixed uint Registers[1024];
        public fixed uint Offsets[512];
        public fixed uint Values[512];
        public uint Reads, Writes, FailRead, FailWrite, CorruptWrite;
        public byte RetainIsr;
    }
    private sealed class Owner : IDisposable
    {
        public readonly Model* M = (Model*)NativeMemory.AllocZeroed((nuint)sizeof(Model));
        public readonly DeadlineQueue.Entry* Entries = (DeadlineQueue.Entry*)NativeMemory.AllocZeroed(128);
        public Owner()
        {
            M->Registers[0x320 / 4] = 0x10051;
            M->Registers[0x3e0 / 4] = 0xb;
            M->Registers[0x80 / 4] = 0xd0;
            M->Registers[0xf0 / 4] = 0x1ff;
        }
        public XApicSetupAccess Access => new() { Context = M, Read32 = &Read, Write32 = &Write };
        public Timer Prepare(uint maximumDeferredTicks = 3, ulong initialTick = 0)
        {
            Timer timer = default;
            HostCheck.Equal(Timer.Status.Ok, timer.Prepare(Key, Apic, Reload, maximumDeferredTicks, Access, 2, initialTick));
            return timer; // Transfer sole ownership before any physical writes.
        }
        public DeadlineQueue Queue(ulong initialTick = 0)
        {
            DeadlineQueue queue = default;
            HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Initialize(Key, Entries, 128, 8, initialTick));
            return queue;
        }
        public void Expire() { M->Registers[0x390 / 4] = 0; M->Registers[0x170 / 4] = 2; }
        public Timer.Result Tick(ref Timer timer, ref DeadlineQueue queue, Timer.Phase phase = Timer.Phase.Safe)
        {
            Expire(); return timer.HandleInterrupt(Key, 2, 0x202, ref queue, Key, phase);
        }
        public void Dispose() { NativeMemory.Free(M); NativeMemory.Free(Entries); }
    }
    [UnmanagedCallersOnly]
    private static byte Read(void* context, ulong address, uint* value)
    {
        Model* m = (Model*)context;
        m->Reads++;
        if (address < Apic || address >= Apic + 4096 || (address & 3) != 0 || m->Reads == m->FailRead) return 0;
        *value = m->Registers[(address - Apic) / 4];
        return 1;
    }
    [UnmanagedCallersOnly]
    private static byte Write(void* context, ulong address, uint value)
    {
        Model* m = (Model*)context;
        if (address < Apic || address >= Apic + 4096 || (address & 3) != 0) return 0;
        uint offset = (uint)(address - Apic), index = m->Writes++;
        if (index < 512) { m->Offsets[index] = offset; m->Values[index] = value; }
        if (offset == 0xb0)
        {
            if (m->RetainIsr == 0) m->Registers[0x170 / 4] = 0;
        }
        else
        {
            m->Registers[offset / 4] = m->Writes == m->CorruptWrite ? value ^ 1U : value;
            if (offset == 0x380) m->Registers[0x390 / 4] = m->Registers[offset / 4];
        }
        return m->Writes == m->FailWrite ? (byte)0 : (byte)1;
    }
    private static void Arm(ref Timer timer) => HostCheck.Equal(Timer.Status.Ok, timer.Arm(Key, 2, true));
    private static void Add(ref DeadlineQueue queue, uint slot, ulong delay) =>
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Schedule(Key, Id(slot), Id(slot), queue.ObservedTicks, delay));
    private static void Restored(ref Timer timer, Owner owner)
    {
        HostCheck.Equal(Timer.Status.Ok, timer.Restore(Key, 2));
        HostCheck.True(!timer.IsPrepared && !timer.IsArmed && !timer.NeedsRestore);
        HostCheck.Equal(0x10051U, owner.M->Registers[0x320 / 4]);
        HostCheck.Equal(0xbU, owner.M->Registers[0x3e0 / 4]);
        HostCheck.Equal(0U, owner.M->Registers[0x380 / 4]);
        HostCheck.Equal(0U, owner.M->Registers[0x390 / 4]);
        HostCheck.Equal(0xd0U, owner.M->Registers[0x80 / 4]);
        HostCheck.Equal(0x1ffU, owner.M->Registers[0xf0 / 4]);
    }

    [HostTest("m09.005 invalid timer admission rejects before callbacks or writes", HostLane.Fast, 0)]
    private static void Admission()
    {
        using Owner owner = new(); Timer timer = default;
        HostCheck.Equal(Timer.Status.Uninitialized, timer.Arm(Key, 2, true));
        HostCheck.Equal(Timer.Status.Uninitialized, timer.Restore(Key, 2));
        HostCheck.Equal(Timer.Status.InvalidOwner, timer.Prepare(0, Apic, Reload, 3, owner.Access, 2));
        foreach (var bad in new (ulong Address, uint Count, uint Deferrals)[]
        { (0, Reload, 3), (Apic + 1, Reload, 3), (0x100000000, Reload, 3), (Apic, 0, 3), (Apic, Reload, 0) })
            HostCheck.Equal(Timer.Status.InvalidConfiguration, timer.Prepare(Key, bad.Address, bad.Count, bad.Deferrals, owner.Access, 2));
        HostCheck.Equal(Timer.Status.InvalidAccess, timer.Prepare(Key, Apic, Reload, 3, default, 2));
        foreach (ulong flags in new ulong[] { 0, 0x200, 0x202 })
            HostCheck.Equal(Timer.Status.InterruptsEnabled, timer.Prepare(Key, Apic, Reload, 3, owner.Access, flags));
        HostCheck.True(!timer.IsPrepared && owner.M->Reads == 0 && owner.M->Writes == 0);
        owner.M->FailRead = 3;
        HostCheck.Equal(Timer.Status.ReadFailed, timer.Prepare(Key, Apic, Reload, 3, owner.Access, 2));
        HostCheck.True(!timer.IsPrepared && owner.M->Writes == 0);
    }

    [HostTest("m09.005 prepare requires stopped masked quiescent APIC while retaining blocked IRR", HostLane.Fast, 1)]
    private static void Baseline()
    {
        foreach (var bad in new (uint Offset, uint Value, Timer.Status Code)[]
        {
            (0x320, 0xe1, Timer.Status.BaselineMismatch), (0x3e0, 4, Timer.Status.BaselineMismatch),
            (0x380, 1, Timer.Status.BaselineMismatch), (0x390, 1, Timer.Status.BaselineMismatch),
            (0x80, 0, Timer.Status.BaselineMismatch), (0xf0, 0xff, Timer.Status.BaselineMismatch),
            (0x100, 1, Timer.Status.PendingInterrupt), (0x170, 2, Timer.Status.PendingInterrupt),
            (0x270, 1, Timer.Status.PendingInterrupt)
        })
        {
            using Owner owner = new(); Timer timer = default; owner.M->Registers[bad.Offset / 4] = bad.Value;
            HostCheck.Equal(bad.Code, timer.Prepare(Key, Apic, Reload, 3, owner.Access, 2));
            HostCheck.True(!timer.IsPrepared && owner.M->Writes == 0);
        }
        using Owner good = new(); good.M->Registers[0x260 / 4] = 0x80000000;
        Timer accepted = good.Prepare();
        HostCheck.Equal(Timer.Status.AlreadyPrepared, accepted.Prepare(0, 0, 0, 0, default, 0));
        Restored(ref accepted, good);
        HostCheck.Equal(0x80000000U, good.M->Registers[0x260 / 4]);
    }

    [HostTest("m09.005 arm keeps IF clear and writes positive one-shot reload only after readiness", HostLane.Fast, 2)]
    private static void Arming()
    {
        using Owner owner = new(); Timer timer = owner.Prepare();
        HostCheck.Equal(Timer.Status.WrongOwner, timer.Arm(Key + 1, 2, true));
        HostCheck.Equal(Timer.Status.InterruptsEnabled, timer.Arm(Key, 0x202, true));
        HostCheck.Equal(Timer.Status.PathsNotReady, timer.Arm(Key, 2, false));
        HostCheck.Equal(0U, owner.M->Writes);
        Arm(ref timer);
        HostCheck.True(timer.IsPrepared && timer.IsArmed && timer.NeedsRestore);
        HostCheck.Equal(4U, owner.M->Writes);
        uint[] offsets = [0x320, 0x3e0, 0x320, 0x380], values = [0x100e1, 3, 0xe1, Reload];
        for (int i = 0; i < 4; i++)
        { HostCheck.Equal(offsets[i], owner.M->Offsets[i]); HostCheck.Equal(values[i], owner.M->Values[i]); }
        HostCheck.Equal(Timer.Status.AlreadyArmed, timer.Arm(Key, 2, true));
        HostCheck.Equal(Timer.Status.InterruptsEnabled, timer.Restore(Key, 0x202));
        HostCheck.Equal(4U, owner.M->Writes);
        Restored(ref timer, owner);
    }

    [HostTest("m09.005 ticks require expired owned ISR and EOI readback before reload", HostLane.Fast, 3)]
    private static void InterruptOrdering()
    {
        foreach (uint isr in new uint[] { 0, 1, 3, 0x80000002 })
        {
            using Owner owner = new(); Timer timer = owner.Prepare(); DeadlineQueue queue = owner.Queue(); Arm(ref timer);
            owner.Expire(); owner.M->Registers[0x170 / 4] = isr;
            HostCheck.Equal(Timer.Status.NotTimerInterrupt, timer.HandleInterrupt(Key, 2, 0x202, ref queue, Key, Timer.Phase.Safe).Code);
            HostCheck.True(timer.Ticks == 0 && owner.M->Writes == 4 && !timer.IsArmed && timer.NeedsRestore);
        }
        using Owner good = new(); Timer armed = good.Prepare(); DeadlineQueue empty = good.Queue(); Arm(ref armed);
        uint before = good.M->Writes;
        Timer.Result tick = good.Tick(ref armed, ref empty);
        HostCheck.True(tick.Code == Timer.Status.Ok && tick.Next == Timer.Action.Rearmed && tick.Tick == 1 && armed.IsArmed);
        HostCheck.Equal(0xb0U, good.M->Offsets[before]); HostCheck.Equal(0U, good.M->Values[before]);
        HostCheck.Equal(0x380U, good.M->Offsets[before + 1]); HostCheck.Equal(Reload, good.M->Values[before + 1]);
        good.M->RetainIsr = 1; before = good.M->Writes;
        HostCheck.Equal(Timer.Status.PendingInterrupt, good.Tick(ref armed, ref empty).Code);
        HostCheck.True(armed.Ticks == 1 && good.M->Writes == before + 1 && !armed.IsArmed);
        HostCheck.Equal(Timer.Status.PendingInterrupt, armed.Restore(Key, 2));
        HostCheck.True(armed.IsPrepared && armed.NeedsRestore);
        good.M->Registers[0x170 / 4] = 0; Restored(ref armed, good);
    }

    [HostTest("m09.005 wake is never early consumes no queue entries and respects cancellation", HostLane.Fast, 4)]
    private static void DeadlineWake()
    {
        using Owner owner = new(); Timer timer = owner.Prepare(); DeadlineQueue queue = owner.Queue();
        Add(ref queue, 1, 2); Add(ref queue, 2, 2); Add(ref queue, 3, 1);
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(3))); Arm(ref timer);
        HostCheck.Equal(Timer.Action.Rearmed, owner.Tick(ref timer, ref queue).Next);
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(1)));
        Timer.Result wake = owner.Tick(ref timer, ref queue);
        HostCheck.True(wake.Code == Timer.Status.Ok && wake.Next == Timer.Action.WakeReady && wake.Tick == 2);
        HostCheck.Equal(Id(2).Value, wake.Due.Identity.Value); HostCheck.Equal(2UL, wake.Due.Deadline);
        HostCheck.True(queue.Count == 1 && queue.Peek(Key).Value.Identity.Value == Id(2).Value && !timer.IsArmed);
        HostCheck.Equal(Timer.Status.NotArmed, timer.HandleInterrupt(Key, 2, 0x202, ref queue, Key, Timer.Phase.Safe).Code);
        HostCheck.Equal(Timer.Status.DirtyState, timer.Arm(Key, 2, true));
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(2))); Restored(ref timer, owner);
    }

    [HostTest("m09.005 allocation initialization and publication ticks defer without draining roots or wakes", HostLane.Fast, 5)]
    private static void PublicationBoundaries()
    {
        using Owner owner = new(); Timer timer = owner.Prepare(); DeadlineQueue queue = owner.Queue(); Add(ref queue, 1, 1); Arm(ref timer);
        uint count = 0;
        foreach (Timer.Phase phase in new[] { Timer.Phase.Allocating, Timer.Phase.Initializing, Timer.Phase.Publishing })
        {
            Timer.Result deferred = owner.Tick(ref timer, ref queue, phase);
            HostCheck.True(deferred.Code == Timer.Status.Ok && deferred.Next == Timer.Action.Deferred && deferred.Due.Identity.Value == 0);
            HostCheck.True(queue.Count == 1 && timer.DeferredTicks == ++count && timer.IsArmed);
        }
        Timer.Result ready = owner.Tick(ref timer, ref queue);
        HostCheck.True(ready.Code == Timer.Status.Ok && ready.Next == Timer.Action.WakeReady && ready.Tick == 4 && timer.DeferredTicks == 0);
        HostCheck.Equal(Id(1).Value, ready.Due.Identity.Value);
        Restored(ref timer, owner);
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(1)));
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Release(Key));
        // Cancelling a due entry during deferral removes it, rather than leaving a
        // remembered identity that could wake a reused context on the next tick.
        Timer again = owner.Prepare(); DeadlineQueue fresh = owner.Queue(); Add(ref fresh, 2, 0); Arm(ref again);
        HostCheck.Equal(Timer.Action.Deferred, owner.Tick(ref again, ref fresh, Timer.Phase.Initializing).Next);
        HostCheck.Equal(DeadlineQueue.Status.Ok, fresh.Cancel(Key, Id(2)));
        HostCheck.Equal(Timer.Action.Rearmed, owner.Tick(ref again, ref fresh).Next);
        Restored(ref again, owner);
    }

    [HostTest("m09.005 bounded unsafe phases clock mismatch and tick overflow stop without wrap or wake", HostLane.Fast, 6)]
    private static void Boundaries()
    {
        using Owner owner = new(); Timer timer = owner.Prepare(1); DeadlineQueue queue = owner.Queue(); Arm(ref timer);
        HostCheck.Equal(Timer.Action.Deferred, owner.Tick(ref timer, ref queue, Timer.Phase.Publishing).Next);
        HostCheck.Equal(Timer.Status.DeferralLimit, owner.Tick(ref timer, ref queue, Timer.Phase.Publishing).Code);
        HostCheck.True(timer.Ticks == 2 && !timer.IsArmed && timer.DeferredTicks == 1); Restored(ref timer, owner);
        Timer overflow = owner.Prepare(initialTick: ulong.MaxValue); Arm(ref overflow);
        HostCheck.Equal(Timer.Status.TickOverflow, owner.Tick(ref overflow, ref queue).Code);
        HostCheck.True(overflow.Ticks == ulong.MaxValue && !overflow.IsArmed); Restored(ref overflow, owner);
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Release(Key));
        Timer late = owner.Prepare(); DeadlineQueue future = owner.Queue(9); Arm(ref late);
        HostCheck.Equal(Timer.Status.ClockMismatch, owner.Tick(ref late, ref future).Code);
        HostCheck.True(late.Ticks == 1 && !late.IsArmed); Restored(ref late, owner);
    }

    [HostTest("m09.005 every partial arm failure retains borrow and restores changed registers", HostLane.Fast, 7)]
    private static void PartialArm()
    {
        foreach (bool corrupt in new[] { false, true })
        for (uint failure = 1; failure <= 4; failure++)
        {
            using Owner owner = new(); Timer timer = owner.Prepare();
            if (corrupt) owner.M->CorruptWrite = failure; else owner.M->FailWrite = failure;
            HostCheck.Equal(corrupt ? Timer.Status.ReadBackFailed : Timer.Status.WriteFailed, timer.Arm(Key, 2, true));
            HostCheck.True(timer.NeedsRestore && timer.IsPrepared && !timer.IsArmed);
            HostCheck.Equal(failure, owner.M->Writes);
            Restored(ref timer, owner);
            HostCheck.Equal(0x320U, owner.M->Offsets[failure]); HostCheck.Equal(0x100e1U, owner.M->Values[failure]);
            HostCheck.Equal(0x380U, owner.M->Offsets[failure + 1]); HostCheck.Equal(0U, owner.M->Values[failure + 1]);
        }
    }

    [HostTest("m09.005 failed EOI reload and restoration retain recoverable hardware ownership", HostLane.Fast, 8)]
    private static void FailureCleanup()
    {
        foreach (uint failure in new uint[] { 5, 6 })
        {
            using Owner owner = new(); Timer timer = owner.Prepare(); DeadlineQueue queue = owner.Queue(); Arm(ref timer);
            owner.M->FailWrite = failure;
            HostCheck.Equal(Timer.Status.WriteFailed, owner.Tick(ref timer, ref queue).Code);
            HostCheck.True(!timer.IsArmed && timer.NeedsRestore && timer.IsPrepared);
            HostCheck.Equal(failure == 5 ? 0UL : 1UL, timer.Ticks); Restored(ref timer, owner);
        }
        using Owner retained = new(); Timer pending = retained.Prepare(); Arm(ref pending);
        retained.M->Registers[0x270 / 4] = 2;
        HostCheck.Equal(Timer.Status.PendingInterrupt, pending.Restore(Key, 2));
        HostCheck.True(pending.IsPrepared && pending.NeedsRestore && !pending.IsArmed);
        retained.M->Registers[0x270 / 4] = 0;
        retained.M->FailWrite = retained.M->Writes + 1;
        HostCheck.Equal(Timer.Status.WriteFailed, pending.Restore(Key, 2));
        HostCheck.True(pending.IsPrepared && pending.NeedsRestore);
        Restored(ref pending, retained);
    }

    [HostTest("m09.005 warmed timer MMIO and queue checks allocate no managed storage", HostLane.Fast, 9)]
    private static void NoAllocation()
    {
        using Owner owner = new(); DeadlineQueue queue = owner.Queue();
        bool good = true;
        for (int pass = 0; pass < 2; pass++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 16; i++)
            {
                Timer timer = default;
                good &= timer.Prepare(Key, Apic, Reload, 3, owner.Access, 2) == Timer.Status.Ok;
                good &= timer.Arm(Key, 2, true) == Timer.Status.Ok;
                owner.Expire();
                Timer.Result tick = timer.HandleInterrupt(Key, 2, 0x202, ref queue, Key, Timer.Phase.Safe);
                good &= tick.Code == Timer.Status.Ok && tick.Next == Timer.Action.Rearmed && tick.Tick == 1;
                good &= timer.Restore(Key, 2) == Timer.Status.Ok;
            }
            if (pass == 1) HostCheck.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        HostCheck.True(good);
    }

    [HostTest("m09.005 invalid policy acknowledges only authenticated timer and can restore cleanly", HostLane.Fast, 10)]
    private static void InvalidPolicy()
    {
        HostCheck.True(!default(Timer.Result).Succeeded);
        foreach (var bad in new (ulong Flags, Timer.Phase Phase, ulong QueueOwner, Timer.Status Code)[]
        {
            (2, Timer.Phase.Safe, Key, Timer.Status.InvalidInterruptedFlags),
            (0x202, (Timer.Phase)255, Key, Timer.Status.InvalidPhase),
            (0x202, Timer.Phase.Safe, Key + 1, Timer.Status.InvalidDeadlineQueue)
        })
        {
            using Owner owner = new(); Timer timer = owner.Prepare(); DeadlineQueue queue = owner.Queue(); Arm(ref timer); owner.Expire();
            Timer.Result result = timer.HandleInterrupt(Key, 2, bad.Flags, ref queue, bad.QueueOwner, bad.Phase);
            HostCheck.Equal(bad.Code, result.Code);
            HostCheck.True(!result.Succeeded && timer.Ticks == 1 && !timer.IsArmed && owner.M->Registers[0x170 / 4] == 0);
            HostCheck.Equal(5U, owner.M->Writes); Restored(ref timer, owner);
        }
    }

    [HostTest("m09.005 minimum maximum reload and final acknowledged tick retain exact boundaries", HostLane.Fast, 11)]
    private static void CountLimits()
    {
        foreach (uint count in new uint[] { 1, uint.MaxValue })
        {
            using Owner owner = new(); Timer timer = default;
            HostCheck.Equal(Timer.Status.Ok, timer.Prepare(Key, Apic, count, uint.MaxValue, owner.Access, 2, ulong.MaxValue - 1));
            DeadlineQueue queue = owner.Queue(ulong.MaxValue - 1);
            HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Schedule(Key, Id(1), Id(1), ulong.MaxValue - 1, 1));
            Arm(ref timer); HostCheck.Equal(count, owner.M->Registers[0x380 / 4]);
            Timer.Result last = owner.Tick(ref timer, ref queue);
            HostCheck.True(last.Succeeded && last.Next == Timer.Action.WakeReady && last.Tick == ulong.MaxValue);
            HostCheck.Equal(ulong.MaxValue, last.Due.Deadline); Restored(ref timer, owner);
        }
    }

    [HostTest("kernel.thread idle timer repeats restore prepare arm without resetting the lifecycle epoch")]
    private static void LifecycleEpoch()
    {
        using Owner hardware = new(); using KernelThreadTableTests.Model threads = new(2, 2, 2);
        var a = threads.Create(0); var b = threads.Create(1);
        HostCheck.True(a.Succeeded && b.Succeeded);
        ulong threadOwner = KernelThreadTableTests.Key, epoch = 0;
        for (int cycle = 0; cycle < 3; cycle++)
        {
            foreach (OSHostHandle id in new[] { a.Identity, b.Identity })
            {
                HostCheck.Equal(id.Value, threads.Dispatch().Value);
                HostCheck.True(threads.Table->Sleep(threadOwner, 2, id, threads.Capture(id), epoch, 2).Succeeded);
                threads.Confirm();
            }
            HostCheck.Equal(0U, threads.Table->ReadyCount); HostCheck.Equal(2U, threads.Table->DeadlineCount);
            Timer timer = hardware.Prepare(initialTick: epoch); Arm(ref timer);
            for (int interrupt = 1; interrupt <= 2; interrupt++)
            {
                hardware.Expire();
                HostCheck.Equal(KernelThreadTable.Status.Ok,
                    threads.Table->HandleIdleTimer(threadOwner, 2, ref timer, Key, 0x202, out var tick));
                HostCheck.Equal(epoch + (uint)interrupt, tick.Tick);
                HostCheck.Equal(interrupt == 1 ? Timer.Action.Rearmed : Timer.Action.WakeReady, tick.Next);
                HostCheck.Equal(interrupt == 1 ? 0U : 2U, threads.Table->ReadyCount);
            }
            epoch = timer.Ticks; HostCheck.Equal(epoch, threads.Table->ObservedTicks);
            HostCheck.Equal(0U, threads.Table->DeadlineCount);
            HostCheck.Equal(Timer.Status.DirtyState, timer.Arm(Key, 2, true));
            if (cycle == 1)
            {
                // A failed restoration cannot release the IRQ/gate/mapping borrow
                // or permit preparation for another period. The saved epoch survives.
                hardware.M->Registers[0x270 / 4] = 2;
                HostCheck.Equal(Timer.Status.PendingInterrupt, timer.Restore(Key, 2));
                HostCheck.True(timer.IsPrepared && timer.NeedsRestore); HostCheck.Equal(epoch, timer.Ticks);
                HostCheck.Equal(Timer.Status.AlreadyPrepared, timer.Prepare(Key, Apic, Reload, 3, hardware.Access, 2, epoch));
                hardware.M->Registers[0x270 / 4] = 0;
            }
            Restored(ref timer, hardware); // Save epoch BEFORE Restore clears the timer.
            HostCheck.Equal(0UL, timer.Ticks);
        }
        HostCheck.Equal(6UL, epoch);
        HostCheck.Equal(a.Identity.Value, threads.Dispatch().Value); threads.Exit(a.Identity, 17);
        HostCheck.Equal(b.Identity.Value, threads.Dispatch().Value); threads.Exit(b.Identity, 23);
        HostCheck.True(threads.Table->Reap(threadOwner, 2, a.Identity, threads.SupervisorRsp).Succeeded);
        HostCheck.True(threads.Table->Reap(threadOwner, 2, b.Identity, threads.SupervisorRsp).Succeeded);
        HostCheck.Equal(KernelThreadTable.Status.Ok, threads.Table->Release(threadOwner, 2, threads.SupervisorRsp));
    }

    [HostTest("kernel.thread idle timer rejects active workers bad epochs and owner flags before MMIO")]
    private static void LifecycleTimerAdmission()
    {
        using Owner hardware = new(); using KernelThreadTableTests.Model threads = new();
        Timer timer = hardware.Prepare(); Arm(ref timer); ulong owner = KernelThreadTableTests.Key;
        uint reads = hardware.M->Reads, writes = hardware.M->Writes;
        HostCheck.Equal(KernelThreadTable.Status.WrongOwner,
            threads.Table->HandleIdleTimer(owner + 1, 2, ref timer, Key, 0x202, out _));
        HostCheck.Equal(KernelThreadTable.Status.InterruptsEnabled,
            threads.Table->HandleIdleTimer(owner, 0x202, ref timer, Key, 0x202, out _));
        var worker = threads.Create(0); threads.Dispatch();
        HostCheck.Equal(KernelThreadTable.Status.Busy,
            threads.Table->HandleIdleTimer(owner, 2, ref timer, Key, 0x202, out _));
        HostCheck.Equal(reads, hardware.M->Reads); HostCheck.Equal(writes, hardware.M->Writes);
        threads.Exit(worker.Identity); HostCheck.Equal(KernelThreadTable.Status.Ok, threads.Table->Advance(owner, 2, 1));
        HostCheck.Equal(KernelThreadTable.Status.ClockRegression,
            threads.Table->HandleIdleTimer(owner, 2, ref timer, Key, 0x202, out _));
        HostCheck.Equal(reads, hardware.M->Reads); HostCheck.Equal(writes, hardware.M->Writes);
        Restored(ref timer, hardware);
        timer = hardware.Prepare(initialTick: 1); Arm(ref timer); hardware.Expire();
        hardware.M->FailWrite = hardware.M->Writes + 1; // EOI takes effect before callback failure.
        HostCheck.Equal(KernelThreadTable.Status.TimerFailure,
            threads.Table->HandleIdleTimer(owner, 2, ref timer, Key, 0x202, out var failed));
        HostCheck.Equal(Timer.Status.WriteFailed, failed.Code);
        HostCheck.True(timer.NeedsRestore && !timer.IsArmed); HostCheck.Equal(1UL, threads.Table->ObservedTicks);
        Restored(ref timer, hardware);
    }
}
