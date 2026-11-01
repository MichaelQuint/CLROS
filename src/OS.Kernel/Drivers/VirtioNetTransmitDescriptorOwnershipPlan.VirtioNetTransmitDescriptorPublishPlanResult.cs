namespace OS.Drivers;
public readonly struct VirtioNetTransmitDescriptorPublishPlanResult
{
    private VirtioNetTransmitDescriptorPublishPlanResult(VirtioNetTransmitDescriptorPublishPlanStatus status, VirtioNetQueuePlanStatus queuePlanStatus, VirtioNetTransmitDescriptorFreeListStatus freeListStatus, VirtioNetTransmitLifecycleStatus transmitPublishStatus, VirtioNetHeaderStatus headerStatus, VirtioNetTransmitDescriptorOwnershipStatus ownershipStatus, bool transmitPublishAttempted, bool ownershipMarkAttempted, ushort headerDescriptorIndex, ushort frameDescriptorIndex, ushort nextAvailableRingIndex, int stateTableLength, int requiredStateTableLength, ushort descriptorsSelected, ushort descriptorsWritten, ushort descriptorsMarkedInFlight, VirtioNetTransmitDescriptorFreeListResult freeList, VirtioNetTransmitLifecycleResult transmitPublish, VirtioNetTransmitDescriptorOwnershipPlanResult ownershipMark, bool isInitialized)
    {
        Status = status;
        QueuePlanStatus = queuePlanStatus;
        FreeListStatus = freeListStatus;
        TransmitPublishStatus = transmitPublishStatus;
        HeaderStatus = headerStatus;
        OwnershipStatus = ownershipStatus;
        TransmitPublishAttempted = transmitPublishAttempted;
        OwnershipMarkAttempted = ownershipMarkAttempted;
        HeaderDescriptorIndex = headerDescriptorIndex;
        FrameDescriptorIndex = frameDescriptorIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsSelected = descriptorsSelected;
        DescriptorsWritten = descriptorsWritten;
        DescriptorsMarkedInFlight = descriptorsMarkedInFlight;
        FreeList = freeList;
        TransmitPublish = transmitPublish;
        OwnershipMark = ownershipMark;
        IsInitialized = isInitialized;
    }

    public VirtioNetTransmitDescriptorPublishPlanStatus Status { get; }
    public VirtioNetQueuePlanStatus QueuePlanStatus { get; }
    public VirtioNetTransmitDescriptorFreeListStatus FreeListStatus { get; }
    public VirtioNetTransmitLifecycleStatus TransmitPublishStatus { get; }
    public VirtioNetHeaderStatus HeaderStatus { get; }
    public VirtioNetTransmitDescriptorOwnershipStatus OwnershipStatus { get; }
    public bool TransmitPublishAttempted { get; }
    public bool OwnershipMarkAttempted { get; }
    public ushort HeaderDescriptorIndex { get; }
    public ushort FrameDescriptorIndex { get; }
    public ushort NextAvailableRingIndex { get; }
    public int StateTableLength { get; }
    public int RequiredStateTableLength { get; }
    public ushort DescriptorsSelected { get; }
    public ushort DescriptorsWritten { get; }
    public ushort DescriptorsMarkedInFlight { get; }
    public VirtioNetTransmitDescriptorFreeListResult FreeList { get; }
    public VirtioNetTransmitLifecycleResult TransmitPublish { get; }
    public VirtioNetTransmitDescriptorOwnershipPlanResult OwnershipMark { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetTransmitDescriptorPublishPlanStatus.Success;

    internal static VirtioNetTransmitDescriptorPublishPlanResult Create(VirtioNetTransmitDescriptorPublishPlanStatus status, VirtioNetQueuePlanStatus queuePlanStatus, VirtioNetTransmitDescriptorFreeListResult freeList, VirtioNetTransmitLifecycleResult transmitPublish, VirtioNetTransmitDescriptorOwnershipPlanResult ownershipMark, bool transmitPublishAttempted, bool ownershipMarkAttempted)
    {
        return new VirtioNetTransmitDescriptorPublishPlanResult(status, queuePlanStatus, freeList.Status, transmitPublish.Status, transmitPublish.HeaderStatus, ownershipMark.Status, transmitPublishAttempted, ownershipMarkAttempted, freeList.HeaderDescriptorIndex, freeList.FrameDescriptorIndex, transmitPublish.NextAvailableRingIndex, freeList.StateTableLength, freeList.RequiredStateTableLength, freeList.Succeeded ? freeList.DescriptorsRequired : (ushort)0, transmitPublish.DescriptorsWritten, ownershipMark.DescriptorsMarkedInFlight, freeList, transmitPublish, ownershipMark, isInitialized: true);
    }
}
