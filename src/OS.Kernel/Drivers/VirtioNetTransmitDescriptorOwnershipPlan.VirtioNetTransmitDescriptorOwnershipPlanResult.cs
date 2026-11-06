namespace OS.Drivers;

public readonly struct VirtioNetTransmitDescriptorOwnershipPlanResult
{
    private VirtioNetTransmitDescriptorOwnershipPlanResult(VirtioNetTransmitDescriptorOwnershipStatus status, VirtioNetTransmitLifecycleStatus transmitPublishStatus, VirtioNetTransmitCompletionPlanStatus completionPlanStatus, ushort headerDescriptorIndex, ushort frameDescriptorIndex, int stateTableLength, int requiredStateTableLength, ushort descriptorsMarkedInFlight, ushort descriptorsFreed, bool isInitialized)
    {
        Status = status;
        TransmitPublishStatus = transmitPublishStatus;
        CompletionPlanStatus = completionPlanStatus;
        HeaderDescriptorIndex = headerDescriptorIndex;
        FrameDescriptorIndex = frameDescriptorIndex;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsMarkedInFlight = descriptorsMarkedInFlight;
        DescriptorsFreed = descriptorsFreed;
        IsInitialized = isInitialized;
    }

    public VirtioNetTransmitDescriptorOwnershipStatus Status
    {
        get;
    }
    public VirtioNetTransmitLifecycleStatus TransmitPublishStatus
    {
        get;
    }
    public VirtioNetTransmitCompletionPlanStatus CompletionPlanStatus
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
    public int StateTableLength
    {
        get;
    }
    public int RequiredStateTableLength
    {
        get;
    }
    public ushort DescriptorsMarkedInFlight
    {
        get;
    }
    public ushort DescriptorsFreed
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetTransmitDescriptorOwnershipStatus.Success;

    internal static VirtioNetTransmitDescriptorOwnershipPlanResult Create(VirtioNetTransmitDescriptorOwnershipStatus status, VirtioNetTransmitLifecycleStatus transmitPublishStatus, VirtioNetTransmitCompletionPlanStatus completionPlanStatus, ushort headerDescriptorIndex, ushort frameDescriptorIndex, int stateTableLength, int requiredStateTableLength, ushort descriptorsMarkedInFlight, ushort descriptorsFreed)
    {
        return new VirtioNetTransmitDescriptorOwnershipPlanResult(status, transmitPublishStatus, completionPlanStatus, headerDescriptorIndex, frameDescriptorIndex, stateTableLength, requiredStateTableLength, descriptorsMarkedInFlight, descriptorsFreed, isInitialized: true);
    }
}
