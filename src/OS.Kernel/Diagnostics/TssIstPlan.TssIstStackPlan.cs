using OS.Std.Binary;

namespace OS.Kernel.Diagnostics;
public readonly struct TssIstStackPlan
{
    public TssIstStackPlan(ulong stackTop, ulong stackSizeBytes, ulong guardSizeBytes)
    {
        StackTop = stackTop;
        StackSizeBytes = stackSizeBytes;
        GuardSizeBytes = guardSizeBytes;
    }

    public ulong StackTop { get; }
    public ulong StackSizeBytes { get; }
    public ulong GuardSizeBytes { get; }
}
