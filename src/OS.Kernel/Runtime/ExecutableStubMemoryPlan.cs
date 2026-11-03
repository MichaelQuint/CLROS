namespace OS.Kernel.Runtime;

public static class ExecutableStubMemoryPlan
{
    public const uint MinimumPageSize = 4096;
    public const uint DefaultPageSize = 4096;
    public const uint DefaultAlignment = 16;
    public const nuint MetadataPrefixByteLength = 16;
    public static ExecutableStubMemoryPlanResult Create(nuint payloadLength, uint pageSize = DefaultPageSize, uint alignment = DefaultAlignment)
    {
        return Create(payloadLength, pageSize, alignment, ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Write, ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute, ExecutableStubMemoryProtection.Read | ExecutableStubMemoryProtection.Execute, ExecutableStubMemoryPlanFlags.None);
    }

    public static ExecutableStubMemoryPlanResult Create(nuint payloadLength, uint pageSize, uint alignment, ExecutableStubMemoryProtection writePhaseProtection, ExecutableStubMemoryProtection executePhaseProtection, ExecutableStubMemoryProtection finalProtection, ExecutableStubMemoryPlanFlags flags)
    {
        ExecutableStubMemoryPlanStatus status = Validate(payloadLength, pageSize, alignment, writePhaseProtection, executePhaseProtection, finalProtection, flags);
        if (status != ExecutableStubMemoryPlanStatus.Success)
        {
            return ExecutableStubMemoryPlanResult.Create(status, payloadLength, pageSize, alignment, 0, 0, writePhaseProtection, executePhaseProtection, finalProtection, flags);
        }

        nuint payloadOffset = AlignUp(MetadataPrefixByteLength, alignment);
        nuint unalignedLength = payloadOffset + payloadLength;
        if (unalignedLength < payloadOffset)
        {
            return ExecutableStubMemoryPlanResult.Create(ExecutableStubMemoryPlanStatus.PayloadLengthOverflow, payloadLength, pageSize, alignment, 0, 0, writePhaseProtection, executePhaseProtection, finalProtection, flags);
        }

        nuint requiredByteLength = AlignUp(unalignedLength, pageSize);
        if (requiredByteLength < unalignedLength)
        {
            return ExecutableStubMemoryPlanResult.Create(ExecutableStubMemoryPlanStatus.PayloadLengthOverflow, payloadLength, pageSize, alignment, 0, 0, writePhaseProtection, executePhaseProtection, finalProtection, flags);
        }

        return ExecutableStubMemoryPlanResult.Create(ExecutableStubMemoryPlanStatus.Success, payloadLength, pageSize, alignment, payloadOffset, requiredByteLength, writePhaseProtection, executePhaseProtection, finalProtection, flags);
    }

    public static ExecutableStubMemoryPlanStatus Validate(nuint payloadLength, uint pageSize, uint alignment, ExecutableStubMemoryProtection writePhaseProtection, ExecutableStubMemoryProtection executePhaseProtection, ExecutableStubMemoryProtection finalProtection, ExecutableStubMemoryPlanFlags flags)
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

        if (!AllowsWritableExecutable(flags) && (IsWritableExecutable(writePhaseProtection) || IsWritableExecutable(executePhaseProtection) || IsWritableExecutable(finalProtection)))
        {
            return ExecutableStubMemoryPlanStatus.AccidentalWritableExecutable;
        }

        return ExecutableStubMemoryPlanStatus.Success;
    }

    public static bool IsWritableExecutable(ExecutableStubMemoryProtection protection)
    {
        return (protection & ExecutableStubMemoryProtection.Write) != 0 && (protection & ExecutableStubMemoryProtection.Execute) != 0;
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
