using OS.Manifest;

namespace OS.Kernel.Runtime;

public static class NativeAppPayloadMappingPlanner
{
    public static NativeAppPayloadMappingPlanResult Create(NativeAppLaunchDescriptorResult descriptorResult)
    {
        if (!descriptorResult.Succeeded)
        {
            return NativeAppPayloadMappingPlanResult.Fail(NativeAppPayloadMappingPlanStatus.DescriptorFailed, descriptorResult.Status, descriptorResult.PayloadHeaderStatus);
        }

        NativeAppLaunchDescriptor descriptor = descriptorResult.Descriptor;
        ulong payloadLength = descriptor.AppBinaryLength;
        ulong headerLength = descriptor.PayloadHeader.HeaderSizeValue;
        if (headerLength < NativeAppPayloadHeader.HeaderSize || payloadLength < headerLength)
        {
            return NativeAppPayloadMappingPlanResult.Fail(NativeAppPayloadMappingPlanStatus.PayloadTooSmall, NativeAppLaunchDescriptorStatus.Success, NativeAppPayloadHeaderStatus.Success);
        }

        ulong executableLength = payloadLength - headerLength;
        if (executableLength == 0)
        {
            return NativeAppPayloadMappingPlanResult.Fail(NativeAppPayloadMappingPlanStatus.ZeroExecutableContent, NativeAppLaunchDescriptorStatus.Success, NativeAppPayloadHeaderStatus.Success);
        }

        ulong entryOffsetInPayload = descriptor.PayloadHeader.EntryOffset;
        if (entryOffsetInPayload < headerLength || entryOffsetInPayload >= payloadLength)
        {
            return NativeAppPayloadMappingPlanResult.Fail(NativeAppPayloadMappingPlanStatus.InvalidEntryOffset, NativeAppLaunchDescriptorStatus.Success, NativeAppPayloadHeaderStatus.Success);
        }

        ulong payloadOffsetInPackage = descriptor.AppBinary.ContentOffset;
        if (!TryAdd(payloadOffsetInPackage, headerLength, out ulong executableOffsetInPackage) || !TryAdd(payloadOffsetInPackage, entryOffsetInPayload, out ulong entryOffsetInPackage))
        {
            return NativeAppPayloadMappingPlanResult.Fail(NativeAppPayloadMappingPlanStatus.RangeOverflow, NativeAppLaunchDescriptorStatus.Success, NativeAppPayloadHeaderStatus.Success);
        }

        NativeAppPayloadMappingPlan plan = new NativeAppPayloadMappingPlan(payloadOffsetInPackage, payloadLength, headerLength, headerLength, executableOffsetInPackage, executableLength, entryOffsetInPayload, entryOffsetInPayload - headerLength, entryOffsetInPackage);
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
