namespace OS.Drivers;
using OS.Std.Binary;

public readonly struct VirtioNetTransmitLifecycleResult
{
    private VirtioNetTransmitLifecycleResult(VirtioNetTransmitLifecycleStatus status, VirtioNetQueuePlanStatus queuePlanStatus, VirtioNetHeaderStatus headerStatus, ushort headDescriptorIndex, ushort nextAvailableRingIndex, ushort descriptorsWritten, int headerBytesWritten, int ethernetFrameBytes, VirtioNetTransmitLifecyclePlan plan)
    {
        IsInitialized = true;
        Status = status;
        QueuePlanStatus = queuePlanStatus;
        HeaderStatus = headerStatus;
        HeadDescriptorIndex = headDescriptorIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorsWritten = descriptorsWritten;
        HeaderBytesWritten = headerBytesWritten;
        EthernetFrameBytes = ethernetFrameBytes;
        Plan = plan;
    }

    public bool IsInitialized { get; }
    public VirtioNetTransmitLifecycleStatus Status { get; }
    public VirtioNetQueuePlanStatus QueuePlanStatus { get; }
    public VirtioNetHeaderStatus HeaderStatus { get; }
    public ushort HeadDescriptorIndex { get; }
    public ushort NextAvailableRingIndex { get; }
    public ushort DescriptorsWritten { get; }
    public int HeaderBytesWritten { get; }
    public int EthernetFrameBytes { get; }
    public VirtioNetTransmitLifecyclePlan Plan { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetTransmitLifecycleStatus.Success;

    public static VirtioNetTransmitLifecycleResult Success(VirtioNetTransmitLifecyclePlan plan, int headerBytesWritten, int ethernetFrameBytes)
    {
        return new VirtioNetTransmitLifecycleResult(VirtioNetTransmitLifecycleStatus.Success, VirtioNetQueuePlanStatus.Success, VirtioNetHeaderStatus.Success, plan.HeadDescriptorIndex, GetNextAvailableRingIndex(plan.AvailableRingIndex), VirtioNetTransmitLifecycle.RequiredDescriptorCount, headerBytesWritten, ethernetFrameBytes, plan);
    }

    public static VirtioNetTransmitLifecycleResult Fail(VirtioNetTransmitLifecycleStatus status, VirtioNetQueuePlanStatus queuePlanStatus, VirtioNetHeaderStatus headerStatus = VirtioNetHeaderStatus.Success)
    {
        return new VirtioNetTransmitLifecycleResult(status, queuePlanStatus, headerStatus, headDescriptorIndex: 0, nextAvailableRingIndex: 0, descriptorsWritten: 0, headerBytesWritten: 0, ethernetFrameBytes: 0, default);
    }

    internal static ushort GetNextAvailableRingIndex(ushort availableRingIndex)
    {
        return unchecked((ushort)(availableRingIndex + 1));
    }
}
