namespace OS.Boot;
/// <summary>
/// Preparation-only description of the modeled probe-leaf rewrite and the
/// exact one-target invalidation invocation that a later composite transition
/// may consume. The caller-owned leaf is restored before this value can
/// succeed. No instruction is invoked.
/// </summary>

public readonly struct M01LivePagingProtectionPreparation
{
    private readonly M01LivePagingStatus _status;
    private readonly byte _initialLeafRestored;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;
    private readonly OS.Kernel.Memory.X64PageMapResult _mutation;
    private readonly OS.Kernel.Memory.X64InvlpgInvocationPlanResult _invlpgInvocation;
    private readonly ulong _protectedLeafReadback;
    internal M01LivePagingProtectionPreparation(M01LivePagingStatus status, OS.Kernel.Memory.X64PageMapResult mutation, OS.Kernel.Memory.X64InvlpgInvocationPlanResult invlpgInvocation, ulong protectedLeafReadback, bool initialLeafRestored)
    {
        _status = status;
        _initialLeafRestored = initialLeafRestored ? (byte)1 : (byte)0;
        _reserved0 = 0;
        _reserved1 = 0;
        _mutation = mutation;
        _invlpgInvocation = invlpgInvocation;
        _protectedLeafReadback = protectedLeafReadback;
    }

    public M01LivePagingStatus Status => _status;
    public OS.Kernel.Memory.X64PageMapResult Mutation => _mutation;
    public OS.Kernel.Memory.X64InvlpgInvocationPlanResult InvlpgInvocation => _invlpgInvocation;
    public ulong ProtectedLeafReadback => _protectedLeafReadback;
    public bool InitialLeafRestored => _initialLeafRestored == 1;
    public bool Succeeded => _status == M01LivePagingStatus.Success && _initialLeafRestored == 1 && _reserved0 == 0 && _reserved1 == 0 && _mutation.Succeeded && _invlpgInvocation.Succeeded && _protectedLeafReadback == _mutation.ReplacementEntry.RawValue;
}
