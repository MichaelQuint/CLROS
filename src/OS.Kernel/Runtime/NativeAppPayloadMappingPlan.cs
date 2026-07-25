using OS.Manifest;

namespace OS.Kernel.Runtime;

public enum NativeAppPayloadMappingPlanStatus
{
    Success = 0,
    DescriptorFailed = 1,
    PayloadTooSmall = 2,
    ZeroExecutableContent = 3,
    InvalidEntryOffset = 4,
    RangeOverflow = 5
}

public readonly struct NativeAppPayloadMappingPlan
{
    public NativeAppPayloadMappingPlan(
        ulong payloadOffsetInPackage,
        ulong payloadLength,
        ulong headerLength,
        ulong executableOffsetInPayload,
        ulong executableOffsetInPackage,
        ulong executableLength,
        ulong entryOffsetInPayload,
        ulong entryOffsetInExecutable,
        ulong entryOffsetInPackage)
    {
        PayloadOffsetInPackage = payloadOffsetInPackage;
        PayloadLength = payloadLength;
        HeaderLength = headerLength;
        ExecutableOffsetInPayload = executableOffsetInPayload;
        ExecutableOffsetInPackage = executableOffsetInPackage;
        ExecutableLength = executableLength;
        EntryOffsetInPayload = entryOffsetInPayload;
        EntryOffsetInExecutable = entryOffsetInExecutable;
        EntryOffsetInPackage = entryOffsetInPackage;
    }

    public ulong PayloadOffsetInPackage { get; }

    public ulong PayloadLength { get; }

    public ulong HeaderLength { get; }

    public ulong ExecutableOffsetInPayload { get; }

    public ulong ExecutableOffsetInPackage { get; }

    public ulong ExecutableLength { get; }

    public ulong EntryOffsetInPayload { get; }

    public ulong EntryOffsetInExecutable { get; }

    public ulong EntryOffsetInPackage { get; }
}

public readonly struct NativeAppPayloadMappingPlanResult
{
    private NativeAppPayloadMappingPlanResult(
        NativeAppPayloadMappingPlanStatus status,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        NativeAppPayloadHeaderStatus payloadHeaderStatus,
        bool isInitialized,
        NativeAppPayloadMappingPlan plan)
    {
        Status = status;
        DescriptorStatus = descriptorStatus;
        PayloadHeaderStatus = payloadHeaderStatus;
        IsInitialized = isInitialized;
        Plan = plan;
    }

    public NativeAppPayloadMappingPlanStatus Status { get; }

    public NativeAppLaunchDescriptorStatus DescriptorStatus { get; }

    public NativeAppPayloadHeaderStatus PayloadHeaderStatus { get; }

    public bool IsInitialized { get; }

    public NativeAppPayloadMappingPlan Plan { get; }

    public bool Succeeded => IsInitialized && Status == NativeAppPayloadMappingPlanStatus.Success;

    public static NativeAppPayloadMappingPlanResult Success(NativeAppPayloadMappingPlan plan)
    {
        return new NativeAppPayloadMappingPlanResult(
            NativeAppPayloadMappingPlanStatus.Success,
            NativeAppLaunchDescriptorStatus.Success,
            NativeAppPayloadHeaderStatus.Success,
            isInitialized: true,
            plan);
    }

    public static NativeAppPayloadMappingPlanResult Fail(
        NativeAppPayloadMappingPlanStatus status,
        NativeAppLaunchDescriptorStatus descriptorStatus,
        NativeAppPayloadHeaderStatus payloadHeaderStatus)
    {
        return new NativeAppPayloadMappingPlanResult(
            status,
            descriptorStatus,
            payloadHeaderStatus,
            isInitialized: true,
            default);
    }
}

public static class NativeAppPayloadMappingPlanner
{
    public static NativeAppPayloadMappingPlanResult Create(
        NativeAppLaunchDescriptorResult descriptorResult)
    {
        if (!descriptorResult.Succeeded)
        {
            return NativeAppPayloadMappingPlanResult.Fail(
                NativeAppPayloadMappingPlanStatus.DescriptorFailed,
                descriptorResult.Status,
                descriptorResult.PayloadHeaderStatus);
        }

        NativeAppLaunchDescriptor descriptor = descriptorResult.Descriptor;
        ulong payloadLength = descriptor.AppBinaryLength;
        ulong headerLength = descriptor.PayloadHeader.HeaderSizeValue;
        if (headerLength < NativeAppPayloadHeader.HeaderSize || payloadLength < headerLength)
        {
            return NativeAppPayloadMappingPlanResult.Fail(
                NativeAppPayloadMappingPlanStatus.PayloadTooSmall,
                NativeAppLaunchDescriptorStatus.Success,
                NativeAppPayloadHeaderStatus.Success);
        }

        ulong executableLength = payloadLength - headerLength;
        if (executableLength == 0)
        {
            return NativeAppPayloadMappingPlanResult.Fail(
                NativeAppPayloadMappingPlanStatus.ZeroExecutableContent,
                NativeAppLaunchDescriptorStatus.Success,
                NativeAppPayloadHeaderStatus.Success);
        }

        ulong entryOffsetInPayload = descriptor.PayloadHeader.EntryOffset;
        if (entryOffsetInPayload < headerLength || entryOffsetInPayload >= payloadLength)
        {
            return NativeAppPayloadMappingPlanResult.Fail(
                NativeAppPayloadMappingPlanStatus.InvalidEntryOffset,
                NativeAppLaunchDescriptorStatus.Success,
                NativeAppPayloadHeaderStatus.Success);
        }

        ulong payloadOffsetInPackage = descriptor.AppBinary.ContentOffset;
        if (!TryAdd(payloadOffsetInPackage, headerLength, out ulong executableOffsetInPackage)
            || !TryAdd(payloadOffsetInPackage, entryOffsetInPayload, out ulong entryOffsetInPackage))
        {
            return NativeAppPayloadMappingPlanResult.Fail(
                NativeAppPayloadMappingPlanStatus.RangeOverflow,
                NativeAppLaunchDescriptorStatus.Success,
                NativeAppPayloadHeaderStatus.Success);
        }

        NativeAppPayloadMappingPlan plan = new NativeAppPayloadMappingPlan(
            payloadOffsetInPackage,
            payloadLength,
            headerLength,
            headerLength,
            executableOffsetInPackage,
            executableLength,
            entryOffsetInPayload,
            entryOffsetInPayload - headerLength,
            entryOffsetInPackage);
        return NativeAppPayloadMappingPlanResult.Success(plan);
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
