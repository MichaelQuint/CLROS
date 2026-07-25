namespace OS.Drivers;

public enum VirtioNetReceiveReadinessStatus
{
    Success = 0,
    QueueSetupPlanFailed = 1,
    QueueSetupWindowExecutionFailed = 2,
    QueueSetupWindowExecutionMismatch = 3,
    ReceivePublishFailed = 4,
    NotificationPlanFailed = 5,
    BarrierNotReady = 6,
    NotificationWindowExecutionFailed = 7
}

public readonly struct VirtioNetReceiveReadinessResult
{
    private VirtioNetReceiveReadinessResult(
        VirtioNetReceiveReadinessStatus status,
        VirtioNetQueueMmioSetupPlanStatus queueSetupPlanStatus,
        VirtioNetQueueMmioSetupRegisterWindowExecutionStatus queueSetupWindowExecutionStatus,
        VirtioPciBarRegisterWindowPlanStatus queueSetupRegisterWindowStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus queueSetupAccessExecutionStatus,
        VirtioNetReceiveLifecyclePublishStatus receivePublishStatus,
        VirtioNetQueueNotificationPlanStatus notificationPlanStatus,
        VirtioPciQueueNotificationPlanStatus pciNotificationStatus,
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus notificationWindowExecutionStatus,
        VirtioPciBarRegisterWindowPlanStatus notificationRegisterWindowStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus notificationAccessExecutionStatus,
        bool receivePublishAttempted,
        bool notificationPlanAttempted,
        bool notificationWindowExecutionAttempted,
        bool barrierSucceeded,
        uint barrierSequence,
        ushort firstDescriptorIndex,
        ushort publishedDescriptorCount,
        ushort previousUsedIndex,
        ushort expectedNextUsedIndex,
        ushort firstAvailableRingSlot,
        ushort lastAvailableRingSlot,
        uint frameBufferBytes,
        uint receiveBufferStrideBytes,
        ushort queueIndex,
        ushort queueNotifyOffset,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        uint notifyRelativeOffset,
        ushort writeValue,
        uint writeWidthBytes,
        VirtioPciCapabilityType capabilityType,
        byte barIndex,
        VirtioPciRegisterAccessWidth accessWidth,
        uint accessWidthBytes,
        ulong barRelativeOffset,
        ulong writtenValue,
        VirtioNetReceiveLifecyclePublishResult receivePublish,
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notificationExecution)
    {
        IsInitialized = true;
        Status = status;
        QueueSetupPlanStatus = queueSetupPlanStatus;
        QueueSetupWindowExecutionStatus = queueSetupWindowExecutionStatus;
        QueueSetupRegisterWindowStatus = queueSetupRegisterWindowStatus;
        QueueSetupAccessExecutionStatus = queueSetupAccessExecutionStatus;
        ReceivePublishStatus = receivePublishStatus;
        NotificationPlanStatus = notificationPlanStatus;
        PciNotificationStatus = pciNotificationStatus;
        NotificationWindowExecutionStatus = notificationWindowExecutionStatus;
        NotificationRegisterWindowStatus = notificationRegisterWindowStatus;
        NotificationAccessExecutionStatus = notificationAccessExecutionStatus;
        ReceivePublishAttempted = receivePublishAttempted;
        NotificationPlanAttempted = notificationPlanAttempted;
        NotificationWindowExecutionAttempted = notificationWindowExecutionAttempted;
        BarrierSucceeded = barrierSucceeded;
        BarrierSequence = barrierSequence;
        FirstDescriptorIndex = firstDescriptorIndex;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousUsedIndex = previousUsedIndex;
        ExpectedNextUsedIndex = expectedNextUsedIndex;
        FirstAvailableRingSlot = firstAvailableRingSlot;
        LastAvailableRingSlot = lastAvailableRingSlot;
        FrameBufferBytes = frameBufferBytes;
        ReceiveBufferStrideBytes = receiveBufferStrideBytes;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
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
        ReceivePublish = receivePublish;
        NotificationPlan = notificationPlan;
        NotificationExecution = notificationExecution;
    }

    public VirtioNetReceiveReadinessStatus Status { get; }

    public bool IsInitialized { get; }

    public VirtioNetQueueMmioSetupPlanStatus QueueSetupPlanStatus { get; }

    public VirtioNetQueueMmioSetupRegisterWindowExecutionStatus QueueSetupWindowExecutionStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus QueueSetupRegisterWindowStatus { get; }

    public VirtioPciBarRegisterWindowAccessExecutionStatus QueueSetupAccessExecutionStatus { get; }

    public VirtioNetReceiveLifecyclePublishStatus ReceivePublishStatus { get; }

    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }

    public VirtioPciQueueNotificationPlanStatus PciNotificationStatus { get; }

    public VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus NotificationWindowExecutionStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus NotificationRegisterWindowStatus { get; }

    public VirtioPciBarRegisterWindowAccessExecutionStatus NotificationAccessExecutionStatus { get; }

    public bool ReceivePublishAttempted { get; }

    public bool NotificationPlanAttempted { get; }

    public bool NotificationWindowExecutionAttempted { get; }

    public bool BarrierSucceeded { get; }

    public uint BarrierSequence { get; }

    public ushort FirstDescriptorIndex { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousUsedIndex { get; }

    public ushort ExpectedNextUsedIndex { get; }

    public ushort FirstAvailableRingSlot { get; }

    public ushort LastAvailableRingSlot { get; }

    public uint FrameBufferBytes { get; }

    public uint ReceiveBufferStrideBytes { get; }

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

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

    public VirtioNetReceiveLifecyclePublishResult ReceivePublish { get; }

    public VirtioNetQueueNotificationPlanResult NotificationPlan { get; }

    public VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult NotificationExecution { get; }

    public bool Succeeded => IsInitialized && Status == VirtioNetReceiveReadinessStatus.Success;

    internal static VirtioNetReceiveReadinessResult Create(
        VirtioNetReceiveReadinessStatus status,
        VirtioNetQueueMmioSetupPlanResult queueSetup,
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult queueSetupExecution,
        VirtioNetReceiveLifecyclePublishResult receivePublish,
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notificationExecution,
        VirtioNetQueueNotificationBarrierReadiness barrier,
        bool receivePublishAttempted,
        bool notificationPlanAttempted,
        bool notificationWindowExecutionAttempted)
    {
        return new VirtioNetReceiveReadinessResult(
            status,
            queueSetup.Status,
            queueSetupExecution.Status,
            queueSetupExecution.RegisterWindowStatus,
            queueSetupExecution.AccessExecutionStatus,
            receivePublish.Status,
            notificationPlan.Status,
            notificationPlan.NotificationStatus,
            notificationExecution.Status,
            notificationExecution.RegisterWindowPlanStatus,
            notificationExecution.AccessExecutionStatus,
            receivePublishAttempted,
            notificationPlanAttempted,
            notificationWindowExecutionAttempted,
            barrier.Succeeded,
            barrier.BarrierSequence,
            receivePublish.FirstDescriptorIndex,
            receivePublish.PublishedDescriptorCount,
            receivePublish.PreviousUsedIndex,
            receivePublish.ExpectedNextUsedIndex,
            receivePublish.FirstAvailableRingSlot,
            receivePublish.LastAvailableRingSlot,
            receivePublish.FrameBufferBytes,
            receivePublish.ReceiveBufferStrideBytes,
            notificationExecution.QueueIndex,
            notificationExecution.QueueNotifyOffset,
            notificationExecution.PreviousAvailableRingIndex,
            notificationExecution.NextAvailableRingIndex,
            notificationExecution.NotifyRelativeOffset,
            notificationExecution.WriteValue,
            notificationExecution.WriteWidthBytes,
            notificationExecution.CapabilityType,
            notificationExecution.BarIndex,
            notificationExecution.AccessWidth,
            notificationExecution.AccessWidthBytes,
            notificationExecution.BarRelativeOffset,
            notificationExecution.WrittenValue,
            receivePublish,
            notificationPlan,
            notificationExecution);
    }
}

public static class VirtioNetReceiveReadinessPlanner
{
    public const bool RequiresQueueSetupRegisterWindowExecution = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool RequiresBarrierReadiness = true;
    public const bool UsesCallerOwnedQueueMemory = true;
    public const bool UsesCallerOwnedNotifyWindowBuffer = true;
    public const bool PublishesCallerOwnedReceiveDescriptors = true;
    public const bool WritesQueueMemory = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool WritesCallerOwnedNotifyWindowBuffer = true;
    public const bool PerformsLiveMmio = false;
    public const bool MapsBars = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsPacketReceive = false;
    public const bool ReadsQueueMemory = false;

    public static VirtioNetReceiveReadinessResult ComposeReceive(
        VirtioNetQueueMmioSetupPlanResult queueSetup,
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult queueSetupExecution,
        ushort firstDescriptorIndex,
        ushort availableDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort previousUsedIndex,
        ulong firstReceiveBufferPhysicalAddress,
        uint receiveBufferStrideBytes,
        ushort receiveBufferCount,
        uint frameBufferBytes,
        Span<byte> queueMemory,
        VirtioNetQueueNotificationBarrierReadiness barrier,
        VirtioPciBarAperture notifyAperture,
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        Span<byte> notifyWindowBytes,
        ulong negotiatedFeatures,
        bool queueEnabled = true)
    {
        if (!queueSetup.Succeeded)
        {
            return VirtioNetReceiveReadinessResult.Create(
                VirtioNetReceiveReadinessStatus.QueueSetupPlanFailed,
                queueSetup,
                queueSetupExecution,
                default,
                default,
                default,
                barrier,
                receivePublishAttempted: false,
                notificationPlanAttempted: false,
                notificationWindowExecutionAttempted: false);
        }

        if (!queueSetupExecution.Succeeded)
        {
            return VirtioNetReceiveReadinessResult.Create(
                VirtioNetReceiveReadinessStatus.QueueSetupWindowExecutionFailed,
                queueSetup,
                queueSetupExecution,
                default,
                default,
                default,
                barrier,
                receivePublishAttempted: false,
                notificationPlanAttempted: false,
                notificationWindowExecutionAttempted: false);
        }

        if (queueSetupExecution.SetupPlanStatus != queueSetup.Status ||
            queueSetupExecution.CommandsExecuted != queueSetup.CommandCount)
        {
            return VirtioNetReceiveReadinessResult.Create(
                VirtioNetReceiveReadinessStatus.QueueSetupWindowExecutionMismatch,
                queueSetup,
                queueSetupExecution,
                default,
                default,
                default,
                barrier,
                receivePublishAttempted: false,
                notificationPlanAttempted: false,
                notificationWindowExecutionAttempted: false);
        }

        VirtioNetReceiveLifecyclePublishResult receivePublish =
            VirtioNetReceiveLifecycle.PublishReceiveBuffers(
                queueSetup.Plan.RxQueue.PlannedQueueSize,
                firstDescriptorIndex,
                availableDescriptorCount,
                previousAvailableRingIndex,
                previousUsedIndex,
                firstReceiveBufferPhysicalAddress,
                receiveBufferStrideBytes,
                receiveBufferCount,
                frameBufferBytes,
                queueMemory);
        if (!receivePublish.Succeeded)
        {
            return VirtioNetReceiveReadinessResult.Create(
                VirtioNetReceiveReadinessStatus.ReceivePublishFailed,
                queueSetup,
                queueSetupExecution,
                receivePublish,
                default,
                default,
                barrier,
                receivePublishAttempted: true,
                notificationPlanAttempted: false,
                notificationWindowExecutionAttempted: false);
        }

        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                queueSetup,
                receivePublish,
                notifyAperture,
                negotiatedFeatures,
                queueEnabled);
        if (!notificationPlan.Succeeded)
        {
            return VirtioNetReceiveReadinessResult.Create(
                VirtioNetReceiveReadinessStatus.NotificationPlanFailed,
                queueSetup,
                queueSetupExecution,
                receivePublish,
                notificationPlan,
                default,
                barrier,
                receivePublishAttempted: true,
                notificationPlanAttempted: true,
                notificationWindowExecutionAttempted: false);
        }

        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notificationExecution =
            VirtioNetQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notificationPlan,
                barrier,
                notifyConfiguration,
                notifyWindowBytes);
        if (!notificationExecution.Succeeded)
        {
            VirtioNetReceiveReadinessStatus status =
                notificationExecution.Status ==
                    VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady
                        ? VirtioNetReceiveReadinessStatus.BarrierNotReady
                        : VirtioNetReceiveReadinessStatus.NotificationWindowExecutionFailed;

            return VirtioNetReceiveReadinessResult.Create(
                status,
                queueSetup,
                queueSetupExecution,
                receivePublish,
                notificationPlan,
                notificationExecution,
                barrier,
                receivePublishAttempted: true,
                notificationPlanAttempted: true,
                notificationWindowExecutionAttempted: true);
        }

        return VirtioNetReceiveReadinessResult.Create(
            VirtioNetReceiveReadinessStatus.Success,
            queueSetup,
            queueSetupExecution,
            receivePublish,
            notificationPlan,
            notificationExecution,
            barrier,
            receivePublishAttempted: true,
            notificationPlanAttempted: true,
            notificationWindowExecutionAttempted: true);
    }
}
