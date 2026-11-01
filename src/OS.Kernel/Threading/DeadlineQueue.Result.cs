using OS.Pal;

namespace OS.Kernel.Threading;
public unsafe partial struct DeadlineQueue
{
    /// <summary>Earliest entry by value. Default/failure is not a valid entry.</summary>

    public readonly struct Result
    {
        public readonly Status Code;
        public readonly Entry Value;
        public bool Succeeded => Code == Status.Ok && Value.Identity.IsValid;

        internal Result(Status code, Entry value = default)
        {
            Code = code;
            Value = value;
        }
    }
}
