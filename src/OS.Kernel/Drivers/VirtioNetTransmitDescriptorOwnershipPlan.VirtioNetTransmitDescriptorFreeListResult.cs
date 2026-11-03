namespace OS.Drivers;

public readonly struct VirtioNetTransmitDescriptorFreeListResult
{
    private VirtioNetTransmitDescriptorFreeListResult(VirtioNetTransmitDescriptorFreeListStatus status, VirtioNetQueuePlanStatus queuePlanStatus, ushort headerDescriptorIndex, ushort frameDescriptorIndex, int stateTableLength, int requiredStateTableLength, ushort descriptorsRequired, ushort descriptorsScanned, ushort invalidDescriptorIndex, bool isInitialized)
    {
        Status = status;
        QueuePlanStatus = queuePlanStatus;
        HeaderDescriptorIndex = headerDescriptorIndex;
        FrameDescriptorIndex = frameDescriptorIndex;
        FirstDescriptorIndex = headerDescriptorIndex;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsRequired = descriptorsRequired;
        DescriptorsScanned = descriptorsScanned;
        InvalidDescriptorIndex = invalidDescriptorIndex;
        IsInitialized = isInitialized;
    }

    public VirtioNetTransmitDescriptorFreeListStatus Status
    {
        get;
    }
    public VirtioNetQueuePlanStatus QueuePlanStatus
    {
        get;
    }
    public ushort HeaderDescriptorIndex
    {
        get;
    }
    public ushort FrameDescriptorIndex
    {
        get;
    }
    public ushort FirstDescriptorIndex
    {
        get;
    }
    public int StateTableLength
    {
        get;
    }
    public int RequiredStateTableLength
    {
        get;
    }
    public ushort DescriptorsRequired
    {
        get;
    }
    public ushort DescriptorsScanned
    {
        get;
    }
    public ushort InvalidDescriptorIndex
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetTransmitDescriptorFreeListStatus.Success;

    internal static VirtioNetTransmitDescriptorFreeListResult Create(VirtioNetTransmitDescriptorFreeListStatus status, VirtioNetQueuePlanStatus queuePlanStatus, ushort headerDescriptorIndex, ushort frameDescriptorIndex, int stateTableLength, int requiredStateTableLength, ushort descriptorsRequired, ushort descriptorsScanned, ushort invalidDescriptorIndex)
    {
        return new VirtioNetTransmitDescriptorFreeListResult(status, queuePlanStatus, headerDescriptorIndex, frameDescriptorIndex, stateTableLength, requiredStateTableLength, descriptorsRequired, descriptorsScanned, invalidDescriptorIndex, isInitialized: true);
    }
}
