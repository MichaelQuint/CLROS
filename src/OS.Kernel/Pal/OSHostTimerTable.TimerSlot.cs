namespace OS.Pal;
public sealed partial class OSHostTimerTable
{
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

        public void Arm(ulong dueTicks, ulong periodTicks, OSHostHandle completionWaitHandle, bool periodic)
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
