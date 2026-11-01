namespace OS.Drivers;
public readonly struct VirtioNetQueuePlanResult
{
    private VirtioNetQueuePlanResult(VirtioNetQueuePlanStatus status, ushort availableQueueCount, ushort rxQueueSize, ushort txQueueSize, ushort rxBufferCount, uint frameBufferBytes, VirtqueueSplitRingPlanStatus rxQueueStatus, VirtqueueSplitRingPlanStatus txQueueStatus, VirtioNetQueuePlan plan)
    {
        IsInitialized = true;
        Status = status;
        AvailableQueueCount = availableQueueCount;
        RxQueueSize = rxQueueSize;
        TxQueueSize = txQueueSize;
        RxBufferCount = rxBufferCount;
        FrameBufferBytes = frameBufferBytes;
        RxQueueStatus = rxQueueStatus;
        TxQueueStatus = txQueueStatus;
        Plan = plan;
    }

    public bool IsInitialized { get; }
    public VirtioNetQueuePlanStatus Status { get; }
    public ushort AvailableQueueCount { get; }
    public ushort RxQueueSize { get; }
    public ushort TxQueueSize { get; }
    public ushort RxBufferCount { get; }
    public uint FrameBufferBytes { get; }
    public VirtqueueSplitRingPlanStatus RxQueueStatus { get; }
    public VirtqueueSplitRingPlanStatus TxQueueStatus { get; }
    public VirtioNetQueuePlan Plan { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetQueuePlanStatus.Success;

    public static VirtioNetQueuePlanResult Success(ushort availableQueueCount, ushort rxQueueSize, ushort txQueueSize, ushort rxBufferCount, uint frameBufferBytes, VirtqueueSplitRingPlanStatus rxQueueStatus, VirtqueueSplitRingPlanStatus txQueueStatus, VirtioNetQueuePlan plan)
    {
        return new VirtioNetQueuePlanResult(VirtioNetQueuePlanStatus.Success, availableQueueCount, rxQueueSize, txQueueSize, rxBufferCount, frameBufferBytes, rxQueueStatus, txQueueStatus, plan);
    }

    public static VirtioNetQueuePlanResult Fail(VirtioNetQueuePlanStatus status, ushort availableQueueCount, ushort rxQueueSize, ushort txQueueSize, ushort rxBufferCount, uint frameBufferBytes, VirtqueueSplitRingPlanStatus rxQueueStatus = VirtqueueSplitRingPlanStatus.Success, VirtqueueSplitRingPlanStatus txQueueStatus = VirtqueueSplitRingPlanStatus.Success)
    {
        return new VirtioNetQueuePlanResult(status, availableQueueCount, rxQueueSize, txQueueSize, rxBufferCount, frameBufferBytes, rxQueueStatus, txQueueStatus, default);
    }
}
