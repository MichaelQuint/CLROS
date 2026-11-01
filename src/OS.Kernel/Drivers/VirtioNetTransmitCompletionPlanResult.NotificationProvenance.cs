namespace OS.Drivers;
using OS.Std.Binary;

public readonly partial struct VirtioNetTransmitCompletionPlanResult
{
    internal readonly struct NotificationProvenance
    {
        private NotificationProvenance(bool usesRegisterWindowNotification, bool succeeded, VirtioNetQueueNotificationExecutionStatus executionStatus, VirtioNetQueueNotificationPlanStatus notificationPlanStatus, VirtioPciQueueNotificationPlanStatus notificationStatus, VirtioPciNotifyWriteStatus sinkStatus, VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus windowExecutionStatus, VirtioPciBarRegisterWindowPlanStatus registerWindowStatus, VirtioPciBarRegisterWindowAccessExecutionStatus windowAccessExecutionStatus, bool windowExecutionAttempted, bool barrierSucceeded, uint barrierSequence, VirtioNetQueueNotificationKind kind, ushort queueIndex, ushort queueNotifyOffset, uint notifyRelativeOffset, ushort writeValue, uint writeWidthBytes, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort nextAvailableRingIndex, VirtioPciCapabilityType capabilityType, byte barIndex, VirtioPciRegisterAccessWidth accessWidth, uint accessWidthBytes, ulong barRelativeOffset, ulong writtenValue)
        {
            UsesRegisterWindowNotification = usesRegisterWindowNotification;
            Succeeded = succeeded;
            ExecutionStatus = executionStatus;
            NotificationPlanStatus = notificationPlanStatus;
            NotificationStatus = notificationStatus;
            SinkStatus = sinkStatus;
            WindowExecutionStatus = windowExecutionStatus;
            RegisterWindowStatus = registerWindowStatus;
            WindowAccessExecutionStatus = windowAccessExecutionStatus;
            WindowExecutionAttempted = windowExecutionAttempted;
            BarrierSucceeded = barrierSucceeded;
            BarrierSequence = barrierSequence;
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
        }

        public bool UsesRegisterWindowNotification { get; }
        public bool Succeeded { get; }
        public VirtioNetQueueNotificationExecutionStatus ExecutionStatus { get; }
        public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }
        public VirtioPciQueueNotificationPlanStatus NotificationStatus { get; }
        public VirtioPciNotifyWriteStatus SinkStatus { get; }
        public VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus WindowExecutionStatus { get; }
        public VirtioPciBarRegisterWindowPlanStatus RegisterWindowStatus { get; }
        public VirtioPciBarRegisterWindowAccessExecutionStatus WindowAccessExecutionStatus { get; }
        public bool WindowExecutionAttempted { get; }
        public bool BarrierSucceeded { get; }
        public uint BarrierSequence { get; }
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

        public static NotificationProvenance FromFakeSink(VirtioNetQueueNotificationExecutionResult notification)
        {
            return new NotificationProvenance(false, notification.Succeeded, notification.Status, notification.NotificationPlanStatus, notification.PciNotificationStatus, notification.SinkStatus, VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success, VirtioPciBarRegisterWindowPlanStatus.Success, VirtioPciBarRegisterWindowAccessExecutionStatus.Success, false, notification.BarrierSucceeded, notification.BarrierSequence, notification.Kind, notification.QueueIndex, notification.QueueNotifyOffset, notification.NotifyRelativeOffset, notification.WriteValue, notification.WriteWidthBytes, notification.PublishedDescriptorCount, notification.PreviousAvailableRingIndex, notification.NextAvailableRingIndex, default, 0, default, 0, 0, 0);
        }

        public static NotificationProvenance FromRegisterWindow(VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notification)
        {
            return new NotificationProvenance(true, notification.Succeeded, MapRegisterWindowStatus(notification.Status), notification.NotificationPlanStatus, notification.PciNotificationStatus, VirtioPciNotifyWriteStatus.Success, notification.Status, notification.RegisterWindowPlanStatus, notification.AccessExecutionStatus, notification.RegisterWindowExecutionAttempted, notification.BarrierSucceeded, notification.BarrierSequence, notification.Kind, notification.QueueIndex, notification.QueueNotifyOffset, notification.NotifyRelativeOffset, notification.WriteValue, notification.WriteWidthBytes, notification.PublishedDescriptorCount, notification.PreviousAvailableRingIndex, notification.NextAvailableRingIndex, notification.CapabilityType, notification.BarIndex, notification.AccessWidth, notification.AccessWidthBytes, notification.BarRelativeOffset, notification.WrittenValue);
        }

        private static VirtioNetQueueNotificationExecutionStatus MapRegisterWindowStatus(VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus status)
        {
            return status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success ? VirtioNetQueueNotificationExecutionStatus.Success : status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.NotificationPlanFailed ? VirtioNetQueueNotificationExecutionStatus.NotificationPlanFailed : status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady ? VirtioNetQueueNotificationExecutionStatus.MemoryBarrierNotReady : status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.UnsupportedWriteWidth ? VirtioNetQueueNotificationExecutionStatus.UnsupportedWriteWidth : VirtioNetQueueNotificationExecutionStatus.SinkWriteFailed;
        }
    }
}
