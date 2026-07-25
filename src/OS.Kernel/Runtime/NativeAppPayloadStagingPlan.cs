using OS.Manifest;
using OS.Std.Memory;

namespace OS.Kernel.Runtime;

public enum NativeAppPayloadStagingStatus
{
    Success = 0,
    MappingPlanFailed = 1,
    RangeOverflow = 2,
    PackageRangeOutOfBounds = 3,
    DestinationTooSmall = 4
}

public readonly struct NativeAppPayloadStagingResult
{
    private NativeAppPayloadStagingResult(
        NativeAppPayloadStagingStatus status,
        NativeAppPayloadMappingPlanStatus mappingStatus,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        NativeAppPayloadHeaderStatus payloadHeaderStatus,
        bool isInitialized,
        ulong payloadOffsetInPackage,
        ulong executableOffsetInPackage,
        ulong executableBytes,
        ulong bytesWritten,
        ulong requiredBytes)
    {
        Status = status;
        MappingStatus = mappingStatus;
        DescriptorStatus = descriptorStatus;
        PayloadHeaderStatus = payloadHeaderStatus;
        IsInitialized = isInitialized;
        PayloadOffsetInPackage = payloadOffsetInPackage;
        ExecutableOffsetInPackage = executableOffsetInPackage;
        ExecutableBytes = executableBytes;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
    }

    public NativeAppPayloadStagingStatus Status { get; }

    public NativeAppPayloadMappingPlanStatus MappingStatus { get; }

    public NativeAppLaunchDescriptorStatus DescriptorStatus { get; }

    public NativeAppPayloadHeaderStatus PayloadHeaderStatus { get; }

    public bool IsInitialized { get; }

    public ulong PayloadOffsetInPackage { get; }

    public ulong ExecutableOffsetInPackage { get; }

    public ulong ExecutableBytes { get; }

    public ulong BytesWritten { get; }

    public ulong RequiredBytes { get; }

    public bool Succeeded => IsInitialized && Status == NativeAppPayloadStagingStatus.Success;

    public static NativeAppPayloadStagingResult Success(
        NativeAppPayloadMappingPlanResult mappingPlan,
        ulong bytesWritten)
    {
        return new NativeAppPayloadStagingResult(
            NativeAppPayloadStagingStatus.Success,
            mappingPlan.Status,
            mappingPlan.DescriptorStatus,
            mappingPlan.PayloadHeaderStatus,
            isInitialized: true,
            mappingPlan.Plan.PayloadOffsetInPackage,
            mappingPlan.Plan.ExecutableOffsetInPackage,
            mappingPlan.Plan.ExecutableLength,
            bytesWritten,
            mappingPlan.Plan.ExecutableLength);
    }

    public static NativeAppPayloadStagingResult Fail(
        NativeAppPayloadStagingStatus status,
        NativeAppPayloadMappingPlanStatus mappingStatus,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        NativeAppPayloadHeaderStatus payloadHeaderStatus,
        ulong payloadOffsetInPackage,
        ulong executableOffsetInPackage,
        ulong executableBytes,
        ulong requiredBytes)
    {
        return new NativeAppPayloadStagingResult(
            status,
            mappingStatus,
            descriptorStatus,
            payloadHeaderStatus,
            isInitialized: true,
            payloadOffsetInPackage,
            executableOffsetInPackage,
            executableBytes,
            bytesWritten: 0,
            requiredBytes);
    }
}

public static class NativeAppPayloadStagingPlanner
{
    public const bool ReadsLiveStorage = false;
    public const bool MapsExecutableMemory = false;
    public const bool ExecutesApp = false;
    public const bool CopiesExecutableBytes = true;

    public static NativeAppPayloadStagingResult CopyExecutableToStaging(
        ReadOnlySpan<byte> package,
        NativeAppPayloadMappingPlanResult mappingPlan,
        Span<byte> destination)
    {
        if (!mappingPlan.Succeeded)
        {
            return NativeAppPayloadStagingResult.Fail(
                NativeAppPayloadStagingStatus.MappingPlanFailed,
                mappingPlan.Status,
                mappingPlan.DescriptorStatus,
                mappingPlan.PayloadHeaderStatus,
                payloadOffsetInPackage: 0,
                executableOffsetInPackage: 0,
                executableBytes: 0,
                requiredBytes: 0);
        }

        NativeAppPayloadMappingPlan plan = mappingPlan.Plan;
        if (!TryAdd(plan.ExecutableOffsetInPackage, plan.ExecutableLength, out ulong executableEndInPackage))
        {
            return FailWithPlan(
                NativeAppPayloadStagingStatus.RangeOverflow,
                mappingPlan,
                plan.ExecutableLength);
        }

        if (plan.ExecutableOffsetInPackage > int.MaxValue
            || plan.ExecutableLength > int.MaxValue
            || executableEndInPackage > (ulong)package.Length)
        {
            return FailWithPlan(
                NativeAppPayloadStagingStatus.PackageRangeOutOfBounds,
                mappingPlan,
                plan.ExecutableLength);
        }

        if ((ulong)destination.Length < plan.ExecutableLength)
        {
            return FailWithPlan(
                NativeAppPayloadStagingStatus.DestinationTooSmall,
                mappingPlan,
                plan.ExecutableLength);
        }

        ReadOnlySpan<byte> source = package.Slice(
            (int)plan.ExecutableOffsetInPackage,
            (int)plan.ExecutableLength);
        BufferOperationResult copy = BufferOperations.MemoryCopy(source, destination);
        if (!copy.Succeeded)
        {
            return FailWithPlan(
                NativeAppPayloadStagingStatus.DestinationTooSmall,
                mappingPlan,
                plan.ExecutableLength);
        }

        return NativeAppPayloadStagingResult.Success(mappingPlan, plan.ExecutableLength);
    }

    private static NativeAppPayloadStagingResult FailWithPlan(
        NativeAppPayloadStagingStatus status,
        NativeAppPayloadMappingPlanResult mappingPlan,
        ulong requiredBytes)
    {
        NativeAppPayloadMappingPlan plan = mappingPlan.Plan;
        return NativeAppPayloadStagingResult.Fail(
            status,
            mappingPlan.Status,
            mappingPlan.DescriptorStatus,
            mappingPlan.PayloadHeaderStatus,
            plan.PayloadOffsetInPackage,
            plan.ExecutableOffsetInPackage,
            plan.ExecutableLength,
            requiredBytes);
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
