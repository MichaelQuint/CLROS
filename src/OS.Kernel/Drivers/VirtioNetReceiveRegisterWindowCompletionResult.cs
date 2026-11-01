namespace OS.Drivers;
using OS.Net;

public readonly struct VirtioNetReceiveRegisterWindowCompletionResult
{
    private VirtioNetReceiveRegisterWindowCompletionResult(VirtioNetReceiveRegisterWindowCompletionStatus status, VirtioNetReceiveReadinessStatus readinessStatus, VirtioNetReceiveLifecycleCompletionStatus completionStatus, VirtioNetReceiveLifecyclePublishStatus publishStatus, VirtioNetReceiveFrameClassifierStatus classifierStatus, PacketParseStatus packetStatus, bool completionAttempted, bool usesRegisterWindowNotification, VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus notificationWindowExecutionStatus, VirtioPciBarRegisterWindowPlanStatus notificationRegisterWindowStatus, VirtioPciBarRegisterWindowAccessExecutionStatus notificationWindowAccessExecutionStatus, bool notificationBarrierSucceeded, uint notificationBarrierSequence, ushort queueIndex, ushort queueNotifyOffset, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort nextAvailableRingIndex, uint notifyRelativeOffset, ushort writeValue, uint writeWidthBytes, VirtioPciCapabilityType capabilityType, byte barIndex, VirtioPciRegisterAccessWidth accessWidth, uint accessWidthBytes, ulong barRelativeOffset, ulong writtenValue, ushort descriptorHead, ushort bufferIndex, ushort previousUsedIndex, ushort currentUsedIndex, ushort nextUsedIndex, ushort usedRingSlot, uint usedRingElementOffset, uint usedByteCount, uint frameBufferBytes, EthernetPayloadKind payloadKind, int payloadOffset, int payloadLength)
    {
        IsInitialized = true;
        Status = status;
        ReadinessStatus = readinessStatus;
        CompletionStatus = completionStatus;
        PublishStatus = publishStatus;
        ClassifierStatus = classifierStatus;
        PacketStatus = packetStatus;
        CompletionAttempted = completionAttempted;
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
        DescriptorHead = descriptorHead;
        BufferIndex = bufferIndex;
        PreviousUsedIndex = previousUsedIndex;
        CurrentUsedIndex = currentUsedIndex;
        NextUsedIndex = nextUsedIndex;
        UsedRingSlot = usedRingSlot;
        UsedRingElementOffset = usedRingElementOffset;
        UsedByteCount = usedByteCount;
        FrameBufferBytes = frameBufferBytes;
        PayloadKind = payloadKind;
        PayloadOffset = payloadOffset;
        PayloadLength = payloadLength;
    }

    public VirtioNetReceiveRegisterWindowCompletionStatus Status { get; }
    public bool IsInitialized { get; }
    public VirtioNetReceiveReadinessStatus ReadinessStatus { get; }
    public VirtioNetReceiveLifecycleCompletionStatus CompletionStatus { get; }
    public VirtioNetReceiveLifecyclePublishStatus PublishStatus { get; }
    public VirtioNetReceiveFrameClassifierStatus ClassifierStatus { get; }
    public PacketParseStatus PacketStatus { get; }
    public bool CompletionAttempted { get; }
    public bool UsesRegisterWindowNotification { get; }
    public VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus NotificationWindowExecutionStatus { get; }
    public VirtioPciBarRegisterWindowPlanStatus NotificationRegisterWindowStatus { get; }
    public VirtioPciBarRegisterWindowAccessExecutionStatus NotificationWindowAccessExecutionStatus { get; }
    public bool NotificationBarrierSucceeded { get; }
    public uint NotificationBarrierSequence { get; }
    public ushort QueueIndex { get; }
    public ushort QueueNotifyOffset { get; }
    public ushort PublishedDescriptorCount { get; }
    public ushort PreviousAvailableRingIndex { get; }
    public ushort NextAvailableRingIndex { get; }
    public uint NotifyRelativeOffset { get; }
    public ushort WriteValue { get; }
    public uint WriteWidthBytes { get; }
    public VirtioPciCapabilityType CapabilityType { get; }
    public byte BarIndex { get; }
    public VirtioPciRegisterAccessWidth AccessWidth { get; }
    public uint AccessWidthBytes { get; }
    public ulong BarRelativeOffset { get; }
    public ulong WrittenValue { get; }
    public ushort DescriptorHead { get; }
    public ushort BufferIndex { get; }
    public ushort PreviousUsedIndex { get; }
    public ushort CurrentUsedIndex { get; }
    public ushort NextUsedIndex { get; }
    public ushort UsedRingSlot { get; }
    public uint UsedRingElementOffset { get; }
    public uint UsedByteCount { get; }
    public uint FrameBufferBytes { get; }
    public EthernetPayloadKind PayloadKind { get; }
    public int PayloadOffset { get; }
    public int PayloadLength { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetReceiveRegisterWindowCompletionStatus.Success;

    internal static VirtioNetReceiveRegisterWindowCompletionResult Create(VirtioNetReceiveRegisterWindowCompletionStatus status, VirtioNetReceiveReadinessResult readiness, VirtioNetReceiveLifecycleCompletionResult completion, bool completionAttempted)
    {
        return new VirtioNetReceiveRegisterWindowCompletionResult(status, readiness.Status, completion.Status, completionAttempted ? completion.PublishStatus : readiness.ReceivePublishStatus, completion.ClassifierStatus, completion.PacketStatus, completionAttempted, readiness.NotificationWindowExecutionAttempted, readiness.NotificationWindowExecutionStatus, readiness.NotificationRegisterWindowStatus, readiness.NotificationAccessExecutionStatus, readiness.BarrierSucceeded, readiness.BarrierSequence, readiness.QueueIndex, readiness.QueueNotifyOffset, readiness.PublishedDescriptorCount, readiness.PreviousAvailableRingIndex, readiness.NextAvailableRingIndex, readiness.NotifyRelativeOffset, readiness.WriteValue, readiness.WriteWidthBytes, readiness.CapabilityType, readiness.BarIndex, readiness.AccessWidth, readiness.AccessWidthBytes, readiness.BarRelativeOffset, readiness.WrittenValue, completion.DescriptorHead, completion.BufferIndex, completionAttempted ? completion.PreviousUsedIndex : readiness.PreviousUsedIndex, completionAttempted ? completion.CurrentUsedIndex : readiness.PreviousUsedIndex, completionAttempted ? completion.NextUsedIndex : readiness.PreviousUsedIndex, completion.UsedRingSlot, completion.UsedRingElementOffset, completion.UsedByteCount, completionAttempted ? completion.FrameBufferBytes : readiness.FrameBufferBytes, completion.Classification.PayloadKind, completion.Classification.PayloadOffset, completion.Classification.PayloadLength);
    }
}
