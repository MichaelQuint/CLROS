using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public readonly struct VirtioBlockDescriptorChainWriterResult
{
    private VirtioBlockDescriptorChainWriterResult(
        VirtioBlockDescriptorChainWriterStatus status,
        VirtioBlockDescriptorChainPlanStatus descriptorPlanStatus,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        ushort headDescriptorIndex,
        ushort nextAvailableRingIndex,
        ushort descriptorsWritten)
    {
        IsInitialized = true;
        Status = status;
        DescriptorPlanStatus = descriptorPlanStatus;
        QueuePlanStatus = queuePlanStatus;
        HeadDescriptorIndex = headDescriptorIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorsWritten = descriptorsWritten;
    }

    public bool IsInitialized
    {
        get;
    }

    public VirtioBlockDescriptorChainWriterStatus Status
    {
        get;
    }

    public VirtioBlockDescriptorChainPlanStatus DescriptorPlanStatus
    {
        get;
    }

    public VirtqueueSplitRingPlanStatus QueuePlanStatus
    {
        get;
    }

    public ushort HeadDescriptorIndex
    {
        get;
    }

    public ushort NextAvailableRingIndex
    {
        get;
    }

    public ushort DescriptorsWritten
    {
        get;
    }

    public bool Succeeded => IsInitialized && Status == VirtioBlockDescriptorChainWriterStatus.Success;

    public static VirtioBlockDescriptorChainWriterResult Success(
        VirtioBlockDescriptorChainPlan plan,
        VirtqueueSplitRingPlanStatus queuePlanStatus)
    {
        return new VirtioBlockDescriptorChainWriterResult(
            VirtioBlockDescriptorChainWriterStatus.Success,
            VirtioBlockDescriptorChainPlanStatus.Success,
            queuePlanStatus,
            plan.HeadDescriptorIndex,
            GetNextAvailableRingIndex(plan.AvailableRingIndex),
            VirtioBlockDescriptorChainPlan.RequiredDescriptorCount);
    }

    public static VirtioBlockDescriptorChainWriterResult Fail(
        VirtioBlockDescriptorChainWriterStatus status,
        VirtioBlockDescriptorChainPlanStatus descriptorPlanStatus,
        VirtqueueSplitRingPlanStatus queuePlanStatus)
    {
        return new VirtioBlockDescriptorChainWriterResult(
            status,
            descriptorPlanStatus,
            queuePlanStatus,
            headDescriptorIndex: 0,
            nextAvailableRingIndex: 0,
            descriptorsWritten: 0);
    }

    internal static ushort GetNextAvailableRingIndex(ushort availableRingIndex)
    {
        return unchecked((ushort)(availableRingIndex + 1));
    }
}
