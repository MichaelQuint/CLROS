namespace OS.Pal;

public readonly struct OSHostWaitTableEntry
{
    public readonly OSHostHandle Waitable;
    public readonly OSHostWaitableKind Kind;
    public readonly bool Signaled;
    public readonly bool Canceled;
    public OSHostWaitTableEntry(OSHostHandle waitable, OSHostWaitableKind kind, bool signaled, bool canceled)
    {
        Waitable = waitable;
        Kind = kind;
        Signaled = signaled;
        Canceled = canceled;
    }
}
