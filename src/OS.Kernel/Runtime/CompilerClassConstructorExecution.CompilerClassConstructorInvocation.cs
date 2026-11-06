namespace OS.Kernel.Runtime;
/// <summary>
/// One caller-retained invocation over the original bound storage. Default is
/// noninvoking. Numeric identity checks reject stale or changed receipts; this
/// ticket does not authenticate forged/reset storage or its mapping/lifetime.
/// </summary>

public readonly struct CompilerClassConstructorInvocation
{
    public readonly ulong Storage, Capacity, Context, Target;
    public readonly uint RecordIndex, ParentIndex;
    public bool RequiresInvocation => Target != 0;

    internal CompilerClassConstructorInvocation(ulong storage, ulong capacity, ulong context, ulong target, uint recordIndex, uint parentIndex)
    {
        Storage = storage;
        Capacity = capacity;
        Context = context;
        Target = target;
        RecordIndex = recordIndex;
        ParentIndex = parentIndex;
    }
}
