namespace OS.Drivers;

public enum VirtioNetQueueNotificationKind
{
    Receive = 0,
    Transmit = 1
}

public enum VirtioNetQueueNotificationPlanStatus
{
    Success = 0,
    QueueSetupFailed = 1,
    ReceivePublishFailed = 2,
    TransmitPublishFailed = 3,
    NotificationPlanFailed = 4
}

public enum VirtioNetQueueNotificationRegisterWindowPlanStatus
{
    Success = 0,
    NotificationPlanFailed = 1,
    UnsupportedWriteWidth = 2,
    RegisterWindowPlanFailed = 3
}

public enum VirtioNetQueueNotificationRegisterWindowExecutionStatus
{
    Success = 0,
    NotificationPlanFailed = 1,
    UnsupportedWriteWidth = 2,
    RegisterWindowPlanFailed = 3,
    AccessExecutionFailed = 4
}

public enum VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus
{
    Success = 0,
    NotificationPlanFailed = 1,
    MemoryBarrierNotReady = 2,
    UnsupportedWriteWidth = 3,
    RegisterWindowPlanFailed = 4,
    AccessExecutionFailed = 5
}

public enum VirtioPciNotifyWriteStatus
{
    Success = 0,
    NotReady = 1,
    RangeOutsideAperture = 2,
    WriteFailed = 3
}

public enum VirtioPciRegisterWriteStatus
{
    Success = 0,
    NotReady = 1,
    RangeOutsideAperture = 2,
    UnsupportedWidth = 3,
    WriteFailed = 4
}

public enum VirtioPciRegisterReadStatus
{
    Success = 0,
    NotReady = 1,
    RangeOutsideAperture = 2,
    UnsupportedWidth = 3,
    ReadFailed = 4
}

public readonly struct VirtioPciRegisterRead16Result
{
    public VirtioPciRegisterRead16Result(
        VirtioPciRegisterReadStatus status,
        ushort value)
    {
        Status = status;
        Value = value;
        IsInitialized = true;
    }

    public VirtioPciRegisterReadStatus Status { get; }

    public ushort Value { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized && Status == VirtioPciRegisterReadStatus.Success;
}

public enum VirtioNetQueueNotificationExecutionStatus
{
    Success = 0,
    NotificationPlanFailed = 1,
    MemoryBarrierNotReady = 2,
    UnsupportedWriteWidth = 3,
    SinkNotReady = 4,
    SinkRangeMismatch = 5,
    SinkWriteFailed = 6
}

public interface IVirtioPciRegisterAccessor
{
    VirtioPciRegisterRead16Result Read16(
        VirtioPciBarAperture aperture,
        uint relativeOffset);

    VirtioPciRegisterWriteStatus Write16(
        VirtioPciBarAperture aperture,
        uint relativeOffset,
        ushort value);

    VirtioPciRegisterWriteStatus Write64(
        VirtioPciBarAperture aperture,
        uint relativeOffset,
        ulong value);
}

public interface IVirtioPciNotifyWriteSink
{
    VirtioPciNotifyWriteStatus Write16(
        VirtioPciBarAperture aperture,
        uint relativeOffset,
        ushort value);
}

public sealed class VirtioPciNotifyRegisterAccessorSink : IVirtioPciNotifyWriteSink
{
    private readonly IVirtioPciRegisterAccessor? _accessor;

    public const bool RequiresInjectedAccessor = true;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsLiveMmioImplementation = false;
    public const bool MapsBars = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool NotifiesLiveDevice = false;

    public VirtioPciNotifyRegisterAccessorSink(IVirtioPciRegisterAccessor? accessor)
    {
        _accessor = accessor;
    }

    public bool HasAccessor => _accessor is not null;

    public VirtioPciNotifyWriteStatus Write16(
        VirtioPciBarAperture aperture,
        uint relativeOffset,
        ushort value)
    {
        if (_accessor is null)
        {
            return VirtioPciNotifyWriteStatus.NotReady;
        }

        return Map(_accessor.Write16(aperture, relativeOffset, value));
    }

    private static VirtioPciNotifyWriteStatus Map(
        VirtioPciRegisterWriteStatus status)
    {
        return status == VirtioPciRegisterWriteStatus.Success
            ? VirtioPciNotifyWriteStatus.Success
            : status == VirtioPciRegisterWriteStatus.RangeOutsideAperture
                ? VirtioPciNotifyWriteStatus.RangeOutsideAperture
                : status == VirtioPciRegisterWriteStatus.NotReady
                    ? VirtioPciNotifyWriteStatus.NotReady
                    : VirtioPciNotifyWriteStatus.WriteFailed;
    }
}

public readonly struct VirtioNetQueueNotificationBarrierReadiness
{
    public VirtioNetQueueNotificationBarrierReadiness(
        bool descriptorWritesVisible,
        bool availableRingWritesVisible,
        bool orderedBeforeNotify,
        uint barrierSequence)
    {
        DescriptorWritesVisible = descriptorWritesVisible;
        AvailableRingWritesVisible = availableRingWritesVisible;
        OrderedBeforeNotify = orderedBeforeNotify;
        BarrierSequence = barrierSequence;
    }

    public bool DescriptorWritesVisible { get; }

    public bool AvailableRingWritesVisible { get; }

    public bool OrderedBeforeNotify { get; }

    public uint BarrierSequence { get; }

    public bool Succeeded =>
        DescriptorWritesVisible &&
        AvailableRingWritesVisible &&
        OrderedBeforeNotify;

    public static VirtioNetQueueNotificationBarrierReadiness Ready(uint barrierSequence)
    {
        return new VirtioNetQueueNotificationBarrierReadiness(
            descriptorWritesVisible: true,
            availableRingWritesVisible: true,
            orderedBeforeNotify: true,
            barrierSequence);
    }
}

public readonly struct VirtioNetQueueNotificationPlan
{
    internal VirtioNetQueueNotificationPlan(
        VirtioNetQueueNotificationKind kind,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        VirtioPciQueueNotificationPlan notificationPlan)
    {
        Kind = kind;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        NotificationPlan = notificationPlan;
    }

    public VirtioNetQueueNotificationKind Kind { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public VirtioPciQueueNotificationPlan NotificationPlan { get; }

    public ushort QueueIndex => NotificationPlan.QueueIndex;

    public ushort QueueNotifyOffset => NotificationPlan.QueueNotifyOffset;

    public uint NotifyRelativeOffset => NotificationPlan.RelativeOffset;

    public ulong NotifyAddress => NotificationPlan.Address;

    public ushort WriteValue => NotificationPlan.WriteValue;

    public uint WriteWidthBytes => NotificationPlan.WriteWidthBytes;

    public bool PerformsMmio => false;
}

public readonly struct VirtioNetQueueNotificationRegisterWindowPlanResult
{
    private VirtioNetQueueNotificationRegisterWindowPlanResult(
        VirtioNetQueueNotificationRegisterWindowPlanStatus status,
        VirtioNetQueueNotificationPlanStatus notificationPlanStatus,
        VirtioPciQueueNotificationPlanStatus pciNotificationStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioNetQueueNotificationKind kind,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyRelativeOffset,
        ushort writeValue,
        uint writeWidthBytes,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        VirtioPciBarRegisterWindowAccessPlan accessPlan,
        bool isInitialized)
    {
        Status = status;
        NotificationPlanStatus = notificationPlanStatus;
        PciNotificationStatus = pciNotificationStatus;
        RegisterWindowPlanStatus = registerWindowPlanStatus;
        Kind = kind;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyRelativeOffset = notifyRelativeOffset;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        AccessPlan = accessPlan;
        IsInitialized = isInitialized;
    }

    public VirtioNetQueueNotificationRegisterWindowPlanStatus Status { get; }

    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }

    public VirtioPciQueueNotificationPlanStatus PciNotificationStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus { get; }

    public VirtioNetQueueNotificationKind Kind { get; }

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public uint NotifyRelativeOffset { get; }

    public ushort WriteValue { get; }

    public uint WriteWidthBytes { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public VirtioPciBarRegisterWindowAccessPlan AccessPlan { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioNetQueueNotificationRegisterWindowPlanStatus.Success;

    internal static VirtioNetQueueNotificationRegisterWindowPlanResult FailedNotification(
        VirtioNetQueueNotificationPlanResult notificationPlan)
    {
        return new VirtioNetQueueNotificationRegisterWindowPlanResult(
            VirtioNetQueueNotificationRegisterWindowPlanStatus.NotificationPlanFailed,
            notificationPlan.Status,
            notificationPlan.NotificationStatus,
            VirtioPciBarRegisterWindowPlanStatus.Success,
            notificationPlan.Kind,
            queueIndex: 0,
            queueNotifyOffset: 0,
            notifyRelativeOffset: 0,
            writeValue: 0,
            writeWidthBytes: 0,
            publishedDescriptorCount: 0,
            previousAvailableRingIndex: 0,
            nextAvailableRingIndex: 0,
            default,
            isInitialized: true);
    }

    internal static VirtioNetQueueNotificationRegisterWindowPlanResult FromPlan(
        VirtioNetQueueNotificationRegisterWindowPlanStatus status,
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessPlan accessPlan)
    {
        VirtioNetQueueNotificationPlan plan = notificationPlan.Plan;
        return new VirtioNetQueueNotificationRegisterWindowPlanResult(
            status,
            notificationPlan.Status,
            notificationPlan.NotificationStatus,
            registerWindowPlanStatus,
            notificationPlan.Kind,
            plan.QueueIndex,
            plan.QueueNotifyOffset,
            plan.NotifyRelativeOffset,
            plan.WriteValue,
            plan.WriteWidthBytes,
            plan.PublishedDescriptorCount,
            plan.PreviousAvailableRingIndex,
            plan.NextAvailableRingIndex,
            accessPlan,
            isInitialized: true);
    }
}

public readonly struct VirtioNetQueueNotificationRegisterWindowExecutionResult
{
    private VirtioNetQueueNotificationRegisterWindowExecutionResult(
        VirtioNetQueueNotificationRegisterWindowExecutionStatus status,
        VirtioNetQueueNotificationPlanStatus notificationPlanStatus,
        VirtioPciQueueNotificationPlanStatus pciNotificationStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioNetQueueNotificationKind kind,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyRelativeOffset,
        ushort writeValue,
        uint writeWidthBytes,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        VirtioPciCapabilityType capabilityType,
        byte barIndex,
        VirtioPciRegisterAccessWidth accessWidth,
        uint accessWidthBytes,
        ulong barRelativeOffset,
        ulong writtenValue,
        bool isInitialized)
    {
        Status = status;
        NotificationPlanStatus = notificationPlanStatus;
        PciNotificationStatus = pciNotificationStatus;
        RegisterWindowPlanStatus = registerWindowPlanStatus;
        AccessExecutionStatus = accessExecutionStatus;
        Kind = kind;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyRelativeOffset = notifyRelativeOffset;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        AccessWidth = accessWidth;
        AccessWidthBytes = accessWidthBytes;
        BarRelativeOffset = barRelativeOffset;
        WrittenValue = writtenValue;
        IsInitialized = isInitialized;
    }

    public VirtioNetQueueNotificationRegisterWindowExecutionStatus Status { get; }

    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }

    public VirtioPciQueueNotificationPlanStatus PciNotificationStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus { get; }

    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus { get; }

    public VirtioNetQueueNotificationKind Kind { get; }

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public uint NotifyRelativeOffset { get; }

    public ushort WriteValue { get; }

    public uint WriteWidthBytes { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public VirtioPciCapabilityType CapabilityType { get; }

    public byte BarIndex { get; }

    public VirtioPciRegisterAccessWidth AccessWidth { get; }

    public uint AccessWidthBytes { get; }

    public ulong BarRelativeOffset { get; }

    public ulong WrittenValue { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioNetQueueNotificationRegisterWindowExecutionStatus.Success;

    internal static VirtioNetQueueNotificationRegisterWindowExecutionResult FromPlan(
        VirtioNetQueueNotificationRegisterWindowExecutionStatus status,
        VirtioNetQueueNotificationRegisterWindowPlanResult plan,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus)
    {
        return new VirtioNetQueueNotificationRegisterWindowExecutionResult(
            status,
            plan.NotificationPlanStatus,
            plan.PciNotificationStatus,
            plan.RegisterWindowPlanStatus,
            accessExecutionStatus,
            plan.Kind,
            plan.QueueIndex,
            plan.QueueNotifyOffset,
            plan.NotifyRelativeOffset,
            plan.WriteValue,
            plan.WriteWidthBytes,
            plan.PublishedDescriptorCount,
            plan.PreviousAvailableRingIndex,
            plan.NextAvailableRingIndex,
            plan.AccessPlan.CapabilityType,
            plan.AccessPlan.BarIndex,
            plan.AccessPlan.Width,
            plan.AccessPlan.WidthBytes,
            plan.AccessPlan.BarRelativeOffset,
            plan.WriteValue,
            isInitialized: true);
    }
}

public readonly struct VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult
{
    private VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult(
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus status,
        VirtioNetQueueNotificationPlanStatus notificationPlanStatus,
        VirtioPciQueueNotificationPlanStatus pciNotificationStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioNetQueueNotificationKind kind,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyRelativeOffset,
        ushort writeValue,
        uint writeWidthBytes,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        bool descriptorWritesVisible,
        bool availableRingWritesVisible,
        bool orderedBeforeNotify,
        uint barrierSequence,
        bool registerWindowExecutionAttempted,
        VirtioPciCapabilityType capabilityType,
        byte barIndex,
        VirtioPciRegisterAccessWidth accessWidth,
        uint accessWidthBytes,
        ulong barRelativeOffset,
        ulong writtenValue,
        bool isInitialized)
    {
        Status = status;
        NotificationPlanStatus = notificationPlanStatus;
        PciNotificationStatus = pciNotificationStatus;
        RegisterWindowPlanStatus = registerWindowPlanStatus;
        AccessExecutionStatus = accessExecutionStatus;
        Kind = kind;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyRelativeOffset = notifyRelativeOffset;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorWritesVisible = descriptorWritesVisible;
        AvailableRingWritesVisible = availableRingWritesVisible;
        OrderedBeforeNotify = orderedBeforeNotify;
        BarrierSequence = barrierSequence;
        RegisterWindowExecutionAttempted = registerWindowExecutionAttempted;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        AccessWidth = accessWidth;
        AccessWidthBytes = accessWidthBytes;
        BarRelativeOffset = barRelativeOffset;
        WrittenValue = writtenValue;
        IsInitialized = isInitialized;
    }

    public VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus Status { get; }

    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }

    public VirtioPciQueueNotificationPlanStatus PciNotificationStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus { get; }

    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus { get; }

    public VirtioNetQueueNotificationKind Kind { get; }

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public uint NotifyRelativeOffset { get; }

    public ushort WriteValue { get; }

    public uint WriteWidthBytes { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public bool DescriptorWritesVisible { get; }

    public bool AvailableRingWritesVisible { get; }

    public bool OrderedBeforeNotify { get; }

    public uint BarrierSequence { get; }

    public bool BarrierSucceeded =>
        DescriptorWritesVisible &&
        AvailableRingWritesVisible &&
        OrderedBeforeNotify;

    public bool RegisterWindowExecutionAttempted { get; }

    public VirtioPciCapabilityType CapabilityType { get; }

    public byte BarIndex { get; }

    public VirtioPciRegisterAccessWidth AccessWidth { get; }

    public uint AccessWidthBytes { get; }

    public ulong BarRelativeOffset { get; }

    public ulong WrittenValue { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success;

    internal static VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult FromPlan(
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus status,
        VirtioNetQueueNotificationRegisterWindowPlanResult plan,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioNetQueueNotificationBarrierReadiness barrier,
        bool registerWindowExecutionAttempted)
    {
        return new VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult(
            status,
            plan.NotificationPlanStatus,
            plan.PciNotificationStatus,
            plan.RegisterWindowPlanStatus,
            accessExecutionStatus,
            plan.Kind,
            plan.QueueIndex,
            plan.QueueNotifyOffset,
            plan.NotifyRelativeOffset,
            plan.WriteValue,
            plan.WriteWidthBytes,
            plan.PublishedDescriptorCount,
            plan.PreviousAvailableRingIndex,
            plan.NextAvailableRingIndex,
            barrier.DescriptorWritesVisible,
            barrier.AvailableRingWritesVisible,
            barrier.OrderedBeforeNotify,
            barrier.BarrierSequence,
            registerWindowExecutionAttempted,
            plan.AccessPlan.CapabilityType,
            plan.AccessPlan.BarIndex,
            plan.AccessPlan.Width,
            plan.AccessPlan.WidthBytes,
            plan.AccessPlan.BarRelativeOffset,
            plan.WriteValue,
            isInitialized: true);
    }

    internal static VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult FromExecution(
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus status,
        VirtioNetQueueNotificationRegisterWindowExecutionResult execution,
        VirtioNetQueueNotificationBarrierReadiness barrier)
    {
        return new VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult(
            status,
            execution.NotificationPlanStatus,
            execution.PciNotificationStatus,
            execution.RegisterWindowPlanStatus,
            execution.AccessExecutionStatus,
            execution.Kind,
            execution.QueueIndex,
            execution.QueueNotifyOffset,
            execution.NotifyRelativeOffset,
            execution.WriteValue,
            execution.WriteWidthBytes,
            execution.PublishedDescriptorCount,
            execution.PreviousAvailableRingIndex,
            execution.NextAvailableRingIndex,
            barrier.DescriptorWritesVisible,
            barrier.AvailableRingWritesVisible,
            barrier.OrderedBeforeNotify,
            barrier.BarrierSequence,
            registerWindowExecutionAttempted: true,
            execution.CapabilityType,
            execution.BarIndex,
            execution.AccessWidth,
            execution.AccessWidthBytes,
            execution.BarRelativeOffset,
            execution.WrittenValue,
            isInitialized: true);
    }
}

public readonly struct VirtioNetQueueNotificationExecutionResult
{
    private VirtioNetQueueNotificationExecutionResult(
        VirtioNetQueueNotificationExecutionStatus status,
        VirtioNetQueueNotificationPlanStatus notificationPlanStatus,
        VirtioPciQueueNotificationPlanStatus pciNotificationStatus,
        VirtioPciNotifyWriteStatus sinkStatus,
        VirtioNetQueueNotificationKind kind,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyRelativeOffset,
        ushort writeValue,
        uint writeWidthBytes,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        bool descriptorWritesVisible,
        bool availableRingWritesVisible,
        bool orderedBeforeNotify,
        uint barrierSequence,
        bool isInitialized)
    {
        Status = status;
        NotificationPlanStatus = notificationPlanStatus;
        PciNotificationStatus = pciNotificationStatus;
        SinkStatus = sinkStatus;
        Kind = kind;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyRelativeOffset = notifyRelativeOffset;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorWritesVisible = descriptorWritesVisible;
        AvailableRingWritesVisible = availableRingWritesVisible;
        OrderedBeforeNotify = orderedBeforeNotify;
        BarrierSequence = barrierSequence;
        IsInitialized = isInitialized;
    }

    public VirtioNetQueueNotificationExecutionStatus Status { get; }

    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }

    public VirtioPciQueueNotificationPlanStatus PciNotificationStatus { get; }

    public VirtioPciNotifyWriteStatus SinkStatus { get; }

    public VirtioNetQueueNotificationKind Kind { get; }

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public uint NotifyRelativeOffset { get; }

    public ushort WriteValue { get; }

    public uint WriteWidthBytes { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public bool DescriptorWritesVisible { get; }

    public bool AvailableRingWritesVisible { get; }

    public bool OrderedBeforeNotify { get; }

    public uint BarrierSequence { get; }

    public bool BarrierSucceeded =>
        DescriptorWritesVisible &&
        AvailableRingWritesVisible &&
        OrderedBeforeNotify;

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized && Status == VirtioNetQueueNotificationExecutionStatus.Success;

    internal static VirtioNetQueueNotificationExecutionResult FromPlan(
        VirtioNetQueueNotificationExecutionStatus status,
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioNetQueueNotificationBarrierReadiness barrier,
        VirtioPciNotifyWriteStatus sinkStatus)
    {
        VirtioNetQueueNotificationPlan plan = notificationPlan.Plan;
        return new VirtioNetQueueNotificationExecutionResult(
            status,
            notificationPlan.Status,
            notificationPlan.NotificationStatus,
            sinkStatus,
            notificationPlan.Kind,
            plan.QueueIndex,
            plan.QueueNotifyOffset,
            plan.NotifyRelativeOffset,
            plan.WriteValue,
            plan.WriteWidthBytes,
            plan.PublishedDescriptorCount,
            plan.PreviousAvailableRingIndex,
            plan.NextAvailableRingIndex,
            barrier.DescriptorWritesVisible,
            barrier.AvailableRingWritesVisible,
            barrier.OrderedBeforeNotify,
            barrier.BarrierSequence,
            isInitialized: true);
    }

    internal static VirtioNetQueueNotificationExecutionResult FailedPlan(
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioNetQueueNotificationBarrierReadiness barrier)
    {
        return new VirtioNetQueueNotificationExecutionResult(
            VirtioNetQueueNotificationExecutionStatus.NotificationPlanFailed,
            notificationPlan.Status,
            notificationPlan.NotificationStatus,
            VirtioPciNotifyWriteStatus.NotReady,
            notificationPlan.Kind,
            queueIndex: 0,
            queueNotifyOffset: 0,
            notifyRelativeOffset: 0,
            writeValue: 0,
            writeWidthBytes: 0,
            publishedDescriptorCount: 0,
            previousAvailableRingIndex: 0,
            nextAvailableRingIndex: 0,
            barrier.DescriptorWritesVisible,
            barrier.AvailableRingWritesVisible,
            barrier.OrderedBeforeNotify,
            barrier.BarrierSequence,
            isInitialized: true);
    }
}

public readonly struct VirtioNetQueueNotificationPlanResult
{
    private VirtioNetQueueNotificationPlanResult(
        VirtioNetQueueNotificationPlanStatus status,
        VirtioNetQueueNotificationKind kind,
        VirtioNetQueueMmioSetupPlanStatus queueSetupStatus,
        VirtioNetReceiveLifecyclePublishStatus receivePublishStatus,
        VirtioNetTransmitLifecycleStatus transmitPublishStatus,
        VirtioPciQueueNotificationPlanStatus notificationStatus,
        VirtioNetQueueNotificationPlan plan,
        bool isInitialized)
    {
        Status = status;
        Kind = kind;
        QueueSetupStatus = queueSetupStatus;
        ReceivePublishStatus = receivePublishStatus;
        TransmitPublishStatus = transmitPublishStatus;
        NotificationStatus = notificationStatus;
        Plan = plan;
        IsInitialized = isInitialized;
    }

    public VirtioNetQueueNotificationPlanStatus Status { get; }

    public VirtioNetQueueNotificationKind Kind { get; }

    public VirtioNetQueueMmioSetupPlanStatus QueueSetupStatus { get; }

    public VirtioNetReceiveLifecyclePublishStatus ReceivePublishStatus { get; }

    public VirtioNetTransmitLifecycleStatus TransmitPublishStatus { get; }

    public VirtioPciQueueNotificationPlanStatus NotificationStatus { get; }

    public VirtioNetQueueNotificationPlan Plan { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized && Status == VirtioNetQueueNotificationPlanStatus.Success;

    internal static VirtioNetQueueNotificationPlanResult Success(
        VirtioNetQueueNotificationKind kind,
        VirtioNetQueueMmioSetupPlanStatus queueSetupStatus,
        VirtioNetReceiveLifecyclePublishStatus receivePublishStatus,
        VirtioNetTransmitLifecycleStatus transmitPublishStatus,
        VirtioPciQueueNotificationPlanStatus notificationStatus,
        VirtioNetQueueNotificationPlan plan)
    {
        return new VirtioNetQueueNotificationPlanResult(
            VirtioNetQueueNotificationPlanStatus.Success,
            kind,
            queueSetupStatus,
            receivePublishStatus,
            transmitPublishStatus,
            notificationStatus,
            plan,
            isInitialized: true);
    }

    internal static VirtioNetQueueNotificationPlanResult Fail(
        VirtioNetQueueNotificationPlanStatus status,
        VirtioNetQueueNotificationKind kind,
        VirtioNetQueueMmioSetupPlanStatus queueSetupStatus,
        VirtioNetReceiveLifecyclePublishStatus receivePublishStatus,
        VirtioNetTransmitLifecycleStatus transmitPublishStatus,
        VirtioPciQueueNotificationPlanStatus notificationStatus)
    {
        return new VirtioNetQueueNotificationPlanResult(
            status,
            kind,
            queueSetupStatus,
            receivePublishStatus,
            transmitPublishStatus,
            notificationStatus,
            default,
            isInitialized: true);
    }
}

public static class VirtioNetQueueNotificationExecutor
{
    public const bool RequiresBarrierReadiness = true;
    public const bool UsesFakeSink = true;
    public const bool WritesNotifySink = true;
    public const bool PerformsLiveMmio = false;
    public const bool MapsBars = false;
    public const bool PublishesDescriptors = false;
    public const bool MutatesDescriptorState = false;
    public const bool ReadsQueueMemory = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsPacketIo = false;
    public const bool NotifiesLiveDevice = false;

    public static VirtioNetQueueNotificationExecutionResult Execute(
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioNetQueueNotificationBarrierReadiness barrier,
        IVirtioPciNotifyWriteSink? sink)
    {
        if (!notificationPlan.Succeeded)
        {
            return VirtioNetQueueNotificationExecutionResult.FailedPlan(
                notificationPlan,
                barrier);
        }

        if (!barrier.Succeeded)
        {
            return VirtioNetQueueNotificationExecutionResult.FromPlan(
                VirtioNetQueueNotificationExecutionStatus.MemoryBarrierNotReady,
                notificationPlan,
                barrier,
                VirtioPciNotifyWriteStatus.NotReady);
        }

        VirtioNetQueueNotificationPlan plan = notificationPlan.Plan;
        if (plan.WriteWidthBytes !=
            VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBytes)
        {
            return VirtioNetQueueNotificationExecutionResult.FromPlan(
                VirtioNetQueueNotificationExecutionStatus.UnsupportedWriteWidth,
                notificationPlan,
                barrier,
                VirtioPciNotifyWriteStatus.NotReady);
        }

        if (sink is null)
        {
            return VirtioNetQueueNotificationExecutionResult.FromPlan(
                VirtioNetQueueNotificationExecutionStatus.SinkNotReady,
                notificationPlan,
                barrier,
                VirtioPciNotifyWriteStatus.NotReady);
        }

        VirtioPciNotifyWriteStatus sinkStatus = sink.Write16(
            plan.NotificationPlan.Aperture,
            plan.NotifyRelativeOffset,
            plan.WriteValue);

        if (sinkStatus == VirtioPciNotifyWriteStatus.Success)
        {
            return VirtioNetQueueNotificationExecutionResult.FromPlan(
                VirtioNetQueueNotificationExecutionStatus.Success,
                notificationPlan,
                barrier,
                sinkStatus);
        }

        VirtioNetQueueNotificationExecutionStatus status =
            sinkStatus == VirtioPciNotifyWriteStatus.RangeOutsideAperture
                ? VirtioNetQueueNotificationExecutionStatus.SinkRangeMismatch
                : sinkStatus == VirtioPciNotifyWriteStatus.NotReady
                    ? VirtioNetQueueNotificationExecutionStatus.SinkNotReady
                    : VirtioNetQueueNotificationExecutionStatus.SinkWriteFailed;

        return VirtioNetQueueNotificationExecutionResult.FromPlan(
            status,
            notificationPlan,
            barrier,
            sinkStatus);
    }
}

public static class VirtioNetQueueNotificationRegisterWindowExecutor
{
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool WritesCallerOwnedWindowBuffer = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;

    public static VirtioNetQueueNotificationRegisterWindowExecutionResult Execute(
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        Span<byte> notifyWindowBytes)
    {
        VirtioNetQueueNotificationRegisterWindowPlanResult plan =
            VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess(
                notificationPlan,
                notifyConfiguration);

        if (!plan.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowExecutionResult.FromPlan(
                MapPlanStatus(plan.Status),
                plan,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success);
        }

        VirtioPciBarRegisterWindowAccessExecutionResult write =
            VirtioPciBarRegisterWindowAccessExecutor.Write(
                plan.AccessPlan,
                plan.WriteValue,
                notifyWindowBytes);
        if (!write.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowExecutionResult.FromPlan(
                VirtioNetQueueNotificationRegisterWindowExecutionStatus.AccessExecutionFailed,
                plan,
                write.Status);
        }

        return VirtioNetQueueNotificationRegisterWindowExecutionResult.FromPlan(
            VirtioNetQueueNotificationRegisterWindowExecutionStatus.Success,
            plan,
            write.Status);
    }

    private static VirtioNetQueueNotificationRegisterWindowExecutionStatus MapPlanStatus(
        VirtioNetQueueNotificationRegisterWindowPlanStatus status)
    {
        return status == VirtioNetQueueNotificationRegisterWindowPlanStatus.NotificationPlanFailed
            ? VirtioNetQueueNotificationRegisterWindowExecutionStatus.NotificationPlanFailed
            : status == VirtioNetQueueNotificationRegisterWindowPlanStatus.UnsupportedWriteWidth
                ? VirtioNetQueueNotificationRegisterWindowExecutionStatus.UnsupportedWriteWidth
                : VirtioNetQueueNotificationRegisterWindowExecutionStatus.RegisterWindowPlanFailed;
    }
}

public static class VirtioNetQueueNotificationRegisterWindowBarrierExecutor
{
    public const bool RequiresBarrierReadiness = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool WritesCallerOwnedWindowBuffer = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;

    public static VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult Execute(
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioNetQueueNotificationBarrierReadiness barrier,
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        Span<byte> notifyWindowBytes)
    {
        VirtioNetQueueNotificationRegisterWindowPlanResult plan =
            VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess(
                notificationPlan,
                notifyConfiguration);

        if (!plan.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(
                MapPlanStatus(plan.Status),
                plan,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                barrier,
                registerWindowExecutionAttempted: false);
        }

        if (!barrier.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady,
                plan,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                barrier,
                registerWindowExecutionAttempted: false);
        }

        VirtioPciBarRegisterWindowAccessExecutionResult write =
            VirtioPciBarRegisterWindowAccessExecutor.Write(
                plan.AccessPlan,
                plan.WriteValue,
                notifyWindowBytes);
        if (!write.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed,
                plan,
                write.Status,
                barrier,
                registerWindowExecutionAttempted: true);
        }

        return VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(
            VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success,
            plan,
            write.Status,
            barrier,
            registerWindowExecutionAttempted: true);
    }

    private static VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus MapPlanStatus(
        VirtioNetQueueNotificationRegisterWindowPlanStatus status)
    {
        return status == VirtioNetQueueNotificationRegisterWindowPlanStatus.NotificationPlanFailed
            ? VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.NotificationPlanFailed
            : status == VirtioNetQueueNotificationRegisterWindowPlanStatus.UnsupportedWriteWidth
                ? VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.UnsupportedWriteWidth
                : VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.RegisterWindowPlanFailed;
    }
}

public static class VirtioNetQueueNotificationPlanner
{
    public const bool PerformsMmio = false;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool PlansMappedRegisterAddresses = true;
    public const bool MapsBars = false;
    public const bool PerformsLiveMmio = false;
    public const bool ContainsVolatileAccess = false;
    public const bool ReadsDeviceRegisters = false;
    public const bool WritesDeviceRegisters = false;
    public const bool PublishesDescriptors = false;
    public const bool NotifiesDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;
    public const bool SupportsNotificationData = false;

    public static VirtioNetQueueNotificationRegisterWindowPlanResult PlanRegisterWindowAccess(
        VirtioNetQueueNotificationPlanResult notificationPlan,
        VirtioPciBarRegisterWindowBinding notifyConfiguration)
    {
        if (!notificationPlan.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowPlanResult.FailedNotification(
                notificationPlan);
        }

        VirtioNetQueueNotificationPlan plan = notificationPlan.Plan;
        if (plan.WriteWidthBytes !=
            VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBytes)
        {
            return VirtioNetQueueNotificationRegisterWindowPlanResult.FromPlan(
                VirtioNetQueueNotificationRegisterWindowPlanStatus.UnsupportedWriteWidth,
                notificationPlan,
                VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth,
                default);
        }

        VirtioPciBarRegisterWindowAccessPlanResult accessPlan =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                notifyConfiguration,
                plan.NotifyRelativeOffset,
                VirtioPciRegisterAccessWidth.Word);
        if (!accessPlan.Succeeded)
        {
            return VirtioNetQueueNotificationRegisterWindowPlanResult.FromPlan(
                VirtioNetQueueNotificationRegisterWindowPlanStatus.RegisterWindowPlanFailed,
                notificationPlan,
                accessPlan.Status,
                default);
        }

        return VirtioNetQueueNotificationRegisterWindowPlanResult.FromPlan(
            VirtioNetQueueNotificationRegisterWindowPlanStatus.Success,
            notificationPlan,
            accessPlan.Status,
            accessPlan.Plan);
    }

    public static VirtioNetQueueNotificationPlanResult CreateReceiveNotification(
        VirtioNetQueueMmioSetupPlanResult queueSetup,
        VirtioNetReceiveLifecyclePublishResult receivePublish,
        VirtioPciBarAperture notifyAperture,
        ulong negotiatedFeatures,
        bool queueEnabled = true)
    {
        if (!queueSetup.Succeeded)
        {
            return Fail(
                VirtioNetQueueNotificationPlanStatus.QueueSetupFailed,
                VirtioNetQueueNotificationKind.Receive,
                queueSetup.Status,
                receivePublish.Status,
                VirtioNetTransmitLifecycleStatus.Success,
                VirtioPciQueueNotificationPlanStatus.Success);
        }

        if (!receivePublish.Succeeded)
        {
            return Fail(
                VirtioNetQueueNotificationPlanStatus.ReceivePublishFailed,
                VirtioNetQueueNotificationKind.Receive,
                queueSetup.Status,
                receivePublish.Status,
                VirtioNetTransmitLifecycleStatus.Success,
                VirtioPciQueueNotificationPlanStatus.Success);
        }

        return CreateNotification(
            VirtioNetQueueNotificationKind.Receive,
            queueSetup,
            queueSetup.Plan.RxQueue,
            notifyAperture,
            negotiatedFeatures,
            queueEnabled,
            receivePublish.PublishedDescriptorCount,
            receivePublish.PreviousAvailableRingIndex,
            receivePublish.NextAvailableRingIndex,
            receivePublish.Status,
            VirtioNetTransmitLifecycleStatus.Success);
    }

    public static VirtioNetQueueNotificationPlanResult CreateTransmitNotification(
        VirtioNetQueueMmioSetupPlanResult queueSetup,
        VirtioNetTransmitLifecycleResult transmitPublish,
        VirtioPciBarAperture notifyAperture,
        ulong negotiatedFeatures,
        bool queueEnabled = true)
    {
        if (!queueSetup.Succeeded)
        {
            return Fail(
                VirtioNetQueueNotificationPlanStatus.QueueSetupFailed,
                VirtioNetQueueNotificationKind.Transmit,
                queueSetup.Status,
                VirtioNetReceiveLifecyclePublishStatus.Success,
                transmitPublish.Status,
                VirtioPciQueueNotificationPlanStatus.Success);
        }

        if (!transmitPublish.Succeeded)
        {
            return Fail(
                VirtioNetQueueNotificationPlanStatus.TransmitPublishFailed,
                VirtioNetQueueNotificationKind.Transmit,
                queueSetup.Status,
                VirtioNetReceiveLifecyclePublishStatus.Success,
                transmitPublish.Status,
                VirtioPciQueueNotificationPlanStatus.Success);
        }

        return CreateNotification(
            VirtioNetQueueNotificationKind.Transmit,
            queueSetup,
            queueSetup.Plan.TxQueue,
            notifyAperture,
            negotiatedFeatures,
            queueEnabled,
            transmitPublish.DescriptorsWritten,
            transmitPublish.Plan.AvailableRingIndex,
            transmitPublish.NextAvailableRingIndex,
            VirtioNetReceiveLifecyclePublishStatus.Success,
            transmitPublish.Status);
    }

    private static VirtioNetQueueNotificationPlanResult CreateNotification(
        VirtioNetQueueNotificationKind kind,
        VirtioNetQueueMmioSetupPlanResult queueSetup,
        VirtioNetQueueMmioSetupQueuePlan queue,
        VirtioPciBarAperture notifyAperture,
        ulong negotiatedFeatures,
        bool queueEnabled,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        VirtioNetReceiveLifecyclePublishStatus receivePublishStatus,
        VirtioNetTransmitLifecycleStatus transmitPublishStatus)
    {
        VirtioPciQueueNotificationPlanResult notification =
            VirtioPciQueueNotificationPlanner.Create(
                notifyAperture,
                queue.QueueIndex,
                VirtioNetQueuePlan.MinimumQueueCount,
                queue.NotifyOffset,
                negotiatedFeatures,
                queueEnabled,
                hasNewlyPublishedDescriptors: publishedDescriptorCount != 0);
        if (!notification.Succeeded)
        {
            return Fail(
                VirtioNetQueueNotificationPlanStatus.NotificationPlanFailed,
                kind,
                queueSetup.Status,
                receivePublishStatus,
                transmitPublishStatus,
                notification.Status);
        }

        VirtioNetQueueNotificationPlan plan = new VirtioNetQueueNotificationPlan(
            kind,
            publishedDescriptorCount,
            previousAvailableRingIndex,
            nextAvailableRingIndex,
            notification.Plan);

        return VirtioNetQueueNotificationPlanResult.Success(
            kind,
            queueSetup.Status,
            receivePublishStatus,
            transmitPublishStatus,
            notification.Status,
            plan);
    }

    private static VirtioNetQueueNotificationPlanResult Fail(
        VirtioNetQueueNotificationPlanStatus status,
        VirtioNetQueueNotificationKind kind,
        VirtioNetQueueMmioSetupPlanStatus queueSetupStatus,
        VirtioNetReceiveLifecyclePublishStatus receivePublishStatus,
        VirtioNetTransmitLifecycleStatus transmitPublishStatus,
        VirtioPciQueueNotificationPlanStatus notificationStatus)
    {
        return VirtioNetQueueNotificationPlanResult.Fail(
            status,
            kind,
            queueSetupStatus,
            receivePublishStatus,
            transmitPublishStatus,
            notificationStatus);
    }
}
