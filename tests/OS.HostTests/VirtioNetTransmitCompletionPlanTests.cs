using System.Runtime.CompilerServices;
using System.Reflection;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static partial class VirtioNetTransmitCompletionPlanTests
{
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const ushort RxQueueSize = 8;
    private const ushort TxQueueSize = 4;
    private const ushort RxBufferCount = 3;
    private const ushort RxNotifyOffset = 3;
    private const ushort TxNotifyOffset = 5;
    private const ushort RxDeviceMaximumQueueSize = 16;
    private const ushort TxDeviceMaximumQueueSize = 8;
    private const uint NotifyOffsetMultiplier = 4;
    private const uint NotifyApertureBarOffset = 0x80;
    private const ulong NotifyBarPhysicalBase = 0x80001000UL;
    private const ulong NotifyBarVirtualBase = 0xFFFF800000001000UL;
    private const uint FrameBufferBytes = VirtioNetReceiveLifecycle.MaximumReceiveBufferBytes;
    private const ulong RxBufferPhysicalAddress = 0x0040_0000UL;
    private const ulong TxHeaderPhysicalAddress = 0x0050_0000UL;
    private const ulong TxFramePhysicalAddress = 0x0060_0000UL;
    [HostTest("drivers.virtionet.tx.completion accepts executed notification", HostLane.Fast, 1)]
    [HostTest("drivers.virtionet.tx.completion accepts matching used entry", HostLane.Fast, 0)]
    private static bool TestAcceptsMatchingUsedEntry()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetQueueNotificationExecutionResult notification = CreateExecutedTxNotification(context, publish);
        const ushort previousUsedIndex = 3;
        WriteUsedCompletion(publish.Plan.TxQueue, queueMemory, previousUsedIndex, unchecked((ushort)(previousUsedIndex + 1)), publish.HeadDescriptorIndex, usedByteCount: 0);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, notification, previousUsedIndex, queueMemory);
        return HostCheck.Result(result.Succeeded && result.IsInitialized && result.Status == VirtioNetTransmitCompletionPlanStatus.Success && result.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.Success && result.NotificationExecutionStatus == VirtioNetQueueNotificationExecutionStatus.Success && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success && result.NotificationStatus == VirtioPciQueueNotificationPlanStatus.Success && result.NotificationSinkStatus == VirtioPciNotifyWriteStatus.Success && result.PreviousUsedIndex == previousUsedIndex && result.CurrentUsedIndex == previousUsedIndex + 1 && result.NextUsedIndex == result.CurrentUsedIndex && result.UsedRingSlot == 3 && result.UsedRingElementOffset == GetUsedEntryOffset(publish.Plan.TxQueue, 3) && result.DescriptorHead == publish.HeadDescriptorIndex && result.UsedByteCount == VirtioNetTransmitCompletionPlanner.ExpectedTransmitUsedByteCount && result.ReclaimedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount && result.HeaderDescriptorIndex == publish.Plan.HeaderDescriptor.DescriptorIndex && result.FrameDescriptorIndex == publish.Plan.FrameDescriptor.DescriptorIndex);
    }

    [HostTest("drivers.virtionet.tx.completion advances wrapped used index", HostLane.Fast, 2)]
    private static bool TestAdvancesWrappedUsedIndex()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetQueueNotificationExecutionResult notification = CreateExecutedTxNotification(context, publish);
        const ushort previousUsedIndex = ushort.MaxValue;
        WriteUsedCompletion(publish.Plan.TxQueue, queueMemory, previousUsedIndex, currentUsedIndex: 0, publish.HeadDescriptorIndex, usedByteCount: 0);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, notification, previousUsedIndex, queueMemory);
        return HostCheck.Result(result.Succeeded && result.PreviousUsedIndex == ushort.MaxValue && result.CurrentUsedIndex == 0 && result.NextUsedIndex == 0 && result.UsedRingSlot == TxQueueSize - 1);
    }

    [HostTest("drivers.virtionet.tx.completion reports no completion", HostLane.Fast, 3)]
    private static bool TestReportsNoCompletion()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetQueueNotificationExecutionResult notification = CreateExecutedTxNotification(context, publish);
        const ushort previousUsedIndex = 2;
        EndianPrimitives.WriteUInt16LittleEndian(queueMemory.AsSpan((int)publish.Plan.TxQueue.UsedRing.Offset + 2, sizeof(ushort)), previousUsedIndex);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, notification, previousUsedIndex, queueMemory);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.NoCompletion && result.CurrentUsedIndex == previousUsedIndex && result.ReclaimedDescriptorCount == 0);
    }

    [HostTest("drivers.virtionet.tx.completion rejects unexpected used index", HostLane.Fast, 4)]
    private static bool TestRejectsUnexpectedUsedIndex()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetQueueNotificationExecutionResult notification = CreateExecutedTxNotification(context, publish);
        const ushort previousUsedIndex = 5;
        WriteUsedCompletion(publish.Plan.TxQueue, queueMemory, previousUsedIndex, currentUsedIndex: 7, publish.HeadDescriptorIndex, usedByteCount: 0);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, notification, previousUsedIndex, queueMemory);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.UnexpectedUsedIndex && result.CurrentUsedIndex == 7 && result.DescriptorHead == 0);
    }

    [HostTest("drivers.virtionet.tx.completion rejects wrong descriptor head", HostLane.Fast, 5)]
    private static bool TestRejectsWrongDescriptorHead()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetQueueNotificationExecutionResult notification = CreateExecutedTxNotification(context, publish);
        const ushort previousUsedIndex = 1;
        uint wrongHead = publish.HeadDescriptorIndex + 2u;
        WriteUsedCompletion(publish.Plan.TxQueue, queueMemory, previousUsedIndex, unchecked((ushort)(previousUsedIndex + 1)), wrongHead, usedByteCount: 0);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, notification, previousUsedIndex, queueMemory);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.UnexpectedDescriptorHead && result.DescriptorHead == wrongHead && result.UsedByteCount == 0);
    }

    [HostTest("drivers.virtionet.tx.completion rejects unexpected byte count", HostLane.Fast, 6)]
    private static bool TestRejectsUnexpectedByteCount()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetQueueNotificationExecutionResult notification = CreateExecutedTxNotification(context, publish);
        const ushort previousUsedIndex = 6;
        WriteUsedCompletion(publish.Plan.TxQueue, queueMemory, previousUsedIndex, unchecked((ushort)(previousUsedIndex + 1)), publish.HeadDescriptorIndex, usedByteCount: (uint)publish.EthernetFrameBytes);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, notification, previousUsedIndex, queueMemory);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.UnexpectedByteCount && result.DescriptorHead == publish.HeadDescriptorIndex && result.UsedByteCount == publish.EthernetFrameBytes);
    }

    [HostTest("drivers.virtionet.tx.completion rejects short queue memory", HostLane.Fast, 7)]
    private static bool TestRejectsShortQueueMemory()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out _);
        VirtioNetQueueNotificationExecutionResult notification = CreateExecutedTxNotification(context, publish);
        byte[] shortQueueMemory = new byte[(int)publish.Plan.TxQueue.UsedRing.Offset + 2];
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, notification, previousUsedIndex: 0, shortQueueMemory);
        return HostCheck.Result(!result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.QueueMemoryTooSmall && result.ReclaimedDescriptorCount == 0);
    }

    [HostTest("drivers.virtionet.tx.completion rejects failed publish", HostLane.Fast, 8)]
    private static bool TestRejectsFailedPublish()
    {
        TestContext context = CreateContext();
        byte[] queueMemory = new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset];
        VirtioNetTransmitLifecycleResult failedPublish = VirtioNetTransmitLifecycle.Publish(context.QueuePlan, firstDescriptorIndex: 0, availableDescriptorCount: VirtioNetTransmitLifecycle.RequiredDescriptorCount, availableRingIndex: 0, txHeaderPhysicalAddress: TxHeaderPhysicalAddress, ethernetFramePhysicalAddress: TxFramePhysicalAddress, ethernetFrame: CreateEthernetFrame(), txHeader: [], queueMemory);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(failedPublish, (VirtioNetQueueNotificationExecutionResult)default, previousUsedIndex: 0, queueMemory);
        return HostCheck.Result(!failedPublish.Succeeded && failedPublish.Status == VirtioNetTransmitLifecycleStatus.HeaderBufferTooSmall && !result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.TransmitPublishFailed && result.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.HeaderBufferTooSmall);
    }

    [HostTest("drivers.virtionet.tx.completion rejects failed notification", HostLane.Fast, 9)]
    private static bool TestRejectsFailedNotification()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetQueueNotificationPlanResult failedNotificationPlan = VirtioNetQueueNotificationPlanner.CreateTransmitNotification(context.QueueSetup, publish, context.Apertures.NotifyConfiguration, negotiatedFeatures: 0, queueEnabled: false);
        VirtioNetQueueNotificationExecutionResult failedNotification = VirtioNetQueueNotificationExecutor.Execute(failedNotificationPlan, VirtioNetQueueNotificationBarrierReadiness.Ready(20), new FakeNotifyWriteSink());
        VirtioNetReceiveLifecyclePublishResult rxPublish = CreateRxPublish();
        VirtioNetQueueNotificationExecutionResult rxNotification = CreateExecutedRxNotification(context, rxPublish);
        VirtioNetTransmitLifecycleResult otherPublish = CreateTxPublish(context.QueuePlan, out _);
        VirtioNetQueueNotificationExecutionResult mismatchedNotification = CreateExecutedTxNotification(context, otherPublish);
        VirtioNetTransmitCompletionPlanResult failed = VirtioNetTransmitCompletionPlanner.Create(publish, failedNotification, previousUsedIndex: 0, queueMemory);
        VirtioNetTransmitCompletionPlanResult wrongKind = VirtioNetTransmitCompletionPlanner.Create(publish, rxNotification, previousUsedIndex: 0, queueMemory);
        VirtioNetTransmitCompletionPlanResult mismatch = VirtioNetTransmitCompletionPlanner.Create(CreateTxPublish(context.QueuePlan, availableRingIndex: 3, out byte[] mismatchMemory), mismatchedNotification, previousUsedIndex: 0, mismatchMemory);
        return HostCheck.Result(!failedNotificationPlan.Succeeded && !failedNotification.Succeeded && failedNotification.Status == VirtioNetQueueNotificationExecutionStatus.NotificationPlanFailed && !failed.Succeeded && failed.Status == VirtioNetTransmitCompletionPlanStatus.NotificationFailed && failed.NotificationStatus == VirtioPciQueueNotificationPlanStatus.QueueNotEnabled && failed.NotificationExecutionStatus == VirtioNetQueueNotificationExecutionStatus.NotificationPlanFailed && rxNotification.Succeeded && !wrongKind.Succeeded && wrongKind.Status == VirtioNetTransmitCompletionPlanStatus.NotificationKindMismatch && mismatchedNotification.Succeeded && !mismatch.Succeeded && mismatch.Status == VirtioNetTransmitCompletionPlanStatus.NotificationPublishMismatch);
    }

    [HostTest("drivers.virtionet.tx.completion rejects failed notification execution", HostLane.Fast, 10)]
    private static bool TestRejectsFailedNotificationExecution()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetQueueNotificationPlanResult notificationPlan = CreateTxNotificationPlan(context, publish);
        VirtioNetQueueNotificationExecutionResult failedNotification = VirtioNetQueueNotificationExecutor.Execute(notificationPlan, VirtioNetQueueNotificationBarrierReadiness.Ready(21), sink: null);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, failedNotification, previousUsedIndex: 0, queueMemory);
        return HostCheck.Result(!failedNotification.Succeeded && failedNotification.Status == VirtioNetQueueNotificationExecutionStatus.SinkNotReady && !result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.NotificationFailed && result.NotificationExecutionStatus == VirtioNetQueueNotificationExecutionStatus.SinkNotReady && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success && result.NotificationStatus == VirtioPciQueueNotificationPlanStatus.Success && result.NotificationSinkStatus == VirtioPciNotifyWriteStatus.NotReady);
    }

    [HostTest("drivers.virtionet.tx.completion rejects wrong executed notification kind", HostLane.Fast, 11)]
    private static bool TestRejectsWrongExecutedNotificationKind()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetReceiveLifecyclePublishResult rxPublish = CreateRxPublish();
        VirtioNetQueueNotificationExecutionResult rxNotification = CreateExecutedRxNotification(context, rxPublish);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, rxNotification, previousUsedIndex: 0, queueMemory);
        return HostCheck.Result(rxNotification.Succeeded && rxNotification.Kind == VirtioNetQueueNotificationKind.Receive && !result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.NotificationKindMismatch && result.NotificationExecutionStatus == VirtioNetQueueNotificationExecutionStatus.Success);
    }

    [HostTest("drivers.virtionet.tx.completion rejects executed notification queue mismatch", HostLane.Fast, 12)]
    private static bool TestRejectsExecutedNotificationQueueMismatch()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetQueueNotificationExecutionResult txNotification = CreateExecutedTxNotification(context, publish);
        VirtioNetQueueNotificationExecutionResult mismatchedNotification = CreateExecutedTxNotificationWithQueueIndex(txNotification, VirtioNetQueuePlan.ReceiveQueueIndex);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, mismatchedNotification, previousUsedIndex: 0, queueMemory);
        return HostCheck.Result(txNotification.Succeeded && mismatchedNotification.Succeeded && mismatchedNotification.Kind == VirtioNetQueueNotificationKind.Transmit && mismatchedNotification.QueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex && mismatchedNotification.QueueIndex != VirtioNetQueuePlan.TransmitQueueIndex && !result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.NotificationPublishMismatch);
    }

    [HostTest("drivers.virtionet.tx.completion rejects executed notification index mismatch", HostLane.Fast, 13)]
    private static bool TestRejectsExecutedNotificationIndexMismatch()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, availableRingIndex: 3, out byte[] queueMemory);
        VirtioNetTransmitLifecycleResult otherPublish = CreateTxPublish(context.QueuePlan, out _);
        VirtioNetQueueNotificationExecutionResult mismatchedNotification = CreateExecutedTxNotification(context, otherPublish);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, mismatchedNotification, previousUsedIndex: 0, queueMemory);
        return HostCheck.Result(mismatchedNotification.Succeeded && mismatchedNotification.Kind == VirtioNetQueueNotificationKind.Transmit && mismatchedNotification.PreviousAvailableRingIndex != publish.Plan.AvailableRingIndex && !result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.NotificationPublishMismatch && result.NotificationExecutionStatus == VirtioNetQueueNotificationExecutionStatus.Success);
    }

    [HostTest("drivers.virtionet.tx.completion accepts register-window notification", HostLane.Fast, 14)]
    private static bool TestAcceptsRegisterWindowNotification()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notification = CreateExecutedTxRegisterWindowNotification(context, publish, notifyBytes, barrierSequence: 31);
        const ushort previousUsedIndex = 2;
        WriteUsedCompletion(publish.Plan.TxQueue, queueMemory, previousUsedIndex, unchecked((ushort)(previousUsedIndex + 1)), publish.HeadDescriptorIndex, usedByteCount: 0);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, notification, previousUsedIndex, queueMemory);
        ulong expectedBarOffset = NotifyApertureBarOffset + TxNotifyOffset * NotifyOffsetMultiplier;
        return HostCheck.Result(notification.Succeeded && result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.Success && result.UsesRegisterWindowNotification && result.NotificationWindowExecutionStatus == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success && result.NotificationRegisterWindowStatus == VirtioPciBarRegisterWindowPlanStatus.Success && result.NotificationWindowAccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success && result.NotificationWindowExecutionAttempted && result.NotificationBarrierSucceeded && result.NotificationBarrierSequence == 31 && result.NotificationKind == VirtioNetQueueNotificationKind.Transmit && result.NotificationQueueIndex == VirtioNetQueuePlan.TransmitQueueIndex && result.NotificationQueueNotifyOffset == TxNotifyOffset && result.NotificationNotifyRelativeOffset == TxNotifyOffset * NotifyOffsetMultiplier && result.NotificationWriteValue == VirtioNetQueuePlan.TransmitQueueIndex && result.NotificationWriteWidthBytes == sizeof(ushort) && result.NotificationPublishedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount && result.NotificationPreviousAvailableRingIndex == publish.Plan.AvailableRingIndex && result.NotificationNextAvailableRingIndex == publish.NextAvailableRingIndex && result.NotificationCapabilityType == VirtioPciCapabilityType.NotifyConfiguration && result.NotificationBarIndex == 1 && result.NotificationAccessWidth == VirtioPciRegisterAccessWidth.Word && result.NotificationAccessWidthBytes == sizeof(ushort) && result.NotificationBarRelativeOffset == expectedBarOffset && result.NotificationWrittenValue == VirtioNetQueuePlan.TransmitQueueIndex && ReadUInt16AtBarOffset(notifyBytes, result.NotificationBarRelativeOffset) == VirtioNetQueuePlan.TransmitQueueIndex && result.DescriptorHead == publish.HeadDescriptorIndex && result.ReclaimedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount);
    }

    [HostTest("drivers.virtionet.tx.completion rejects failed register-window notification", HostLane.Fast, 15)]
    private static bool TestRejectsFailedRegisterWindowNotification()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        byte[] shortNotifyBytes = CreateNotifyWindowBytes(checked((int)(NotifyApertureBarOffset + TxNotifyOffset * NotifyOffsetMultiplier + 1)));
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notification = CreateExecutedTxRegisterWindowNotification(context, publish, shortNotifyBytes, barrierSequence: 32);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, notification, previousUsedIndex: 0, queueMemory);
        return HostCheck.Result(!notification.Succeeded && notification.Status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed && !result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.NotificationFailed && result.UsesRegisterWindowNotification && result.NotificationWindowExecutionStatus == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed && result.NotificationExecutionStatus == VirtioNetQueueNotificationExecutionStatus.SinkWriteFailed && result.NotificationWindowAccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall && result.NotificationWindowExecutionAttempted && result.NotificationBarrierSucceeded && result.NotificationBarrierSequence == 32 && result.ReclaimedDescriptorCount == 0);
    }

    [HostTest("drivers.virtionet.tx.completion rejects wrong register-window notification kind", HostLane.Fast, 16)]
    private static bool TestRejectsWrongRegisterWindowNotificationKind()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetReceiveLifecyclePublishResult rxPublish = CreateRxPublish();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult rxNotification = CreateExecutedRxRegisterWindowNotification(context, rxPublish, notifyBytes, barrierSequence: 33);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, rxNotification, previousUsedIndex: 0, queueMemory);
        return HostCheck.Result(rxNotification.Succeeded && rxNotification.Kind == VirtioNetQueueNotificationKind.Receive && !result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.NotificationKindMismatch && result.UsesRegisterWindowNotification && result.NotificationKind == VirtioNetQueueNotificationKind.Receive && result.NotificationWindowExecutionStatus == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success);
    }

    [HostTest("drivers.virtionet.tx.completion rejects register-window notification queue mismatch", HostLane.Fast, 17)]
    private static bool TestRejectsRegisterWindowNotificationQueueMismatch()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult txNotification = CreateExecutedTxRegisterWindowNotification(context, publish, notifyBytes, barrierSequence: 34);
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult mismatchedNotification = CreateRegisterWindowNotificationWithQueueIndex(txNotification, VirtioNetQueuePlan.ReceiveQueueIndex);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, mismatchedNotification, previousUsedIndex: 0, queueMemory);
        return HostCheck.Result(txNotification.Succeeded && mismatchedNotification.Succeeded && mismatchedNotification.Kind == VirtioNetQueueNotificationKind.Transmit && mismatchedNotification.QueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex && !result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.NotificationPublishMismatch && result.UsesRegisterWindowNotification && result.NotificationQueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex);
    }

    [HostTest("drivers.virtionet.tx.completion rejects register-window notification index mismatch", HostLane.Fast, 18)]
    private static bool TestRejectsRegisterWindowNotificationIndexMismatch()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, availableRingIndex: 3, out byte[] queueMemory);
        VirtioNetTransmitLifecycleResult otherPublish = CreateTxPublish(context.QueuePlan, out _);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult mismatchedNotification = CreateExecutedTxRegisterWindowNotification(context, otherPublish, notifyBytes, barrierSequence: 35);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, mismatchedNotification, previousUsedIndex: 0, queueMemory);
        return HostCheck.Result(mismatchedNotification.Succeeded && mismatchedNotification.Kind == VirtioNetQueueNotificationKind.Transmit && mismatchedNotification.PreviousAvailableRingIndex != publish.Plan.AvailableRingIndex && !result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.NotificationPublishMismatch && result.UsesRegisterWindowNotification && result.NotificationPreviousAvailableRingIndex != publish.Plan.AvailableRingIndex);
    }

    [HostTest("drivers.virtionet.tx.completion preserves register-window notification boundary", HostLane.Fast, 19)]
    private static bool TestPreservesRegisterWindowNotificationBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notification = CreateExecutedTxRegisterWindowNotification(context, publish, notifyBytes, barrierSequence: 36);
        WriteUsedCompletion(publish.Plan.TxQueue, queueMemory, previousUsedIndex: 0, currentUsedIndex: 1, publish.HeadDescriptorIndex, usedByteCount: 0);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, notification, previousUsedIndex: 0, queueMemory);
        return HostCheck.Result(result.Succeeded && result.UsesRegisterWindowNotification && result.NotificationWindowExecutionAttempted && VirtioNetQueueNotificationRegisterWindowBarrierExecutor.RequiresBarrierReadiness && VirtioNetQueueNotificationRegisterWindowBarrierExecutor.RequiresRegisterWindowBinding && VirtioNetQueueNotificationRegisterWindowBarrierExecutor.UsesCallerOwnedWindowBuffer && VirtioNetQueueNotificationRegisterWindowBarrierExecutor.InvokesRegisterWindowAccessExecutor && VirtioNetQueueNotificationRegisterWindowBarrierExecutor.WritesCallerOwnedWindowBuffer && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.MapsBars && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.PerformsLiveMmio && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.ContainsVolatileAccess && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.AccessesPhysicalAddresses && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.ExecutesMemoryBarriers && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.NotifiesLiveDevice && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.PollsDevice && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.HandlesInterrupts && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.AllocatesDmaMemory && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.PerformsDmaMapping && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.PerformsPacketIo && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult>() && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetTransmitCompletionPlanResult>());
    }

    [HostTest("drivers.virtionet.tx.completion preserves executed-notification boundary", HostLane.Fast, 21)]
    [HostTest("drivers.virtionet.tx.completion preserves no-mmio boundary", HostLane.Fast, 20)]
    private static bool TestPreservesNoMmioBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan, out byte[] queueMemory);
        VirtioNetQueueNotificationExecutionResult notification = CreateExecutedTxNotification(context, publish);
        WriteUsedCompletion(publish.Plan.TxQueue, queueMemory, previousUsedIndex: 0, currentUsedIndex: 1, publish.HeadDescriptorIndex, usedByteCount: 0);
        VirtioNetTransmitCompletionPlanResult result = VirtioNetTransmitCompletionPlanner.Create(publish, notification, previousUsedIndex: 0, queueMemory);
        return HostCheck.Result(result.Succeeded && !VirtioNetTransmitCompletionPlanner.PerformsMmio && !VirtioNetTransmitCompletionPlanner.PollsDevice && !VirtioNetTransmitCompletionPlanner.HandlesInterrupts && !VirtioNetTransmitCompletionPlanner.NotifiesDevice && !VirtioNetTransmitCompletionPlanner.PerformsDmaMapping && !VirtioNetTransmitCompletionPlanner.PerformsPacketIo && !VirtioNetTransmitCompletionPlanner.MutatesDescriptorAllocator && VirtioNetQueueNotificationExecutor.RequiresBarrierReadiness && VirtioNetQueueNotificationExecutor.UsesFakeSink && VirtioNetQueueNotificationExecutor.WritesNotifySink && !VirtioNetQueueNotificationExecutor.PerformsLiveMmio && !VirtioNetQueueNotificationExecutor.MapsBars && !VirtioNetQueueNotificationExecutor.ReadsQueueMemory && !VirtioNetQueueNotificationExecutor.MutatesDescriptorState && !VirtioNetQueueNotificationExecutor.PollsDevice && !VirtioNetQueueNotificationExecutor.HandlesInterrupts && !VirtioNetQueueNotificationExecutor.PerformsDmaMapping && !VirtioNetQueueNotificationExecutor.AllocatesDmaMemory && !VirtioNetQueueNotificationExecutor.PerformsPacketIo && !VirtioNetQueueNotificationExecutor.NotifiesLiveDevice && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetTransmitCompletionPlanResult>());
    }

    [HostTest("drivers.virtionet.tx.completion default result is not success", HostLane.Fast, 22)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioNetTransmitCompletionPlanResult result = default;
        return HostCheck.Result(!result.IsInitialized && !result.Succeeded && result.Status == VirtioNetTransmitCompletionPlanStatus.Success && result.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.Success && result.NotificationExecutionStatus == VirtioNetQueueNotificationExecutionStatus.Success && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success && result.NotificationStatus == VirtioPciQueueNotificationPlanStatus.Success && result.NotificationSinkStatus == VirtioPciNotifyWriteStatus.Success && result.ReclaimedDescriptorCount == 0 && result.HeaderDescriptorIndex == 0 && result.FrameDescriptorIndex == 0 && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetTransmitCompletionPlanResult>());
    }

    private static VirtioNetTransmitLifecycleResult CreateTxPublish(VirtioNetQueuePlanResult queuePlan, out byte[] queueMemory)
    {
        return CreateTxPublish(queuePlan, availableRingIndex: 7, out queueMemory);
    }

    private static VirtioNetTransmitLifecycleResult CreateTxPublish(VirtioNetQueuePlanResult queuePlan, ushort availableRingIndex, out byte[] queueMemory)
    {
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        queueMemory = new byte[(int)queuePlan.Plan.TxQueue.EndOffset];
        return VirtioNetTransmitLifecycle.Publish(queuePlan, firstDescriptorIndex: 0, availableDescriptorCount: VirtioNetTransmitLifecycle.RequiredDescriptorCount, availableRingIndex, txHeaderPhysicalAddress: TxHeaderPhysicalAddress, ethernetFramePhysicalAddress: TxFramePhysicalAddress, ethernetFrame: CreateEthernetFrame(), txHeader, queueMemory);
    }

    private static VirtioNetQueueNotificationPlanResult CreateTxNotificationPlan(TestContext context, VirtioNetTransmitLifecycleResult publish)
    {
        return VirtioNetQueueNotificationPlanner.CreateTransmitNotification(context.QueueSetup, publish, context.Apertures.NotifyConfiguration, negotiatedFeatures: 0);
    }

    private static VirtioNetQueueNotificationExecutionResult CreateExecutedTxNotification(TestContext context, VirtioNetTransmitLifecycleResult publish)
    {
        return ExecuteNotification(CreateTxNotificationPlan(context, publish));
    }

    private static VirtioNetQueueNotificationExecutionResult CreateExecutedRxNotification(TestContext context, VirtioNetReceiveLifecyclePublishResult publish)
    {
        VirtioNetQueueNotificationPlanResult plan = VirtioNetQueueNotificationPlanner.CreateReceiveNotification(context.QueueSetup, publish, context.Apertures.NotifyConfiguration, negotiatedFeatures: 0);
        return ExecuteNotification(plan);
    }

    private static VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult CreateExecutedTxRegisterWindowNotification(TestContext context, VirtioNetTransmitLifecycleResult publish, Span<byte> notifyBytes, uint barrierSequence)
    {
        return VirtioNetQueueNotificationRegisterWindowBarrierExecutor.Execute(CreateTxNotificationPlan(context, publish), VirtioNetQueueNotificationBarrierReadiness.Ready(barrierSequence), context.RegisterWindows.Bindings.NotifyConfiguration, notifyBytes);
    }

    private static VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult CreateExecutedRxRegisterWindowNotification(TestContext context, VirtioNetReceiveLifecyclePublishResult publish, Span<byte> notifyBytes, uint barrierSequence)
    {
        VirtioNetQueueNotificationPlanResult plan = VirtioNetQueueNotificationPlanner.CreateReceiveNotification(context.QueueSetup, publish, context.Apertures.NotifyConfiguration, negotiatedFeatures: 0);
        return VirtioNetQueueNotificationRegisterWindowBarrierExecutor.Execute(plan, VirtioNetQueueNotificationBarrierReadiness.Ready(barrierSequence), context.RegisterWindows.Bindings.NotifyConfiguration, notifyBytes);
    }

    private static VirtioNetQueueNotificationExecutionResult ExecuteNotification(VirtioNetQueueNotificationPlanResult plan)
    {
        return VirtioNetQueueNotificationExecutor.Execute(plan, VirtioNetQueueNotificationBarrierReadiness.Ready(20), new FakeNotifyWriteSink());
    }

    private static VirtioNetQueueNotificationExecutionResult CreateExecutedTxNotificationWithQueueIndex(VirtioNetQueueNotificationExecutionResult source, ushort queueIndex)
    {
        Type resultType = typeof(VirtioNetQueueNotificationExecutionResult);
        ConstructorInfo? constructor = resultType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, binder: null, types: [typeof(VirtioNetQueueNotificationExecutionStatus), typeof(VirtioNetQueueNotificationPlanStatus), typeof(VirtioPciQueueNotificationPlanStatus), typeof(VirtioPciNotifyWriteStatus), typeof(VirtioNetQueueNotificationKind), typeof(ushort), typeof(ushort), typeof(uint), typeof(ushort), typeof(uint), typeof(ushort), typeof(ushort), typeof(ushort), typeof(bool), typeof(bool), typeof(bool), typeof(uint), typeof(bool)], modifiers: null);
        if (constructor is null)
        {
            throw new InvalidOperationException("Missing notification execution constructor.");
        }

        return (VirtioNetQueueNotificationExecutionResult)constructor.Invoke([source.Status, source.NotificationPlanStatus, source.PciNotificationStatus, source.SinkStatus, VirtioNetQueueNotificationKind.Transmit, queueIndex, source.QueueNotifyOffset, source.NotifyRelativeOffset, source.WriteValue, source.WriteWidthBytes, source.PublishedDescriptorCount, source.PreviousAvailableRingIndex, source.NextAvailableRingIndex, source.DescriptorWritesVisible, source.AvailableRingWritesVisible, source.OrderedBeforeNotify, source.BarrierSequence, source.IsInitialized]);
    }

    private static VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult CreateRegisterWindowNotificationWithQueueIndex(VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult source, ushort queueIndex)
    {
        Type resultType = typeof(VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult);
        ConstructorInfo? constructor = resultType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, binder: null, types: [typeof(VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus), typeof(VirtioNetQueueNotificationPlanStatus), typeof(VirtioPciQueueNotificationPlanStatus), typeof(VirtioPciBarRegisterWindowPlanStatus), typeof(VirtioPciBarRegisterWindowAccessExecutionStatus), typeof(VirtioNetQueueNotificationKind), typeof(ushort), typeof(ushort), typeof(uint), typeof(ushort), typeof(uint), typeof(ushort), typeof(ushort), typeof(ushort), typeof(bool), typeof(bool), typeof(bool), typeof(uint), typeof(bool), typeof(VirtioPciCapabilityType), typeof(byte), typeof(VirtioPciRegisterAccessWidth), typeof(uint), typeof(ulong), typeof(ulong), typeof(bool)], modifiers: null);
        if (constructor is null)
        {
            throw new InvalidOperationException("Missing register-window notification execution constructor.");
        }

        return (VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult)constructor.Invoke([source.Status, source.NotificationPlanStatus, source.PciNotificationStatus, source.RegisterWindowPlanStatus, source.AccessExecutionStatus, VirtioNetQueueNotificationKind.Transmit, queueIndex, source.QueueNotifyOffset, source.NotifyRelativeOffset, source.WriteValue, source.WriteWidthBytes, source.PublishedDescriptorCount, source.PreviousAvailableRingIndex, source.NextAvailableRingIndex, source.DescriptorWritesVisible, source.AvailableRingWritesVisible, source.OrderedBeforeNotify, source.BarrierSequence, source.RegisterWindowExecutionAttempted, source.CapabilityType, source.BarIndex, source.AccessWidth, source.AccessWidthBytes, source.BarRelativeOffset, source.WrittenValue, source.IsInitialized]);
    }

    private static VirtioNetReceiveLifecyclePublishResult CreateRxPublish()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(RxQueueSize);
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];
        return VirtioNetReceiveLifecycle.PublishReceiveBuffers(queueSize: RxQueueSize, firstDescriptorIndex: 1, availableDescriptorCount: 2, previousAvailableRingIndex: 6, previousUsedIndex: 4, firstReceiveBufferPhysicalAddress: RxBufferPhysicalAddress, receiveBufferStrideBytes: FrameBufferBytes, receiveBufferCount: 2, frameBufferBytes: FrameBufferBytes, queueMemory);
    }

    private static void WriteUsedCompletion(VirtqueueSplitRingPlan queue, byte[] queueMemory, ushort previousUsedIndex, ushort currentUsedIndex, uint descriptorHead, uint usedByteCount)
    {
        EndianPrimitives.WriteUInt16LittleEndian(queueMemory.AsSpan((int)queue.UsedRing.Offset + 2, sizeof(ushort)), currentUsedIndex);
        ushort slot = (ushort)(previousUsedIndex & (queue.QueueSize - 1));
        int elementOffset = GetUsedEntryOffset(queue, slot);
        EndianPrimitives.WriteUInt32LittleEndian(queueMemory.AsSpan(elementOffset, sizeof(uint)), descriptorHead);
        EndianPrimitives.WriteUInt32LittleEndian(queueMemory.AsSpan(elementOffset + sizeof(uint), sizeof(uint)), usedByteCount);
    }

    private static int GetUsedEntryOffset(VirtqueueSplitRingPlan queue, int slot)
    {
        return (int)queue.UsedRing.Offset + (int)VirtqueueSplitRingPlan.UsedRingHeaderLength + (slot * (int)VirtqueueSplitRingPlan.UsedRingElementLength);
    }

    private static TestContext CreateContext()
    {
        VirtioPciBindingPlanResult bindingPlan = CreateBindingPlan();
        VirtioPciBarRegisterWindowBindingResult registerWindows = VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(bindingPlan, CreateRegisterWindows());
        VirtioDeviceDescriptor descriptor = CreateNetworkDescriptor(bindingPlan);
        VirtioNetQueuePlanResult queuePlan = VirtioNetQueuePlan.Create(VirtioNetQueuePlan.MinimumQueueCount, RxQueueSize, TxQueueSize, RxBufferCount, FrameBufferBytes);
        if (!queuePlan.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic queue plan.");
        }

        VirtioNetDriverQueueBringUpPlanResult bringUp = VirtioNetDriverQueueBringUpPlan.Create(descriptor, RxQueueSize, TxQueueSize, RxBufferCount, FrameBufferBytes, queuePlan.Plan.RxQueue.RequiredByteLength, queuePlan.Plan.TxQueue.RequiredByteLength, queuePlan.Plan.RxBuffers.RequiredBufferBytes, queuePlan.Plan.TxFrame.RequiredBufferBytes);
        if (!bringUp.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic bring-up plan.");
        }

        VirtioNetQueueMmioSetupCommand[] commands = new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult queueSetup = VirtioNetQueueMmioSetupPlanner.Create(bringUp, CreateRxAddresses(), CreateTxAddresses(), CreateRxMetadata(), CreateTxMetadata(), commands);
        if (!queueSetup.Succeeded || !registerWindows.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic queue setup plan.");
        }

        return new TestContext(bindingPlan.Binding.Apertures, registerWindows, queueSetup, queuePlan);
    }

    private static byte[] CreateEthernetFrame()
    {
        byte[] frame = new byte[EthernetHeader.HeaderLength + 4];
        for (int i = 0; i < frame.Length; i++)
        {
            frame[i] = (byte)(0x20 + i);
        }

        return frame;
    }

    private static VirtioNetQueueMmioSetupQueueAddresses CreateRxAddresses()
    {
        return new VirtioNetQueueMmioSetupQueueAddresses(descriptorTablePhysicalAddress: 0x100000, driverRingPhysicalAddress: 0x101000, deviceRingPhysicalAddress: 0x102000);
    }

    private static VirtioNetQueueMmioSetupQueueAddresses CreateTxAddresses()
    {
        return new VirtioNetQueueMmioSetupQueueAddresses(descriptorTablePhysicalAddress: 0x110000, driverRingPhysicalAddress: 0x111000, deviceRingPhysicalAddress: 0x112000);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateRxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(RxDeviceMaximumQueueSize, hasNotifyOffset: true, RxNotifyOffset);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateTxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(TxDeviceMaximumQueueSize, hasNotifyOffset: true, TxNotifyOffset);
    }

    private static VirtioDeviceDescriptor CreateNetworkDescriptor(VirtioPciBindingPlanResult bindingPlan)
    {
        VirtioDeviceDescriptorSource[] sources = [new VirtioDeviceDescriptorSource(sourceIndex: 41, bindingPlan, VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask, VirtioDeviceInitializationProfile.NetworkRequiredQueueCount, featuresOkAccepted: true)];
        VirtioDeviceConfigurationSource[] configurations = [new VirtioDeviceConfigurationSource(sourceIndex: 41, CreateNetworkConfiguration(new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xCD), VirtioNetDeviceConfig.StatusLinkUp))];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];
        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(sources, configurations, destination);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic virtio-net descriptor.");
        }

        return destination[0];
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan()
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart + VirtioPciDeviceDiscovery.NetworkDeviceId);
        return VirtioPciBindingPlan.Create(CreateScannedDevice(deviceId), CreatePciConfiguration(deviceId), CreateBarSizes());
    }

    private static PciEcamScannedDevice CreateScannedDevice(ushort deviceId)
    {
        return new PciEcamScannedDevice(segmentIndex: 2, new PciEcamSegment(basePhysicalAddress: 0xE0000000UL, segmentGroup: 1, startBus: 0, endBus: 0x7F), new PciScannedDevice(bus: 3, device: 5, function: 0, vendorId: VirtioPciDeviceDiscovery.VirtioVendorId, deviceId: deviceId, revisionId: 0, classCode: 0, subclass: 0, programmingInterface: 0, headerType: PciConfigurationHeader.Type0HeaderType, isMultifunction: false, hasCapabilitiesList: true, capabilityPointer: CommonOffset));
    }

    private static byte[] CreatePciConfiguration(ushort deviceId)
    {
        byte[] config = new byte[256];
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x00, sizeof(ushort)), VirtioPciDeviceDiscovery.VirtioVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x02, sizeof(ushort)), deviceId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x06, sizeof(ushort)), PciConfigurationHeader.StatusCapabilitiesListBit);
        config[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(config, 0x10, 0x80000000);
        WriteUInt32(config, 0x14, 0x80001000);
        WriteUInt32(config, 0x18, 0x80002000);
        WriteUInt32(config, 0x1C, 0x80003000);
        config[0x34] = CommonOffset;
        WriteVirtioCapability(config, CommonOffset, NotifyOffset, VirtioPciCapabilityType.CommonConfiguration, bar: 0, offsetValue: 0x20, length: VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(config, NotifyOffset, IsrOffset, VirtioPciCapabilityType.NotifyConfiguration, bar: 1, offsetValue: NotifyApertureBarOffset, length: 0x100, notifyOffsetMultiplier: NotifyOffsetMultiplier, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(config, IsrOffset, DeviceOffset, VirtioPciCapabilityType.IsrStatus, bar: 2, offsetValue: 0, length: 1, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(config, DeviceOffset, nextOffset: 0, VirtioPciCapabilityType.DeviceConfiguration, bar: 3, offsetValue: 0, length: 0x10, notifyOffsetMultiplier: 0, capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return [0x1000, 0x1000, 0x1000, 0x1000, 0, 0];
    }

    private static VirtioPciMappedBarWindow[] CreateRegisterWindows()
    {
        return [CreateRegisterWindow(0, 0x80000000UL, 0xFFFF800000000000UL), CreateRegisterWindow(1, NotifyBarPhysicalBase, NotifyBarVirtualBase), CreateRegisterWindow(2, 0x80002000UL, 0xFFFF800000002000UL), CreateRegisterWindow(3, 0x80003000UL, 0xFFFF800000003000UL), default, default];
    }

    private static VirtioPciMappedBarWindow CreateRegisterWindow(byte barIndex, ulong physicalBaseAddress, ulong virtualBaseAddress)
    {
        return new VirtioPciMappedBarWindow(barIndex, physicalBaseAddress, virtualBaseAddress, length: 0x1000, isMapped: true, isMmio: true, isDeviceMemory: true, isUncached: true);
    }

    private static byte[] CreateNetworkConfiguration(MacAddress mac, ushort status)
    {
        byte[] configuration = new byte[8];
        configuration[0] = mac.Octet0;
        configuration[1] = mac.Octet1;
        configuration[2] = mac.Octet2;
        configuration[3] = mac.Octet3;
        configuration[4] = mac.Octet4;
        configuration[5] = mac.Octet5;
        EndianPrimitives.WriteUInt16LittleEndian(configuration.AsSpan(6, sizeof(ushort)), status);
        return configuration;
    }

    private static void WriteVirtioCapability(Span<byte> config, byte offset, byte nextOffset, VirtioPciCapabilityType type, byte bar, uint offsetValue, uint length, uint notifyOffsetMultiplier, byte capabilityLength)
    {
        config[offset] = VirtioPciCapabilityListParser.VendorSpecificCapabilityId;
        config[offset + 1] = nextOffset;
        config[offset + 2] = capabilityLength;
        config[offset + 3] = (byte)type;
        config[offset + 4] = bar;
        WriteUInt32(config, offset + 8, offsetValue);
        WriteUInt32(config, offset + 12, length);
        if (type == VirtioPciCapabilityType.NotifyConfiguration)
        {
            WriteUInt32(config, offset + 16, notifyOffsetMultiplier);
        }
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        EndianPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }

    private static byte[] CreateNotifyWindowBytes(int length = 0x1000)
    {
        byte[] notifyBytes = new byte[length];
        Fill(notifyBytes, 0xCC);
        return notifyBytes;
    }

    private static ushort ReadUInt16AtBarOffset(ReadOnlySpan<byte> source, ulong barRelativeOffset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(source.Slice(checked((int)barRelativeOffset), sizeof(ushort)));
    }

    private static void Fill(Span<byte> bytes, byte value)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = value;
        }
    }
}
