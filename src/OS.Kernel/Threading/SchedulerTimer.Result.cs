using OS.Drivers;

namespace OS.Kernel.Threading;

public unsafe partial struct SchedulerTimer
{
    /// <summary>Acknowledged tick and earliest due identity by value. WakeReady
    /// does not drain the queue or grant restore/root authority: the owner must
    /// revalidate its live context, commit the wake, then cancel/drain the entry.
    /// Failure retains the hardware borrow and may have stopped/acknowledged it.</summary>

    public readonly struct Result
    {
        public readonly Status Code;
        public readonly Action Next;
        public readonly ulong Tick;
        public readonly DeadlineQueue.Entry Due;
        public readonly bool Succeeded => Code == Status.Ok && Next != Action.None;

        public Result(Status code, Action next = Action.None, ulong tick = 0, DeadlineQueue.Entry due = default)
        {
            Code = code;
            Next = next;
            Tick = tick;
            Due = due;
        }
    }
}
