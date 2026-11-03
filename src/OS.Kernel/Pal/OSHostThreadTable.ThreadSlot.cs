namespace OS.Pal;

public sealed partial class OSHostThreadTable
{
    private struct ThreadSlot
    {
        public bool InUse;
        public uint Generation;
        public OSHostHandle Thread;
        public ulong ThreadId;
        public OSHostThreadLifecycleState State;
        public OSHostThreadCreateOptions Options;
        public int ExitCode;
        public static ThreadSlot Create(OSHostHandle thread, uint generation, ulong threadId, OSHostThreadCreateOptions options)
        {
            return new ThreadSlot
            {
                InUse = true,
                Generation = generation,
                Thread = thread,
                ThreadId = threadId,
                State = OSHostThreadLifecycleState.Created,
                Options = options,
                ExitCode = 0
            };
        }
    }
}
