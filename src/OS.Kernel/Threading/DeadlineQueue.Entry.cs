using OS.Pal;

namespace OS.Kernel.Threading;

public unsafe partial struct DeadlineQueue
{
    /// <summary>Opaque generation-tagged identity and absolute tick deadline.
    /// The external owner pins the identity until cancellation/drain and separately
    /// validates current lifetime/context before publishing any wake.</summary>

    public readonly struct Entry
    {
        public readonly OSHostHandle Identity;
        public readonly ulong Deadline;
        internal Entry(OSHostHandle identity, ulong deadline)
        {
            Identity = identity;
            Deadline = deadline;
        }
    }
}
