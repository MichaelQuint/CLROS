namespace OS.Kernel.Runtime;

public readonly struct ExecutableStubMemoryPlanResult
{
    private ExecutableStubMemoryPlanResult(bool isInitialized, ExecutableStubMemoryPlanStatus status, nuint requestedPayloadLength, uint pageSize, uint alignment, nuint payloadOffset, nuint requiredByteLength, ExecutableStubMemoryProtection writePhaseProtection, ExecutableStubMemoryProtection executePhaseProtection, ExecutableStubMemoryProtection finalProtection, ExecutableStubMemoryPlanFlags flags)
    {
        IsInitialized = isInitialized;
        Status = status;
        RequestedPayloadLength = requestedPayloadLength;
        PageSize = pageSize;
        Alignment = alignment;
        PayloadOffset = payloadOffset;
        RequiredByteLength = requiredByteLength;
        WritePhaseProtection = writePhaseProtection;
        ExecutePhaseProtection = executePhaseProtection;
        FinalProtection = finalProtection;
        Flags = flags;
    }

    public bool IsInitialized
    {
        get;
    }
    public ExecutableStubMemoryPlanStatus Status
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == ExecutableStubMemoryPlanStatus.Success;
    public nuint RequestedPayloadLength
    {
        get;
    }
    public uint PageSize
    {
        get;
    }
    public uint Alignment
    {
        get;
    }
    public nuint PayloadOffset
    {
        get;
    }
    public nuint RequiredByteLength
    {
        get;
    }
    public ExecutableStubMemoryProtection WritePhaseProtection
    {
        get;
    }
    public ExecutableStubMemoryProtection ExecutePhaseProtection
    {
        get;
    }
    public ExecutableStubMemoryProtection FinalProtection
    {
        get;
    }
    public ExecutableStubMemoryPlanFlags Flags
    {
        get;
    }

    public static ExecutableStubMemoryPlanResult Create(ExecutableStubMemoryPlanStatus status, nuint requestedPayloadLength, uint pageSize, uint alignment, nuint payloadOffset, nuint requiredByteLength, ExecutableStubMemoryProtection writePhaseProtection, ExecutableStubMemoryProtection executePhaseProtection, ExecutableStubMemoryProtection finalProtection, ExecutableStubMemoryPlanFlags flags)
    {
        return new ExecutableStubMemoryPlanResult(true, status, requestedPayloadLength, pageSize, alignment, payloadOffset, requiredByteLength, writePhaseProtection, executePhaseProtection, finalProtection, flags);
    }
}
