using OS.Pal;

namespace OS.Kernel.Threading;
public unsafe partial struct DeadlineQueue
{
    /// <summary>Completed drain result; default is not success evidence. Count0
    /// means no entry expired in this call, not that the queue/timer is empty/live.</summary>

    public readonly struct DrainResult
    {
        public readonly Status Code;
        public readonly uint Count;
        private readonly bool _completed;
        public bool Succeeded => _completed && Code == Status.Ok;

        internal DrainResult(Status code, uint count = 0)
        {
            Code = code;
            Count = count;
            _completed = true;
        }
    }
}
