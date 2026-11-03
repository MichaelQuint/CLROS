namespace OS.Kernel.Diagnostics;

public readonly struct GuardedIstStackArenaRequest
{
    public GuardedIstStackArenaRequest(ulong arenaBaseAddress, ulong arenaSizeBytes, ulong stackSizeBytes, ulong guardSizeBytes, ulong ownerToken, bool stackWritable, bool stackExecutable, ulong reservedBaseAddress = 0, ulong reservedSizeBytes = 0)
    {
        ArenaBaseAddress = arenaBaseAddress;
        ArenaSizeBytes = arenaSizeBytes;
        StackSizeBytes = stackSizeBytes;
        GuardSizeBytes = guardSizeBytes;
        OwnerToken = ownerToken;
        StackWritable = stackWritable;
        StackExecutable = stackExecutable;
        ReservedBaseAddress = reservedBaseAddress;
        ReservedSizeBytes = reservedSizeBytes;
    }

    public ulong ArenaBaseAddress
    {
        get;
    }
    public ulong ArenaSizeBytes
    {
        get;
    }
    public ulong StackSizeBytes
    {
        get;
    }
    public ulong GuardSizeBytes
    {
        get;
    }
    public ulong OwnerToken
    {
        get;
    }
    public bool StackWritable
    {
        get;
    }
    public bool StackExecutable
    {
        get;
    }
    public ulong ReservedBaseAddress
    {
        get;
    }
    public ulong ReservedSizeBytes
    {
        get;
    }
}
