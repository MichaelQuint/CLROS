using OS.Manifest;

namespace OS.Kernel.Runtime;
public static class NativeAppExecutableMemoryPlan
{
    public const uint MinimumPageSize = 4096;
    public const uint DefaultPageSize = 4096;
    public const uint DefaultExecutableAlignment = 16;
    public static NativeAppExecutableMemoryPlanResult Create(NativeAppPayloadMappingPlanResult payloadMappingPlan, uint pageSize = DefaultPageSize, uint executableAlignment = DefaultExecutableAlignment)
    {
        return Create(payloadMappingPlan, pageSize, executableAlignment, NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Write, NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Execute, NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Execute);
    }

    public static NativeAppExecutableMemoryPlanResult Create(NativeAppPayloadMappingPlanResult payloadMappingPlan, uint pageSize, uint executableAlignment, NativeAppExecutableMemoryProtection writableStagingProtection, NativeAppExecutableMemoryProtection executableProtection, NativeAppExecutableMemoryProtection finalProtection)
    {
        if (!payloadMappingPlan.Succeeded)
        {
            return NativeAppExecutableMemoryPlanResult.Fail(NativeAppExecutableMemoryPlanStatus.MappingPlanFailed, payloadMappingPlan.Status, payloadMappingPlan.DescriptorStatus, payloadMappingPlan.PayloadHeaderStatus, pageSize, executableAlignment, writableStagingProtection, executableProtection, finalProtection);
        }

        return Create(payloadMappingPlan.Plan, payloadMappingPlan.Status, payloadMappingPlan.DescriptorStatus, payloadMappingPlan.PayloadHeaderStatus, pageSize, executableAlignment, writableStagingProtection, executableProtection, finalProtection);
    }

    public static NativeAppExecutableMemoryPlanResult Create(NativeAppPayloadMappingPlan payloadMappingPlan, uint pageSize = DefaultPageSize, uint executableAlignment = DefaultExecutableAlignment)
    {
        return Create(payloadMappingPlan, NativeAppPayloadMappingPlanStatus.Success, NativeAppLaunchDescriptorStatus.Success, NativeAppPayloadHeaderStatus.Success, pageSize, executableAlignment, NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Write, NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Execute, NativeAppExecutableMemoryProtection.Read | NativeAppExecutableMemoryProtection.Execute);
    }

    public static NativeAppExecutableMemoryPlanResult Create(NativeAppPayloadMappingPlan payloadMappingPlan, uint pageSize, uint executableAlignment, NativeAppExecutableMemoryProtection writableStagingProtection, NativeAppExecutableMemoryProtection executableProtection, NativeAppExecutableMemoryProtection finalProtection)
    {
        return Create(payloadMappingPlan, NativeAppPayloadMappingPlanStatus.Success, NativeAppLaunchDescriptorStatus.Success, NativeAppPayloadHeaderStatus.Success, pageSize, executableAlignment, writableStagingProtection, executableProtection, finalProtection);
    }

    private static NativeAppExecutableMemoryPlanResult Create(NativeAppPayloadMappingPlan payloadMappingPlan, NativeAppPayloadMappingPlanStatus payloadMappingStatus, NativeAppLaunchDescriptorStatus descriptorStatus, NativeAppPayloadHeaderStatus payloadHeaderStatus, uint pageSize, uint executableAlignment, NativeAppExecutableMemoryProtection writableStagingProtection, NativeAppExecutableMemoryProtection executableProtection, NativeAppExecutableMemoryProtection finalProtection)
    {
        NativeAppExecutableMemoryPlanStatus status = Validate(payloadMappingPlan, pageSize, executableAlignment, writableStagingProtection, executableProtection, finalProtection);
        if (status != NativeAppExecutableMemoryPlanStatus.Success)
        {
            return NativeAppExecutableMemoryPlanResult.Fail(status, payloadMappingStatus, descriptorStatus, payloadHeaderStatus, pageSize, executableAlignment, writableStagingProtection, executableProtection, finalProtection);
        }

        ulong executableMapOffsetInPackage = AlignDown(payloadMappingPlan.ExecutableOffsetInPackage, pageSize);
        ulong executableContentOffsetInMapping = payloadMappingPlan.ExecutableOffsetInPackage - executableMapOffsetInPackage;
        if (!TryAdd(executableContentOffsetInMapping, payloadMappingPlan.ExecutableLength, out ulong unalignedMapByteLength) || !TryAlignUp(unalignedMapByteLength, pageSize, out ulong executableMapByteLength) || !TryAdd(executableMapOffsetInPackage, executableMapByteLength, out ulong executableMapEndOffsetInPackage) || !TryAdd(executableContentOffsetInMapping, payloadMappingPlan.EntryOffsetInExecutable, out ulong entryOffsetInExecutableMapping))
        {
            return NativeAppExecutableMemoryPlanResult.Fail(NativeAppExecutableMemoryPlanStatus.RangeOverflow, payloadMappingStatus, descriptorStatus, payloadHeaderStatus, pageSize, executableAlignment, writableStagingProtection, executableProtection, finalProtection);
        }

        if (entryOffsetInExecutableMapping >= executableMapByteLength)
        {
            return NativeAppExecutableMemoryPlanResult.Fail(NativeAppExecutableMemoryPlanStatus.InvalidEntryOffset, payloadMappingStatus, descriptorStatus, payloadHeaderStatus, pageSize, executableAlignment, writableStagingProtection, executableProtection, finalProtection);
        }

        return NativeAppExecutableMemoryPlanResult.Success(payloadMappingStatus, descriptorStatus, payloadHeaderStatus, pageSize, executableAlignment, executableMapByteLength, payloadMappingPlan.ExecutableLength, executableMapOffsetInPackage, executableMapByteLength, executableMapEndOffsetInPackage, executableContentOffsetInMapping, payloadMappingPlan.EntryOffsetInExecutable, entryOffsetInExecutableMapping, writableStagingProtection, executableProtection, finalProtection);
    }

    public static NativeAppExecutableMemoryPlanStatus Validate(NativeAppPayloadMappingPlan payloadMappingPlan, uint pageSize, uint executableAlignment, NativeAppExecutableMemoryProtection writableStagingProtection, NativeAppExecutableMemoryProtection executableProtection, NativeAppExecutableMemoryProtection finalProtection)
    {
        if (!HasValidMappingShape(payloadMappingPlan))
        {
            return NativeAppExecutableMemoryPlanStatus.InvalidPayloadMappingPlan;
        }

        if (payloadMappingPlan.ExecutableLength == 0)
        {
            return NativeAppExecutableMemoryPlanStatus.ZeroExecutableContent;
        }

        if (payloadMappingPlan.EntryOffsetInExecutable >= payloadMappingPlan.ExecutableLength)
        {
            return NativeAppExecutableMemoryPlanStatus.InvalidEntryOffset;
        }

        if (pageSize < MinimumPageSize || !IsPowerOfTwo(pageSize))
        {
            return NativeAppExecutableMemoryPlanStatus.InvalidPageSize;
        }

        if (executableAlignment == 0 || !IsPowerOfTwo(executableAlignment))
        {
            return NativeAppExecutableMemoryPlanStatus.InvalidAlignment;
        }

        if (executableAlignment > pageSize)
        {
            return NativeAppExecutableMemoryPlanStatus.AlignmentGreaterThanPageSize;
        }

        if (!IsAligned(payloadMappingPlan.ExecutableOffsetInPackage, executableAlignment))
        {
            return NativeAppExecutableMemoryPlanStatus.UnalignedExecutableOffset;
        }

        if (IsWritableExecutable(writableStagingProtection) || IsWritableExecutable(executableProtection) || IsWritableExecutable(finalProtection))
        {
            return NativeAppExecutableMemoryPlanStatus.AccidentalWritableExecutable;
        }

        if (!IsSafeWriteThenExecutePolicy(writableStagingProtection, executableProtection, finalProtection))
        {
            return NativeAppExecutableMemoryPlanStatus.UnsafeProtectionState;
        }

        return NativeAppExecutableMemoryPlanStatus.Success;
    }

    public static bool IsWritableExecutable(NativeAppExecutableMemoryProtection protection)
    {
        return (protection & NativeAppExecutableMemoryProtection.Write) != 0 && (protection & NativeAppExecutableMemoryProtection.Execute) != 0;
    }

    private static bool HasValidMappingShape(NativeAppPayloadMappingPlan payloadMappingPlan)
    {
        if (payloadMappingPlan.PayloadLength == 0 || payloadMappingPlan.HeaderLength == 0 || payloadMappingPlan.ExecutableOffsetInPayload < payloadMappingPlan.HeaderLength || payloadMappingPlan.ExecutableOffsetInPayload > payloadMappingPlan.PayloadLength || payloadMappingPlan.EntryOffsetInPayload < payloadMappingPlan.ExecutableOffsetInPayload || payloadMappingPlan.EntryOffsetInExecutable != payloadMappingPlan.EntryOffsetInPayload - payloadMappingPlan.ExecutableOffsetInPayload)
        {
            return false;
        }

        if (!TryAdd(payloadMappingPlan.ExecutableOffsetInPayload, payloadMappingPlan.ExecutableLength, out ulong executableEndInPayload) || executableEndInPayload != payloadMappingPlan.PayloadLength)
        {
            return false;
        }

        if (!TryAdd(payloadMappingPlan.PayloadOffsetInPackage, payloadMappingPlan.ExecutableOffsetInPayload, out ulong expectedExecutableOffsetInPackage) || expectedExecutableOffsetInPackage != payloadMappingPlan.ExecutableOffsetInPackage)
        {
            return false;
        }

        if (!TryAdd(payloadMappingPlan.PayloadOffsetInPackage, payloadMappingPlan.EntryOffsetInPayload, out ulong expectedEntryOffsetInPackage) || expectedEntryOffsetInPackage != payloadMappingPlan.EntryOffsetInPackage)
        {
            return false;
        }

        return true;
    }

    private static bool IsSafeWriteThenExecutePolicy(NativeAppExecutableMemoryProtection writableStagingProtection, NativeAppExecutableMemoryProtection executableProtection, NativeAppExecutableMemoryProtection finalProtection)
    {
        return writableStagingProtection != NativeAppExecutableMemoryProtection.None && executableProtection != NativeAppExecutableMemoryProtection.None && finalProtection != NativeAppExecutableMemoryProtection.None && (writableStagingProtection & NativeAppExecutableMemoryProtection.Write) != 0 && (writableStagingProtection & NativeAppExecutableMemoryProtection.Execute) == 0 && (executableProtection & NativeAppExecutableMemoryProtection.Execute) != 0 && (executableProtection & NativeAppExecutableMemoryProtection.Write) == 0 && (finalProtection & NativeAppExecutableMemoryProtection.Execute) != 0 && (finalProtection & NativeAppExecutableMemoryProtection.Write) == 0;
    }

    private static bool IsPowerOfTwo(uint value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }

    private static bool IsAligned(ulong value, uint alignment)
    {
        ulong mask = (ulong)alignment - 1;
        return (value & mask) == 0;
    }

    private static ulong AlignDown(ulong value, uint alignment)
    {
        return value & ~((ulong)alignment - 1);
    }

    private static bool TryAlignUp(ulong value, uint alignment, out ulong aligned)
    {
        ulong mask = (ulong)alignment - 1;
        if (value > ulong.MaxValue - mask)
        {
            aligned = 0;
            return false;
        }

        aligned = (value + mask) & ~mask;
        return true;
    }

    private static bool TryAdd(ulong left, ulong right, out ulong value)
    {
        if (left > ulong.MaxValue - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }
}
