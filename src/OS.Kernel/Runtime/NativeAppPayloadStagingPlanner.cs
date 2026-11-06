using OS.Manifest;
using OS.Std.Memory;

namespace OS.Kernel.Runtime;

public static class NativeAppPayloadStagingPlanner
{
    public const bool ReadsLiveStorage = false;
    public const bool MapsExecutableMemory = false;
    public const bool ExecutesApp = false;
    public const bool CopiesExecutableBytes = true;
    public static NativeAppPayloadStagingResult CopyExecutableToStaging(ReadOnlySpan<byte> package, NativeAppPayloadMappingPlanResult mappingPlan, Span<byte> destination)
    {
        if (!mappingPlan.Succeeded)
        {
            return NativeAppPayloadStagingResult.Fail(NativeAppPayloadStagingStatus.MappingPlanFailed, mappingPlan.Status, mappingPlan.DescriptorStatus, mappingPlan.PayloadHeaderStatus, payloadOffsetInPackage: 0, executableOffsetInPackage: 0, executableBytes: 0, requiredBytes: 0);
        }

        NativeAppPayloadMappingPlan plan = mappingPlan.Plan;
        if (!TryAdd(plan.ExecutableOffsetInPackage, plan.ExecutableLength, out ulong executableEndInPackage))
        {
            return FailWithPlan(NativeAppPayloadStagingStatus.RangeOverflow, mappingPlan, plan.ExecutableLength);
        }

        if (plan.ExecutableOffsetInPackage > int.MaxValue || plan.ExecutableLength > int.MaxValue || executableEndInPackage > (ulong)package.Length)
        {
            return FailWithPlan(NativeAppPayloadStagingStatus.PackageRangeOutOfBounds, mappingPlan, plan.ExecutableLength);
        }

        if ((ulong)destination.Length < plan.ExecutableLength)
        {
            return FailWithPlan(NativeAppPayloadStagingStatus.DestinationTooSmall, mappingPlan, plan.ExecutableLength);
        }

        ReadOnlySpan<byte> source = package.Slice((int)plan.ExecutableOffsetInPackage, (int)plan.ExecutableLength);
        BufferOperationResult copy = BufferOperations.MemoryCopy(source, destination);
        if (!copy.Succeeded)
        {
            return FailWithPlan(NativeAppPayloadStagingStatus.DestinationTooSmall, mappingPlan, plan.ExecutableLength);
        }

        return NativeAppPayloadStagingResult.Success(mappingPlan, plan.ExecutableLength);
    }

    private static NativeAppPayloadStagingResult FailWithPlan(NativeAppPayloadStagingStatus status, NativeAppPayloadMappingPlanResult mappingPlan, ulong requiredBytes)
    {
        NativeAppPayloadMappingPlan plan = mappingPlan.Plan;
        return NativeAppPayloadStagingResult.Fail(status, mappingPlan.Status, mappingPlan.DescriptorStatus, mappingPlan.PayloadHeaderStatus, plan.PayloadOffsetInPackage, plan.ExecutableOffsetInPackage, plan.ExecutableLength, requiredBytes);
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
