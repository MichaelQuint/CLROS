namespace OS.Pal;

public enum OSHostTimerLifecycleState : uint
{
    Closed = 0,
    Created = 1,
    Armed = 2
}

public enum OSHostTimeoutState : uint
{
    Infinite = 0,
    Pending = 1,
    Expired = 2
}

public readonly struct OSHostTimeoutEvaluationResult
{
    public readonly OSHostStatus Status;

    public readonly OSHostTimeoutState State;

    public readonly ulong DeadlineTicks;

    public readonly ulong RemainingTicks;

    public OSHostTimeoutEvaluationResult(
        OSHostStatus status,
        OSHostTimeoutState state,
        ulong deadlineTicks,
        ulong remainingTicks,
        bool isInitialized = true)
    {
        Status = status;
        State = state;
        DeadlineTicks = deadlineTicks;
        RemainingTicks = remainingTicks;
        IsInitialized = isInitialized;
    }

    public readonly bool IsInitialized;

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public bool IsExpired => State == OSHostTimeoutState.Expired;

    public static OSHostTimeoutEvaluationResult FromStatus(OSHostStatus status)
    {
        return new OSHostTimeoutEvaluationResult(
            status,
            OSHostTimeoutState.Expired,
            deadlineTicks: 0,
            remainingTicks: 0);
    }
}

public readonly struct OSHostTimerCreateResult
{
    public readonly OSHostStatus Status;

    public readonly OSHostHandle Timer;

    public OSHostTimerCreateResult(
        OSHostStatus status,
        OSHostHandle timer,
        bool isInitialized = true)
    {
        Status = status;
        Timer = timer;
        IsInitialized = isInitialized;
    }

    public readonly bool IsInitialized;

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostTimerCreateResult FromStatus(OSHostStatus status)
    {
        return new OSHostTimerCreateResult(status, default);
    }
}

public readonly struct OSHostTimerPollResult
{
    public readonly OSHostStatus Status;

    public readonly ulong FiredCount;

    public readonly ulong NextDueTicks;

    public readonly OSHostHandle CompletionWaitHandle;

    public readonly bool Signaled;

    public OSHostTimerPollResult(
        OSHostStatus status,
        ulong firedCount,
        ulong nextDueTicks,
        OSHostHandle completionWaitHandle,
        bool signaled,
        bool isInitialized = true)
    {
        Status = status;
        FiredCount = firedCount;
        NextDueTicks = nextDueTicks;
        CompletionWaitHandle = completionWaitHandle;
        Signaled = signaled;
        IsInitialized = isInitialized;
    }

    public readonly bool IsInitialized;

    public bool Succeeded => IsInitialized && Status == OSHostStatus.Ok;

    public static OSHostTimerPollResult FromStatus(OSHostStatus status)
    {
        return new OSHostTimerPollResult(status, 0, 0, default, signaled: false);
    }
}

public readonly struct OSHostTimerTableEntry
{
    public readonly OSHostHandle Timer;

    public readonly OSHostTimerLifecycleState State;

    public readonly ulong DueTicks;

    public readonly ulong PeriodTicks;

    public readonly ulong TotalFiredCount;

    public readonly OSHostHandle CompletionWaitHandle;

    public readonly bool Periodic;

    public OSHostTimerTableEntry(
        OSHostHandle timer,
        OSHostTimerLifecycleState state,
        ulong dueTicks,
        ulong periodTicks,
        ulong totalFiredCount,
        OSHostHandle completionWaitHandle,
        bool periodic)
    {
        Timer = timer;
        State = state;
        DueTicks = dueTicks;
        PeriodTicks = periodTicks;
        TotalFiredCount = totalFiredCount;
        CompletionWaitHandle = completionWaitHandle;
        Periodic = periodic;
    }
}

public sealed class OSHostTimerTable
{
    public const uint TimerOptionsVersion = 1;

    public const uint TimerFlagPeriodic = 1;

    private readonly TimerSlot[] slots;

    private ulong currentTicks;

    public OSHostTimerTable(ulong tickFrequency, int capacity)
    {
        TickFrequency = tickFrequency == 0 ? 1 : tickFrequency;
        slots = capacity > 0
            ? new TimerSlot[capacity]
            : [];
    }

    public ulong TickFrequency { get; }

    public ulong CurrentTicks => currentTicks;

    public int Capacity => slots.Length;

    public int ActiveCount
    {
        get
        {
            int count = 0;
            for (int index = 0; index < slots.Length; index++)
            {
                if (slots[index].InUse)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public OSHostTimeSnapshot GetSnapshot(long wallClockUnixNanoseconds = 0)
    {
        return new OSHostTimeSnapshot(
            currentTicks,
            TickFrequency,
            wallClockUnixNanoseconds);
    }

    public OSHostTimeoutEvaluationResult EvaluateTimeout(OSHostTimeout timeout)
    {
        if (!OSHostSocketTimeouts.IsValid(timeout))
        {
            return OSHostTimeoutEvaluationResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        return timeout.Kind switch
        {
            OSHostSocketConstants.TimeoutKindInfinite => new OSHostTimeoutEvaluationResult(
                OSHostStatus.Ok,
                OSHostTimeoutState.Infinite,
                deadlineTicks: 0,
                remainingTicks: ulong.MaxValue),
            OSHostSocketConstants.TimeoutKindImmediate => new OSHostTimeoutEvaluationResult(
                OSHostStatus.Ok,
                OSHostTimeoutState.Expired,
                deadlineTicks: currentTicks,
                remainingTicks: 0),
            OSHostSocketConstants.TimeoutKindRelativeTicks => EvaluateRelativeTimeout(timeout),
            OSHostSocketConstants.TimeoutKindAbsoluteTicks => EvaluateAbsoluteTimeout(timeout),
            _ => OSHostTimeoutEvaluationResult.FromStatus(OSHostStatus.InvalidArgument)
        };
    }

    public OSHostStatus AdvanceTo(ulong ticks)
    {
        if (ticks < currentTicks)
        {
            return OSHostStatus.InvalidArgument;
        }

        currentTicks = ticks;
        return OSHostStatus.Ok;
    }

    public OSHostStatus AdvanceBy(ulong ticks)
    {
        if (ulong.MaxValue - currentTicks < ticks)
        {
            return OSHostStatus.InvalidArgument;
        }

        currentTicks += ticks;
        return OSHostStatus.Ok;
    }

    public OSHostTimerCreateResult Create()
    {
        int slotIndex = FindFreeSlot();
        if (slotIndex < 0)
        {
            return OSHostTimerCreateResult.FromStatus(OSHostStatus.OutOfMemory);
        }

        uint generation = NextGeneration(slots[slotIndex].Generation);
        OSHostHandle handle = CreateHandle(slotIndex, generation);
        slots[slotIndex] = TimerSlot.Create(handle, generation);
        return new OSHostTimerCreateResult(OSHostStatus.Ok, handle);
    }

    public OSHostTimerCreateResult Create(OSHostTimerOptions options)
    {
        OSHostTimerCreateResult create = Create();
        if (!create.Succeeded)
        {
            return create;
        }

        OSHostStatus armStatus = Arm(create.Timer, options);
        if (armStatus != OSHostStatus.Ok)
        {
            Close(create.Timer);
            return OSHostTimerCreateResult.FromStatus(armStatus);
        }

        return create;
    }

    public OSHostStatus Arm(OSHostHandle timer, OSHostTimerOptions options)
    {
        OSHostStatus status = TryFindSlot(timer, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return status;
        }

        if (!ValidateOptions(options))
        {
            return OSHostStatus.InvalidArgument;
        }

        slots[slotIndex].Arm(
            options.DueTicks,
            options.PeriodTicks,
            options.CompletionWaitHandle,
            (options.Flags & TimerFlagPeriodic) != 0);
        return OSHostStatus.Ok;
    }

    public OSHostStatus Cancel(OSHostHandle timer)
    {
        OSHostStatus status = TryFindSlot(timer, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return status;
        }

        slots[slotIndex].Cancel();
        return OSHostStatus.Ok;
    }

    public OSHostTimerPollResult Poll(OSHostHandle timer)
    {
        OSHostStatus status = TryFindSlot(timer, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return OSHostTimerPollResult.FromStatus(status);
        }

        TimerSlot slot = slots[slotIndex];
        if (slot.State != OSHostTimerLifecycleState.Armed)
        {
            return new OSHostTimerPollResult(
                OSHostStatus.Ok,
                0,
                slot.DueTicks,
                slot.CompletionWaitHandle,
                signaled: false);
        }

        if (currentTicks < slot.DueTicks)
        {
            return new OSHostTimerPollResult(
                OSHostStatus.Ok,
                0,
                slot.DueTicks,
                slot.CompletionWaitHandle,
                signaled: false);
        }

        ulong firedCount = 1;
        if (slot.Periodic)
        {
            firedCount += (currentTicks - slot.DueTicks) / slot.PeriodTicks;
        }

        OSHostHandle completionWaitHandle = slot.CompletionWaitHandle;
        slots[slotIndex].RecordFired(firedCount);
        return new OSHostTimerPollResult(
            OSHostStatus.Ok,
            firedCount,
            slots[slotIndex].DueTicks,
            completionWaitHandle,
            signaled: true);
    }

    public OSHostStatus Close(OSHostHandle timer)
    {
        OSHostStatus status = TryFindSlot(timer, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            return status;
        }

        uint generation = slots[slotIndex].Generation;
        slots[slotIndex] = new TimerSlot
        {
            Generation = generation
        };
        return OSHostStatus.Ok;
    }

    public OSHostStatus TryGetEntry(OSHostHandle timer, out OSHostTimerTableEntry entry)
    {
        OSHostStatus status = TryFindSlot(timer, out int slotIndex);
        if (status != OSHostStatus.Ok)
        {
            entry = default;
            return status;
        }

        TimerSlot slot = slots[slotIndex];
        entry = new OSHostTimerTableEntry(
            slot.Timer,
            slot.State,
            slot.DueTicks,
            slot.PeriodTicks,
            slot.TotalFiredCount,
            slot.CompletionWaitHandle,
            slot.Periodic);
        return OSHostStatus.Ok;
    }

    private static bool ValidateOptions(OSHostTimerOptions options)
    {
        if (options.Version != TimerOptionsVersion ||
            (options.Flags & ~TimerFlagPeriodic) != 0 ||
            options.DueTicks == 0)
        {
            return false;
        }

        bool periodic = (options.Flags & TimerFlagPeriodic) != 0;
        return periodic ? options.PeriodTicks != 0 : options.PeriodTicks == 0;
    }

    private OSHostTimeoutEvaluationResult EvaluateRelativeTimeout(OSHostTimeout timeout)
    {
        if (timeout.TickFrequency != TickFrequency)
        {
            return OSHostTimeoutEvaluationResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        ulong deadlineTicks = AddSaturating(currentTicks, timeout.Ticks);
        return timeout.Ticks == 0
            ? new OSHostTimeoutEvaluationResult(
                OSHostStatus.Ok,
                OSHostTimeoutState.Expired,
                deadlineTicks,
                remainingTicks: 0)
            : new OSHostTimeoutEvaluationResult(
                OSHostStatus.Ok,
                OSHostTimeoutState.Pending,
                deadlineTicks,
                timeout.Ticks);
    }

    private OSHostTimeoutEvaluationResult EvaluateAbsoluteTimeout(OSHostTimeout timeout)
    {
        if (timeout.TickFrequency != TickFrequency)
        {
            return OSHostTimeoutEvaluationResult.FromStatus(OSHostStatus.InvalidArgument);
        }

        if (currentTicks >= timeout.Ticks)
        {
            return new OSHostTimeoutEvaluationResult(
                OSHostStatus.Ok,
                OSHostTimeoutState.Expired,
                timeout.Ticks,
                remainingTicks: 0);
        }

        return new OSHostTimeoutEvaluationResult(
            OSHostStatus.Ok,
            OSHostTimeoutState.Pending,
            timeout.Ticks,
            timeout.Ticks - currentTicks);
    }

    private static ulong AddSaturating(ulong left, ulong right)
    {
        return ulong.MaxValue - left < right
            ? ulong.MaxValue
            : left + right;
    }

    private int FindFreeSlot()
    {
        for (int index = 0; index < slots.Length; index++)
        {
            if (!slots[index].InUse)
            {
                return index;
            }
        }

        return -1;
    }

    private OSHostStatus TryFindSlot(OSHostHandle timer, out int slotIndex)
    {
        if (!timer.IsValid)
        {
            slotIndex = -1;
            return OSHostStatus.InvalidArgument;
        }

        uint indexPart = (uint)(timer.Value & 0xFFFF_FFFFUL);
        uint generation = (uint)(timer.Value >> 32);
        if (indexPart == 0 || generation == 0)
        {
            slotIndex = -1;
            return OSHostStatus.InvalidArgument;
        }

        ulong zeroBasedIndex = (ulong)indexPart - 1;
        if (zeroBasedIndex >= (ulong)slots.Length)
        {
            slotIndex = -1;
            return OSHostStatus.InvalidArgument;
        }

        slotIndex = (int)zeroBasedIndex;
        return slots[slotIndex].InUse && slots[slotIndex].Generation == generation
            ? OSHostStatus.Ok
            : OSHostStatus.InvalidArgument;
    }

    private static OSHostHandle CreateHandle(int slotIndex, uint generation)
    {
        return new OSHostHandle(((ulong)generation << 32) | ((uint)slotIndex + 1));
    }

    private static uint NextGeneration(uint previous)
    {
        uint next = previous + 1;
        return next == 0 ? 1 : next;
    }

    private struct TimerSlot
    {
        public bool InUse;

        public uint Generation;

        public OSHostHandle Timer;

        public OSHostTimerLifecycleState State;

        public ulong DueTicks;

        public ulong PeriodTicks;

        public ulong TotalFiredCount;

        public OSHostHandle CompletionWaitHandle;

        public bool Periodic;

        public static TimerSlot Create(OSHostHandle timer, uint generation)
        {
            return new TimerSlot
            {
                InUse = true,
                Generation = generation,
                Timer = timer,
                State = OSHostTimerLifecycleState.Created,
                DueTicks = 0,
                PeriodTicks = 0,
                TotalFiredCount = 0,
                CompletionWaitHandle = default,
                Periodic = false
            };
        }

        public void Arm(
            ulong dueTicks,
            ulong periodTicks,
            OSHostHandle completionWaitHandle,
            bool periodic)
        {
            State = OSHostTimerLifecycleState.Armed;
            DueTicks = dueTicks;
            PeriodTicks = periodTicks;
            TotalFiredCount = 0;
            CompletionWaitHandle = completionWaitHandle;
            Periodic = periodic;
        }

        public void Cancel()
        {
            State = OSHostTimerLifecycleState.Created;
            DueTicks = 0;
            PeriodTicks = 0;
            CompletionWaitHandle = default;
            Periodic = false;
        }

        public void RecordFired(ulong firedCount)
        {
            TotalFiredCount += firedCount;
            if (Periodic)
            {
                DueTicks += firedCount * PeriodTicks;
                return;
            }

            State = OSHostTimerLifecycleState.Created;
            DueTicks = 0;
            CompletionWaitHandle = default;
        }
    }
}
