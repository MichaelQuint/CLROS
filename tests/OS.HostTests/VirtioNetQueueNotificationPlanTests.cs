using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class VirtioNetQueueNotificationPlanTests
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

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("drivers.virtionet.queue.notify plans rx notification", TestPlansRxNotification, ref failures);
        Run("drivers.virtionet.queue.notify plans tx notification", TestPlansTxNotification, ref failures);
        Run("drivers.virtionet.queue.notify rejects failed setup", TestRejectsFailedSetup, ref failures);
        Run("drivers.virtionet.queue.notify rejects failed publish", TestRejectsFailedPublish, ref failures);
        Run("drivers.virtionet.queue.notify propagates notify failure", TestPropagatesNotifyFailure, ref failures);
        Run("drivers.virtionet.queue.notify preserves no-mmio boundary", TestPreservesNoMmioBoundary, ref failures);
        Run(
            "drivers.virtionet.queue.notify.window plans tx notification access",
            TestPlansTxNotificationRegisterWindowAccess,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.window plans rx notification access",
            TestPlansRxNotificationRegisterWindowAccess,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.window rejects failed notification plan",
            TestRegisterWindowRejectsFailedNotificationPlan,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.window propagates register window failure",
            TestRegisterWindowPropagatesWindowFailure,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.window preserves no-live-mmio boundary",
            TestRegisterWindowPreservesNoLiveMmioBoundary,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.windowexec writes planned tx notification",
            TestRegisterWindowExecutionWritesPlannedTxNotification,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.windowexec writes planned rx notification",
            TestRegisterWindowExecutionWritesPlannedRxNotification,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.windowexec rejects failed notification plan",
            TestRegisterWindowExecutionRejectsFailedNotificationPlan,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.windowexec propagates register window failure",
            TestRegisterWindowExecutionPropagatesRegisterWindowFailure,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.windowexec propagates access failure",
            TestRegisterWindowExecutionPropagatesAccessFailure,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.windowexec preserves no-live-mmio boundary",
            TestRegisterWindowExecutionPreservesNoLiveMmioBoundary,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.windowbarrier writes planned tx notification",
            TestRegisterWindowBarrierExecutionWritesPlannedTxNotification,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.windowbarrier rejects missing barrier",
            TestRegisterWindowBarrierExecutionRejectsMissingBarrier,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.windowbarrier rejects failed notification plan",
            TestRegisterWindowBarrierExecutionRejectsFailedNotificationPlan,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.windowbarrier propagates register window failure",
            TestRegisterWindowBarrierExecutionPropagatesRegisterWindowFailure,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.windowbarrier propagates access failure",
            TestRegisterWindowBarrierExecutionPropagatesAccessFailure,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.windowbarrier preserves no-live-mmio boundary",
            TestRegisterWindowBarrierExecutionPreservesNoLiveMmioBoundary,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.exec writes planned tx notification",
            TestExecutesPlannedTxNotification,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.exec writes planned rx notification",
            TestExecutesPlannedRxNotification,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.exec rejects missing barrier",
            TestExecutionRejectsMissingBarrier,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.exec rejects failed notification plan",
            TestExecutionRejectsFailedNotificationPlan,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.exec propagates sink failure",
            TestExecutionPropagatesSinkFailure,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.exec preserves no-live-mmio boundary",
            TestExecutionPreservesNoLiveMmioBoundary,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.accessor writes planned tx notification",
            TestAccessorWritesPlannedTxNotification,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.accessor propagates range failure",
            TestAccessorPropagatesRangeFailure,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.accessor propagates write failure",
            TestAccessorPropagatesWriteFailure,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify.accessor preserves no-live-mmio boundary",
            TestAccessorPreservesNoLiveMmioBoundary,
            ref failures);
        Run(
            "drivers.virtionet.queue.notify rejects default results",
            TestRejectsDefaultResults,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException(
                "Virtio-net queue notification plan host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestPlansRxNotification()
    {
        TestContext context = CreateContext();
        VirtioNetReceiveLifecyclePublishResult publish = CreateRxPublish();

        VirtioNetQueueNotificationPlanResult result =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);

        VirtioNetQueueNotificationPlan plan = result.Plan;
        return result.Succeeded
            && result.Status == VirtioNetQueueNotificationPlanStatus.Success
            && result.Kind == VirtioNetQueueNotificationKind.Receive
            && result.QueueSetupStatus == VirtioNetQueueMmioSetupPlanStatus.Success
            && result.ReceivePublishStatus == VirtioNetReceiveLifecyclePublishStatus.Success
            && result.NotificationStatus == VirtioPciQueueNotificationPlanStatus.Success
            && plan.Kind == VirtioNetQueueNotificationKind.Receive
            && plan.QueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex
            && plan.QueueNotifyOffset == RxNotifyOffset
            && plan.PublishedDescriptorCount == publish.PublishedDescriptorCount
            && plan.PreviousAvailableRingIndex == publish.PreviousAvailableRingIndex
            && plan.NextAvailableRingIndex == publish.NextAvailableRingIndex
            && plan.NotifyRelativeOffset == RxNotifyOffset * NotifyOffsetMultiplier
            && plan.WriteValue == VirtioNetQueuePlan.ReceiveQueueIndex
            && plan.WriteWidthBytes == 2
            && !plan.PerformsMmio;
    }

    private static bool TestPlansTxNotification()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);

        VirtioNetQueueNotificationPlanResult result =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);

        VirtioNetQueueNotificationPlan plan = result.Plan;
        return result.Succeeded
            && result.Status == VirtioNetQueueNotificationPlanStatus.Success
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueSetupStatus == VirtioNetQueueMmioSetupPlanStatus.Success
            && result.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.Success
            && result.NotificationStatus == VirtioPciQueueNotificationPlanStatus.Success
            && plan.Kind == VirtioNetQueueNotificationKind.Transmit
            && plan.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && plan.QueueNotifyOffset == TxNotifyOffset
            && plan.PublishedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && plan.PreviousAvailableRingIndex == publish.Plan.AvailableRingIndex
            && plan.NextAvailableRingIndex == publish.NextAvailableRingIndex
            && plan.NotifyRelativeOffset == TxNotifyOffset * NotifyOffsetMultiplier
            && plan.WriteValue == VirtioNetQueuePlan.TransmitQueueIndex
            && plan.WriteWidthBytes == 2;
    }

    private static bool TestRejectsFailedSetup()
    {
        VirtioNetQueueMmioSetupPlanResult failedSetup = CreateFailedQueueSetup();
        VirtioNetReceiveLifecyclePublishResult publish = CreateRxPublish();

        VirtioNetQueueNotificationPlanResult result =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                failedSetup,
                publish,
                CreateBindingPlan().Binding.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);

        return !failedSetup.Succeeded
            && failedSetup.Status == VirtioNetQueueMmioSetupPlanStatus.CommandDestinationTooSmall
            && !result.Succeeded
            && result.Status == VirtioNetQueueNotificationPlanStatus.QueueSetupFailed
            && result.QueueSetupStatus == VirtioNetQueueMmioSetupPlanStatus.CommandDestinationTooSmall
            && result.ReceivePublishStatus == VirtioNetReceiveLifecyclePublishStatus.Success
            && result.NotificationStatus == VirtioPciQueueNotificationPlanStatus.Success;
    }

    private static bool TestRejectsFailedPublish()
    {
        TestContext context = CreateContext();
        VirtioNetReceiveLifecyclePublishResult failedRx =
            VirtioNetReceiveLifecycle.PublishReceiveBuffers(
                queueSize: RxQueueSize,
                firstDescriptorIndex: 0,
                availableDescriptorCount: 0,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                firstReceiveBufferPhysicalAddress: RxBufferPhysicalAddress,
                receiveBufferStrideBytes: FrameBufferBytes,
                receiveBufferCount: 0,
                frameBufferBytes: FrameBufferBytes,
                queueMemory: new byte[(int)context.QueuePlan.Plan.RxQueue.EndOffset]);
        VirtioNetTransmitLifecycleResult failedTx =
            VirtioNetTransmitLifecycle.Publish(
                context.QueuePlan,
                firstDescriptorIndex: 0,
                availableDescriptorCount: VirtioNetTransmitLifecycle.RequiredDescriptorCount,
                availableRingIndex: 0,
                txHeaderPhysicalAddress: TxHeaderPhysicalAddress,
                ethernetFramePhysicalAddress: TxFramePhysicalAddress,
                ethernetFrame: CreateEthernetFrame(),
                txHeader: [],
                queueMemory: new byte[(int)context.QueuePlan.Plan.TxQueue.EndOffset]);

        VirtioNetQueueNotificationPlanResult rxResult =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                context.QueueSetup,
                failedRx,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        VirtioNetQueueNotificationPlanResult txResult =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                failedTx,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);

        return !failedRx.Succeeded
            && failedRx.Status == VirtioNetReceiveLifecyclePublishStatus.InvalidReceiveBufferCount
            && !rxResult.Succeeded
            && rxResult.Status == VirtioNetQueueNotificationPlanStatus.ReceivePublishFailed
            && rxResult.ReceivePublishStatus == VirtioNetReceiveLifecyclePublishStatus.InvalidReceiveBufferCount
            && !failedTx.Succeeded
            && failedTx.Status == VirtioNetTransmitLifecycleStatus.HeaderBufferTooSmall
            && !txResult.Succeeded
            && txResult.Status == VirtioNetQueueNotificationPlanStatus.TransmitPublishFailed
            && txResult.TransmitPublishStatus == VirtioNetTransmitLifecycleStatus.HeaderBufferTooSmall;
    }

    private static bool TestPropagatesNotifyFailure()
    {
        TestContext context = CreateContext();
        VirtioNetReceiveLifecyclePublishResult rxPublish = CreateRxPublish();
        VirtioNetTransmitLifecycleResult txPublish = CreateTxPublish(context.QueuePlan);

        VirtioNetQueueNotificationPlanResult disabled =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                txPublish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0,
                queueEnabled: false);
        VirtioNetQueueNotificationPlanResult unsupportedFeature =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                context.QueueSetup,
                rxPublish,
                context.Apertures.NotifyConfiguration,
                VirtioPciQueueNotificationFeatureBits.NotificationData);
        VirtioNetQueueNotificationPlanResult invalidAperture =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                context.QueueSetup,
                rxPublish,
                context.Apertures.CommonConfiguration,
                negotiatedFeatures: 0);

        return !disabled.Succeeded
            && disabled.Status == VirtioNetQueueNotificationPlanStatus.NotificationPlanFailed
            && disabled.NotificationStatus == VirtioPciQueueNotificationPlanStatus.QueueNotEnabled
            && !unsupportedFeature.Succeeded
            && unsupportedFeature.NotificationStatus ==
                VirtioPciQueueNotificationPlanStatus.UnsupportedNotificationDataFeature
            && !invalidAperture.Succeeded
            && invalidAperture.NotificationStatus ==
                VirtioPciQueueNotificationPlanStatus.InvalidNotifyAperture;
    }

    private static bool TestPreservesNoMmioBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult result =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);

        return result.Succeeded
            && !VirtioNetQueueNotificationPlanner.PerformsMmio
            && !VirtioNetQueueNotificationPlanner.PublishesDescriptors
            && !VirtioNetQueueNotificationPlanner.NotifiesDevice
            && !VirtioNetQueueNotificationPlanner.HandlesInterrupts
            && !VirtioNetQueueNotificationPlanner.PerformsPacketIo
            && !VirtioNetQueueNotificationPlanner.SupportsNotificationData
            && !VirtioPciQueueNotificationPlanner.PerformsMmio
            && !VirtioPciQueueNotificationPlanner.PublishesDescriptors
            && !VirtioPciQueueNotificationPlanner.HandlesInterrupts
            && !VirtioPciQueueNotificationPlanner.PerformsPacketIo
            && !result.Plan.PerformsMmio
            && !result.Plan.NotificationPlan.PerformsMmio;
    }

    private static bool TestPlansTxNotificationRegisterWindowAccess()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();

        VirtioNetQueueNotificationRegisterWindowPlanResult result =
            VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess(
                notificationPlan,
                windows.Bindings.NotifyConfiguration);

        uint notifyRelativeOffset = TxNotifyOffset * NotifyOffsetMultiplier;
        ulong barRelativeOffset = NotifyApertureBarOffset + notifyRelativeOffset;
        return notificationPlan.Succeeded
            && windows.Succeeded
            && result.Succeeded
            && result.Status == VirtioNetQueueNotificationRegisterWindowPlanStatus.Success
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success
            && result.PciNotificationStatus == VirtioPciQueueNotificationPlanStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && result.QueueNotifyOffset == TxNotifyOffset
            && result.NotifyRelativeOffset == notifyRelativeOffset
            && result.WriteValue == VirtioNetQueuePlan.TransmitQueueIndex
            && result.WriteWidthBytes == sizeof(ushort)
            && result.PublishedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.PreviousAvailableRingIndex == publish.Plan.AvailableRingIndex
            && result.NextAvailableRingIndex == publish.NextAvailableRingIndex
            && result.AccessPlan.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.AccessPlan.BarIndex == 1
            && result.AccessPlan.RelativeOffset == notifyRelativeOffset
            && result.AccessPlan.Width == VirtioPciRegisterAccessWidth.Word
            && result.AccessPlan.WidthBytes == sizeof(ushort)
            && result.AccessPlan.BarRelativeOffset == barRelativeOffset
            && result.AccessPlan.PhysicalAddress == NotifyBarPhysicalBase + barRelativeOffset
            && result.AccessPlan.VirtualAddress == NotifyBarVirtualBase + barRelativeOffset;
    }

    private static bool TestPlansRxNotificationRegisterWindowAccess()
    {
        TestContext context = CreateContext();
        VirtioNetReceiveLifecyclePublishResult publish = CreateRxPublish();
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();

        VirtioNetQueueNotificationRegisterWindowPlanResult result =
            VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess(
                notificationPlan,
                windows.Bindings.NotifyConfiguration);

        uint notifyRelativeOffset = RxNotifyOffset * NotifyOffsetMultiplier;
        ulong barRelativeOffset = NotifyApertureBarOffset + notifyRelativeOffset;
        return notificationPlan.Succeeded
            && windows.Succeeded
            && result.Succeeded
            && result.Kind == VirtioNetQueueNotificationKind.Receive
            && result.QueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex
            && result.QueueNotifyOffset == RxNotifyOffset
            && result.NotifyRelativeOffset == notifyRelativeOffset
            && result.WriteValue == VirtioNetQueuePlan.ReceiveQueueIndex
            && result.WriteWidthBytes == sizeof(ushort)
            && result.PublishedDescriptorCount == publish.PublishedDescriptorCount
            && result.PreviousAvailableRingIndex == publish.PreviousAvailableRingIndex
            && result.NextAvailableRingIndex == publish.NextAvailableRingIndex
            && result.AccessPlan.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.AccessPlan.Width == VirtioPciRegisterAccessWidth.Word
            && result.AccessPlan.BarRelativeOffset == barRelativeOffset
            && result.AccessPlan.PhysicalAddress == NotifyBarPhysicalBase + barRelativeOffset
            && result.AccessPlan.VirtualAddress == NotifyBarVirtualBase + barRelativeOffset;
    }

    private static bool TestRegisterWindowRejectsFailedNotificationPlan()
    {
        TestContext context = CreateContext();
        VirtioNetReceiveLifecyclePublishResult publish = CreateRxPublish();
        VirtioNetQueueNotificationPlanResult failedNotificationPlan =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0,
                queueEnabled: false);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();

        VirtioNetQueueNotificationRegisterWindowPlanResult result =
            VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess(
                failedNotificationPlan,
                windows.Bindings.NotifyConfiguration);

        return !failedNotificationPlan.Succeeded
            && failedNotificationPlan.Status == VirtioNetQueueNotificationPlanStatus.NotificationPlanFailed
            && failedNotificationPlan.NotificationStatus == VirtioPciQueueNotificationPlanStatus.QueueNotEnabled
            && windows.Succeeded
            && !result.Succeeded
            && result.Status == VirtioNetQueueNotificationRegisterWindowPlanStatus.NotificationPlanFailed
            && result.NotificationPlanStatus == failedNotificationPlan.Status
            && result.PciNotificationStatus == failedNotificationPlan.NotificationStatus
            && result.Kind == VirtioNetQueueNotificationKind.Receive
            && result.QueueIndex == 0
            && result.NotifyRelativeOffset == 0
            && result.WriteWidthBytes == 0
            && result.AccessPlan.WidthBytes == 0;
    }

    private static bool TestRegisterWindowPropagatesWindowFailure()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);

        VirtioNetQueueNotificationRegisterWindowPlanResult result =
            VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess(
                notificationPlan,
                notifyConfiguration: default);

        return notificationPlan.Succeeded
            && !result.Succeeded
            && result.Status == VirtioNetQueueNotificationRegisterWindowPlanStatus.RegisterWindowPlanFailed
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success
            && result.PciNotificationStatus == VirtioPciQueueNotificationPlanStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.MissingWindow
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && result.QueueNotifyOffset == TxNotifyOffset
            && result.NotifyRelativeOffset == TxNotifyOffset * NotifyOffsetMultiplier
            && result.WriteValue == VirtioNetQueuePlan.TransmitQueueIndex
            && result.WriteWidthBytes == sizeof(ushort)
            && result.AccessPlan.WidthBytes == 0;
    }

    private static bool TestRegisterWindowPreservesNoLiveMmioBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();

        VirtioNetQueueNotificationRegisterWindowPlanResult result =
            VirtioNetQueueNotificationPlanner.PlanRegisterWindowAccess(
                notificationPlan,
                windows.Bindings.NotifyConfiguration);

        return result.Succeeded
            && VirtioNetQueueNotificationPlanner.RequiresRegisterWindowBinding
            && VirtioNetQueueNotificationPlanner.PlansMappedRegisterAddresses
            && !VirtioNetQueueNotificationPlanner.MapsBars
            && !VirtioNetQueueNotificationPlanner.PerformsLiveMmio
            && !VirtioNetQueueNotificationPlanner.ContainsVolatileAccess
            && !VirtioNetQueueNotificationPlanner.ReadsDeviceRegisters
            && !VirtioNetQueueNotificationPlanner.WritesDeviceRegisters
            && !VirtioNetQueueNotificationPlanner.PublishesDescriptors
            && !VirtioNetQueueNotificationPlanner.NotifiesDevice
            && !VirtioNetQueueNotificationPlanner.HandlesInterrupts
            && !VirtioNetQueueNotificationPlanner.AllocatesDmaMemory
            && !VirtioNetQueueNotificationPlanner.PerformsDmaMapping
            && !VirtioNetQueueNotificationPlanner.PerformsPacketIo
            && !VirtioPciBarRegisterWindowPlanner.MapsBars
            && !VirtioPciBarRegisterWindowPlanner.PerformsLiveMmio
            && !VirtioPciBarRegisterWindowPlanner.ContainsVolatileAccess
            && !VirtioPciBarRegisterWindowPlanner.WritesDeviceRegisters
            && !VirtioPciBarRegisterWindowPlanner.NotifiesLiveDevice
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetQueueNotificationRegisterWindowPlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciBarRegisterWindowAccessPlan>();
    }

    private static bool TestRegisterWindowExecutionWritesPlannedTxNotification()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetQueueNotificationRegisterWindowExecutionResult result =
            VirtioNetQueueNotificationRegisterWindowExecutor.Execute(
                notificationPlan,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return notificationPlan.Succeeded
            && windows.Succeeded
            && result.Succeeded
            && result.Status == VirtioNetQueueNotificationRegisterWindowExecutionStatus.Success
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success
            && result.PciNotificationStatus == VirtioPciQueueNotificationPlanStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && result.QueueNotifyOffset == TxNotifyOffset
            && result.NotifyRelativeOffset == TxNotifyOffset * NotifyOffsetMultiplier
            && result.WriteValue == VirtioNetQueuePlan.TransmitQueueIndex
            && result.WriteWidthBytes == sizeof(ushort)
            && result.PublishedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.PreviousAvailableRingIndex == publish.Plan.AvailableRingIndex
            && result.NextAvailableRingIndex == publish.NextAvailableRingIndex
            && result.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.BarIndex == 1
            && result.AccessWidth == VirtioPciRegisterAccessWidth.Word
            && result.AccessWidthBytes == sizeof(ushort)
            && result.BarRelativeOffset
                == NotifyApertureBarOffset + TxNotifyOffset * NotifyOffsetMultiplier
            && result.WrittenValue == VirtioNetQueuePlan.TransmitQueueIndex
            && ReadUInt16AtBarOffset(notifyBytes, result.BarRelativeOffset)
                == VirtioNetQueuePlan.TransmitQueueIndex;
    }

    private static bool TestRegisterWindowExecutionWritesPlannedRxNotification()
    {
        TestContext context = CreateContext();
        VirtioNetReceiveLifecyclePublishResult publish = CreateRxPublish();
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetQueueNotificationRegisterWindowExecutionResult result =
            VirtioNetQueueNotificationRegisterWindowExecutor.Execute(
                notificationPlan,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return notificationPlan.Succeeded
            && windows.Succeeded
            && result.Succeeded
            && result.Kind == VirtioNetQueueNotificationKind.Receive
            && result.QueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex
            && result.QueueNotifyOffset == RxNotifyOffset
            && result.NotifyRelativeOffset == RxNotifyOffset * NotifyOffsetMultiplier
            && result.WriteValue == VirtioNetQueuePlan.ReceiveQueueIndex
            && result.WriteWidthBytes == sizeof(ushort)
            && result.PublishedDescriptorCount == publish.PublishedDescriptorCount
            && result.PreviousAvailableRingIndex == publish.PreviousAvailableRingIndex
            && result.NextAvailableRingIndex == publish.NextAvailableRingIndex
            && result.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.BarIndex == 1
            && result.AccessWidth == VirtioPciRegisterAccessWidth.Word
            && result.AccessWidthBytes == sizeof(ushort)
            && result.BarRelativeOffset
                == NotifyApertureBarOffset + RxNotifyOffset * NotifyOffsetMultiplier
            && result.WrittenValue == VirtioNetQueuePlan.ReceiveQueueIndex
            && ReadUInt16AtBarOffset(notifyBytes, result.BarRelativeOffset)
                == VirtioNetQueuePlan.ReceiveQueueIndex;
    }

    private static bool TestRegisterWindowExecutionRejectsFailedNotificationPlan()
    {
        TestContext context = CreateContext();
        VirtioNetReceiveLifecyclePublishResult publish = CreateRxPublish();
        VirtioNetQueueNotificationPlanResult failedNotificationPlan =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0,
                queueEnabled: false);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetQueueNotificationRegisterWindowExecutionResult result =
            VirtioNetQueueNotificationRegisterWindowExecutor.Execute(
                failedNotificationPlan,
                CreateBoundRegisterWindows().Bindings.NotifyConfiguration,
                notifyBytes);

        return !failedNotificationPlan.Succeeded
            && !result.Succeeded
            && result.Status ==
                VirtioNetQueueNotificationRegisterWindowExecutionStatus.NotificationPlanFailed
            && result.NotificationPlanStatus == failedNotificationPlan.Status
            && result.PciNotificationStatus == failedNotificationPlan.NotificationStatus
            && result.Kind == VirtioNetQueueNotificationKind.Receive
            && result.QueueIndex == 0
            && result.WriteWidthBytes == 0
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestRegisterWindowExecutionPropagatesRegisterWindowFailure()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetQueueNotificationRegisterWindowExecutionResult result =
            VirtioNetQueueNotificationRegisterWindowExecutor.Execute(
                notificationPlan,
                notifyConfiguration: default,
                notifyBytes);

        return notificationPlan.Succeeded
            && !result.Succeeded
            && result.Status ==
                VirtioNetQueueNotificationRegisterWindowExecutionStatus.RegisterWindowPlanFailed
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.MissingWindow
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && result.WriteValue == VirtioNetQueuePlan.TransmitQueueIndex
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestRegisterWindowExecutionPropagatesAccessFailure()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes(
            checked((int)(NotifyApertureBarOffset + notificationPlan.Plan.NotifyRelativeOffset + 1)));

        VirtioNetQueueNotificationRegisterWindowExecutionResult result =
            VirtioNetQueueNotificationRegisterWindowExecutor.Execute(
                notificationPlan,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return !result.Succeeded
            && result.Status ==
                VirtioNetQueueNotificationRegisterWindowExecutionStatus.AccessExecutionFailed
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestRegisterWindowExecutionPreservesNoLiveMmioBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetQueueNotificationRegisterWindowExecutionResult result =
            VirtioNetQueueNotificationRegisterWindowExecutor.Execute(
                notificationPlan,
                CreateBoundRegisterWindows().Bindings.NotifyConfiguration,
                notifyBytes);

        return result.Succeeded
            && VirtioNetQueueNotificationRegisterWindowExecutor.RequiresRegisterWindowBinding
            && VirtioNetQueueNotificationRegisterWindowExecutor.UsesCallerOwnedWindowBuffer
            && VirtioNetQueueNotificationRegisterWindowExecutor.InvokesRegisterWindowAccessExecutor
            && VirtioNetQueueNotificationRegisterWindowExecutor.WritesCallerOwnedWindowBuffer
            && !VirtioNetQueueNotificationRegisterWindowExecutor.MapsBars
            && !VirtioNetQueueNotificationRegisterWindowExecutor.PerformsLiveMmio
            && !VirtioNetQueueNotificationRegisterWindowExecutor.ContainsVolatileAccess
            && !VirtioNetQueueNotificationRegisterWindowExecutor.AccessesPhysicalAddresses
            && !VirtioNetQueueNotificationRegisterWindowExecutor.ExecutesMemoryBarriers
            && !VirtioNetQueueNotificationRegisterWindowExecutor.NotifiesLiveDevice
            && !VirtioNetQueueNotificationRegisterWindowExecutor.PollsDevice
            && !VirtioNetQueueNotificationRegisterWindowExecutor.HandlesInterrupts
            && !VirtioNetQueueNotificationRegisterWindowExecutor.AllocatesDmaMemory
            && !VirtioNetQueueNotificationRegisterWindowExecutor.PerformsDmaMapping
            && !VirtioNetQueueNotificationRegisterWindowExecutor.PerformsPacketIo
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetQueueNotificationRegisterWindowExecutionResult>();
    }

    private static bool TestRegisterWindowBarrierExecutionWritesPlannedTxNotification()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult result =
            VirtioNetQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notificationPlan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(21),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return notificationPlan.Succeeded
            && windows.Succeeded
            && result.Succeeded
            && result.Status ==
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success
            && result.PciNotificationStatus == VirtioPciQueueNotificationPlanStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && result.QueueNotifyOffset == TxNotifyOffset
            && result.NotifyRelativeOffset == TxNotifyOffset * NotifyOffsetMultiplier
            && result.WriteValue == VirtioNetQueuePlan.TransmitQueueIndex
            && result.WriteWidthBytes == sizeof(ushort)
            && result.PublishedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.PreviousAvailableRingIndex == publish.Plan.AvailableRingIndex
            && result.NextAvailableRingIndex == publish.NextAvailableRingIndex
            && result.BarrierSucceeded
            && result.BarrierSequence == 21
            && result.RegisterWindowExecutionAttempted
            && result.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.BarIndex == 1
            && result.AccessWidth == VirtioPciRegisterAccessWidth.Word
            && result.AccessWidthBytes == sizeof(ushort)
            && result.BarRelativeOffset
                == NotifyApertureBarOffset + TxNotifyOffset * NotifyOffsetMultiplier
            && result.WrittenValue == VirtioNetQueuePlan.TransmitQueueIndex
            && ReadUInt16AtBarOffset(notifyBytes, result.BarRelativeOffset)
                == VirtioNetQueuePlan.TransmitQueueIndex;
    }

    private static bool TestRegisterWindowBarrierExecutionRejectsMissingBarrier()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioNetQueueNotificationBarrierReadiness missingBarrier =
            new VirtioNetQueueNotificationBarrierReadiness(
                descriptorWritesVisible: true,
                availableRingWritesVisible: false,
                orderedBeforeNotify: true,
                barrierSequence: 22);

        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult result =
            VirtioNetQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notificationPlan,
                missingBarrier,
                CreateBoundRegisterWindows().Bindings.NotifyConfiguration,
                notifyBytes);

        return notificationPlan.Succeeded
            && !result.Succeeded
            && result.Status ==
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && !result.BarrierSucceeded
            && result.DescriptorWritesVisible
            && !result.AvailableRingWritesVisible
            && result.OrderedBeforeNotify
            && result.BarrierSequence == 22
            && !result.RegisterWindowExecutionAttempted
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestRegisterWindowBarrierExecutionRejectsFailedNotificationPlan()
    {
        TestContext context = CreateContext();
        VirtioNetReceiveLifecyclePublishResult publish = CreateRxPublish();
        VirtioNetQueueNotificationPlanResult failedNotificationPlan =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0,
                queueEnabled: false);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult result =
            VirtioNetQueueNotificationRegisterWindowBarrierExecutor.Execute(
                failedNotificationPlan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(23),
                CreateBoundRegisterWindows().Bindings.NotifyConfiguration,
                notifyBytes);

        return !failedNotificationPlan.Succeeded
            && !result.Succeeded
            && result.Status ==
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.NotificationPlanFailed
            && result.NotificationPlanStatus == failedNotificationPlan.Status
            && result.PciNotificationStatus == failedNotificationPlan.NotificationStatus
            && result.Kind == VirtioNetQueueNotificationKind.Receive
            && result.QueueIndex == 0
            && result.WriteWidthBytes == 0
            && result.BarrierSucceeded
            && result.BarrierSequence == 23
            && !result.RegisterWindowExecutionAttempted
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestRegisterWindowBarrierExecutionPropagatesRegisterWindowFailure()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult result =
            VirtioNetQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notificationPlan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(24),
                notifyConfiguration: default,
                notifyBytes);

        return notificationPlan.Succeeded
            && !result.Succeeded
            && result.Status ==
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.RegisterWindowPlanFailed
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.MissingWindow
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && result.WriteValue == VirtioNetQueuePlan.TransmitQueueIndex
            && result.BarrierSucceeded
            && result.BarrierSequence == 24
            && !result.RegisterWindowExecutionAttempted
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestRegisterWindowBarrierExecutionPropagatesAccessFailure()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes(
            checked((int)(NotifyApertureBarOffset + notificationPlan.Plan.NotifyRelativeOffset + 1)));

        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult result =
            VirtioNetQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notificationPlan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(25),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return !result.Succeeded
            && result.Status ==
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus ==
                VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && result.BarrierSucceeded
            && result.BarrierSequence == 25
            && result.RegisterWindowExecutionAttempted
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestRegisterWindowBarrierExecutionPreservesNoLiveMmioBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult notificationPlan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult result =
            VirtioNetQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notificationPlan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(26),
                CreateBoundRegisterWindows().Bindings.NotifyConfiguration,
                notifyBytes);

        return result.Succeeded
            && VirtioNetQueueNotificationRegisterWindowBarrierExecutor.RequiresBarrierReadiness
            && VirtioNetQueueNotificationRegisterWindowBarrierExecutor.RequiresRegisterWindowBinding
            && VirtioNetQueueNotificationRegisterWindowBarrierExecutor.UsesCallerOwnedWindowBuffer
            && VirtioNetQueueNotificationRegisterWindowBarrierExecutor.InvokesRegisterWindowAccessExecutor
            && VirtioNetQueueNotificationRegisterWindowBarrierExecutor.WritesCallerOwnedWindowBuffer
            && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.MapsBars
            && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.PerformsLiveMmio
            && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.ContainsVolatileAccess
            && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.AccessesPhysicalAddresses
            && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.ExecutesMemoryBarriers
            && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.NotifiesLiveDevice
            && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.PollsDevice
            && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.HandlesInterrupts
            && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.AllocatesDmaMemory
            && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.PerformsDmaMapping
            && !VirtioNetQueueNotificationRegisterWindowBarrierExecutor.PerformsPacketIo
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult>();
    }

    private static bool TestExecutesPlannedTxNotification()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult plan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        byte[] notifyBytes = CreateNotifyBytes(context);
        FakeNotifyWriteSink sink = new FakeNotifyWriteSink(notifyBytes);

        VirtioNetQueueNotificationExecutionResult result =
            VirtioNetQueueNotificationExecutor.Execute(
                plan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(11),
                sink);

        int notifyOffset = checked((int)plan.Plan.NotifyRelativeOffset);
        return result.Succeeded
            && result.Status == VirtioNetQueueNotificationExecutionStatus.Success
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success
            && result.PciNotificationStatus == VirtioPciQueueNotificationPlanStatus.Success
            && result.SinkStatus == VirtioPciNotifyWriteStatus.Success
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && result.QueueNotifyOffset == TxNotifyOffset
            && result.NotifyRelativeOffset == TxNotifyOffset * NotifyOffsetMultiplier
            && result.WriteValue == VirtioNetQueuePlan.TransmitQueueIndex
            && result.WriteWidthBytes == 2
            && result.PublishedDescriptorCount == VirtioNetTransmitLifecycle.RequiredDescriptorCount
            && result.PreviousAvailableRingIndex == publish.Plan.AvailableRingIndex
            && result.NextAvailableRingIndex == publish.NextAvailableRingIndex
            && result.BarrierSucceeded
            && result.BarrierSequence == 11
            && sink.WriteCount == 1
            && sink.LastAperture.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && sink.LastRelativeOffset == result.NotifyRelativeOffset
            && sink.LastValue == result.WriteValue
            && ReadUInt16(notifyBytes, notifyOffset) == VirtioNetQueuePlan.TransmitQueueIndex
            && AllBytesEqual(notifyBytes, 0, notifyOffset, 0xCC)
            && AllBytesEqual(
                notifyBytes,
                notifyOffset + sizeof(ushort),
                notifyBytes.Length - notifyOffset - sizeof(ushort),
                0xCC);
    }

    private static bool TestExecutesPlannedRxNotification()
    {
        TestContext context = CreateContext();
        VirtioNetReceiveLifecyclePublishResult publish = CreateRxPublish();
        VirtioNetQueueNotificationPlanResult plan =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        byte[] notifyBytes = CreateNotifyBytes(context);
        FakeNotifyWriteSink sink = new FakeNotifyWriteSink(notifyBytes);

        VirtioNetQueueNotificationExecutionResult result =
            VirtioNetQueueNotificationExecutor.Execute(
                plan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(12),
                sink);

        int notifyOffset = checked((int)plan.Plan.NotifyRelativeOffset);
        return result.Succeeded
            && result.Kind == VirtioNetQueueNotificationKind.Receive
            && result.QueueIndex == VirtioNetQueuePlan.ReceiveQueueIndex
            && result.QueueNotifyOffset == RxNotifyOffset
            && result.NotifyRelativeOffset == RxNotifyOffset * NotifyOffsetMultiplier
            && result.WriteValue == VirtioNetQueuePlan.ReceiveQueueIndex
            && result.PublishedDescriptorCount == publish.PublishedDescriptorCount
            && result.PreviousAvailableRingIndex == publish.PreviousAvailableRingIndex
            && result.NextAvailableRingIndex == publish.NextAvailableRingIndex
            && result.BarrierSequence == 12
            && sink.WriteCount == 1
            && sink.LastRelativeOffset == result.NotifyRelativeOffset
            && sink.LastValue == result.WriteValue
            && ReadUInt16(notifyBytes, notifyOffset) == VirtioNetQueuePlan.ReceiveQueueIndex
            && AllBytesEqual(notifyBytes, 0, notifyOffset, 0xCC)
            && AllBytesEqual(
                notifyBytes,
                notifyOffset + sizeof(ushort),
                notifyBytes.Length - notifyOffset - sizeof(ushort),
                0xCC);
    }

    private static bool TestExecutionRejectsMissingBarrier()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult plan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        byte[] notifyBytes = CreateNotifyBytes(context);
        FakeNotifyWriteSink sink = new FakeNotifyWriteSink(notifyBytes);
        VirtioNetQueueNotificationBarrierReadiness missingBarrier =
            new VirtioNetQueueNotificationBarrierReadiness(
                descriptorWritesVisible: true,
                availableRingWritesVisible: false,
                orderedBeforeNotify: true,
                barrierSequence: 13);

        VirtioNetQueueNotificationExecutionResult result =
            VirtioNetQueueNotificationExecutor.Execute(plan, missingBarrier, sink);

        return !result.Succeeded
            && result.Status == VirtioNetQueueNotificationExecutionStatus.MemoryBarrierNotReady
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success
            && result.SinkStatus == VirtioPciNotifyWriteStatus.NotReady
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && !result.BarrierSucceeded
            && result.DescriptorWritesVisible
            && !result.AvailableRingWritesVisible
            && result.OrderedBeforeNotify
            && result.BarrierSequence == 13
            && sink.WriteCount == 0
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestExecutionRejectsFailedNotificationPlan()
    {
        TestContext context = CreateContext();
        VirtioNetReceiveLifecyclePublishResult publish = CreateRxPublish();
        VirtioNetQueueNotificationPlanResult failedPlan =
            VirtioNetQueueNotificationPlanner.CreateReceiveNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0,
                queueEnabled: false);
        byte[] notifyBytes = CreateNotifyBytes(context);
        FakeNotifyWriteSink sink = new FakeNotifyWriteSink(notifyBytes);

        VirtioNetQueueNotificationExecutionResult result =
            VirtioNetQueueNotificationExecutor.Execute(
                failedPlan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(14),
                sink);

        return !failedPlan.Succeeded
            && failedPlan.Status == VirtioNetQueueNotificationPlanStatus.NotificationPlanFailed
            && failedPlan.NotificationStatus == VirtioPciQueueNotificationPlanStatus.QueueNotEnabled
            && !result.Succeeded
            && result.Status == VirtioNetQueueNotificationExecutionStatus.NotificationPlanFailed
            && result.NotificationPlanStatus == failedPlan.Status
            && result.PciNotificationStatus == failedPlan.NotificationStatus
            && result.SinkStatus == VirtioPciNotifyWriteStatus.NotReady
            && result.Kind == VirtioNetQueueNotificationKind.Receive
            && result.QueueIndex == 0
            && result.WriteWidthBytes == 0
            && result.BarrierSucceeded
            && result.BarrierSequence == 14
            && sink.WriteCount == 0
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestExecutionPropagatesSinkFailure()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult plan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        byte[] failedNotifyBytes = CreateNotifyBytes(context);
        FakeNotifyWriteSink failingSink = new FakeNotifyWriteSink(failedNotifyBytes)
        {
            FailureStatus = VirtioPciNotifyWriteStatus.WriteFailed
        };
        byte[] shortNotifyBytes = CreateNotifyBytes(context, length: (int)plan.Plan.NotifyRelativeOffset + 1);
        FakeNotifyWriteSink shortSink = new FakeNotifyWriteSink(shortNotifyBytes);

        VirtioNetQueueNotificationExecutionResult failed =
            VirtioNetQueueNotificationExecutor.Execute(
                plan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(15),
                failingSink);
        VirtioNetQueueNotificationExecutionResult rangeMismatch =
            VirtioNetQueueNotificationExecutor.Execute(
                plan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(16),
                shortSink);
        VirtioNetQueueNotificationExecutionResult notReady =
            VirtioNetQueueNotificationExecutor.Execute(
                plan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(17),
                sink: null);

        return !failed.Succeeded
            && failed.Status == VirtioNetQueueNotificationExecutionStatus.SinkWriteFailed
            && failed.SinkStatus == VirtioPciNotifyWriteStatus.WriteFailed
            && failed.Kind == VirtioNetQueueNotificationKind.Transmit
            && failed.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && failingSink.WriteCount == 1
            && AllBytesEqual(failedNotifyBytes, 0xCC)
            && !rangeMismatch.Succeeded
            && rangeMismatch.Status == VirtioNetQueueNotificationExecutionStatus.SinkRangeMismatch
            && rangeMismatch.SinkStatus == VirtioPciNotifyWriteStatus.RangeOutsideAperture
            && shortSink.WriteCount == 1
            && AllBytesEqual(shortNotifyBytes, 0xCC)
            && !notReady.Succeeded
            && notReady.Status == VirtioNetQueueNotificationExecutionStatus.SinkNotReady
            && notReady.SinkStatus == VirtioPciNotifyWriteStatus.NotReady;
    }

    private static bool TestExecutionPreservesNoLiveMmioBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult plan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        FakeNotifyWriteSink sink = new FakeNotifyWriteSink(CreateNotifyBytes(context));

        VirtioNetQueueNotificationExecutionResult result =
            VirtioNetQueueNotificationExecutor.Execute(
                plan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(18),
                sink);

        return result.Succeeded
            && VirtioNetQueueNotificationExecutor.RequiresBarrierReadiness
            && VirtioNetQueueNotificationExecutor.UsesFakeSink
            && VirtioNetQueueNotificationExecutor.WritesNotifySink
            && !VirtioNetQueueNotificationExecutor.PerformsLiveMmio
            && !VirtioNetQueueNotificationExecutor.MapsBars
            && !VirtioNetQueueNotificationExecutor.PublishesDescriptors
            && !VirtioNetQueueNotificationExecutor.MutatesDescriptorState
            && !VirtioNetQueueNotificationExecutor.ReadsQueueMemory
            && !VirtioNetQueueNotificationExecutor.PollsDevice
            && !VirtioNetQueueNotificationExecutor.HandlesInterrupts
            && !VirtioNetQueueNotificationExecutor.PerformsDmaMapping
            && !VirtioNetQueueNotificationExecutor.AllocatesDmaMemory
            && !VirtioNetQueueNotificationExecutor.PerformsPacketIo
            && !VirtioNetQueueNotificationExecutor.NotifiesLiveDevice
            && !VirtioNetQueueNotificationPlanner.PerformsMmio
            && !VirtioNetQueueNotificationPlanner.NotifiesDevice
            && !VirtioPciQueueNotificationPlanner.PerformsMmio
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetQueueNotificationBarrierReadiness>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetQueueNotificationExecutionResult>();
    }

    private static bool TestAccessorWritesPlannedTxNotification()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult plan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        byte[] notifyBytes = CreateNotifyBytes(context);
        FakeRegisterAccessor accessor = new FakeRegisterAccessor(notifyBytes);
        VirtioPciNotifyRegisterAccessorSink sink = new VirtioPciNotifyRegisterAccessorSink(accessor);

        VirtioNetQueueNotificationExecutionResult result =
            VirtioNetQueueNotificationExecutor.Execute(
                plan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(19),
                sink);

        int notifyOffset = checked((int)plan.Plan.NotifyRelativeOffset);
        return plan.Succeeded
            && sink.HasAccessor
            && result.Succeeded
            && result.Status == VirtioNetQueueNotificationExecutionStatus.Success
            && result.SinkStatus == VirtioPciNotifyWriteStatus.Success
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && result.QueueIndex == VirtioNetQueuePlan.TransmitQueueIndex
            && result.NotifyRelativeOffset == TxNotifyOffset * NotifyOffsetMultiplier
            && result.WriteValue == VirtioNetQueuePlan.TransmitQueueIndex
            && result.BarrierSequence == 19
            && accessor.Write16Count == 1
            && accessor.LastStatus == VirtioPciRegisterWriteStatus.Success
            && accessor.LastAperture.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && accessor.LastRelativeOffset == result.NotifyRelativeOffset
            && accessor.LastValue == result.WriteValue
            && ReadUInt16(notifyBytes, notifyOffset) == VirtioNetQueuePlan.TransmitQueueIndex
            && AllBytesEqual(notifyBytes, 0, notifyOffset, 0xCC)
            && AllBytesEqual(
                notifyBytes,
                notifyOffset + sizeof(ushort),
                notifyBytes.Length - notifyOffset - sizeof(ushort),
                0xCC);
    }

    private static bool TestAccessorPropagatesRangeFailure()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult plan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        byte[] notifyBytes = CreateNotifyBytes(
            context,
            length: checked((int)plan.Plan.NotifyRelativeOffset + 1));
        FakeRegisterAccessor accessor = new FakeRegisterAccessor(notifyBytes);
        VirtioPciNotifyRegisterAccessorSink sink = new VirtioPciNotifyRegisterAccessorSink(accessor);

        VirtioNetQueueNotificationExecutionResult result =
            VirtioNetQueueNotificationExecutor.Execute(
                plan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(20),
                sink);

        return !result.Succeeded
            && result.Status == VirtioNetQueueNotificationExecutionStatus.SinkRangeMismatch
            && result.SinkStatus == VirtioPciNotifyWriteStatus.RangeOutsideAperture
            && result.NotificationPlanStatus == VirtioNetQueueNotificationPlanStatus.Success
            && result.PciNotificationStatus == VirtioPciQueueNotificationPlanStatus.Success
            && result.Kind == VirtioNetQueueNotificationKind.Transmit
            && accessor.Write16Count == 1
            && accessor.LastStatus == VirtioPciRegisterWriteStatus.RangeOutsideAperture
            && accessor.LastRelativeOffset == result.NotifyRelativeOffset
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestAccessorPropagatesWriteFailure()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult plan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        byte[] failedBytes = CreateNotifyBytes(context);
        FakeRegisterAccessor failingAccessor = new FakeRegisterAccessor(failedBytes)
        {
            FailureStatus = VirtioPciRegisterWriteStatus.WriteFailed
        };
        byte[] unsupportedBytes = CreateNotifyBytes(context);
        FakeRegisterAccessor unsupportedAccessor = new FakeRegisterAccessor(unsupportedBytes)
        {
            FailureStatus = VirtioPciRegisterWriteStatus.UnsupportedWidth
        };

        VirtioNetQueueNotificationExecutionResult failed =
            VirtioNetQueueNotificationExecutor.Execute(
                plan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(21),
                new VirtioPciNotifyRegisterAccessorSink(failingAccessor));
        VirtioNetQueueNotificationExecutionResult unsupported =
            VirtioNetQueueNotificationExecutor.Execute(
                plan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(22),
                new VirtioPciNotifyRegisterAccessorSink(unsupportedAccessor));

        return !failed.Succeeded
            && failed.Status == VirtioNetQueueNotificationExecutionStatus.SinkWriteFailed
            && failed.SinkStatus == VirtioPciNotifyWriteStatus.WriteFailed
            && failingAccessor.Write16Count == 1
            && failingAccessor.LastStatus == VirtioPciRegisterWriteStatus.WriteFailed
            && AllBytesEqual(failedBytes, 0xCC)
            && !unsupported.Succeeded
            && unsupported.Status == VirtioNetQueueNotificationExecutionStatus.SinkWriteFailed
            && unsupported.SinkStatus == VirtioPciNotifyWriteStatus.WriteFailed
            && unsupportedAccessor.Write16Count == 1
            && unsupportedAccessor.LastStatus == VirtioPciRegisterWriteStatus.UnsupportedWidth
            && AllBytesEqual(unsupportedBytes, 0xCC);
    }

    private static bool TestAccessorPreservesNoLiveMmioBoundary()
    {
        TestContext context = CreateContext();
        VirtioNetTransmitLifecycleResult publish = CreateTxPublish(context.QueuePlan);
        VirtioNetQueueNotificationPlanResult plan =
            VirtioNetQueueNotificationPlanner.CreateTransmitNotification(
                context.QueueSetup,
                publish,
                context.Apertures.NotifyConfiguration,
                negotiatedFeatures: 0);
        FakeRegisterAccessor accessor = new FakeRegisterAccessor(CreateNotifyBytes(context));
        VirtioPciNotifyRegisterAccessorSink sink = new VirtioPciNotifyRegisterAccessorSink(accessor);

        VirtioNetQueueNotificationExecutionResult result =
            VirtioNetQueueNotificationExecutor.Execute(
                plan,
                VirtioNetQueueNotificationBarrierReadiness.Ready(23),
                sink);

        return result.Succeeded
            && sink.HasAccessor
            && VirtioPciNotifyRegisterAccessorSink.RequiresInjectedAccessor
            && !VirtioPciNotifyRegisterAccessorSink.PerformsLiveMmio
            && !VirtioPciNotifyRegisterAccessorSink.ContainsLiveMmioImplementation
            && !VirtioPciNotifyRegisterAccessorSink.MapsBars
            && !VirtioPciNotifyRegisterAccessorSink.AllocatesDmaMemory
            && !VirtioPciNotifyRegisterAccessorSink.PerformsPacketIo
            && !VirtioPciNotifyRegisterAccessorSink.NotifiesLiveDevice
            && !VirtioNetQueueNotificationExecutor.PerformsLiveMmio
            && !VirtioNetQueueNotificationExecutor.MapsBars
            && !VirtioNetQueueNotificationExecutor.PerformsDmaMapping
            && !VirtioNetQueueNotificationExecutor.AllocatesDmaMemory
            && !VirtioNetQueueNotificationExecutor.PollsDevice
            && !VirtioNetQueueNotificationExecutor.HandlesInterrupts
            && !VirtioNetQueueNotificationExecutor.PerformsPacketIo
            && !VirtioNetQueueNotificationExecutor.NotifiesLiveDevice
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciRegisterWriteStatus>();
    }

    private static bool TestRejectsDefaultResults()
    {
        VirtioPciRegisterRead16Result registerRead = default;
        VirtioNetQueueNotificationPlanResult notificationPlan = default;
        VirtioNetQueueNotificationRegisterWindowPlanResult windowPlan = default;
        VirtioNetQueueNotificationRegisterWindowExecutionResult windowExecution = default;
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult barrierExecution = default;
        VirtioNetQueueNotificationExecutionResult execution = default;

        return !registerRead.IsInitialized
            && !registerRead.Succeeded
            && registerRead.Status == VirtioPciRegisterReadStatus.Success
            && !notificationPlan.IsInitialized
            && !notificationPlan.Succeeded
            && notificationPlan.Status == VirtioNetQueueNotificationPlanStatus.Success
            && !windowPlan.IsInitialized
            && !windowPlan.Succeeded
            && windowPlan.Status == VirtioNetQueueNotificationRegisterWindowPlanStatus.Success
            && !windowExecution.IsInitialized
            && !windowExecution.Succeeded
            && windowExecution.Status ==
                VirtioNetQueueNotificationRegisterWindowExecutionStatus.Success
            && !barrierExecution.IsInitialized
            && !barrierExecution.Succeeded
            && barrierExecution.Status ==
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success
            && !execution.IsInitialized
            && !execution.Succeeded
            && execution.Status == VirtioNetQueueNotificationExecutionStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioPciRegisterRead16Result>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetQueueNotificationPlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetQueueNotificationRegisterWindowPlanResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetQueueNotificationRegisterWindowExecutionResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetQueueNotificationExecutionResult>();
    }

    private static TestContext CreateContext()
    {
        VirtioPciBindingPlanResult bindingPlan = CreateBindingPlan();
        VirtioDeviceDescriptor descriptor = CreateNetworkDescriptor(bindingPlan);
        VirtioNetQueuePlanResult queuePlan = VirtioNetQueuePlan.Create(
            VirtioNetQueuePlan.MinimumQueueCount,
            RxQueueSize,
            TxQueueSize,
            RxBufferCount,
            FrameBufferBytes);
        if (!queuePlan.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic queue plan.");
        }

        VirtioNetDriverQueueBringUpPlanResult bringUp = VirtioNetDriverQueueBringUpPlan.Create(
            descriptor,
            RxQueueSize,
            TxQueueSize,
            RxBufferCount,
            FrameBufferBytes,
            queuePlan.Plan.RxQueue.RequiredByteLength,
            queuePlan.Plan.TxQueue.RequiredByteLength,
            queuePlan.Plan.RxBuffers.RequiredBufferBytes,
            queuePlan.Plan.TxFrame.RequiredBufferBytes);
        if (!bringUp.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic bring-up plan.");
        }

        VirtioNetQueueMmioSetupCommand[] commands =
            new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioNetQueueMmioSetupPlanResult queueSetup = VirtioNetQueueMmioSetupPlanner.Create(
            bringUp,
            CreateRxAddresses(),
            CreateTxAddresses(),
            CreateRxMetadata(),
            CreateTxMetadata(),
            commands);
        if (!queueSetup.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic queue setup plan.");
        }

        return new TestContext(bindingPlan.Binding.Apertures, queueSetup, queuePlan);
    }

    private static VirtioNetQueueMmioSetupPlanResult CreateFailedQueueSetup()
    {
        TestContext context = CreateContext();
        VirtioDeviceDescriptor descriptor = CreateNetworkDescriptor(CreateBindingPlan());
        VirtioNetDriverQueueBringUpPlanResult bringUp = VirtioNetDriverQueueBringUpPlan.Create(
            descriptor,
            RxQueueSize,
            TxQueueSize,
            RxBufferCount,
            FrameBufferBytes,
            context.QueuePlan.Plan.RxQueue.RequiredByteLength,
            context.QueuePlan.Plan.TxQueue.RequiredByteLength,
            context.QueuePlan.Plan.RxBuffers.RequiredBufferBytes,
            context.QueuePlan.Plan.TxFrame.RequiredBufferBytes);
        VirtioNetQueueMmioSetupCommand[] commands =
            new VirtioNetQueueMmioSetupCommand[VirtioNetQueueMmioSetupPlanner.RequiredCommandCount - 1];

        return VirtioNetQueueMmioSetupPlanner.Create(
            bringUp,
            CreateRxAddresses(),
            CreateTxAddresses(),
            CreateRxMetadata(),
            CreateTxMetadata(),
            commands);
    }

    private static VirtioNetReceiveLifecyclePublishResult CreateRxPublish()
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(RxQueueSize);
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];

        return VirtioNetReceiveLifecycle.PublishReceiveBuffers(
            queueSize: RxQueueSize,
            firstDescriptorIndex: 1,
            availableDescriptorCount: 2,
            previousAvailableRingIndex: 6,
            previousUsedIndex: 4,
            firstReceiveBufferPhysicalAddress: RxBufferPhysicalAddress,
            receiveBufferStrideBytes: FrameBufferBytes,
            receiveBufferCount: 2,
            frameBufferBytes: FrameBufferBytes,
            queueMemory);
    }

    private static VirtioNetTransmitLifecycleResult CreateTxPublish(
        VirtioNetQueuePlanResult queuePlan)
    {
        byte[] txHeader = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        byte[] queueMemory = new byte[(int)queuePlan.Plan.TxQueue.EndOffset];

        return VirtioNetTransmitLifecycle.Publish(
            queuePlan,
            firstDescriptorIndex: 0,
            availableDescriptorCount: VirtioNetTransmitLifecycle.RequiredDescriptorCount,
            availableRingIndex: 7,
            txHeaderPhysicalAddress: TxHeaderPhysicalAddress,
            ethernetFramePhysicalAddress: TxFramePhysicalAddress,
            ethernetFrame: CreateEthernetFrame(),
            txHeader,
            queueMemory);
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
        return new VirtioNetQueueMmioSetupQueueAddresses(
            descriptorTablePhysicalAddress: 0x100000,
            driverRingPhysicalAddress: 0x101000,
            deviceRingPhysicalAddress: 0x102000);
    }

    private static VirtioNetQueueMmioSetupQueueAddresses CreateTxAddresses()
    {
        return new VirtioNetQueueMmioSetupQueueAddresses(
            descriptorTablePhysicalAddress: 0x110000,
            driverRingPhysicalAddress: 0x111000,
            deviceRingPhysicalAddress: 0x112000);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateRxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(
            RxDeviceMaximumQueueSize,
            hasNotifyOffset: true,
            RxNotifyOffset);
    }

    private static VirtioNetQueueMmioSetupQueueDeviceMetadata CreateTxMetadata()
    {
        return new VirtioNetQueueMmioSetupQueueDeviceMetadata(
            TxDeviceMaximumQueueSize,
            hasNotifyOffset: true,
            TxNotifyOffset);
    }

    private static VirtioDeviceDescriptor CreateNetworkDescriptor(
        VirtioPciBindingPlanResult bindingPlan)
    {
        VirtioDeviceDescriptorSource[] sources =
        [
            new VirtioDeviceDescriptorSource(
                sourceIndex: 40,
                bindingPlan,
                VirtioDeviceInitializationProfile.NetworkOptionalFeatureMask,
                VirtioDeviceInitializationProfile.NetworkRequiredQueueCount,
                featuresOkAccepted: true)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(
                sourceIndex: 40,
                CreateNetworkConfiguration(
                    new MacAddress(0x02, 0x33, 0x44, 0x55, 0x66, 0xCC),
                    VirtioNetDeviceConfig.StatusLinkUp))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result = VirtioDeviceDescriptorCatalog.Build(
            sources,
            configurations,
            destination);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic virtio-net descriptor.");
        }

        return destination[0];
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan()
    {
        ushort deviceId = (ushort)(VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.NetworkDeviceId);

        return VirtioPciBindingPlan.Create(
            CreateScannedDevice(deviceId),
            CreatePciConfiguration(deviceId),
            CreateBarSizes());
    }

    private static PciEcamScannedDevice CreateScannedDevice(ushort deviceId)
    {
        return new PciEcamScannedDevice(
            segmentIndex: 2,
            new PciEcamSegment(
                basePhysicalAddress: 0xE0000000UL,
                segmentGroup: 1,
                startBus: 0,
                endBus: 0x7F),
            new PciScannedDevice(
                bus: 3,
                device: 5,
                function: 0,
                vendorId: VirtioPciDeviceDiscovery.VirtioVendorId,
                deviceId: deviceId,
                revisionId: 0,
                classCode: 0,
                subclass: 0,
                programmingInterface: 0,
                headerType: PciConfigurationHeader.Type0HeaderType,
                isMultifunction: false,
                hasCapabilitiesList: true,
                capabilityPointer: CommonOffset));
    }

    private static byte[] CreatePciConfiguration(ushort deviceId)
    {
        byte[] config = new byte[256];
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(0x00, sizeof(ushort)),
            VirtioPciDeviceDiscovery.VirtioVendorId);
        EndianPrimitives.WriteUInt16LittleEndian(config.AsSpan(0x02, sizeof(ushort)), deviceId);
        EndianPrimitives.WriteUInt16LittleEndian(
            config.AsSpan(0x06, sizeof(ushort)),
            PciConfigurationHeader.StatusCapabilitiesListBit);
        config[0x0E] = PciConfigurationHeader.Type0HeaderType;
        WriteUInt32(config, 0x10, 0x80000000);
        WriteUInt32(config, 0x14, 0x80001000);
        WriteUInt32(config, 0x18, 0x80002000);
        WriteUInt32(config, 0x1C, 0x80003000);
        config[0x34] = CommonOffset;

        WriteVirtioCapability(
            config,
            CommonOffset,
            NotifyOffset,
            VirtioPciCapabilityType.CommonConfiguration,
            bar: 0,
            offsetValue: 0x20,
            length: VirtioPciDeviceDiscovery.CommonConfigurationMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            NotifyOffset,
            IsrOffset,
            VirtioPciCapabilityType.NotifyConfiguration,
            bar: 1,
            offsetValue: 0x80,
            length: 0x100,
            notifyOffsetMultiplier: NotifyOffsetMultiplier,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioNotifyCapabilityLength);
        WriteVirtioCapability(
            config,
            IsrOffset,
            DeviceOffset,
            VirtioPciCapabilityType.IsrStatus,
            bar: 2,
            offsetValue: 0,
            length: 1,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            DeviceOffset,
            nextOffset: 0,
            VirtioPciCapabilityType.DeviceConfiguration,
            bar: 3,
            offsetValue: 0,
            length: 0x10,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);

        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return [0x1000, 0x1000, 0x1000, 0x1000, 0, 0];
    }

    private static VirtioPciBarRegisterWindowBindingResult CreateBoundRegisterWindows()
    {
        return VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
            CreateBindingPlan(),
            CreateRegisterWindows());
    }

    private static VirtioPciMappedBarWindow[] CreateRegisterWindows()
    {
        return
        [
            CreateRegisterWindow(0, 0x80000000UL, 0xFFFF800000000000UL),
            CreateRegisterWindow(1, NotifyBarPhysicalBase, NotifyBarVirtualBase),
            CreateRegisterWindow(2, 0x80002000UL, 0xFFFF800000002000UL),
            CreateRegisterWindow(3, 0x80003000UL, 0xFFFF800000003000UL),
            default,
            default
        ];
    }

    private static VirtioPciMappedBarWindow CreateRegisterWindow(
        byte barIndex,
        ulong physicalBaseAddress,
        ulong virtualBaseAddress)
    {
        return new VirtioPciMappedBarWindow(
            barIndex,
            physicalBaseAddress,
            virtualBaseAddress,
            length: 0x1000,
            isMapped: true,
            isMmio: true,
            isDeviceMemory: true,
            isUncached: true);
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

    private static void WriteVirtioCapability(
        Span<byte> config,
        byte offset,
        byte nextOffset,
        VirtioPciCapabilityType type,
        byte bar,
        uint offsetValue,
        uint length,
        uint notifyOffsetMultiplier,
        byte capabilityLength)
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

    private static byte[] CreateNotifyBytes(TestContext context, int length = -1)
    {
        int byteCount = length < 0
            ? checked((int)context.Apertures.NotifyConfiguration.Length)
            : length;
        byte[] notifyBytes = new byte[byteCount];
        Fill(notifyBytes, 0xCC);
        return notifyBytes;
    }

    private static byte[] CreateNotifyWindowBytes(int length = 0x1000)
    {
        byte[] notifyBytes = new byte[length];
        Fill(notifyBytes, 0xCC);
        return notifyBytes;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, int offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(source.Slice(offset, sizeof(ushort)));
    }

    private static ushort ReadUInt16AtBarOffset(ReadOnlySpan<byte> source, ulong barRelativeOffset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(
            source.Slice(checked((int)barRelativeOffset), sizeof(ushort)));
    }

    private static bool AllBytesEqual(ReadOnlySpan<byte> bytes, byte expected)
    {
        return AllBytesEqual(bytes, 0, bytes.Length, expected);
    }

    private static bool AllBytesEqual(
        ReadOnlySpan<byte> bytes,
        int offset,
        int count,
        byte expected)
    {
        for (int index = 0; index < count; index++)
        {
            if (bytes[offset + index] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static void Fill(Span<byte> bytes, byte value)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = value;
        }
    }

    private sealed class FakeNotifyWriteSink : IVirtioPciNotifyWriteSink
    {
        private readonly byte[] _notifyBytes;

        public FakeNotifyWriteSink(byte[] notifyBytes)
        {
            _notifyBytes = notifyBytes;
        }

        public VirtioPciNotifyWriteStatus FailureStatus { get; init; } =
            VirtioPciNotifyWriteStatus.Success;

        public int WriteCount { get; private set; }

        public VirtioPciBarAperture LastAperture { get; private set; }

        public uint LastRelativeOffset { get; private set; }

        public ushort LastValue { get; private set; }

        public VirtioPciNotifyWriteStatus Write16(
            VirtioPciBarAperture aperture,
            uint relativeOffset,
            ushort value)
        {
            WriteCount++;
            LastAperture = aperture;
            LastRelativeOffset = relativeOffset;
            LastValue = value;

            if (FailureStatus != VirtioPciNotifyWriteStatus.Success)
            {
                return FailureStatus;
            }

            ulong endOffset = (ulong)relativeOffset + sizeof(ushort);
            if (relativeOffset > int.MaxValue || endOffset > (ulong)_notifyBytes.Length)
            {
                return VirtioPciNotifyWriteStatus.RangeOutsideAperture;
            }

            EndianPrimitives.WriteUInt16LittleEndian(
                _notifyBytes.AsSpan((int)relativeOffset, sizeof(ushort)),
                value);
            return VirtioPciNotifyWriteStatus.Success;
        }
    }

    private sealed class FakeRegisterAccessor : IVirtioPciRegisterAccessor
    {
        private readonly byte[] _notifyBytes;

        public FakeRegisterAccessor(byte[] notifyBytes)
        {
            _notifyBytes = notifyBytes;
        }

        public VirtioPciRegisterWriteStatus FailureStatus { get; init; } =
            VirtioPciRegisterWriteStatus.Success;

        public VirtioPciRegisterWriteStatus LastStatus { get; private set; } =
            VirtioPciRegisterWriteStatus.NotReady;

        public int Write16Count { get; private set; }

        public VirtioPciBarAperture LastAperture { get; private set; }

        public uint LastRelativeOffset { get; private set; }

        public ushort LastValue { get; private set; }

        public VirtioPciRegisterRead16Result Read16(
            VirtioPciBarAperture aperture,
            uint relativeOffset)
        {
            _ = aperture;
            _ = relativeOffset;
            return new VirtioPciRegisterRead16Result(
                VirtioPciRegisterReadStatus.ReadFailed,
                value: 0);
        }

        public VirtioPciRegisterWriteStatus Write16(
            VirtioPciBarAperture aperture,
            uint relativeOffset,
            ushort value)
        {
            Write16Count++;
            LastAperture = aperture;
            LastRelativeOffset = relativeOffset;
            LastValue = value;

            if (FailureStatus != VirtioPciRegisterWriteStatus.Success)
            {
                LastStatus = FailureStatus;
                return LastStatus;
            }

            ulong endOffset = (ulong)relativeOffset + sizeof(ushort);
            if (relativeOffset > int.MaxValue || endOffset > (ulong)_notifyBytes.Length)
            {
                LastStatus = VirtioPciRegisterWriteStatus.RangeOutsideAperture;
                return LastStatus;
            }

            EndianPrimitives.WriteUInt16LittleEndian(
                _notifyBytes.AsSpan((int)relativeOffset, sizeof(ushort)),
                value);
            LastStatus = VirtioPciRegisterWriteStatus.Success;
            return LastStatus;
        }

        public VirtioPciRegisterWriteStatus Write64(
            VirtioPciBarAperture aperture,
            uint relativeOffset,
            ulong value)
        {
            _ = aperture;
            _ = relativeOffset;
            _ = value;
            return VirtioPciRegisterWriteStatus.UnsupportedWidth;
        }
    }

    private readonly struct TestContext
    {
        public TestContext(
            VirtioPciBarApertureSet apertures,
            VirtioNetQueueMmioSetupPlanResult queueSetup,
            VirtioNetQueuePlanResult queuePlan)
        {
            Apertures = apertures;
            QueueSetup = queueSetup;
            QueuePlan = queuePlan;
        }

        public VirtioPciBarApertureSet Apertures { get; }

        public VirtioNetQueueMmioSetupPlanResult QueueSetup { get; }

        public VirtioNetQueuePlanResult QueuePlan { get; }
    }
}
