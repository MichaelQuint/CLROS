using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct ReadyQueue
{
    /// <summary>Read/removal result by value; failure has no identity. A default
    /// result is not success evidence. No output reference can alias queue storage.</summary>

    public readonly struct Result
    {
        public readonly Status Code;
        public readonly OSHostHandle Identity;
        public bool Succeeded => Code == Status.Ok && Identity.IsValid;

        internal Result(Status code, OSHostHandle identity = default)
        {
            Code = code;
            Identity = identity;
        }
    }
}
