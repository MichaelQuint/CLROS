namespace OS.Kernel.Runtime;

[Flags]
public enum ExecutableStubMemoryProtection : byte
{
    None = 0,
    Read = 1,
    Write = 2,
    Execute = 4
}

[Flags]
public enum ExecutableStubMemoryPlanFlags : byte
{
    None = 0,
    AllowTemporaryWritableExecutableException = 1
}

public enum ExecutableStubMemoryPlanStatus
{
    Success = 0,
    InvalidPayloadLength = 1,
    InvalidPageSize = 2,
    InvalidAlignment = 3,
    AlignmentGreaterThanPageSize = 4,
    AccidentalWritableExecutable = 5,
    PayloadLengthOverflow = 6
}

public readonly struct ExecutableStubMemoryPlanResult
{
    private ExecutableStubMemoryPlanResult(
        bool isInitialized,
        ExecutableStubMemoryPlanStatus status,
        nuint requestedPayloadLength,
        uint pageSize,
        uint alignment,
        nuint payloadOffset,
        nuint requiredByteLength,
        ExecutableStubMemoryProtection writePhaseProtection,
        ExecutableStubMemoryProtection executePhaseProtection,
        ExecutableStubMemoryProtection finalProtection,
        ExecutableStubMemoryPlanFlags flags)
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

    public bool IsInitialized { get; }

    public ExecutableStubMemoryPlanStatus Status { get; }

    public bool Succeeded => IsInitialized && Status == ExecutableStubMemoryPlanStatus.Success;

    public nuint RequestedPayloadLength { get; }

    public uint PageSize { get; }

    public uint Alignment { get; }

    public nuint PayloadOffset { get; }

    public nuint RequiredByteLength { get; }

    public ExecutableStubMemoryProtection WritePhaseProtection { get; }

    public ExecutableStubMemoryProtection ExecutePhaseProtection { get; }

    public ExecutableStubMemoryProtection FinalProtection { get; }

    public ExecutableStubMemoryPlanFlags Flags { get; }

    public static ExecutableStubMemoryPlanResult Create(
        ExecutableStubMemoryPlanStatus status,
        nuint requestedPayloadLength,
        uint pageSize,
        uint alignment,
        nuint payloadOffset,
        nuint requiredByteLength,
        ExecutableStubMemoryProtection writePhaseProtection,
        ExecutableStubMemoryProtection executePhaseProtection,
        ExecutableStubMemoryProtection finalProtection,
        ExecutableStubMemoryPlanFlags flags)
    {
        return new ExecutableStubMemoryPlanResult(
            true,
            status,
            requestedPayloadLength,
            pageSize,
            alignment,
            payloadOffset,
            requiredByteLength,
            writePhaseProtection,
            executePhaseProtection,
            finalProtection,
            flags);
    }
}

public static class ExecutableStubMemoryPlan
{
    public const uint MinimumPageSize = 4096;
    public const uint DefaultPageSize = 4096;
    public const uint DefaultAlignment = 16;
    public const nuint MetadataPrefixByteLength = 16;

    public static ExecutableStubMemoryPlanResult Create(
        nuint payloadLength,
        uint pageSize = DefaultPageSize,
        uint alignment = DefaultAlignment)
    {
        return Create(
            payloadLength,
            pageSize,
            alignment,
            ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Write,
            ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute,
            ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute,
            ExecutableStubMemoryPlanFlags.None);
    }

    public static ExecutableStubMemoryPlanResult Create(
        nuint payloadLength,
        uint pageSize,
        uint alignment,
        ExecutableStubMemoryProtection writePhaseProtection,
        ExecutableStubMemoryProtection executePhaseProtection,
        ExecutableStubMemoryProtection finalProtection,
        ExecutableStubMemoryPlanFlags flags)
    {
        ExecutableStubMemoryPlanStatus status = Validate(
            payloadLength,
            pageSize,
            alignment,
            writePhaseProtection,
            executePhaseProtection,
            finalProtection,
            flags);
        if (status != ExecutableStubMemoryPlanStatus.Success)
        {
            return ExecutableStubMemoryPlanResult.Create(
                status,
                payloadLength,
                pageSize,
                alignment,
                0,
                0,
                writePhaseProtection,
                executePhaseProtection,
                finalProtection,
                flags);
        }

        nuint payloadOffset = AlignUp(MetadataPrefixByteLength, alignment);
        nuint unalignedLength = payloadOffset + payloadLength;
        if (unalignedLength < payloadOffset)
        {
            return ExecutableStubMemoryPlanResult.Create(
                ExecutableStubMemoryPlanStatus.PayloadLengthOverflow,
                payloadLength,
                pageSize,
                alignment,
                0,
                0,
                writePhaseProtection,
                executePhaseProtection,
                finalProtection,
                flags);
        }

        nuint requiredByteLength = AlignUp(unalignedLength, pageSize);
        if (requiredByteLength < unalignedLength)
        {
            return ExecutableStubMemoryPlanResult.Create(
                ExecutableStubMemoryPlanStatus.PayloadLengthOverflow,
                payloadLength,
                pageSize,
                alignment,
                0,
                0,
                writePhaseProtection,
                executePhaseProtection,
                finalProtection,
                flags);
        }

        return ExecutableStubMemoryPlanResult.Create(
            ExecutableStubMemoryPlanStatus.Success,
            payloadLength,
            pageSize,
            alignment,
            payloadOffset,
            requiredByteLength,
            writePhaseProtection,
            executePhaseProtection,
            finalProtection,
            flags);
    }

    public static ExecutableStubMemoryPlanStatus Validate(
        nuint payloadLength,
        uint pageSize,
        uint alignment,
        ExecutableStubMemoryProtection writePhaseProtection,
        ExecutableStubMemoryProtection executePhaseProtection,
        ExecutableStubMemoryProtection finalProtection,
        ExecutableStubMemoryPlanFlags flags)
    {
        if (payloadLength == 0)
        {
            return ExecutableStubMemoryPlanStatus.InvalidPayloadLength;
        }

        if (pageSize < MinimumPageSize || !IsPowerOfTwo(pageSize))
        {
            return ExecutableStubMemoryPlanStatus.InvalidPageSize;
        }

        if (alignment == 0 || !IsPowerOfTwo(alignment))
        {
            return ExecutableStubMemoryPlanStatus.InvalidAlignment;
        }

        if (alignment > pageSize)
        {
            return ExecutableStubMemoryPlanStatus.AlignmentGreaterThanPageSize;
        }

        if (!AllowsWritableExecutable(flags) &&
            (IsWritableExecutable(writePhaseProtection) ||
             IsWritableExecutable(executePhaseProtection) ||
             IsWritableExecutable(finalProtection)))
        {
            return ExecutableStubMemoryPlanStatus.AccidentalWritableExecutable;
        }

        return ExecutableStubMemoryPlanStatus.Success;
    }

    public static bool IsWritableExecutable(ExecutableStubMemoryProtection protection)
    {
        return (protection & ExecutableStubMemoryProtection.Write) != 0
            && (protection & ExecutableStubMemoryProtection.Execute) != 0;
    }

    public static nuint AlignUp(nuint value, uint alignment)
    {
        nuint mask = (nuint)alignment - 1;
        return (value + mask) & ~mask;
    }

    private static bool AllowsWritableExecutable(ExecutableStubMemoryPlanFlags flags)
    {
        return (flags & ExecutableStubMemoryPlanFlags.AllowTemporaryWritableExecutableException) != 0;
    }

    private static bool IsPowerOfTwo(uint value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }
}
