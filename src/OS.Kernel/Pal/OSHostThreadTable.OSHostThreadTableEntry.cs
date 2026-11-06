namespace OS.Pal;

public readonly struct OSHostThreadTableEntry
{
    public readonly OSHostHandle Thread;
    public readonly ulong ThreadId;
    public readonly OSHostThreadLifecycleState State;
    public readonly OSHostThreadCreateOptions Options;
    public readonly int ExitCode;
    public OSHostThreadTableEntry(OSHostHandle thread, ulong threadId, OSHostThreadLifecycleState state, OSHostThreadCreateOptions options, int exitCode)
    {
        Thread = thread;
        ThreadId = threadId;
        State = state;
        Options = options;
        ExitCode = exitCode;
    }
}
