namespace OS.Drivers;

public readonly struct VirtioNetTransmitRegisterWindowReclaimResult
{
    private VirtioNetTransmitRegisterWindowReclaimResult(bool isInitialized, VirtioNetTransmitRegisterWindowReclaimStatus status, VirtioNetTransmitRegisterWindowReadinessStatus readinessStatus, VirtioNetTransmitCompletionPlanStatus completionPlanStatus, VirtioNetTransmitDescriptorOwnershipStatus ownershipStatus, bool completionPlanAttempted, bool ownershipReclaimAttempted, bool usesRegisterWindowNotification, VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus notificationWindowExecutionStatus, VirtioPciBarRegisterWindowPlanStatus notificationRegisterWindowStatus, VirtioPciBarRegisterWindowAccessExecutionStatus notificationWindowAccessExecutionStatus, bool notificationBarrierSucceeded, uint notificationBarrierSequence, ushort queueIndex, ushort queueNotifyOffset, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort nextAvailableRingIndex, uint notifyRelativeOffset, ushort writeValue, uint writeWidthBytes, VirtioPciCapabilityType capabilityType, byte barIndex, VirtioPciRegisterAccessWidth accessWidth, uint accessWidthBytes, ulong barRelativeOffset, ulong writtenValue, ushort previousUsedIndex, ushort currentUsedIndex, ushort nextUsedIndex, ushort usedRingSlot, uint usedRingElementOffset, uint descriptorHead, uint usedByteCount, ushort reclaimedDescriptorCount, ushort headerDescriptorIndex, ushort frameDescriptorIndex, int stateTableLength, int requiredStateTableLength, ushort descriptorsFreed)
    {
        IsInitialized = isInitialized;
        Status = status;
        ReadinessStatus = readinessStatus;
        CompletionPlanStatus = completionPlanStatus;
        OwnershipStatus = ownershipStatus;
        CompletionPlanAttempted = completionPlanAttempted;
        OwnershipReclaimAttempted = ownershipReclaimAttempted;
        UsesRegisterWindowNotification = usesRegisterWindowNotification;
        NotificationWindowExecutionStatus = notificationWindowExecutionStatus;
        NotificationRegisterWindowStatus = notificationRegisterWindowStatus;
        NotificationWindowAccessExecutionStatus = notificationWindowAccessExecutionStatus;
        NotificationBarrierSucceeded = notificationBarrierSucceeded;
        NotificationBarrierSequence = notificationBarrierSequence;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        NotifyRelativeOffset = notifyRelativeOffset;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        AccessWidth = accessWidth;
        AccessWidthBytes = accessWidthBytes;
        BarRelativeOffset = barRelativeOffset;
        WrittenValue = writtenValue;
        PreviousUsedIndex = previousUsedIndex;
        CurrentUsedIndex = currentUsedIndex;
        NextUsedIndex = nextUsedIndex;
        UsedRingSlot = usedRingSlot;
        UsedRingElementOffset = usedRingElementOffset;
        DescriptorHead = descriptorHead;
        UsedByteCount = usedByteCount;
        ReclaimedDescriptorCount = reclaimedDescriptorCount;
        HeaderDescriptorIndex = headerDescriptorIndex;
        FrameDescriptorIndex = frameDescriptorIndex;
        StateTableLength = stateTableLength;
        RequiredStateTableLength = requiredStateTableLength;
        DescriptorsFreed = descriptorsFreed;
    }

    public VirtioNetTransmitRegisterWindowReclaimStatus Status
    {
        get;
    }
    public VirtioNetTransmitRegisterWindowReadinessStatus ReadinessStatus
    {
        get;
    }
    public VirtioNetTransmitCompletionPlanStatus CompletionPlanStatus
    {
        get;
    }
    public VirtioNetTransmitDescriptorOwnershipStatus OwnershipStatus
    {
        get;
    }
    public bool CompletionPlanAttempted
    {
        get;
    }
    public bool OwnershipReclaimAttempted
    {
        get;
    }
    public bool UsesRegisterWindowNotification
    {
        get;
    }
    public VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus NotificationWindowExecutionStatus
    {
        get;
    }
    public VirtioPciBarRegisterWindowPlanStatus NotificationRegisterWindowStatus
    {
        get;
    }
    public VirtioPciBarRegisterWindowAccessExecutionStatus NotificationWindowAccessExecutionStatus
    {
        get;
    }
    public bool NotificationBarrierSucceeded
    {
        get;
    }
    public uint NotificationBarrierSequence
    {
        get;
    }
    public ushort QueueIndex
    {
        get;
    }
    public ushort QueueNotifyOffset
    {
        get;
    }
    public ushort PublishedDescriptorCount
    {
        get;
    }
    public ushort PreviousAvailableRingIndex
    {
        get;
    }
    public ushort NextAvailableRingIndex
    {
        get;
    }
    public uint NotifyRelativeOffset
    {
        get;
    }
    public ushort WriteValue
    {
        get;
    }
    public uint WriteWidthBytes
    {
        get;
    }
    public VirtioPciCapabilityType CapabilityType
    {
        get;
    }
    public byte BarIndex
    {
        get;
    }
    public VirtioPciRegisterAccessWidth AccessWidth
    {
        get;
    }
    public uint AccessWidthBytes
    {
        get;
    }
    public ulong BarRelativeOffset
    {
        get;
    }
    public ulong WrittenValue
    {
        get;
    }
    public ushort PreviousUsedIndex
    {
        get;
    }
    public ushort CurrentUsedIndex
    {
        get;
    }
    public ushort NextUsedIndex
    {
        get;
    }
    public ushort UsedRingSlot
    {
        get;
    }
    public uint UsedRingElementOffset
    {
        get;
    }
    public uint DescriptorHead
    {
        get;
    }
    public uint UsedByteCount
    {
        get;
    }
    public ushort ReclaimedDescriptorCount
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
    public ushort DescriptorsFreed
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetTransmitRegisterWindowReclaimStatus.Success;

    internal static VirtioNetTransmitRegisterWindowReclaimResult Create(VirtioNetTransmitRegisterWindowReclaimStatus status, VirtioNetTransmitRegisterWindowReadinessResult readiness, VirtioNetTransmitCompletionPlanResult completion, VirtioNetTransmitDescriptorOwnershipPlanResult ownershipReclaim, bool completionPlanAttempted, bool ownershipReclaimAttempted, ushort previousUsedIndex)
    {
        return new VirtioNetTransmitRegisterWindowReclaimResult(isInitialized: true, status, readiness.Status, completion.Status, ownershipReclaim.Status, completionPlanAttempted, ownershipReclaimAttempted, readiness.Succeeded && completionPlanAttempted ? completion.UsesRegisterWindowNotification : readiness.NotificationWindowExecutionAttempted, readiness.Succeeded && completionPlanAttempted ? completion.NotificationWindowExecutionStatus : readiness.NotificationWindowExecutionStatus, readiness.Succeeded && completionPlanAttempted ? completion.NotificationRegisterWindowStatus : readiness.NotificationRegisterWindowStatus, readiness.Succeeded && completionPlanAttempted ? completion.NotificationWindowAccessExecutionStatus : readiness.NotificationAccessExecutionStatus, readiness.Succeeded && completionPlanAttempted ? completion.NotificationBarrierSucceeded : readiness.BarrierSucceeded, readiness.Succeeded && completionPlanAttempted ? completion.NotificationBarrierSequence : readiness.BarrierSequence, readiness.Succeeded && completionPlanAttempted ? completion.NotificationQueueIndex : readiness.QueueIndex, readiness.Succeeded && completionPlanAttempted ? completion.NotificationQueueNotifyOffset : readiness.QueueNotifyOffset, readiness.Succeeded && completionPlanAttempted ? completion.NotificationPublishedDescriptorCount : readiness.PublishedDescriptorCount, readiness.Succeeded && completionPlanAttempted ? completion.NotificationPreviousAvailableRingIndex : readiness.PreviousAvailableRingIndex, readiness.Succeeded && completionPlanAttempted ? completion.NotificationNextAvailableRingIndex : readiness.NextAvailableRingIndex, readiness.Succeeded && completionPlanAttempted ? completion.NotificationNotifyRelativeOffset : readiness.NotifyRelativeOffset, readiness.Succeeded && completionPlanAttempted ? completion.NotificationWriteValue : readiness.WriteValue, readiness.Succeeded && completionPlanAttempted ? completion.NotificationWriteWidthBytes : readiness.WriteWidthBytes, readiness.Succeeded && completionPlanAttempted ? completion.NotificationCapabilityType : readiness.CapabilityType, readiness.Succeeded && completionPlanAttempted ? completion.NotificationBarIndex : readiness.BarIndex, readiness.Succeeded && completionPlanAttempted ? completion.NotificationAccessWidth : readiness.AccessWidth, readiness.Succeeded && completionPlanAttempted ? completion.NotificationAccessWidthBytes : readiness.AccessWidthBytes, readiness.Succeeded && completionPlanAttempted ? completion.NotificationBarRelativeOffset : readiness.BarRelativeOffset, readiness.Succeeded && completionPlanAttempted ? completion.NotificationWrittenValue : readiness.WrittenValue, previousUsedIndex, completionPlanAttempted ? completion.CurrentUsedIndex : previousUsedIndex, completionPlanAttempted ? completion.NextUsedIndex : previousUsedIndex, completionPlanAttempted ? completion.UsedRingSlot : (ushort)0, completionPlanAttempted ? completion.UsedRingElementOffset : 0, completionPlanAttempted ? completion.DescriptorHead : 0, completionPlanAttempted ? completion.UsedByteCount : 0, completion.ReclaimedDescriptorCount, completion.Succeeded ? completion.HeaderDescriptorIndex : readiness.HeaderDescriptorIndex, completion.Succeeded ? completion.FrameDescriptorIndex : readiness.FrameDescriptorIndex, ownershipReclaimAttempted ? ownershipReclaim.StateTableLength : 0, ownershipReclaimAttempted ? ownershipReclaim.RequiredStateTableLength : 0, ownershipReclaim.DescriptorsFreed);
    }
}
