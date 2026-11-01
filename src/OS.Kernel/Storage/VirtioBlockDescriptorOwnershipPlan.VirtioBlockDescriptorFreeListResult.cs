using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockDescriptorFreeListResult
{
    private VirtioBlockDescriptorFreeListResult(
        VirtioBlockDescriptorFreeListStatus status,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        ushort requestHeaderDescriptorIndex,
        ushort dataBufferDescriptorIndex,
        ushort statusByteDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsRequired,
        ushort descriptorsScanned,
        ushort invalidDescriptorIndex,
        bool isInitialized)
    {
        Status = status;
        QueuePlanStatus = queuePlanStatus;
        RequestHeaderDescriptorIndex = requestHeaderDescriptorIndex;
        DataBufferDescriptorIndex = dataBufferDescriptorIndex;
        StatusByteDescriptorIndex = statusByteDescriptorIndex;
        FirstDescriptorIndex = requestHeaderDescriptorIndex;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsRequired = descriptorsRequired;
        DescriptorsScanned = descriptorsScanned;
        InvalidDescriptorIndex = invalidDescriptorIndex;
        IsInitialized = isInitialized;
    }

    public VirtioBlockDescriptorFreeListStatus Status { get; }

    public VirtqueueSplitRingPlanStatus QueuePlanStatus { get; }

    public ushort RequestHeaderDescriptorIndex { get; }

    public ushort DataBufferDescriptorIndex { get; }

    public ushort StatusByteDescriptorIndex { get; }

    public ushort FirstDescriptorIndex { get; }

    public int StateTableLength { get; }

    public int RequiredStateTableLength { get; }

    public ushort DescriptorsRequired { get; }

    public ushort DescriptorsScanned { get; }

    public ushort InvalidDescriptorIndex { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockDescriptorFreeListStatus.Success;

    internal static VirtioBlockDescriptorFreeListResult Create(
        VirtioBlockDescriptorFreeListStatus status,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        ushort requestHeaderDescriptorIndex,
        ushort dataBufferDescriptorIndex,
        ushort statusByteDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength,
        ushort descriptorsRequired,
        ushort descriptorsScanned,
        ushort invalidDescriptorIndex)
    {
        return new VirtioBlockDescriptorFreeListResult(
            status,
            queuePlanStatus,
            requestHeaderDescriptorIndex,
            dataBufferDescriptorIndex,
            statusByteDescriptorIndex,
            stateTableLength,
            requiredStateTableLength,
            descriptorsRequired,
            descriptorsScanned,
            invalidDescriptorIndex,
            isInitialized: true);
    }
}
