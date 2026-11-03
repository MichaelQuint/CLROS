using OS.Kernel.Threading;
using Timer = OS.Kernel.Threading.SchedulerTimer;

internal static unsafe partial class SchedulerTimerTests
{
    private static void Retained(in Timer timer, bool drainable)
        => HostCheck.True(timer.IsPrepared && timer.NeedsRestore && timer.IsArmed == drainable);

    private static void StopPending(Owner owner, out Timer timer, ulong epoch = 0, Model.Delivery delivery = Model.Delivery.Requested)
    {
        timer = owner.Prepare(initialTick: epoch);
        Arm(ref timer);
        Deliver(owner.M, delivery);
        HostCheck.Equal(Timer.Status.PendingInterrupt, timer.Restore(Key, 2));
        Retained(in timer, true);
        HostCheck.Equal(epoch, timer.Ticks);
        HostCheck.Equal(0U, owner.M->EoiWrites);
        HostCheck.True((owner.M->Registers[0x320 / 4] & ~0x1000U) == 0x100e1 && owner.M->Registers[0x3e0 / 4] == 3 && owner.M->Registers[0x380 / 4] == 0 && owner.M->Registers[0x390 / 4] == 0);
    }

    private static void CannotDrain(ref Timer timer, Owner owner)
    {
        ulong tick = timer.Ticks;
        uint writes = owner.M->Writes, eois = owner.M->EoiWrites;
        DeadlineQueue queue = default;
        Deliver(owner.M, Model.Delivery.InService);
        HostCheck.Equal(Timer.Status.NotArmed, timer.HandleInterrupt(Key, 2, 0x202, ref queue, Key, Timer.Phase.Safe).Code);
        HostCheck.True(timer.Ticks == tick && owner.M->Writes == writes && owner.M->EoiWrites == eois);
        Retained(in timer, false);
    }

    [HostTest("m09.005 early quiet stop preserves caller epoch and canceled deadline without EOI", HostLane.Fast, 12)]
    private static void EarlyQuietStop()
    {
        // This checks only the rebuilt host layout, not an emitted EFI layout.
        HostCheck.Equal(88, sizeof(Timer));
        foreach (uint current in new uint[] { 0, Reload / 2, Reload })
        {
            using Owner owner = new();
            Timer timer = owner.Prepare(initialTick: 17);
            DeadlineQueue queue = owner.Queue(17);
            Add(ref queue, 1, 1);
            Arm(ref timer);
            owner.M->Registers[0x390 / 4] = current;
            owner.M->Registers[0x260 / 4] = 0x80000000;
            ulong savedEpoch = timer.Ticks;
            uint before = owner.M->Writes;
            Restored(ref timer, owner);
            HostCheck.True(owner.M->EoiWrites == 0 && timer.Ticks == 0 && savedEpoch == 17 && queue.ObservedTicks == 17 && queue.Count == 1);
            HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(1)));
            HostCheck.True(queue.Count == 0 && queue.ObservedTicks == savedEpoch);
            HostCheck.Equal(0x80000000U, owner.M->Registers[0x260 / 4]);
            uint[] offsets = [0x320, 0x380, 0x3e0, 0x320], values = [0x100e1, 0, 0xb, 0x10051];
            HostCheck.Equal(before + 4, owner.M->Writes);
            for (uint i = 0; i < 4; i++)
            {
                HostCheck.Equal(offsets[i], owner.M->Offsets[before + i]);
                HostCheck.Equal(values[i], owner.M->Values[before + i]);
            }
        }
    }

    [HostTest("m09.005 early stop authenticates active ownership before any write", HostLane.Fast, 13)]
    private static void EarlyStopAdmission()
    {
        using Owner good = new();
        Timer owned = good.Prepare();
        Arm(ref owned);
        uint reads = good.M->Reads, writes = good.M->Writes;
        HostCheck.Equal(Timer.Status.WrongOwner, owned.Restore(Key + 1, 2));
        HostCheck.Equal(Timer.Status.InterruptsEnabled, owned.Restore(Key, 0));
        HostCheck.Equal(Timer.Status.InterruptsEnabled, owned.Restore(Key, 0x202));
        HostCheck.True(good.M->Reads == reads && good.M->Writes == writes);
        Retained(in owned, true);
        Restored(ref owned, good);

        foreach (var bad in new (uint Offset, uint Value)[]
        {
            (0x320, 0xe0), (0x320, 0x200e1), (0x320, 0x100e1),
            (0x320, 0x20e1), (0x3e0, 0xb), (0x380, Reload - 1),
            (0x390, Reload + 1), (0x80, 0xc0), (0xf0, 0xff)
        })
        {
            using Owner owner = new();
            Timer timer = owner.Prepare();
            Arm(ref timer);
            owner.M->Registers[bad.Offset / 4] = bad.Value;
            HostCheck.Equal(Timer.Status.BaselineMismatch, timer.Restore(Key, 2));
            HostCheck.Equal(4U, owner.M->Writes);
            HostCheck.Equal(0U, owner.M->EoiWrites);
            Retained(in timer, false);
        }
    }

    [HostTest("m09.005 stopped E1 pending and send pending require one genuine safe due acknowledgement", HostLane.Fast, 14)]
    private static void EarlyPendingDrain()
    {
        foreach (Model.Delivery delivery in new[] { Model.Delivery.Requested, Model.Delivery.SendPending })
        {
            using Owner owner = new();
            StopPending(owner, out Timer timer, 5, delivery);
            DeadlineQueue queue = owner.Queue(5);
            Add(ref queue, 1, 1);
            owner.M->Registers[0x260 / 4] = 0x80000000;
            HostCheck.Equal(Timer.Status.PendingInterrupt, timer.Restore(Key, 2));
            Retained(in timer, true);
            HostCheck.True(timer.Ticks == 5 && owner.M->EoiWrites == 0);
            Deliver(owner.M, Model.Delivery.InService);
            uint before = owner.M->Writes;
            Timer.Result result = timer.HandleInterrupt(Key, 2, 0x202, ref queue, Key, Timer.Phase.Safe);
            HostCheck.True(result.Succeeded && result.Next == Timer.Action.WakeReady && result.Tick == 6 && result.Due.Identity.Value == Id(1).Value);
            HostCheck.True(timer.Ticks == 6 && !timer.IsArmed && timer.DeferredTicks == 0 && queue.Count == 1 && queue.ObservedTicks == 5);
            HostCheck.Equal(before + 1, owner.M->Writes);
            HostCheck.Equal(0xb0U, owner.M->Offsets[before]);
            HostCheck.Equal(0U, owner.M->Values[before]);
            HostCheck.Equal(1U, owner.M->EoiWrites);
            HostCheck.Equal(0U, owner.M->Registers[0x380 / 4]);
            HostCheck.Equal(Timer.Status.NotArmed, timer.HandleInterrupt(Key, 2, 0x202, ref queue, Key, Timer.Phase.Safe).Code);
            HostCheck.Equal(before + 1, owner.M->Writes);
            HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(1)));
            Restored(ref timer, owner);
            HostCheck.Equal(1U, owner.M->EoiWrites);
            HostCheck.Equal(0x80000000U, owner.M->Registers[0x260 / 4]);
        }
    }

    [HostTest("m09.005 expiration at mask stop and stopped reads preserves exact pending classification", HostLane.Fast, 15)]
    private static void EarlyStopDeliveryBoundaries()
    {
        using Owner probe = new();
        Timer sample = probe.Prepare();
        Arm(ref sample);
        uint reads = probe.M->Reads, writes = probe.M->Writes;
        Restored(ref sample, probe);
        uint lastStoppedLvt = 0, firstStoppedIsr = 0;
        for (uint i = reads; i < probe.M->Reads; i++)
        {
            if (probe.M->ReadWrites[i] != writes + 2)
                continue;
            uint offset = probe.M->ReadOffsets[i];
            if (offset == 0x320)
                lastStoppedLvt = i + 1;
            if (offset >= 0x100 && offset <= 0x170 && firstStoppedIsr == 0)
                firstStoppedIsr = i + 1;
        }

        HostCheck.True(lastStoppedLvt != 0 && firstStoppedIsr > lastStoppedLvt);
        // Replay each observed stop/read boundary with an architectural request.
        // A request observed after baseline restoration starts must fail closed.
        for (uint boundary = 1; boundary <= 2; boundary++)
        {
            using Owner owner = new();
            Timer timer = owner.Prepare();
            Arm(ref timer);
            owner.M->AfterWrite = owner.M->Writes + boundary;
            owner.M->InjectDelivery = Model.Delivery.Requested;
            HostCheck.Equal(Timer.Status.PendingInterrupt, timer.Restore(Key, 2));
            Retained(in timer, true);
            HostCheck.True(timer.Ticks == 0 && owner.M->EoiWrites == 0);
        }

        for (uint i = reads; i < probe.M->Reads; i++)
        {
            if (probe.M->ReadWrites[i] < writes + 1 || probe.M->ReadWrites[i] > writes + 2)
                continue;
            foreach (bool after in new[] { false, true })
            {
                using Owner owner = new();
                Timer timer = owner.Prepare();
                Arm(ref timer);
                if (after)
                    owner.M->AfterRead = owner.M->Reads + i - reads + 1;
                else
                    owner.M->BeforeRead = owner.M->Reads + i - reads + 1;
                owner.M->InjectDelivery = Model.Delivery.Requested;
                HostCheck.Equal(Timer.Status.PendingInterrupt, timer.Restore(Key, 2));
                HostCheck.True(timer.IsPrepared && timer.NeedsRestore && timer.Ticks == 0 && owner.M->EoiWrites == 0);
                if (timer.IsArmed)
                    HostCheck.True(owner.M->Registers[0x320 / 4] == 0x100e1 && owner.M->Registers[0x3e0 / 4] == 3 && owner.M->Registers[0x380 / 4] == 0 && owner.M->Registers[0x390 / 4] == 0);
                else
                    CannotDrain(ref timer, owner);
            }
        }
    }

    [HostTest("m09.005 missing in flight request and late E1 never gain stopped drain authority", HostLane.Fast, 16)]
    private static void EarlyStopContradictions()
    {
        using (Owner owner = new())
        {
            Timer timer = owner.Prepare();
            Arm(ref timer);
            Deliver(owner.M, Model.Delivery.SendPending);
            owner.M->AfterWrite = owner.M->Writes + 2;
            owner.M->InjectDelivery = Model.Delivery.Idle;
            HostCheck.Equal(Timer.Status.PendingInterrupt, timer.Restore(Key, 2));
            Retained(in timer, false);
            CannotDrain(ref timer, owner);
        }

        using (Owner owner = new())
        {
            StopPending(owner, out Timer timer);
            Deliver(owner.M, Model.Delivery.Idle);
            HostCheck.Equal(Timer.Status.PendingInterrupt, timer.Restore(Key, 2));
            Retained(in timer, false);
            CannotDrain(ref timer, owner);
        }

        foreach (uint boundary in new uint[] { 3, 4 })
        {
            using Owner owner = new();
            Timer timer = owner.Prepare();
            Arm(ref timer);
            owner.M->AfterWrite = owner.M->Writes + boundary;
            owner.M->InjectDelivery = Model.Delivery.Requested;
            HostCheck.Equal(Timer.Status.PendingInterrupt, timer.Restore(Key, 2));
            Retained(in timer, false);
            CannotDrain(ref timer, owner);
        }
    }

    [HostTest("m09.005 early stop rejects foreign ISR and deliverable IRR without EOI", HostLane.Fast, 17)]
    private static void EarlyStopForeignPending()
    {
        for (uint bank = 0; bank < 8; bank++)
            for (int bit = 0; bit < 32; bit++)
                ForeignPendingCase(0x100 + bank * 16, 1U << bit);
        for (int bit = 0; bit < 32; bit++)
        {
            if (bit == 1)
                continue;
            ForeignPendingCase(0x270, 1U << bit);
            ForeignPendingCase(0x270, (1U << bit) | 2U);
        }
    }

    private static void ForeignPendingCase(uint offset, uint value)
    {
        using Owner owner = new();
        Timer timer = owner.Prepare();
        Arm(ref timer);
        owner.M->Registers[offset / 4] = value;
        HostCheck.Equal(Timer.Status.PendingInterrupt, timer.Restore(Key, 2));
        Retained(in timer, false);
        HostCheck.True(timer.Ticks == 0 && owner.M->EoiWrites == 0);
        CannotDrain(ref timer, owner);
    }

    [HostTest("m09.005 stopped IRQ requires exact idle owned envelope and exact E1 ISR", HostLane.Fast, 18)]
    private static void StoppedInterruptOwnership()
    {
        foreach (var bad in new (uint Offset, uint Value)[]
        {
            (0x320, 0xe1), (0x320, 0x100e0), (0x320, 0x110e1),
            (0x3e0, 0xb), (0x380, Reload), (0x390, 1), (0x80, 0xc0), (0xf0, 0xff),
            (0x100, 1), (0x170, 0), (0x170, 3), (0x270, 2), (0x270, 1)
        })
        {
            using Owner owner = new();
            StopPending(owner, out Timer timer);
            DeadlineQueue queue = owner.Queue();
            Add(ref queue, 1, 1);
            Deliver(owner.M, Model.Delivery.InService);
            owner.M->Registers[bad.Offset / 4] = bad.Value;
            uint writes = owner.M->Writes;
            Timer.Result result = timer.HandleInterrupt(Key, 2, 0x202, ref queue, Key, Timer.Phase.Safe);
            HostCheck.True(!result.Succeeded && result.Next != Timer.Action.WakeReady && timer.Ticks == 0 && owner.M->Writes == writes && owner.M->EoiWrites == 0);
            Retained(in timer, false);
            CannotDrain(ref timer, owner);
        }
    }

    [HostTest("m09.005 stopped IRQ validates safe due policy before EOI and never rearms", HostLane.Fast, 19)]
    private static void StoppedInterruptPolicy()
    {
        foreach (var policy in new (Timer.Phase Phase, ulong Flags, int Queue, Timer.Status Code)[]
        {
            (Timer.Phase.Allocating, 0x202, 0, Timer.Status.InvalidPhase),
            (Timer.Phase.Initializing, 0x202, 0, Timer.Status.InvalidPhase),
            (Timer.Phase.Publishing, 0x202, 0, Timer.Status.InvalidPhase),
            ((Timer.Phase)255, 0x202, 0, Timer.Status.InvalidPhase),
            (Timer.Phase.Safe, 2, 0, Timer.Status.InvalidInterruptedFlags),
            (Timer.Phase.Safe, 0x202, 1, Timer.Status.InvalidDeadlineQueue),
            (Timer.Phase.Safe, 0x202, 2, Timer.Status.InvalidDeadlineQueue),
            (Timer.Phase.Safe, 0x202, 3, Timer.Status.InvalidDeadlineQueue),
            (Timer.Phase.Safe, 0x202, 4, Timer.Status.InvalidDeadlineQueue),
            (Timer.Phase.Safe, 0x202, 5, Timer.Status.InvalidDeadlineQueue),
            (Timer.Phase.Safe, 0x202, 6, Timer.Status.ClockMismatch)
        })
        {
            using Owner owner = new();
            StopPending(owner, out Timer timer);
            DeadlineQueue queue = policy.Queue == 5 ? default : owner.Queue(policy.Queue == 6 ? 9UL : 0UL);
            if (policy.Queue != 1 && policy.Queue != 5)
                Add(ref queue, 1, policy.Queue == 2 ? 2UL : policy.Queue == 6 ? 0UL : 1UL);
            if (policy.Queue == 3)
                HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(1)));
            Deliver(owner.M, Model.Delivery.InService);
            uint writes = owner.M->Writes;
            HostCheck.Equal(policy.Code, timer.HandleInterrupt(Key, 2, policy.Flags, ref queue, policy.Queue == 4 ? Key + 1 : Key, policy.Phase).Code);
            HostCheck.True(timer.Ticks == 0 && timer.DeferredTicks == 0 && owner.M->Writes == writes && owner.M->EoiWrites == 0);
            Retained(in timer, false);
            CannotDrain(ref timer, owner);
        }

        using Owner overflow = new();
        StopPending(overflow, out Timer final, ulong.MaxValue);
        DeadlineQueue last = overflow.Queue(ulong.MaxValue);
        Add(ref last, 1, 0);
        Deliver(overflow.M, Model.Delivery.InService);
        uint before = overflow.M->Writes;
        HostCheck.Equal(Timer.Status.TickOverflow, final.HandleInterrupt(Key, 2, 0x202, ref last, Key, Timer.Phase.Safe).Code);
        HostCheck.True(final.Ticks == ulong.MaxValue && overflow.M->Writes == before && overflow.M->EoiWrites == 0);
        Retained(in final, false);
    }

    [HostTest("m09.005 every stop read and corrupt return failure retains a non drainable borrow", HostLane.Fast, 20)]
    private static void EarlyStopReadFailures()
    {
        foreach (bool pending in new[] { false, true })
        {
            using Owner probe = new();
            Timer sample = probe.Prepare();
            Arm(ref sample);
            if (pending)
                Deliver(probe.M, Model.Delivery.Requested);
            uint before = probe.M->Reads;
            HostCheck.Equal(pending ? Timer.Status.PendingInterrupt : Timer.Status.Ok, sample.Restore(Key, 2));
            uint count = probe.M->Reads - before;
            foreach (bool corrupt in new[] { false, true })
                for (uint failure = 1; failure <= count; failure++)
                {
                    using Owner owner = new();
                    Timer timer = owner.Prepare();
                    Arm(ref timer);
                    if (pending)
                        Deliver(owner.M, Model.Delivery.Requested);
                    if (corrupt)
                    {
                        owner.M->CorruptRead = owner.M->Reads + failure;
                        uint index = before + failure - 1;
                        owner.M->CorruptReadValue = probe.M->ReadOffsets[index] == 0x390 ? Reload + 1 : probe.M->ReadValues[index] ^ 1U;
                    }
                    else
                    {
                        owner.M->FailRead = owner.M->Reads + failure;
                        owner.M->FailReadResult = 2; // Only the exact callback result 1 succeeds.
                    }
                    Timer.Status code = timer.Restore(Key, 2);
                    if (corrupt)
                        HostCheck.True(code != Timer.Status.Ok);
                    else
                        HostCheck.Equal(Timer.Status.ReadFailed, code);
                    Retained(in timer, false);
                    HostCheck.True(timer.Ticks == 0 && owner.M->EoiWrites == 0);
                    CannotDrain(ref timer, owner);
                }
        }
    }

    [HostTest("m09.005 every stop write failure including an effected request remains non drainable", HostLane.Fast, 21)]
    private static void EarlyStopWriteFailures()
    {
        foreach (bool pending in new[] { false, true })
            foreach (bool corrupt in new[] { false, true })
                for (uint failure = 1; failure <= (pending ? 2U : 4U); failure++)
                {
                    using Owner owner = new();
                    Timer timer = owner.Prepare();
                    Arm(ref timer);
                    if (pending)
                        Deliver(owner.M, Model.Delivery.Requested);
                    uint write = owner.M->Writes + failure;
                    if (corrupt)
                        owner.M->CorruptWrite = write;
                    else
                    {
                        owner.M->FailWrite = write;
                        owner.M->FailWriteResult = 2;
                    }
                    owner.M->AfterWrite = write;
                    owner.M->InjectDelivery = Model.Delivery.Requested;
                    HostCheck.Equal(corrupt ? Timer.Status.ReadBackFailed : Timer.Status.WriteFailed, timer.Restore(Key, 2));
                    HostCheck.Equal(write, owner.M->Writes);
                    HostCheck.Equal(2U, owner.M->Registers[0x270 / 4]);
                    HostCheck.True(timer.Ticks == 0 && owner.M->EoiWrites == 0);
                    Retained(in timer, false);
                    CannotDrain(ref timer, owner);
                }
    }

    [HostTest("m09.005 stopped IRQ read EOI and post EOI failures cannot acknowledge twice", HostLane.Fast, 22)]
    private static void StoppedInterruptFailures()
    {
        using Owner probe = new();
        StopPending(probe, out Timer sample);
        DeadlineQueue due = probe.Queue();
        Add(ref due, 1, 1);
        Deliver(probe.M, Model.Delivery.InService);
        uint before = probe.M->Reads, writes = probe.M->Writes;
        HostCheck.Equal(Timer.Action.WakeReady, sample.HandleInterrupt(Key, 2, 0x202, ref due, Key, Timer.Phase.Safe).Next);
        uint count = probe.M->Reads - before;
        foreach (bool corrupt in new[] { false, true })
            for (uint failure = 1; failure <= count; failure++)
            {
                using Owner owner = new();
                StopPending(owner, out Timer timer);
                DeadlineQueue queue = owner.Queue();
                Add(ref queue, 1, 1);
                Deliver(owner.M, Model.Delivery.InService);
                uint index = before + failure - 1;
                if (corrupt)
                {
                    owner.M->CorruptRead = owner.M->Reads + failure;
                    owner.M->CorruptReadValue = probe.M->ReadOffsets[index] == 0x390 ? Reload + 1 : probe.M->ReadValues[index] ^ 1U;
                }
                else
                {
                    owner.M->FailRead = owner.M->Reads + failure;
                    owner.M->FailReadResult = 2;
                }
                Timer.Result result = timer.HandleInterrupt(Key, 2, 0x202, ref queue, Key, Timer.Phase.Safe);
                HostCheck.True(!result.Succeeded && result.Next != Timer.Action.WakeReady && timer.Ticks == 0);
                HostCheck.Equal(probe.M->ReadWrites[index] > writes ? 1U : 0U, owner.M->EoiWrites);
                Retained(in timer, false);
                CannotDrain(ref timer, owner);
            }

        using Owner failed = new();
        StopPending(failed, out Timer pending);
        DeadlineQueue retained = failed.Queue();
        Add(ref retained, 1, 1);
        Deliver(failed.M, Model.Delivery.InService);
        failed.M->FailWrite = failed.M->Writes + 1;
        failed.M->FailWriteResult = 2;
        HostCheck.Equal(Timer.Status.WriteFailed, pending.HandleInterrupt(Key, 2, 0x202, ref retained, Key, Timer.Phase.Safe).Code);
        HostCheck.True(failed.M->EoiWrites == 1 && failed.M->Registers[0x170 / 4] == 0 && pending.Ticks == 0);
        Retained(in pending, false);
        CannotDrain(ref pending, failed);
    }

    // These phases model different architectural observations, rather than
    // treating a stopped counter as proof that its request has been accepted.
    // Hooks run even when the callback subsequently reports failure.
    private static void Deliver(Model* model, Model.Delivery delivery)
    {
        if (delivery == Model.Delivery.None)
            return;
        model->Registers[0x320 / 4] &= ~0x1000U;
        model->Registers[0x170 / 4] = 0;
        model->Registers[0x270 / 4] = 0;
        if (delivery != Model.Delivery.Idle)
            model->Registers[0x390 / 4] = 0;
        if (delivery == Model.Delivery.SendPending)
            model->Registers[0x320 / 4] |= 0x1000;
        else if (delivery == Model.Delivery.Requested)
            model->Registers[0x270 / 4] = 2;
        else if (delivery == Model.Delivery.InService)
            model->Registers[0x170 / 4] = 2;
    }
}
