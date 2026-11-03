namespace OS.Kernel.Diagnostics;

public readonly struct BootEventRecord
{
    private BootEventRecord(bool isInitialized, ulong sequence, BootEventRecordKind kind, uint phase, uint eventId, ulong arg0, ulong arg1, ulong arg2)
    {
        IsInitialized = isInitialized;
        Sequence = sequence;
        Kind = kind;
        Phase = phase;
        EventId = eventId;
        Arg0 = arg0;
        Arg1 = arg1;
        Arg2 = arg2;
    }

    public bool IsInitialized
    {
        get;
    }
    public ulong Sequence
    {
        get;
    }
    public BootEventRecordKind Kind
    {
        get;
    }
    public uint Phase
    {
        get;
    }
    public uint EventId
    {
        get;
    }
    public ulong Arg0
    {
        get;
    }
    public ulong Arg1
    {
        get;
    }
    public ulong Arg2
    {
        get;
    }

    public static BootEventRecord Create(ulong sequence, BootEventRecordKind kind, uint phase, uint eventId, ulong arg0, ulong arg1, ulong arg2)
    {
        return new BootEventRecord(true, sequence, kind, phase, eventId, arg0, arg1, arg2);
    }
}
