namespace OS.Pal;

public static class OSHostMemoryProtectionPlan
{
    public const ulong MinimumPageSize = 4096;
    public const ulong DefaultPageSize = 4096;
    private const OSHostPageProtection KnownProtectionFlags = OSHostPageProtection.Read | OSHostPageProtection.Write | OSHostPageProtection.Execute;
    public static OSHostMemoryProtectionPlanResult Create(ulong baseAddress, ulong byteLength, OSHostPageProtection currentProtection, OSHostPageProtection requestedProtection, ulong pageSizeBytes = DefaultPageSize)
    {
        OSHostMemoryProtectionPlanStatus rangeStatus = ValidateRange(baseAddress, byteLength, pageSizeBytes, out ulong endAddress, out ulong pageCount);
        if (rangeStatus != OSHostMemoryProtectionPlanStatus.Success)
        {
            return OSHostMemoryProtectionPlanResult.Fail(rangeStatus, baseAddress, byteLength, pageSizeBytes, currentProtection, requestedProtection);
        }

        OSHostMemoryProtectionPlanStatus protectionStatus = ValidateProtectionTransition(currentProtection, requestedProtection);
        if (protectionStatus != OSHostMemoryProtectionPlanStatus.Success)
        {
            return OSHostMemoryProtectionPlanResult.Fail(protectionStatus, baseAddress, byteLength, pageSizeBytes, currentProtection, requestedProtection);
        }

        return OSHostMemoryProtectionPlanResult.Success(baseAddress, byteLength, endAddress, pageSizeBytes, pageCount, currentProtection, requestedProtection);
    }

    public static OSHostMemoryProtectionPlanStatus ValidateRange(ulong baseAddress, ulong byteLength, ulong pageSizeBytes)
    {
        return ValidateRange(baseAddress, byteLength, pageSizeBytes, out _, out _);
    }

    public static OSHostMemoryProtectionPlanStatus ValidateProtectionTransition(OSHostPageProtection currentProtection, OSHostPageProtection requestedProtection)
    {
        if (!HasOnlyKnownFlags(currentProtection) || !HasOnlyKnownFlags(requestedProtection))
        {
            return OSHostMemoryProtectionPlanStatus.InvalidProtection;
        }

        if (IsWritableExecutable(currentProtection) || IsWritableExecutable(requestedProtection))
        {
            return OSHostMemoryProtectionPlanStatus.WritableExecutableRejected;
        }

        if (!IsSupportedState(currentProtection) || !IsSupportedState(requestedProtection))
        {
            return OSHostMemoryProtectionPlanStatus.InvalidProtection;
        }

        return IsAllowedTransition(currentProtection, requestedProtection) ? OSHostMemoryProtectionPlanStatus.Success : OSHostMemoryProtectionPlanStatus.InvalidTransition;
    }

    public static bool IsWritableExecutable(OSHostPageProtection protection)
    {
        return (protection & OSHostPageProtection.Write) != 0 && (protection & OSHostPageProtection.Execute) != 0;
    }

    public static OSHostStatus ToHostStatus(OSHostMemoryProtectionPlanStatus status)
    {
        return status switch
        {
            OSHostMemoryProtectionPlanStatus.Success => OSHostStatus.Ok,
            OSHostMemoryProtectionPlanStatus.WritableExecutableRejected => OSHostStatus.AccessDenied,
            _ => OSHostStatus.InvalidArgument
        };
    }

    private static OSHostMemoryProtectionPlanStatus ValidateRange(ulong baseAddress, ulong byteLength, ulong pageSizeBytes, out ulong endAddress, out ulong pageCount)
    {
        endAddress = 0;
        pageCount = 0;
        if (pageSizeBytes < MinimumPageSize || !IsPowerOfTwo(pageSizeBytes))
        {
            return OSHostMemoryProtectionPlanStatus.InvalidPageSize;
        }

        if (baseAddress == 0 || byteLength == 0 || !IsAligned(baseAddress, pageSizeBytes) || !IsAligned(byteLength, pageSizeBytes))
        {
            return OSHostMemoryProtectionPlanStatus.InvalidRange;
        }

        if (!TryAdd(baseAddress, byteLength, out endAddress))
        {
            return OSHostMemoryProtectionPlanStatus.RangeOverflow;
        }

        pageCount = byteLength / pageSizeBytes;
        return OSHostMemoryProtectionPlanStatus.Success;
    }

    private static bool HasOnlyKnownFlags(OSHostPageProtection protection)
    {
        return (protection & ~KnownProtectionFlags) == 0;
    }

    private static bool IsSupportedState(OSHostPageProtection protection)
    {
        return protection == OSHostPageProtection.None || protection == OSHostPageProtection.Read || protection == (OSHostPageProtection.Read | OSHostPageProtection.Write) || protection == (OSHostPageProtection.Read | OSHostPageProtection.Execute);
    }

    private static bool IsAllowedTransition(OSHostPageProtection currentProtection, OSHostPageProtection requestedProtection)
    {
        if (currentProtection == requestedProtection)
        {
            return true;
        }

        if (requestedProtection == OSHostPageProtection.None || requestedProtection == OSHostPageProtection.Read)
        {
            return true;
        }

        if (requestedProtection == (OSHostPageProtection.Read | OSHostPageProtection.Write))
        {
            return currentProtection == OSHostPageProtection.None || currentProtection == OSHostPageProtection.Read || currentProtection == (OSHostPageProtection.Read | OSHostPageProtection.Write);
        }

        if (requestedProtection == (OSHostPageProtection.Read | OSHostPageProtection.Execute))
        {
            return currentProtection == (OSHostPageProtection.Read | OSHostPageProtection.Write) || currentProtection == (OSHostPageProtection.Read | OSHostPageProtection.Execute);
        }

        return false;
    }

    private static bool IsAligned(ulong value, ulong alignment)
    {
        return (value & (alignment - 1)) == 0;
    }

    private static bool IsPowerOfTwo(ulong value)
    {
        return value != 0 && (value & (value - 1)) == 0;
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
