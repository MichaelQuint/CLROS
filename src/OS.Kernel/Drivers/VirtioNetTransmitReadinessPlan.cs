namespace OS.Drivers;

public enum VirtioNetTransmitReadinessStatus
{
    Success = 0,
    QueueSetupPlanFailed = 1,
    QueueSetupExecutionFailed = 2,
    QueueSetupExecutionMismatch = 3,
    TransmitPublishFailed = 4,
    NotificationPlanFailed = 5,
    BarrierNotReady = 6,
    NotificationExecutionFailed = 7
}

public readonly struct VirtioNetTransmitReadinessResult
{
    private VirtioNetTransmitReadinessResult(
        bool isInitialized,
        VirtioNetTransmitReadinessStatus status,
        VirtioNetQueueMmioSetupPlanStatus queueSetupPlanStatus,
        VirtioNetQueueMmioSetupExecutionStatus queueSetupExecutionStatus,
        VirtioNetTransmitDescriptorPublishPlanStatus transmitPublishPlanStatus,
        VirtioNetTransmitLifecycleStatus transmitPublishStatus,
        VirtioNetQueueNotificationPlanStatus notificationPlanStatus,
        VirtioPciQueueNotificationPlanStatus pciNotificationStatus,
        VirtioNetQueueNotificationExecutionStatus notificationExecutionStatus,
        VirtioPciNotifyWriteStatus notificationSinkStatus,
        bool transmitPublishAttempted,
        bool notificationPlanAttempted,
        bool notificationExecutionAttempted,
        bool barrierSucceeded,
        uint barrierSequence,
        ushort headerDescriptorIndex,
        ushort frameDescriptorIndex,
        ushort descriptorsWritten,
        ushort descriptorsMarkedInFlight,
        ushort queueIndex,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        uint notifyRelativeOffset,
        ushort writeValue,
        VirtioNetTransmitDescriptorPublishPlanResult transmitPublish,
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioNetQueueNotificationExecutionResult notificationExecution)
    {
        IsInitialized = isInitialized;
        Status = status;
        QueueSetupPlanStatus = queueSetupPlanStatus;
        QueueSetupExecutionStatus = queueSetupExecutionStatus;
        TransmitPublishPlanStatus = transmitPublishPlanStatus;
        TransmitPublishStatus = transmitPublishStatus;
        NotificationPlanStatus = notificationPlanStatus;
        PciNotificationStatus = pciNotificationStatus;
        NotificationExecutionStatus = notificationExecutionStatus;
        NotificationSinkStatus = notificationSinkStatus;
        TransmitPublishAttempted = transmitPublishAttempted;
        NotificationPlanAttempted = notificationPlanAttempted;
        NotificationExecutionAttempted = notificationExecutionAttempted;
        BarrierSucceeded = barrierSucceeded;
        BarrierSequence = barrierSequence;
        HeaderDescriptorIndex = headerDescriptorIndex;
        FrameDescriptorIndex = frameDescriptorIndex;
        DescriptorsWritten = descriptorsWritten;
        DescriptorsMarkedInFlight = descriptorsMarkedInFlight;
        QueueIndex = queueIndex;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        NotifyRelativeOffset = notifyRelativeOffset;
        WriteValue = writeValue;
        TransmitPublish = transmitPublish;
        NotificationPlan = notificationPlan;
        NotificationExecution = notificationExecution;
    }

    public VirtioNetTransmitReadinessStatus Status { get; }

    public VirtioNetQueueMmioSetupPlanStatus QueueSetupPlanStatus { get; }

    public VirtioNetQueueMmioSetupExecutionStatus QueueSetupExecutionStatus { get; }

    public VirtioNetTransmitDescriptorPublishPlanStatus TransmitPublishPlanStatus { get; }

    public VirtioNetTransmitLifecycleStatus TransmitPublishStatus { get; }

    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }

    public VirtioPciQueueNotificationPlanStatus PciNotificationStatus { get; }

    public VirtioNetQueueNotificationExecutionStatus NotificationExecutionStatus { get; }

    public VirtioPciNotifyWriteStatus NotificationSinkStatus { get; }

    public bool TransmitPublishAttempted { get; }

    public bool NotificationPlanAttempted { get; }

    public bool NotificationExecutionAttempted { get; }

    public bool BarrierSucceeded { get; }

    public uint BarrierSequence { get; }

    public ushort HeaderDescriptorIndex { get; }

    public ushort FrameDescriptorIndex { get; }

    public ushort DescriptorsWritten { get; }

    public ushort DescriptorsMarkedInFlight { get; }

    public ushort QueueIndex { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public uint NotifyRelativeOffset { get; }

    public ushort WriteValue { get; }

    public VirtioNetTransmitDescriptorPublishPlanResult TransmitPublish { get; }

    public VirtioNetQueueNotificationPlanResult NotificationPlan { get; }

    public VirtioNetQueueNotificationExecutionResult NotificationExecution { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioNetTransmitReadinessStatus.Success;

    internal static VirtioNetTransmitReadinessResult Create(
        VirtioNetTransmitReadinessStatus status,
        VirtioNetQueueMmioSetupPlanResult queueSetup,
        VirtioNetQueueMmioSetupExecutionResult queueSetupExecution,
        VirtioNetTransmitDescriptorPublishPlanResult transmitPublish,
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioNetQueueNotificationExecutionResult notificationExecution,
        VirtioNetQueueNotificationBarrierReadiness barrier,
        bool transmitPublishAttempted,
        bool notificationPlanAttempted,
        bool notificationExecutionAttempted)
    {
        return new VirtioNetTransmitReadinessResult(
            isInitialized: true,
            status,
            queueSetup.Status,
            queueSetupExecution.Status,
            transmitPublish.Status,
            transmitPublish.TransmitPublishStatus,
            notificationPlan.Status,
            notificationPlan.NotificationStatus,
            notificationExecution.Status,
            notificationExecution.SinkStatus,
            transmitPublishAttempted,
            notificationPlanAttempted,
            notificationExecutionAttempted,
            barrier.Succeeded,
            barrier.BarrierSequence,
            transmitPublish.HeaderDescriptorIndex,
            transmitPublish.FrameDescriptorIndex,
            transmitPublish.DescriptorsWritten,
            transmitPublish.DescriptorsMarkedInFlight,
            notificationExecution.QueueIndex,
            notificationExecution.PublishedDescriptorCount,
            notificationExecution.PreviousAvailableRingIndex,
            notificationExecution.NextAvailableRingIndex,
            notificationExecution.NotifyRelativeOffset,
            notificationExecution.WriteValue,
            transmitPublish,
            notificationPlan,
            notificationExecution);
    }
}

public static class VirtioNetTransmitReadinessPlanner
{
    public const bool RequiresQueueSetupExecution = true;
    public const bool RequiresBarrierReadiness = true;
    public const bool UsesInjectedRegisterAccessor = true;
    public const bool UsesFakeRegisterAccessorSink = true;
    public const bool PublishesCallerOwnedDescriptors = true;
    public const bool WritesQueueMemory = true;
    public const bool WritesTransmitHeader = true;
    public const bool MutatesCallerOwnedStateTable = true;
    public const bool PerformsLiveMmio = false;
    public const bool MapsBars = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool PerformsPacketTransmit = false;
    public const bool WaitsForUsedRingCompletion = false;
    public const bool ReadsQueueMemory = false;

    public static VirtioNetTransmitReadinessResult ComposeTransmit(
        VirtioNetQueueMmioSetupPlanResult queueSetup,
        VirtioNetQueueMmioSetupExecutionResult queueSetupExecution,
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
        ulong negotiatedFeatures,
        IVirtioPciRegisterAccessor? registerAccessor)
    {
        if (!queueSetup.Succeeded)
        {
            return VirtioNetTransmitReadinessResult.Create(
                VirtioNetTransmitReadinessStatus.QueueSetupPlanFailed,
                queueSetup,
                queueSetupExecution,
                default,
                default,
                default,
                barrier,
                transmitPublishAttempted: false,
                notificationPlanAttempted: false,
                notificationExecutionAttempted: false);
        }

        if (!queueSetupExecution.Succeeded)
        {
            return VirtioNetTransmitReadinessResult.Create(
                VirtioNetTransmitReadinessStatus.QueueSetupExecutionFailed,
                queueSetup,
                queueSetupExecution,
                default,
                default,
                default,
                barrier,
                transmitPublishAttempted: false,
                notificationPlanAttempted: false,
                notificationExecutionAttempted: false);
        }

        if (queueSetupExecution.SetupPlanStatus != queueSetup.Status ||
            queueSetupExecution.CommandsExecuted != queueSetup.CommandCount)
        {
            return VirtioNetTransmitReadinessResult.Create(
                VirtioNetTransmitReadinessStatus.QueueSetupExecutionMismatch,
                queueSetup,
                queueSetupExecution,
                default,
                default,
                default,
                barrier,
                transmitPublishAttempted: false,
                notificationPlanAttempted: false,
                notificationExecutionAttempted: false);
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
            return VirtioNetTransmitReadinessResult.Create(
                VirtioNetTransmitReadinessStatus.TransmitPublishFailed,
                queueSetup,
                queueSetupExecution,
                transmitPublish,
                default,
                default,
                barrier,
                transmitPublishAttempted: true,
                notificationPlanAttempted: false,
                notificationExecutionAttempted: false);
        }

        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                queueSetup,
                transmitPublish.TransmitPublish,
                notifyAperture,
                negotiatedFeatures);
        if (!notificationPlan.Succeeded)
        {
            return VirtioNetTransmitReadinessResult.Create(
                VirtioNetTransmitReadinessStatus.NotificationPlanFailed,
                queueSetup,
                queueSetupExecution,
                transmitPublish,
                notificationPlan,
                default,
                barrier,
                transmitPublishAttempted: true,
                notificationPlanAttempted: true,
                notificationExecutionAttempted: false);
        }

        VirtioNetQueueNotificationExecutionResult notificationExecution =
            VirtioNetQueueNotificationExecutor.Execute(
                notificationPlan,
                barrier,
                new VirtioPciNotifyRegisterAccessorSink(registerAccessor));
        if (!notificationExecution.Succeeded)
        {
            VirtioNetTransmitReadinessStatus status =
                notificationExecution.Status ==
                    VirtioNetQueueNotificationExecutionStatus.MemoryBarrierNotReady
                        ? VirtioNetTransmitReadinessStatus.BarrierNotReady
                        : VirtioNetTransmitReadinessStatus.NotificationExecutionFailed;

            return VirtioNetTransmitReadinessResult.Create(
                status,
                queueSetup,
                queueSetupExecution,
                transmitPublish,
                notificationPlan,
                notificationExecution,
                barrier,
                transmitPublishAttempted: true,
                notificationPlanAttempted: true,
                notificationExecutionAttempted: true);
        }

        return VirtioNetTransmitReadinessResult.Create(
            VirtioNetTransmitReadinessStatus.Success,
            queueSetup,
            queueSetupExecution,
            transmitPublish,
            notificationPlan,
            notificationExecution,
            barrier,
            transmitPublishAttempted: true,
            notificationPlanAttempted: true,
            notificationExecutionAttempted: true);
    }
}
