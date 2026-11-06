namespace OS.Kernel.Diagnostics;

public readonly struct GuardedIstStackDescriptor
{
    internal GuardedIstStackDescriptor(GuardedIstStackKind kind, byte istIndex, ulong guardBaseAddress, ulong guardSizeBytes, ulong stackBaseAddress, ulong stackSizeBytes, ulong stackTopAddress)
    {
        IsInitialized = true;
        Kind = kind;
        IstIndex = istIndex;
        GuardBaseAddress = guardBaseAddress;
        GuardSizeBytes = guardSizeBytes;
        StackBaseAddress = stackBaseAddress;
        StackSizeBytes = stackSizeBytes;
        StackTopAddress = stackTopAddress;
        GuardPagePresent = false;
        StackWritable = true;
        StackExecutable = false;
    }

    public bool IsInitialized
    {
        get;
    }
    public GuardedIstStackKind Kind
    {
        get;
    }
    public byte IstIndex
    {
        get;
    }
    public ulong GuardBaseAddress
    {
        get;
    }
    public ulong GuardSizeBytes
    {
        get;
    }
    public ulong StackBaseAddress
    {
        get;
    }
    public ulong StackSizeBytes
    {
        get;
    }
    public ulong StackTopAddress
    {
        get;
    }
    public bool GuardPagePresent
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
    public bool IsUsable => IsInitialized && IstIndex >= GuardedISTStacksDoubleFaultPageFaultNMI.DoubleFaultIstIndex && IstIndex <= GuardedISTStacksDoubleFaultPageFaultNMI.NmiIstIndex && GuardBaseAddress != 0 && GuardSizeBytes != 0 && StackBaseAddress == GuardBaseAddress + GuardSizeBytes && StackSizeBytes != 0 && StackTopAddress == StackBaseAddress + StackSizeBytes && !GuardPagePresent && StackWritable && !StackExecutable;
}
