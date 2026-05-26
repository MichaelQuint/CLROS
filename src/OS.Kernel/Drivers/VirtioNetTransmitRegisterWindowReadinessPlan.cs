namespace OS.Drivers;

public enum VirtioNetTransmitRegisterWindowReadinessStatus
{
    Success = 0,
    QueueSetupPlanFailed = 1,
    QueueSetupWindowExecutionFailed = 2,
    QueueSetupWindowExecutionMismatch = 3,
    TransmitPublishFailed = 4,
    NotificationPlanFailed = 5,
    BarrierNotReady = 6,
    NotificationWindowExecutionFailed = 7
}

public readonly struct VirtioNetTransmitRegisterWindowReadinessResult
{
    private VirtioNetTransmitRegisterWindowReadinessResult(
        bool isInitialized,
        VirtioNetTransmitRegisterWindowReadinessStatus status,
        VirtioNetQueueMmioSetupPlanStatus queueSetupPlanStatus,
        VirtioNetQueueMmioSetupRegisterWindowExecutionStatus queueSetupWindowExecutionStatus,
        VirtioPciBarRegisterWindowPlanStatus queueSetupRegisterWindowStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus queueSetupAccessExecutionStatus,
        VirtioNetTransmitDescriptorPublishPlanStatus transmitPublishPlanStatus,
        VirtioNetTransmitLifecycleStatus transmitPublishStatus,
        VirtioNetQueueNotificationPlanStatus notificationPlanStatus,
        VirtioPciQueueNotificationPlanStatus pciNotificationStatus,
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus notificationWindowExecutionStatus,
        VirtioPciBarRegisterWindowPlanStatus notificationRegisterWindowStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus notificationAccessExecutionStatus,
        bool transmitPublishAttempted,
        bool notificationPlanAttempted,
        bool notificationWindowExecutionAttempted,
        bool barrierSucceeded,
        uint barrierSequence,
        ushort headerDescriptorIndex,
        ushort frameDescriptorIndex,
        ushort descriptorsWritten,
        ushort descriptorsMarkedInFlight,
        ushort queueIndex,
        ushort queueNotifyOffset,
        ushort publishedDescriptorCount,
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
        VirtioNetTransmitDescriptorPublishPlanResult transmitPublish,
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notificationExecution)
    {
        IsInitialized = isInitialized;
        Status = status;
        QueueSetupPlanStatus = queueSetupPlanStatus;
        QueueSetupWindowExecutionStatus = queueSetupWindowExecutionStatus;
        QueueSetupRegisterWindowStatus = queueSetupRegisterWindowStatus;
        QueueSetupAccessExecutionStatus = queueSetupAccessExecutionStatus;
        TransmitPublishPlanStatus = transmitPublishPlanStatus;
        TransmitPublishStatus = transmitPublishStatus;
        NotificationPlanStatus = notificationPlanStatus;
        PciNotificationStatus = pciNotificationStatus;
        NotificationWindowExecutionStatus = notificationWindowExecutionStatus;
        NotificationRegisterWindowStatus = notificationRegisterWindowStatus;
        NotificationAccessExecutionStatus = notificationAccessExecutionStatus;
        TransmitPublishAttempted = transmitPublishAttempted;
        NotificationPlanAttempted = notificationPlanAttempted;
        NotificationWindowExecutionAttempted = notificationWindowExecutionAttempted;
        BarrierSucceeded = barrierSucceeded;
        BarrierSequence = barrierSequence;
        HeaderDescriptorIndex = headerDescriptorIndex;
        FrameDescriptorIndex = frameDescriptorIndex;
        DescriptorsWritten = descriptorsWritten;
        DescriptorsMarkedInFlight = descriptorsMarkedInFlight;
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
        TransmitPublish = transmitPublish;
        NotificationPlan = notificationPlan;
        NotificationExecution = notificationExecution;
    }

    public VirtioNetTransmitRegisterWindowReadinessStatus Status { get; }

    public VirtioNetQueueMmioSetupPlanStatus QueueSetupPlanStatus { get; }

    public VirtioNetQueueMmioSetupRegisterWindowExecutionStatus QueueSetupWindowExecutionStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus QueueSetupRegisterWindowStatus { get; }

    public VirtioPciBarRegisterWindowAccessExecutionStatus QueueSetupAccessExecutionStatus { get; }

    public VirtioNetTransmitDescriptorPublishPlanStatus TransmitPublishPlanStatus { get; }

    public VirtioNetTransmitLifecycleStatus TransmitPublishStatus { get; }

    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }

    public VirtioPciQueueNotificationPlanStatus PciNotificationStatus { get; }

    public VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus NotificationWindowExecutionStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus NotificationRegisterWindowStatus { get; }

    public VirtioPciBarRegisterWindowAccessExecutionStatus NotificationAccessExecutionStatus { get; }

    public bool TransmitPublishAttempted { get; }

    public bool NotificationPlanAttempted { get; }

    public bool NotificationWindowExecutionAttempted { get; }

    public bool BarrierSucceeded { get; }

    public uint BarrierSequence { get; }

    public ushort HeaderDescriptorIndex { get; }

    public ushort FrameDescriptorIndex { get; }

    public ushort DescriptorsWritten { get; }

    public ushort DescriptorsMarkedInFlight { get; }

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

    public VirtioNetTransmitDescriptorPublishPlanResult TransmitPublish { get; }

    public VirtioNetQueueNotificationPlanResult NotificationPlan { get; }

    public VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult NotificationExecution { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized && Status == VirtioNetTransmitRegisterWindowReadinessStatus.Success;

    internal static VirtioNetTransmitRegisterWindowReadinessResult Create(
        VirtioNetTransmitRegisterWindowReadinessStatus status,
        VirtioNetQueueMmioSetupPlanResult queueSetup,
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult queueSetupExecution,
        VirtioNetTransmitDescriptorPublishPlanResult transmitPublish,
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notificationExecution,
        VirtioNetQueueNotificationBarrierReadiness barrier,
        bool transmitPublishAttempted,
        bool notificationPlanAttempted,
        bool notificationWindowExecutionAttempted)
    {
        return new VirtioNetTransmitRegisterWindowReadinessResult(
            isInitialized: true,
            status,
            queueSetup.Status,
            queueSetupExecution.Status,
            queueSetupExecution.RegisterWindowStatus,
            queueSetupExecution.AccessExecutionStatus,
            transmitPublish.Status,
            transmitPublish.TransmitPublishStatus,
            notificationPlan.Status,
            notificationPlan.NotificationStatus,
            notificationExecution.Status,
            notificationExecution.RegisterWindowPlanStatus,
            notificationExecution.AccessExecutionStatus,
            transmitPublishAttempted,
            notificationPlanAttempted,
            notificationWindowExecutionAttempted,
            barrier.Succeeded,
            barrier.BarrierSequence,
            transmitPublish.HeaderDescriptorIndex,
            transmitPublish.FrameDescriptorIndex,
            transmitPublish.DescriptorsWritten,
            transmitPublish.DescriptorsMarkedInFlight,
            notificationExecution.QueueIndex,
            notificationExecution.QueueNotifyOffset,
            notificationExecution.PublishedDescriptorCount,
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
            transmitPublish,
            notificationPlan,
            notificationExecution);
    }
}

public static class VirtioNetTransmitRegisterWindowReadinessPlanner
{
    public const bool RequiresQueueSetupRegisterWindowExecution = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool RequiresBarrierReadiness = true;
    public const bool UsesCallerOwnedQueueMemory = true;
    public const bool UsesCallerOwnedNotifyWindowBuffer = true;
    public const bool PublishesCallerOwnedTransmitDescriptors = true;
    public const bool WritesQueueMemory = true;
    public const bool WritesTransmitHeader = true;
    public const bool MutatesCallerOwnedStateTable = true;
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
    public const bool PerformsPacketTransmit = false;
    public const bool WaitsForUsedRingCompletion = false;
    public const bool ReadsQueueMemory = false;

    public static VirtioNetTransmitRegisterWindowReadinessResult ComposeTransmit(
        VirtioNetQueueMmioSetupPlanResult queueSetup,
        VirtioNetQueueMmioSetupRegisterWindowExecutionResult queueSetupExecution,
        VirtioNetQueuePlanResult queuePlan,
        Span<VirtioNetTransmitDescriptorOwnershipState> descriptorStates,
        ushort availableRingIndex,
        ulong txHeaderPhysicalAddress,
        ulong ethernetFramePhysicalAddress,
        ReadOnlySpan<byte> ethernetFrame,
        Span<byte> txHeader,
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
            return VirtioNetTransmitRegisterWindowReadinessResult.Create(
                VirtioNetTransmitRegisterWindowReadinessStatus.QueueSetupPlanFailed,
                queueSetup,
                queueSetupExecution,
                default,
                default,
                default,
                barrier,
                transmitPublishAttempted: false,
                notificationPlanAttempted: false,
                notificationWindowExecutionAttempted: false);
        }

        if (!queueSetupExecution.Succeeded)
        {
            return VirtioNetTransmitRegisterWindowReadinessResult.Create(
                VirtioNetTransmitRegisterWindowReadinessStatus.QueueSetupWindowExecutionFailed,
                queueSetup,
                queueSetupExecution,
                default,
                default,
                default,
                barrier,
                transmitPublishAttempted: false,
                notificationPlanAttempted: false,
                notificationWindowExecutionAttempted: false);
        }

        if (queueSetupExecution.SetupPlanStatus != queueSetup.Status ||
            queueSetupExecution.CommandsExecuted != queueSetup.CommandCount)
        {
            return VirtioNetTransmitRegisterWindowReadinessResult.Create(
                VirtioNetTransmitRegisterWindowReadinessStatus.QueueSetupWindowExecutionMismatch,
                queueSetup,
                queueSetupExecution,
                default,
                default,
                default,
                barrier,
                transmitPublishAttempted: false,
                notificationPlanAttempted: false,
                notificationWindowExecutionAttempted: false);
        }

        VirtioNetTransmitDescriptorPublishPlanResult transmitPublish =
            VirtioNetTransmitDescriptorPublishPlanner.PublishFromFreeList(
                queuePlan,
                descriptorStates,
                availableRingIndex,
                txHeaderPhysicalAddress,
                ethernetFramePhysicalAddress,
                ethernetFrame,
                txHeader,
                queueMemory);
        if (!transmitPublish.Succeeded)
        {
            return VirtioNetTransmitRegisterWindowReadinessResult.Create(
                VirtioNetTransmitRegisterWindowReadinessStatus.TransmitPublishFailed,
                queueSetup,
                queueSetupExecution,
                transmitPublish,
                default,
                default,
                barrier,
                transmitPublishAttempted: true,
                notificationPlanAttempted: false,
                notificationWindowExecutionAttempted: false);
        }

        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                queueSetup,
                transmitPublish.TransmitPublish,
                notifyAperture,
                negotiatedFeatures,
                queueEnabled);
        if (!notificationPlan.Succeeded)
        {
            return VirtioNetTransmitRegisterWindowReadinessResult.Create(
                VirtioNetTransmitRegisterWindowReadinessStatus.NotificationPlanFailed,
                queueSetup,
                queueSetupExecution,
                transmitPublish,
                notificationPlan,
                default,
                barrier,
                transmitPublishAttempted: true,
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
            VirtioNetTransmitRegisterWindowReadinessStatus status =
                notificationExecution.Status ==
                    VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady
                        ? VirtioNetTransmitRegisterWindowReadinessStatus.BarrierNotReady
                        : VirtioNetTransmitRegisterWindowReadinessStatus.NotificationWindowExecutionFailed;

            return VirtioNetTransmitRegisterWindowReadinessResult.Create(
                status,
                queueSetup,
                queueSetupExecution,
                transmitPublish,
                notificationPlan,
                notificationExecution,
                barrier,
                transmitPublishAttempted: true,
                notificationPlanAttempted: true,
                notificationWindowExecutionAttempted: true);
        }

        return VirtioNetTransmitRegisterWindowReadinessResult.Create(
            VirtioNetTransmitRegisterWindowReadinessStatus.Success,
            queueSetup,
            queueSetupExecution,
            transmitPublish,
            notificationPlan,
            notificationExecution,
            barrier,
            transmitPublishAttempted: true,
            notificationPlanAttempted: true,
            notificationWindowExecutionAttempted: true);
    }
}
