using OS.Std.Binary;

namespace OS.Storage;

public static class VirtioBlockQueueReadiness
{
    public static VirtioBlockQueueReadinessResult ValidateReadOnlyQueue(
        ulong negotiatedFeatures,
        ushort queueSize,
        VirtioBlockRequestPlanResult requestPlan)
    {
        if ((negotiatedFeatures & VirtioBlockFeatureBits.ReadOnly) == 0)
        {
            return VirtioBlockQueueReadinessResult.Fail(
                VirtioBlockQueueReadinessStatus.MissingReadOnlyFeature,
                requestPlan.Status,
                queueSize);
        }

        if (queueSize < VirtioBlockRequestPlan.RequiredDescriptorCount)
        {
            return VirtioBlockQueueReadinessResult.Fail(
                VirtioBlockQueueReadinessStatus.QueueTooSmall,
                requestPlan.Status,
                queueSize);
        }

        if (!requestPlan.Succeeded)
        {
            return VirtioBlockQueueReadinessResult.Fail(
                VirtioBlockQueueReadinessStatus.RequestPlanFailed,
                requestPlan.Status,
                queueSize);
        }

        if (requestPlan.Plan.RequestType == VirtioBlockRequestType.Write)
        {
            return VirtioBlockQueueReadinessResult.Fail(
                VirtioBlockQueueReadinessStatus.WriteRejectedReadOnlyMode,
                VirtioBlockRequestPlanStatus.WriteRejectedReadOnlyMode,
                queueSize);
        }

        return VirtioBlockQueueReadinessResult.Ready(requestPlan.Plan, queueSize);
    }
}
