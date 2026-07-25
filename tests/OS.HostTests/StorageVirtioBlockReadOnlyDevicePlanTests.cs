using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Std.Binary;
using OS.Storage;

internal static class StorageVirtioBlockReadOnlyDevicePlanTests
{
    private const ushort QueueSize = 8;
    private const ushort DeviceMaximumQueueSize = 16;
    private const ushort QueueNotifyOffset = 7;
    private const ulong HeaderPhysicalAddress = 0x0010_0000UL;
    private const ulong DataPhysicalAddress = 0x0020_0000UL;
    private const ulong StatusPhysicalAddress = 0x0030_0000UL;
    private const byte CommonOffset = 0x40;
    private const byte NotifyOffset = 0x60;
    private const byte IsrOffset = 0x80;
    private const byte DeviceOffset = 0xA0;
    private const uint NotifyApertureBarOffset = 0x80;
    private const uint NotifyOffsetMultiplier = 4;
    private const ulong NotifyBarPhysicalBase = 0x80001000UL;
    private const ulong NotifyBarVirtualBase = 0xFFFF800000001000UL;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("storage.virtioblk.readonly publishes sector read", TestPublishesSectorRead, ref failures);
        Run("storage.virtioblk.readonly rejects invalid block geometry", TestRejectsInvalidBlockGeometry, ref failures);
        Run("storage.virtioblk.readonly rejects out-of-range sector", TestRejectsOutOfRangeSector, ref failures);
        Run("storage.virtioblk.readonly reports lifecycle publish failure", TestReportsLifecyclePublishFailure, ref failures);
        Run("storage.virtioblk.readonly preserves request metadata", TestPreservesRequestMetadata, ref failures);
        Run("storage.virtioblk.readonly default result is not success", TestReadOnlyDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.readqueue publishes valid block range", TestReadQueuePublishesValidBlockRange, ref failures);
        Run("storage.virtioblk.readqueue rejects out-of-range block request", TestReadQueueRejectsOutOfRangeBlockRequest, ref failures);
        Run("storage.virtioblk.readqueue preserves cursor on publish failure", TestReadQueuePreservesCursorOnPublishFailure, ref failures);
        Run("storage.virtioblk.readqueue publish default result is not success", TestReadQueuePublishDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.readqueue preserves no-live-device boundary", TestReadQueuePreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.readqueue completes and advances used index", TestReadQueueCompletesAndAdvancesUsedIndex, ref failures);
        Run("storage.virtioblk.readqueue completion rejects failed publication", TestReadQueueCompletionRejectsFailedPublication, ref failures);
        Run("storage.virtioblk.readqueue completion rejects no completion", TestReadQueueCompletionRejectsNoCompletion, ref failures);
        Run("storage.virtioblk.readqueue completion default result is not success", TestReadQueueCompletionDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.readqueue completion preserves no-live-device boundary", TestReadQueueCompletionPreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.readqueue block result reports completed read", TestReadQueueBlockResultReportsCompletedRead, ref failures);
        Run("storage.virtioblk.readqueue block result maps publication failure", TestReadQueueBlockResultMapsPublicationFailure, ref failures);
        Run("storage.virtioblk.readqueue block result maps completion failure", TestReadQueueBlockResultMapsCompletionFailure, ref failures);
        Run("storage.virtioblk.readqueue block result default result is not success", TestReadQueueBlockResultDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.readqueue block result preserves no-live-device boundary", TestReadQueueBlockResultPreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.notify plans readqueue notification metadata", TestNotifyPlansReadQueueNotificationMetadata, ref failures);
        Run("storage.virtioblk.notify rejects failed publication", TestNotifyRejectsFailedPublication, ref failures);
        Run("storage.virtioblk.notify default result is not success", TestNotifyDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.notify preserves no-live-device boundary", TestNotifyPreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.notifyoffset derives relative offset", TestNotifyOffsetDerivesRelativeOffset, ref failures);
        Run("storage.virtioblk.notifyoffset rejects failed notification metadata", TestNotifyOffsetRejectsFailedNotificationMetadata, ref failures);
        Run("storage.virtioblk.notifyoffset rejects relative offset overflow", TestNotifyOffsetRejectsRelativeOffsetOverflow, ref failures);
        Run("storage.virtioblk.notifyoffset default result is not success", TestNotifyOffsetDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.notifyoffset preserves no-live-device boundary", TestNotifyOffsetPreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.notifywindow plans access", TestNotifyWindowPlansAccess, ref failures);
        Run("storage.virtioblk.notifywindow rejects failed offset plan", TestNotifyWindowRejectsFailedOffsetPlan, ref failures);
        Run("storage.virtioblk.notifywindow propagates register window failure", TestNotifyWindowPropagatesRegisterWindowFailure, ref failures);
        Run("storage.virtioblk.notifywindow default result is not success", TestNotifyWindowDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.notifywindow preserves no-live-device boundary", TestNotifyWindowPreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.notifywindowexec writes planned notify access", TestNotifyWindowExecWritesPlannedNotifyAccess, ref failures);
        Run("storage.virtioblk.notifywindowexec rejects failed plan", TestNotifyWindowExecRejectsFailedPlan, ref failures);
        Run("storage.virtioblk.notifywindowexec propagates access failure", TestNotifyWindowExecPropagatesAccessFailure, ref failures);
        Run("storage.virtioblk.notifywindowexec default result is not success", TestNotifyWindowExecDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.notifywindowexec preserves no-live-device boundary", TestNotifyWindowExecPreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.notifywindowbarrier writes planned notify access", TestNotifyWindowBarrierWritesPlannedNotifyAccess, ref failures);
        Run("storage.virtioblk.notifywindowbarrier rejects missing barrier", TestNotifyWindowBarrierRejectsMissingBarrier, ref failures);
        Run("storage.virtioblk.notifywindowbarrier rejects failed plan", TestNotifyWindowBarrierRejectsFailedPlan, ref failures);
        Run("storage.virtioblk.notifywindowbarrier propagates access failure", TestNotifyWindowBarrierPropagatesAccessFailure, ref failures);
        Run("storage.virtioblk.notifywindowbarrier default result is not success", TestNotifyWindowBarrierDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.notifywindowbarrier preserves no-live-device boundary", TestNotifyWindowBarrierPreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.readnotify composes publication and barrier notify", TestReadNotifyComposesPublicationAndBarrierNotify, ref failures);
        Run("storage.virtioblk.readnotify rejects failed publication", TestReadNotifyRejectsFailedPublication, ref failures);
        Run("storage.virtioblk.readnotify rejects missing barrier", TestReadNotifyRejectsMissingBarrier, ref failures);
        Run("storage.virtioblk.readnotify propagates notify execution failure", TestReadNotifyPropagatesNotifyExecutionFailure, ref failures);
        Run("storage.virtioblk.readnotify default result is not success", TestReadNotifyDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.readnotify preserves no-live-device boundary", TestReadNotifyPreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.readnotifycompletion completes after notify readiness", TestReadNotifyCompletionCompletesAfterNotifyReadiness, ref failures);
        Run("storage.virtioblk.readnotifycompletion rejects failed readiness", TestReadNotifyCompletionRejectsFailedReadiness, ref failures);
        Run("storage.virtioblk.readnotifycompletion rejects no completion", TestReadNotifyCompletionRejectsNoCompletion, ref failures);
        Run("storage.virtioblk.readnotifycompletion propagates ownership failure", TestReadNotifyCompletionPropagatesOwnershipFailure, ref failures);
        Run("storage.virtioblk.readnotifycompletion default result is not success", TestReadNotifyCompletionDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.readnotifycompletion preserves no-live-device boundary", TestReadNotifyCompletionPreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.readnotifyblock projects completed read", TestReadNotifyBlockProjectsCompletedRead, ref failures);
        Run("storage.virtioblk.readnotifyblock maps readiness failure", TestReadNotifyBlockMapsReadinessFailure, ref failures);
        Run("storage.virtioblk.readnotifyblock maps completion failure", TestReadNotifyBlockMapsCompletionFailure, ref failures);
        Run("storage.virtioblk.readnotifyblock default result is not success", TestReadNotifyBlockDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.readnotifyblock preserves no-live-device boundary", TestReadNotifyBlockPreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.readpipeline projects completed read", TestReadPipelineProjectsCompletedRead, ref failures);
        Run("storage.virtioblk.readpipeline rejects publication failure", TestReadPipelineRejectsPublicationFailure, ref failures);
        Run("storage.virtioblk.readpipeline rejects readiness failure", TestReadPipelineRejectsReadinessFailure, ref failures);
        Run("storage.virtioblk.readpipeline maps completion failure", TestReadPipelineMapsCompletionFailure, ref failures);
        Run("storage.virtioblk.readpipeline default result is not success", TestReadPipelineDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.readpipeline preserves no-live-device boundary", TestReadPipelinePreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.readadapter executes pipeline read", TestReadAdapterExecutesPipelineRead, ref failures);
        Run("storage.virtioblk.readadapter rejects short request buffer", TestReadAdapterRejectsShortRequestBuffer, ref failures);
        Run("storage.virtioblk.readadapter rejects short notify window", TestReadAdapterRejectsShortNotifyWindow, ref failures);
        Run("storage.virtioblk.readadapter propagates pipeline failure", TestReadAdapterPropagatesPipelineFailure, ref failures);
        Run("storage.virtioblk.readadapter default result is not success", TestReadAdapterDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.readadapter preserves no-live-device boundary", TestReadAdapterPreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.readsession delegates adapter read", TestReadSessionDelegatesAdapterRead, ref failures);
        Run("storage.virtioblk.readsession preserves cursor state", TestReadSessionPreservesCursorState, ref failures);
        Run("storage.virtioblk.readsession rejects invalid setup", TestReadSessionRejectsInvalidSetup, ref failures);
        Run("storage.virtioblk.readsession default result is not success", TestReadSessionDefaultResultIsNotSuccess, ref failures);
        Run("storage.virtioblk.readsession preserves no-live-device boundary", TestReadSessionPreservesNoLiveDeviceBoundary, ref failures);
        Run("storage.virtioblk.readsession setup gate composes setup and read", TestReadSessionSetupGateComposesSetupAndRead, ref failures);
        Run("storage.virtioblk.readsession setup gate rejects failed setup", TestReadSessionSetupGateRejectsFailedSetup, ref failures);
        Run("storage.virtioblk.readsession setup gate rejects missing barrier", TestReadSessionSetupGateRejectsMissingBarrier, ref failures);
        Run("storage.virtioblk.readsession setup gate propagates read failure", TestReadSessionSetupGatePropagatesReadFailure, ref failures);
        Run("storage.virtioblk.readsession setup gate default result is not success", TestReadSessionSetupGateDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Virtio block read-only device plan host tests failed: " + failures);
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

    private static bool TestPublishesSectorRead()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[1024];
        byte[] status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];
        Array.Fill(queueMemory, (byte)0xcc);

        VirtioBlockReadOnlyDevicePlanResult result =
            VirtioBlockReadOnlyDevicePlan.PublishRead(
                device,
                firstSector: 4,
                sectorCount: 2,
                queueSize: QueueSize,
                firstDescriptorIndex: 1,
                availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                previousAvailableRingIndex: 4,
                previousUsedIndex: 8,
                requestHeaderPhysicalAddress: HeaderPhysicalAddress,
                dataBufferPhysicalAddress: DataPhysicalAddress,
                statusBytePhysicalAddress: StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory);

        return result.Succeeded
            && result.Status == VirtioBlockReadOnlyDevicePlanStatus.Success
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && result.LifecycleInvoked
            && result.LifecyclePublishStatus == VirtioBlockReadLifecyclePublishStatus.Success
            && result.ReadPlanStatus == VirtioBlockReadTransactionPlanStatus.Success
            && result.RequestPlanStatus == VirtioBlockRequestPlanStatus.Success
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.DescriptorPlanStatus == VirtioBlockDescriptorChainPlanStatus.Success
            && result.DescriptorWriterStatus == VirtioBlockDescriptorChainWriterStatus.Success
            && result.SectorSizeBytes == VirtioBlockRequestPlan.SectorSizeBytes
            && result.DeviceSectorCount == 64
            && result.FirstSector == 4
            && result.SectorCount == 2
            && result.LastSectorInclusive == 5
            && result.RequestedByteCount == 1024
            && result.DataByteCount == 1024
            && result.QueueSize == QueueSize
            && result.DescriptorHead == 1
            && result.PreviousAvailableRingIndex == 4
            && result.NextAvailableRingIndex == 5
            && result.PreviousUsedIndex == 8
            && result.ExpectedNextUsedIndex == 9
            && result.AvailableRingSlot == 4
            && result.DescriptorsWritten == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && EndianPrimitives.ReadUInt32LittleEndian(requestHeader.AsSpan(0, sizeof(uint))) == (uint)VirtioBlockRequestType.Read
            && EndianPrimitives.ReadUInt64LittleEndian(requestHeader.AsSpan(8, sizeof(ulong))) == 4
            && ReadUInt16(queueMemory, (int)result.AvailableRingEntryOffset) == result.DescriptorHead
            && ReadUInt16(queueMemory, (int)queue.Plan.AvailableRing.Offset + 2) == result.NextAvailableRingIndex
            && !VirtioBlockReadOnlyDevicePlan.NotifiesDevice
            && !VirtioBlockReadOnlyDevicePlan.PollsDevice
            && !VirtioBlockReadOnlyDevicePlan.HandlesInterrupts
            && !VirtioBlockReadOnlyDevicePlan.CopiesPayload
            && !VirtioBlockReadOnlyDevicePlan.PerformsDiskRead;
    }

    private static bool TestRejectsInvalidBlockGeometry()
    {
        BlockDeviceInfo device = CreateDevice(blockSize: 1024, blockCount: 8);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[512];
        byte[] status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        byte[] queueMemory = new byte[256];
        Array.Fill(queueMemory, (byte)0x47);

        VirtioBlockReadOnlyDevicePlanResult result =
            VirtioBlockReadOnlyDevicePlan.PublishRead(
                device,
                firstSector: 0,
                sectorCount: 1,
                queueSize: QueueSize,
                firstDescriptorIndex: 0,
                availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                requestHeaderPhysicalAddress: HeaderPhysicalAddress,
                dataBufferPhysicalAddress: DataPhysicalAddress,
                statusBytePhysicalAddress: StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory);

        return !result.Succeeded
            && result.Status == VirtioBlockReadOnlyDevicePlanStatus.InvalidBlockGeometry
            && result.BlockDeviceStatus == BlockDeviceStatus.InvalidBlockSize
            && !result.LifecycleInvoked
            && result.SectorSizeBytes == 1024
            && result.DeviceSectorCount == 8
            && result.RequestedByteCount == 0
            && AllBytesEqual(queueMemory, 0x47);
    }

    private static bool TestRejectsOutOfRangeSector()
    {
        BlockDeviceInfo device = CreateDevice(8);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[512];
        byte[] status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        byte[] queueMemory = new byte[256];
        Array.Fill(queueMemory, (byte)0x58);

        VirtioBlockReadOnlyDevicePlanResult result =
            VirtioBlockReadOnlyDevicePlan.PublishRead(
                device,
                firstSector: 8,
                sectorCount: 1,
                queueSize: QueueSize,
                firstDescriptorIndex: 0,
                availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                previousAvailableRingIndex: 2,
                previousUsedIndex: 3,
                requestHeaderPhysicalAddress: HeaderPhysicalAddress,
                dataBufferPhysicalAddress: DataPhysicalAddress,
                statusBytePhysicalAddress: StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory);

        return !result.Succeeded
            && result.Status == VirtioBlockReadOnlyDevicePlanStatus.InvalidSectorRange
            && result.BlockDeviceStatus == BlockDeviceStatus.InvalidBlockRange
            && !result.LifecycleInvoked
            && result.FirstSector == 8
            && result.SectorCount == 1
            && result.RequestedByteCount == 512
            && result.QueueSize == QueueSize
            && AllBytesEqual(queueMemory, 0x58);
    }

    private static bool TestReportsLifecyclePublishFailure()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[512];
        byte[] status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset - 1];
        Array.Fill(queueMemory, (byte)0x69);

        VirtioBlockReadOnlyDevicePlanResult result =
            VirtioBlockReadOnlyDevicePlan.PublishRead(
                device,
                firstSector: 3,
                sectorCount: 1,
                queueSize: QueueSize,
                firstDescriptorIndex: 1,
                availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                previousAvailableRingIndex: 9,
                previousUsedIndex: 11,
                requestHeaderPhysicalAddress: HeaderPhysicalAddress,
                dataBufferPhysicalAddress: DataPhysicalAddress,
                statusBytePhysicalAddress: StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory);

        return !result.Succeeded
            && result.Status == VirtioBlockReadOnlyDevicePlanStatus.LifecyclePublishFailed
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && result.LifecycleInvoked
            && result.LifecyclePublishStatus == VirtioBlockReadLifecyclePublishStatus.DescriptorWriteFailed
            && result.ReadPlanStatus == VirtioBlockReadTransactionPlanStatus.Success
            && result.DescriptorPlanStatus == VirtioBlockDescriptorChainPlanStatus.Success
            && result.DescriptorWriterStatus == VirtioBlockDescriptorChainWriterStatus.QueueMemoryTooSmall
            && result.FirstSector == 3
            && result.SectorCount == 1
            && result.RequestedByteCount == 512
            && result.NextAvailableRingIndex == result.PreviousAvailableRingIndex
            && result.ExpectedNextUsedIndex == result.PreviousUsedIndex
            && AllBytesEqual(queueMemory, 0x69);
    }

    private static bool TestPreservesRequestMetadata()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[1536];
        byte[] status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];

        VirtioBlockReadOnlyDevicePlanResult result =
            VirtioBlockReadOnlyDevicePlan.PublishRead(
                device,
                firstSector: 7,
                sectorCount: 3,
                queueSize: QueueSize,
                firstDescriptorIndex: 2,
                availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                previousAvailableRingIndex: ushort.MaxValue,
                previousUsedIndex: 42,
                requestHeaderPhysicalAddress: HeaderPhysicalAddress,
                dataBufferPhysicalAddress: DataPhysicalAddress,
                statusBytePhysicalAddress: StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory);

        return result.Succeeded
            && result.FirstSector == 7
            && result.SectorCount == 3
            && result.LastSectorInclusive == 9
            && result.RequestedByteCount == 1536
            && result.DataByteCount == 1536
            && result.DescriptorHead == 2
            && result.PreviousAvailableRingIndex == ushort.MaxValue
            && result.NextAvailableRingIndex == 0
            && result.AvailableRingSlot == 7
            && result.PreviousUsedIndex == 42
            && result.ExpectedNextUsedIndex == 43
            && result.DescriptorsWritten == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && EndianPrimitives.ReadUInt64LittleEndian(requestHeader.AsSpan(8, sizeof(ulong))) == 7
            && ReadUInt16(queueMemory, (int)result.AvailableRingEntryOffset) == 2
            && ReadUInt16(queueMemory, (int)queue.Plan.AvailableRing.Offset + 2) == 0;
    }

    private static bool TestReadOnlyDefaultResultIsNotSuccess()
    {
        VirtioBlockReadOnlyDevicePlanResult result = default;
        BlockDeviceInfo device = CreateDevice(blockSize: 1024, blockCount: 8);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[512];
        byte[] status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        byte[] queueMemory = new byte[256];

        VirtioBlockReadOnlyDevicePlanResult failed =
            VirtioBlockReadOnlyDevicePlan.PublishRead(
                device,
                firstSector: 0,
                sectorCount: 1,
                queueSize: QueueSize,
                firstDescriptorIndex: 0,
                availableDescriptorCount: VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
                previousAvailableRingIndex: 0,
                previousUsedIndex: 0,
                requestHeaderPhysicalAddress: HeaderPhysicalAddress,
                dataBufferPhysicalAddress: DataPhysicalAddress,
                statusBytePhysicalAddress: StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory);

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadOnlyDevicePlanStatus.Success
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && !result.LifecycleInvoked
            && failed.IsInitialized
            && !failed.Succeeded
            && failed.Status == VirtioBlockReadOnlyDevicePlanStatus.InvalidBlockGeometry
            && failed.BlockDeviceStatus == BlockDeviceStatus.InvalidBlockSize
            && !failed.LifecycleInvoked
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadOnlyDevicePlanResult>();
    }

    private static bool TestReadQueuePublishesValidBlockRange()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);

        VirtioBlockReadQueuePublishResult result =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out byte[] requestHeader,
                out _,
                out byte[] queueMemory);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockReadQueuePublishStatus.Success
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && result.CursorPublishStatus == VirtioBlockQueueCursorPublishStatus.Success
            && result.DescriptorPublishStatus == VirtioBlockDescriptorPublishPlanStatus.Success
            && result.FreeListStatus == VirtioBlockDescriptorFreeListStatus.Success
            && result.LifecyclePublishStatus == VirtioBlockReadLifecyclePublishStatus.Success
            && result.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.Success
            && result.CursorPublishAttempted
            && result.SectorSizeBytes == VirtioBlockRequestPlan.SectorSizeBytes
            && result.DeviceSectorCount == 64
            && result.FirstSector == 4
            && result.SectorCount == 2
            && result.RequestedByteCount == 1024
            && result.QueueSize == QueueSize
            && result.PreviousAvailableRingIndex == 4
            && result.CurrentAvailableRingIndex == 5
            && result.PreviousUsedRingIndex == 8
            && result.CurrentUsedRingIndex == 8
            && cursor.AvailableRingIndex == 5
            && cursor.UsedRingIndex == 8
            && result.CursorPublish.Publish.RequestHeaderDescriptorIndex == 0
            && result.CursorPublish.Publish.DataBufferDescriptorIndex == 1
            && result.CursorPublish.Publish.StatusByteDescriptorIndex == 2
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight
            && EndianPrimitives.ReadUInt64LittleEndian(requestHeader.AsSpan(8, sizeof(ulong))) == 4
            && ReadUInt16(queueMemory, (int)result.CursorPublish.Publish.LifecyclePublish.AvailableRingEntryOffset) == 0
            && ReadUInt16(queueMemory, (int)queue.Plan.AvailableRing.Offset + 2) == 5;
    }

    private static bool TestReadQueueRejectsOutOfRangeBlockRequest()
    {
        BlockDeviceInfo device = CreateDevice(8);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 2,
                usedRingIndex: 3);
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[512];
        byte[] status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        byte[] queueMemory = new byte[(int)queue.Plan.EndOffset];
        Array.Fill(queueMemory, (byte)0x58);

        VirtioBlockReadQueuePublishResult result =
            VirtioBlockReadQueuePlanner.PublishRead(
                device,
                firstSector: 8,
                sectorCount: 1,
                queueSize: QueueSize,
                states,
                ref cursor,
                requestHeaderPhysicalAddress: HeaderPhysicalAddress,
                dataBufferPhysicalAddress: DataPhysicalAddress,
                statusBytePhysicalAddress: StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockReadQueuePublishStatus.InvalidSectorRange
            && result.BlockDeviceStatus == BlockDeviceStatus.InvalidBlockRange
            && !result.CursorPublishAttempted
            && result.FirstSector == 8
            && result.SectorCount == 1
            && result.RequestedByteCount == 512
            && result.PreviousAvailableRingIndex == 2
            && result.CurrentAvailableRingIndex == 2
            && result.PreviousUsedRingIndex == 3
            && result.CurrentUsedRingIndex == 3
            && cursor.AvailableRingIndex == 2
            && cursor.UsedRingIndex == 3
            && AllBytesEqual(requestHeader, 0)
            && AllBytesEqual(queueMemory, 0x58)
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free);
    }

    private static bool TestReadQueuePreservesCursorOnPublishFailure()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 5,
                usedRingIndex: 7);

        VirtioBlockReadQueuePublishResult result =
            PublishReadQueue(
                device,
                firstSector: 3,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 511,
                out byte[] requestHeader,
                out _,
                out byte[] queueMemory);

        return !result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockReadQueuePublishStatus.CursorPublishFailed
            && result.BlockDeviceStatus == BlockDeviceStatus.Success
            && result.CursorPublishStatus == VirtioBlockQueueCursorPublishStatus.PublishPlanFailed
            && result.DescriptorPublishStatus == VirtioBlockDescriptorPublishPlanStatus.LifecyclePublishFailed
            && result.LifecyclePublishStatus == VirtioBlockReadLifecyclePublishStatus.ReadPlanFailed
            && result.CursorPublishAttempted
            && result.FirstSector == 3
            && result.SectorCount == 1
            && result.RequestedByteCount == 512
            && result.PreviousAvailableRingIndex == 5
            && result.CurrentAvailableRingIndex == 5
            && result.PreviousUsedRingIndex == 7
            && result.CurrentUsedRingIndex == 7
            && cursor.AvailableRingIndex == 5
            && cursor.UsedRingIndex == 7
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free)
            && AllBytesEqual(requestHeader, 0)
            && AllBytesEqual(queueMemory, 0);
    }

    private static bool TestReadQueuePublishDefaultResultIsNotSuccess()
    {
        VirtioBlockReadQueuePublishResult publish = default;
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 2,
                usedRingIndex: 3);

        VirtioBlockReadQueueCompletionResult completion =
            VirtioBlockReadQueueCompletionPlanner.CompleteRead(
                publish,
                ReadOnlySpan<byte>.Empty,
                ReadOnlySpan<byte>.Empty,
                states,
                ref cursor);

        return !publish.IsInitialized
            && !publish.Succeeded
            && publish.Status == VirtioBlockReadQueuePublishStatus.Success
            && !completion.Succeeded
            && completion.Status == VirtioBlockReadQueueCompletionStatus.PublicationFailed
            && completion.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && !completion.CursorReclaimAttempted
            && cursor.AvailableRingIndex == 2
            && cursor.UsedRingIndex == 3
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free);
    }

    private static bool TestReadQueuePreservesNoLiveDeviceBoundary()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 0,
                usedRingIndex: 0);
        VirtioBlockReadQueuePublishResult result =
            PublishReadQueue(
                device,
                firstSector: 1,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 512,
                out _,
                out _,
                out _);

        return result.Succeeded
            && VirtioBlockReadQueuePlanner.RequiresBlockDeviceInfo
            && VirtioBlockReadQueuePlanner.ValidatesBlockGeometryAndRange
            && VirtioBlockReadQueuePlanner.RequiresCallerOwnedCursorState
            && VirtioBlockReadQueuePlanner.RequiresCallerOwnedQueueMemory
            && VirtioBlockReadQueuePlanner.RequiresCallerOwnedStatusBuffer
            && VirtioBlockReadQueuePlanner.RequiresCallerOwnedDescriptorStateTable
            && !VirtioBlockReadQueuePlanner.ReadsQueueMemory
            && !VirtioBlockReadQueuePlanner.ReadsStatusBuffer
            && VirtioBlockReadQueuePlanner.WritesQueueMemory
            && VirtioBlockReadQueuePlanner.WritesRequestHeader
            && VirtioBlockReadQueuePlanner.MutatesCallerOwnedCursorState
            && VirtioBlockReadQueuePlanner.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockReadQueuePlanner.PerformsMmio
            && !VirtioBlockReadQueuePlanner.MapsBars
            && !VirtioBlockReadQueuePlanner.ContainsVolatileAccess
            && !VirtioBlockReadQueuePlanner.AccessesPhysicalAddresses
            && !VirtioBlockReadQueuePlanner.ExecutesMemoryBarriers
            && !VirtioBlockReadQueuePlanner.ConfiguresLiveQueues
            && !VirtioBlockReadQueuePlanner.NotifiesLiveDevice
            && !VirtioBlockReadQueuePlanner.PollsDevice
            && !VirtioBlockReadQueuePlanner.HandlesInterrupts
            && !VirtioBlockReadQueuePlanner.PerformsDmaMapping
            && !VirtioBlockReadQueuePlanner.AllocatesDmaMemory
            && !VirtioBlockReadQueuePlanner.PerformsBlockIo
            && !VirtioBlockReadQueuePlanner.PerformsDiskRead
            && !VirtioBlockReadQueuePlanner.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadQueuePublishResult>();
    }

    private static bool TestReadQueueCompletesAndAdvancesUsedIndex()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        WriteSuccessfulCompletion(publish, queueMemory, status);

        VirtioBlockReadQueueCompletionResult result =
            VirtioBlockReadQueueCompletionPlanner.CompleteRead(
                publish,
                queueMemory,
                status,
                states,
                ref cursor);

        return publish.Succeeded
            && result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockReadQueueCompletionStatus.Success
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.CursorReclaimStatus == VirtioBlockQueueCursorReclaimStatus.Success
            && result.ReclaimPlanStatus == VirtioBlockDescriptorReclaimPlanStatus.Success
            && result.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.Success
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.Success
            && result.CursorReclaimAttempted
            && result.PreviousAvailableRingIndex == 5
            && result.CurrentAvailableRingIndex == 5
            && result.PreviousUsedRingIndex == 8
            && result.CurrentUsedRingIndex == 9
            && cursor.AvailableRingIndex == 5
            && cursor.UsedRingIndex == 9
            && result.CursorReclaim.Reclaim.DescriptorsFreed ==
                VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free);
    }

    private static bool TestReadQueueCompletionRejectsFailedPublication()
    {
        BlockDeviceInfo device = CreateDevice(8);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 2,
                usedRingIndex: 3);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 8,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 512,
                out _,
                out _,
                out _);

        VirtioBlockReadQueueCompletionResult result =
            VirtioBlockReadQueueCompletionPlanner.CompleteRead(
                publish,
                ReadOnlySpan<byte>.Empty,
                ReadOnlySpan<byte>.Empty,
                states,
                ref cursor);

        return !publish.Succeeded
            && !result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockReadQueueCompletionStatus.PublicationFailed
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.InvalidSectorRange
            && !result.CursorReclaimAttempted
            && result.PreviousAvailableRingIndex == 2
            && result.CurrentAvailableRingIndex == 2
            && result.PreviousUsedRingIndex == 3
            && result.CurrentUsedRingIndex == 3
            && cursor.AvailableRingIndex == 2
            && cursor.UsedRingIndex == 3
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free);
    }

    private static bool TestReadQueueCompletionRejectsNoCompletion()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 1,
                usedRingIndex: 6);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 2,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 512,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        EndianPrimitives.WriteUInt16LittleEndian(
            queueMemory.AsSpan((int)publish.CursorPublish.Publish.LifecyclePublish.QueuePlan.UsedRing.Offset + 2, sizeof(ushort)),
            publish.CursorPublish.Publish.PreviousUsedIndex);

        VirtioBlockReadQueueCompletionResult result =
            VirtioBlockReadQueueCompletionPlanner.CompleteRead(
                publish,
                queueMemory,
                status,
                states,
                ref cursor);

        return publish.Succeeded
            && !result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockReadQueueCompletionStatus.CursorReclaimFailed
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.CursorReclaimStatus == VirtioBlockQueueCursorReclaimStatus.ReclaimPlanFailed
            && result.ReclaimPlanStatus == VirtioBlockDescriptorReclaimPlanStatus.CompletionFailed
            && result.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.CompletionFailed
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.NoCompletion
            && result.CursorReclaimAttempted
            && result.PreviousAvailableRingIndex == 2
            && result.CurrentAvailableRingIndex == 2
            && result.PreviousUsedRingIndex == 6
            && result.CurrentUsedRingIndex == 6
            && cursor.AvailableRingIndex == 2
            && cursor.UsedRingIndex == 6
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight;
    }

    private static bool TestReadQueueCompletionDefaultResultIsNotSuccess()
    {
        VirtioBlockReadQueueCompletionResult completion = default;
        VirtioBlockReadQueueBlockReadProjectionResult projection =
            VirtioBlockReadQueueBlockReadProjector.Project(completion);

        return !completion.IsInitialized
            && !completion.Succeeded
            && completion.Status == VirtioBlockReadQueueCompletionStatus.Success
            && !projection.Succeeded
            && projection.Status == VirtioBlockReadQueueBlockReadProjectionStatus.CompletionFailed
            && projection.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.Success
            && projection.BlockRead.Status == BlockDeviceStatus.IoError;
    }

    private static bool TestReadQueueCompletionPreservesNoLiveDeviceBoundary()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 0,
                usedRingIndex: 0);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 1,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 512,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        WriteSuccessfulCompletion(publish, queueMemory, status);
        VirtioBlockReadQueueCompletionResult result =
            VirtioBlockReadQueueCompletionPlanner.CompleteRead(
                publish,
                queueMemory,
                status,
                states,
                ref cursor);

        return result.Succeeded
            && VirtioBlockReadQueueCompletionPlanner.RequiresSuccessfulReadQueuePublication
            && VirtioBlockReadQueueCompletionPlanner.RequiresCallerOwnedCursorState
            && VirtioBlockReadQueueCompletionPlanner.RequiresCallerOwnedQueueMemory
            && VirtioBlockReadQueueCompletionPlanner.RequiresCallerOwnedStatusBuffer
            && VirtioBlockReadQueueCompletionPlanner.RequiresCallerOwnedDescriptorStateTable
            && VirtioBlockReadQueueCompletionPlanner.ReadsQueueMemory
            && VirtioBlockReadQueueCompletionPlanner.ReadsStatusBuffer
            && !VirtioBlockReadQueueCompletionPlanner.WritesQueueMemory
            && !VirtioBlockReadQueueCompletionPlanner.WritesRequestHeader
            && VirtioBlockReadQueueCompletionPlanner.MutatesCallerOwnedCursorState
            && VirtioBlockReadQueueCompletionPlanner.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockReadQueueCompletionPlanner.PerformsMmio
            && !VirtioBlockReadQueueCompletionPlanner.MapsBars
            && !VirtioBlockReadQueueCompletionPlanner.ContainsVolatileAccess
            && !VirtioBlockReadQueueCompletionPlanner.AccessesPhysicalAddresses
            && !VirtioBlockReadQueueCompletionPlanner.ExecutesMemoryBarriers
            && !VirtioBlockReadQueueCompletionPlanner.ConfiguresLiveQueues
            && !VirtioBlockReadQueueCompletionPlanner.NotifiesLiveDevice
            && !VirtioBlockReadQueueCompletionPlanner.PollsDevice
            && !VirtioBlockReadQueueCompletionPlanner.HandlesInterrupts
            && !VirtioBlockReadQueueCompletionPlanner.PerformsDmaMapping
            && !VirtioBlockReadQueueCompletionPlanner.AllocatesDmaMemory
            && !VirtioBlockReadQueueCompletionPlanner.PerformsBlockIo
            && !VirtioBlockReadQueueCompletionPlanner.PerformsDiskRead
            && !VirtioBlockReadQueueCompletionPlanner.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadQueueCompletionResult>();
    }

    private static bool TestReadQueueBlockResultReportsCompletedRead()
    {
        VirtioBlockReadQueueCompletionResult completion =
            CreateCompletedReadQueue(
                firstSector: 4,
                sectorCount: 2,
                destinationLength: 1024);

        VirtioBlockReadQueueBlockReadProjectionResult result =
            VirtioBlockReadQueueBlockReadProjector.Project(completion);

        return completion.Succeeded
            && result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockReadQueueBlockReadProjectionStatus.Success
            && result.BlockRead.Succeeded
            && result.BlockRead.Status == BlockDeviceStatus.Success
            && result.BlockRead.BlocksRead == 2
            && result.BlockRead.BytesRead == 1024
            && result.RequestedBlockCount == 2
            && result.RequestedByteCount == 1024
            && result.CompletedByteCount == 1024
            && result.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.Success
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.CursorReclaimStatus == VirtioBlockQueueCursorReclaimStatus.Success
            && result.ReclaimPlanStatus == VirtioBlockDescriptorReclaimPlanStatus.Success
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.Success;
    }

    private static bool TestReadQueueBlockResultMapsPublicationFailure()
    {
        BlockDeviceInfo device = CreateDevice(8);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 2,
                usedRingIndex: 3);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 8,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 512,
                out _,
                out _,
                out _);
        VirtioBlockReadQueueCompletionResult completion =
            VirtioBlockReadQueueCompletionPlanner.CompleteRead(
                publish,
                ReadOnlySpan<byte>.Empty,
                ReadOnlySpan<byte>.Empty,
                states,
                ref cursor);

        VirtioBlockReadQueueBlockReadProjectionResult result =
            VirtioBlockReadQueueBlockReadProjector.Project(completion);

        return !publish.Succeeded
            && !completion.Succeeded
            && !result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockReadQueueBlockReadProjectionStatus.PublicationFailed
            && result.BlockRead.Status == BlockDeviceStatus.InvalidBlockRange
            && result.BlockRead.BlocksRead == 0
            && result.BlockRead.BytesRead == 0
            && result.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.PublicationFailed
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.InvalidSectorRange
            && result.RequestedBlockCount == 1
            && result.RequestedByteCount == 512
            && result.CompletedByteCount == 0;
    }

    private static bool TestReadQueueBlockResultMapsCompletionFailure()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 1,
                usedRingIndex: 6);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 2,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 512,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        EndianPrimitives.WriteUInt16LittleEndian(
            queueMemory.AsSpan((int)publish.CursorPublish.Publish.LifecyclePublish.QueuePlan.UsedRing.Offset + 2, sizeof(ushort)),
            publish.CursorPublish.Publish.PreviousUsedIndex);
        VirtioBlockReadQueueCompletionResult completion =
            VirtioBlockReadQueueCompletionPlanner.CompleteRead(
                publish,
                queueMemory,
                status,
                states,
                ref cursor);

        VirtioBlockReadQueueBlockReadProjectionResult result =
            VirtioBlockReadQueueBlockReadProjector.Project(completion);

        return publish.Succeeded
            && !completion.Succeeded
            && !result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockReadQueueBlockReadProjectionStatus.CompletionFailed
            && result.BlockRead.Status == BlockDeviceStatus.IoError
            && result.BlockRead.BlocksRead == 0
            && result.BlockRead.BytesRead == 0
            && result.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.CursorReclaimFailed
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.CursorReclaimStatus == VirtioBlockQueueCursorReclaimStatus.ReclaimPlanFailed
            && result.ReclaimPlanStatus == VirtioBlockDescriptorReclaimPlanStatus.CompletionFailed
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.NoCompletion
            && result.RequestedBlockCount == 1
            && result.RequestedByteCount == 512
            && result.CompletedByteCount == 0;
    }

    private static bool TestReadQueueBlockResultDefaultResultIsNotSuccess()
    {
        VirtioBlockReadQueueBlockReadProjectionResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueueBlockReadProjectionStatus.Success
            && !result.BlockRead.Succeeded
            && result.RequestedBlockCount == 0
            && result.RequestedByteCount == 0
            && result.CompletedByteCount == 0;
    }

    private static bool TestReadQueueBlockResultPreservesNoLiveDeviceBoundary()
    {
        VirtioBlockReadQueueCompletionResult completion =
            CreateCompletedReadQueue(
                firstSector: 1,
                sectorCount: 1,
                destinationLength: 512);

        VirtioBlockReadQueueBlockReadProjectionResult result =
            VirtioBlockReadQueueBlockReadProjector.Project(completion);

        return result.Succeeded
            && VirtioBlockReadQueueBlockReadProjector.ConsumesReadQueueCompletionResult
            && !VirtioBlockReadQueueBlockReadProjector.ReadsQueueMemory
            && !VirtioBlockReadQueueBlockReadProjector.ReadsStatusBuffer
            && !VirtioBlockReadQueueBlockReadProjector.WritesQueueMemory
            && !VirtioBlockReadQueueBlockReadProjector.WritesRequestHeader
            && !VirtioBlockReadQueueBlockReadProjector.MutatesCallerOwnedCursorState
            && !VirtioBlockReadQueueBlockReadProjector.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockReadQueueBlockReadProjector.PerformsMmio
            && !VirtioBlockReadQueueBlockReadProjector.MapsBars
            && !VirtioBlockReadQueueBlockReadProjector.ContainsVolatileAccess
            && !VirtioBlockReadQueueBlockReadProjector.AccessesPhysicalAddresses
            && !VirtioBlockReadQueueBlockReadProjector.ExecutesMemoryBarriers
            && !VirtioBlockReadQueueBlockReadProjector.ConfiguresLiveQueues
            && !VirtioBlockReadQueueBlockReadProjector.NotifiesLiveDevice
            && !VirtioBlockReadQueueBlockReadProjector.PollsDevice
            && !VirtioBlockReadQueueBlockReadProjector.HandlesInterrupts
            && !VirtioBlockReadQueueBlockReadProjector.PerformsDmaMapping
            && !VirtioBlockReadQueueBlockReadProjector.AllocatesDmaMemory
            && !VirtioBlockReadQueueBlockReadProjector.PerformsBlockIo
            && !VirtioBlockReadQueueBlockReadProjector.PerformsDiskRead
            && !VirtioBlockReadQueueBlockReadProjector.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadQueueBlockReadProjectionResult>();
    }

    private static bool TestNotifyPlansReadQueueNotificationMetadata()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out _,
                out _);

        VirtioBlockQueueNotificationMetadataResult result =
            VirtioBlockQueueNotificationMetadataPlanner.Create(
                publish,
                queueIndex: 0,
                queueNotifyOffset: 7);

        return publish.Succeeded
            && result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.CursorPublishStatus == VirtioBlockQueueCursorPublishStatus.Success
            && result.DescriptorPublishStatus == VirtioBlockDescriptorPublishPlanStatus.Success
            && result.QueueIndex == 0
            && result.QueueNotifyOffset == 7
            && result.PublishedDescriptorCount == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.PreviousAvailableRingIndex == 4
            && result.NextAvailableRingIndex == 5
            && result.DescriptorHead == 0
            && result.WriteValue == 0
            && result.WriteWidthBytes == sizeof(ushort)
            && cursor.AvailableRingIndex == 5
            && cursor.UsedRingIndex == 8;
    }

    private static bool TestNotifyRejectsFailedPublication()
    {
        BlockDeviceInfo device = CreateDevice(8);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 2,
                usedRingIndex: 3);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 8,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 512,
                out _,
                out _,
                out _);

        VirtioBlockQueueNotificationMetadataResult result =
            VirtioBlockQueueNotificationMetadataPlanner.Create(
                publish,
                queueIndex: 0,
                queueNotifyOffset: 9);

        return !publish.Succeeded
            && !result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockQueueNotificationMetadataStatus.PublicationFailed
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.InvalidSectorRange
            && result.QueueIndex == 0
            && result.QueueNotifyOffset == 9
            && result.PublishedDescriptorCount == 0
            && result.PreviousAvailableRingIndex == 2
            && result.NextAvailableRingIndex == 2
            && result.DescriptorHead == 0
            && result.WriteValue == 0
            && result.WriteWidthBytes == sizeof(ushort)
            && cursor.AvailableRingIndex == 2
            && cursor.UsedRingIndex == 3;
    }

    private static bool TestNotifyDefaultResultIsNotSuccess()
    {
        VirtioBlockQueueNotificationMetadataResult notification = default;
        VirtioBlockQueueNotifyOffsetPlanResult offset =
            VirtioBlockQueueNotifyOffsetPlanner.Create(
                notification,
                notifyOffsetMultiplier: 4);

        return !notification.IsInitialized
            && !notification.Succeeded
            && notification.Status == VirtioBlockQueueNotificationMetadataStatus.Success
            && !offset.Succeeded
            && offset.Status == VirtioBlockQueueNotifyOffsetPlanStatus.NotificationMetadataFailed
            && offset.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && offset.NotifyRelativeOffset == 0
            && offset.PublishedDescriptorCount == 0;
    }

    private static bool TestNotifyPreservesNoLiveDeviceBoundary()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 0,
                usedRingIndex: 0);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 1,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 512,
                out _,
                out _,
                out _);
        VirtioBlockQueueNotificationMetadataResult result =
            VirtioBlockQueueNotificationMetadataPlanner.Create(
                publish,
                queueIndex: 0,
                queueNotifyOffset: 4);

        return result.Succeeded
            && VirtioBlockQueueNotificationMetadataPlanner.ConsumesReadQueuePublication
            && VirtioBlockQueueNotificationMetadataPlanner.ProducesFutureNotifyWriteMetadata
            && !VirtioBlockQueueNotificationMetadataPlanner.ReadsQueueMemory
            && !VirtioBlockQueueNotificationMetadataPlanner.ReadsStatusBuffer
            && !VirtioBlockQueueNotificationMetadataPlanner.WritesQueueMemory
            && !VirtioBlockQueueNotificationMetadataPlanner.WritesRequestHeader
            && !VirtioBlockQueueNotificationMetadataPlanner.MutatesCallerOwnedCursorState
            && !VirtioBlockQueueNotificationMetadataPlanner.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockQueueNotificationMetadataPlanner.PerformsMmio
            && !VirtioBlockQueueNotificationMetadataPlanner.MapsBars
            && !VirtioBlockQueueNotificationMetadataPlanner.ContainsVolatileAccess
            && !VirtioBlockQueueNotificationMetadataPlanner.AccessesPhysicalAddresses
            && !VirtioBlockQueueNotificationMetadataPlanner.ExecutesMemoryBarriers
            && !VirtioBlockQueueNotificationMetadataPlanner.ConfiguresLiveQueues
            && !VirtioBlockQueueNotificationMetadataPlanner.NotifiesLiveDevice
            && !VirtioBlockQueueNotificationMetadataPlanner.PollsDevice
            && !VirtioBlockQueueNotificationMetadataPlanner.HandlesInterrupts
            && !VirtioBlockQueueNotificationMetadataPlanner.PerformsDmaMapping
            && !VirtioBlockQueueNotificationMetadataPlanner.AllocatesDmaMemory
            && !VirtioBlockQueueNotificationMetadataPlanner.PerformsBlockIo
            && !VirtioBlockQueueNotificationMetadataPlanner.PerformsDiskRead
            && !VirtioBlockQueueNotificationMetadataPlanner.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockQueueNotificationMetadataResult>();
    }

    private static bool TestNotifyOffsetDerivesRelativeOffset()
    {
        VirtioBlockQueueNotificationMetadataResult notification =
            CreateSuccessfulNotification(queueNotifyOffset: 7);

        VirtioBlockQueueNotifyOffsetPlanResult result =
            VirtioBlockQueueNotifyOffsetPlanner.Create(
                notification,
                notifyOffsetMultiplier: 4);

        return result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.QueueIndex == 0
            && result.QueueNotifyOffset == 7
            && result.NotifyOffsetMultiplier == 4
            && result.NotifyRelativeOffset == 28
            && result.PublishedDescriptorCount == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.PreviousAvailableRingIndex == 4
            && result.NextAvailableRingIndex == 5
            && result.DescriptorHead == 0
            && result.WriteValue == 0
            && result.WriteWidthBytes == sizeof(ushort);
    }

    private static bool TestNotifyOffsetRejectsFailedNotificationMetadata()
    {
        BlockDeviceInfo device = CreateDevice(8);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 2,
                usedRingIndex: 3);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 8,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 512,
                out _,
                out _,
                out _);
        VirtioBlockQueueNotificationMetadataResult notification =
            VirtioBlockQueueNotificationMetadataPlanner.Create(
                publish,
                queueIndex: 0,
                queueNotifyOffset: 9);

        VirtioBlockQueueNotifyOffsetPlanResult result =
            VirtioBlockQueueNotifyOffsetPlanner.Create(
                notification,
                notifyOffsetMultiplier: 4);

        return !notification.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockQueueNotifyOffsetPlanStatus.NotificationMetadataFailed
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.PublicationFailed
            && result.QueueIndex == 0
            && result.QueueNotifyOffset == 9
            && result.NotifyOffsetMultiplier == 4
            && result.NotifyRelativeOffset == 0
            && result.PublishedDescriptorCount == 0;
    }

    private static bool TestNotifyOffsetRejectsRelativeOffsetOverflow()
    {
        VirtioBlockQueueNotificationMetadataResult notification =
            CreateSuccessfulNotification(queueNotifyOffset: ushort.MaxValue);

        VirtioBlockQueueNotifyOffsetPlanResult result =
            VirtioBlockQueueNotifyOffsetPlanner.Create(
                notification,
                notifyOffsetMultiplier: uint.MaxValue);

        return notification.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockQueueNotifyOffsetPlanStatus.RelativeOffsetOverflow
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.QueueNotifyOffset == ushort.MaxValue
            && result.NotifyOffsetMultiplier == uint.MaxValue
            && result.NotifyRelativeOffset == 0
            && result.PublishedDescriptorCount == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount;
    }

    private static bool TestNotifyOffsetDefaultResultIsNotSuccess()
    {
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset = default;
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();

        VirtioBlockQueueNotificationRegisterWindowPlanResult window =
            VirtioBlockQueueNotificationRegisterWindowPlanner.PlanAccess(
                notifyOffset,
                windows.Bindings.NotifyConfiguration);

        return !notifyOffset.IsInitialized
            && !notifyOffset.Succeeded
            && notifyOffset.Status == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && notifyOffset.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && notifyOffset.NotifyRelativeOffset == 0
            && notifyOffset.PublishedDescriptorCount == 0
            && !window.Succeeded
            && window.Status == VirtioBlockQueueNotificationRegisterWindowPlanStatus.NotifyOffsetPlanFailed
            && window.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && window.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success;
    }

    private static bool TestNotifyOffsetPreservesNoLiveDeviceBoundary()
    {
        VirtioBlockQueueNotificationMetadataResult notification =
            CreateSuccessfulNotification(queueNotifyOffset: 4);
        VirtioBlockQueueNotifyOffsetPlanResult result =
            VirtioBlockQueueNotifyOffsetPlanner.Create(
                notification,
                notifyOffsetMultiplier: 8);

        return result.Succeeded
            && VirtioBlockQueueNotifyOffsetPlanner.ConsumesNotificationMetadata
            && VirtioBlockQueueNotifyOffsetPlanner.ComputesNotifyRelativeOffset
            && !VirtioBlockQueueNotifyOffsetPlanner.ReadsQueueMemory
            && !VirtioBlockQueueNotifyOffsetPlanner.ReadsStatusBuffer
            && !VirtioBlockQueueNotifyOffsetPlanner.WritesQueueMemory
            && !VirtioBlockQueueNotifyOffsetPlanner.WritesRequestHeader
            && !VirtioBlockQueueNotifyOffsetPlanner.MutatesCallerOwnedCursorState
            && !VirtioBlockQueueNotifyOffsetPlanner.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockQueueNotifyOffsetPlanner.PerformsMmio
            && !VirtioBlockQueueNotifyOffsetPlanner.MapsBars
            && !VirtioBlockQueueNotifyOffsetPlanner.ContainsVolatileAccess
            && !VirtioBlockQueueNotifyOffsetPlanner.AccessesPhysicalAddresses
            && !VirtioBlockQueueNotifyOffsetPlanner.ExecutesMemoryBarriers
            && !VirtioBlockQueueNotifyOffsetPlanner.ConfiguresLiveQueues
            && !VirtioBlockQueueNotifyOffsetPlanner.NotifiesLiveDevice
            && !VirtioBlockQueueNotifyOffsetPlanner.PollsDevice
            && !VirtioBlockQueueNotifyOffsetPlanner.HandlesInterrupts
            && !VirtioBlockQueueNotifyOffsetPlanner.PerformsDmaMapping
            && !VirtioBlockQueueNotifyOffsetPlanner.AllocatesDmaMemory
            && !VirtioBlockQueueNotifyOffsetPlanner.PerformsBlockIo
            && !VirtioBlockQueueNotifyOffsetPlanner.PerformsDiskRead
            && !VirtioBlockQueueNotifyOffsetPlanner.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockQueueNotifyOffsetPlanResult>();
    }

    private static bool TestNotifyWindowPlansAccess()
    {
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            CreateSuccessfulNotifyOffset(queueNotifyOffset: 7, notifyOffsetMultiplier: 4);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();

        VirtioBlockQueueNotificationRegisterWindowPlanResult result =
            VirtioBlockQueueNotificationRegisterWindowPlanner.PlanAccess(
                notifyOffset,
                windows.Bindings.NotifyConfiguration);

        uint notifyRelativeOffset = 7 * NotifyOffsetMultiplier;
        ulong barRelativeOffset = NotifyApertureBarOffset + notifyRelativeOffset;
        return notifyOffset.Succeeded
            && windows.Succeeded
            && result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.QueueIndex == 0
            && result.QueueNotifyOffset == 7
            && result.NotifyOffsetMultiplier == NotifyOffsetMultiplier
            && result.NotifyRelativeOffset == notifyRelativeOffset
            && result.PublishedDescriptorCount == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.PreviousAvailableRingIndex == 4
            && result.NextAvailableRingIndex == 5
            && result.DescriptorHead == 0
            && result.WriteValue == 0
            && result.WriteWidthBytes == sizeof(ushort)
            && result.AccessPlan.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.AccessPlan.BarIndex == 1
            && result.AccessPlan.RelativeOffset == notifyRelativeOffset
            && result.AccessPlan.Width == VirtioPciRegisterAccessWidth.Word
            && result.AccessPlan.WidthBytes == sizeof(ushort)
            && result.AccessPlan.BarRelativeOffset == barRelativeOffset
            && result.AccessPlan.PhysicalAddress == NotifyBarPhysicalBase + barRelativeOffset
            && result.AccessPlan.VirtualAddress == NotifyBarVirtualBase + barRelativeOffset;
    }

    private static bool TestNotifyWindowRejectsFailedOffsetPlan()
    {
        VirtioBlockQueueNotificationMetadataResult notification =
            CreateSuccessfulNotification(queueNotifyOffset: ushort.MaxValue);
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            VirtioBlockQueueNotifyOffsetPlanner.Create(
                notification,
                notifyOffsetMultiplier: uint.MaxValue);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();

        VirtioBlockQueueNotificationRegisterWindowPlanResult result =
            VirtioBlockQueueNotificationRegisterWindowPlanner.PlanAccess(
                notifyOffset,
                windows.Bindings.NotifyConfiguration);

        return !notifyOffset.Succeeded
            && notifyOffset.Status == VirtioBlockQueueNotifyOffsetPlanStatus.RelativeOffsetOverflow
            && windows.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockQueueNotificationRegisterWindowPlanStatus.NotifyOffsetPlanFailed
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.RelativeOffsetOverflow
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.QueueNotifyOffset == ushort.MaxValue
            && result.NotifyOffsetMultiplier == uint.MaxValue
            && result.NotifyRelativeOffset == 0
            && result.WriteWidthBytes == sizeof(ushort)
            && result.AccessPlan.WidthBytes == 0;
    }

    private static bool TestNotifyWindowPropagatesRegisterWindowFailure()
    {
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            CreateSuccessfulNotifyOffset(queueNotifyOffset: 3, notifyOffsetMultiplier: 4);

        VirtioBlockQueueNotificationRegisterWindowPlanResult result =
            VirtioBlockQueueNotificationRegisterWindowPlanner.PlanAccess(
                notifyOffset,
                notifyConfiguration: default);

        return notifyOffset.Succeeded
            && !result.Succeeded
            && result.Status == VirtioBlockQueueNotificationRegisterWindowPlanStatus.RegisterWindowPlanFailed
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.MissingWindow
            && result.QueueIndex == 0
            && result.QueueNotifyOffset == 3
            && result.NotifyOffsetMultiplier == NotifyOffsetMultiplier
            && result.NotifyRelativeOffset == 12
            && result.PublishedDescriptorCount == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.AccessPlan.WidthBytes == 0;
    }

    private static bool TestNotifyWindowDefaultResultIsNotSuccess()
    {
        VirtioBlockQueueNotificationRegisterWindowPlanResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.NotifyRelativeOffset == 0
            && result.WriteWidthBytes == 0
            && result.AccessPlan.WidthBytes == 0;
    }

    private static bool TestNotifyWindowPreservesNoLiveDeviceBoundary()
    {
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            CreateSuccessfulNotifyOffset(queueNotifyOffset: 2, notifyOffsetMultiplier: 8);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        VirtioBlockQueueNotificationRegisterWindowPlanResult result =
            VirtioBlockQueueNotificationRegisterWindowPlanner.PlanAccess(
                notifyOffset,
                windows.Bindings.NotifyConfiguration);

        return result.Succeeded
            && VirtioBlockQueueNotificationRegisterWindowPlanner.ConsumesNotifyOffsetPlan
            && VirtioBlockQueueNotificationRegisterWindowPlanner.RequiresRegisterWindowBinding
            && VirtioBlockQueueNotificationRegisterWindowPlanner.PlansMappedRegisterAddresses
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.ReadsQueueMemory
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.ReadsStatusBuffer
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.WritesQueueMemory
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.WritesRequestHeader
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.MutatesCallerOwnedCursorState
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.PerformsMmio
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.MapsBars
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.ContainsVolatileAccess
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.ReadsDeviceRegisters
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.WritesDeviceRegisters
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.AccessesPhysicalAddresses
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.ExecutesMemoryBarriers
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.ConfiguresLiveQueues
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.NotifiesLiveDevice
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.PollsDevice
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.HandlesInterrupts
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.PerformsDmaMapping
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.AllocatesDmaMemory
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.PerformsBlockIo
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.PerformsDiskRead
            && !VirtioBlockQueueNotificationRegisterWindowPlanner.WaitsForUsedRingCompletion
            && !VirtioPciBarRegisterWindowPlanner.PerformsLiveMmio
            && !VirtioPciBarRegisterWindowPlanner.WritesDeviceRegisters
            && !VirtioPciBarRegisterWindowPlanner.NotifiesLiveDevice
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockQueueNotificationRegisterWindowPlanResult>();
    }

    private static bool TestNotifyWindowExecWritesPlannedNotifyAccess()
    {
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            CreateSuccessfulNotifyOffset(queueNotifyOffset: 7, notifyOffsetMultiplier: 4);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockQueueNotificationRegisterWindowExecutionResult result =
            VirtioBlockQueueNotificationRegisterWindowExecutor.Execute(
                notifyOffset,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        ulong barRelativeOffset = NotifyApertureBarOffset + (7UL * NotifyOffsetMultiplier);
        int byteOffset = checked((int)barRelativeOffset);
        return notifyOffset.Succeeded
            && windows.Succeeded
            && result.IsInitialized
            && result.Succeeded
            && result.Status == VirtioBlockQueueNotificationRegisterWindowExecutionStatus.Success
            && result.PlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.QueueIndex == 0
            && result.QueueNotifyOffset == 7
            && result.NotifyOffsetMultiplier == NotifyOffsetMultiplier
            && result.NotifyRelativeOffset == 28
            && result.PublishedDescriptorCount == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.PreviousAvailableRingIndex == 4
            && result.NextAvailableRingIndex == 5
            && result.DescriptorHead == 0
            && result.WriteValue == 0
            && result.WriteWidthBytes == sizeof(ushort)
            && result.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.BarIndex == 1
            && result.AccessWidth == VirtioPciRegisterAccessWidth.Word
            && result.AccessWidthBytes == sizeof(ushort)
            && result.BarRelativeOffset == barRelativeOffset
            && result.WrittenValue == 0
            && ReadUInt16AtBarOffset(notifyBytes, result.BarRelativeOffset) == 0
            && AllBytesEqual(notifyBytes, 0, byteOffset, 0xCC)
            && AllBytesEqual(
                notifyBytes,
                byteOffset + sizeof(ushort),
                notifyBytes.Length - byteOffset - sizeof(ushort),
                0xCC);
    }

    private static bool TestNotifyWindowExecRejectsFailedPlan()
    {
        VirtioBlockQueueNotificationMetadataResult notification =
            CreateSuccessfulNotification(queueNotifyOffset: ushort.MaxValue);
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            VirtioBlockQueueNotifyOffsetPlanner.Create(
                notification,
                notifyOffsetMultiplier: uint.MaxValue);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockQueueNotificationRegisterWindowExecutionResult result =
            VirtioBlockQueueNotificationRegisterWindowExecutor.Execute(
                notifyOffset,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return !notifyOffset.Succeeded
            && windows.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockQueueNotificationRegisterWindowExecutionStatus.PlanFailed
            && result.PlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.NotifyOffsetPlanFailed
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.RelativeOffsetOverflow
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.QueueNotifyOffset == ushort.MaxValue
            && result.NotifyOffsetMultiplier == uint.MaxValue
            && result.NotifyRelativeOffset == 0
            && result.AccessWidthBytes == 0
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestNotifyWindowExecPropagatesAccessFailure()
    {
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            CreateSuccessfulNotifyOffset(queueNotifyOffset: 3, notifyOffsetMultiplier: 4);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        ulong barRelativeOffset = NotifyApertureBarOffset + (3UL * NotifyOffsetMultiplier);
        byte[] notifyBytes = CreateNotifyWindowBytes(length: checked((int)barRelativeOffset + 1));

        VirtioBlockQueueNotificationRegisterWindowExecutionResult result =
            VirtioBlockQueueNotificationRegisterWindowExecutor.Execute(
                notifyOffset,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return notifyOffset.Succeeded
            && windows.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockQueueNotificationRegisterWindowExecutionStatus.AccessExecutionFailed
            && result.PlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && result.QueueNotifyOffset == 3
            && result.NotifyRelativeOffset == 12
            && result.BarRelativeOffset == barRelativeOffset
            && result.WrittenValue == 0
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestNotifyWindowExecDefaultResultIsNotSuccess()
    {
        VirtioBlockQueueNotificationRegisterWindowExecutionResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockQueueNotificationRegisterWindowExecutionStatus.Success
            && result.PlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.QueueIndex == 0
            && result.QueueNotifyOffset == 0
            && result.NotifyOffsetMultiplier == 0
            && result.NotifyRelativeOffset == 0
            && result.PublishedDescriptorCount == 0
            && result.PreviousAvailableRingIndex == 0
            && result.NextAvailableRingIndex == 0
            && result.DescriptorHead == 0
            && result.WriteValue == 0
            && result.WriteWidthBytes == 0
            && result.CapabilityType == (VirtioPciCapabilityType)0
            && result.BarIndex == 0
            && result.AccessWidth == (VirtioPciRegisterAccessWidth)0
            && result.AccessWidthBytes == 0
            && result.BarRelativeOffset == 0
            && result.WrittenValue == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockQueueNotificationRegisterWindowExecutionResult>();
    }

    private static bool TestNotifyWindowExecPreservesNoLiveDeviceBoundary()
    {
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            CreateSuccessfulNotifyOffset(queueNotifyOffset: 1, notifyOffsetMultiplier: 4);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockQueueNotificationRegisterWindowExecutionResult result =
            VirtioBlockQueueNotificationRegisterWindowExecutor.Execute(
                notifyOffset,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return result.Succeeded
            && VirtioBlockQueueNotificationRegisterWindowExecutor.InvokesRegisterWindowPlanner
            && VirtioBlockQueueNotificationRegisterWindowExecutor.RequiresRegisterWindowBinding
            && VirtioBlockQueueNotificationRegisterWindowExecutor.UsesCallerOwnedWindowBuffer
            && VirtioBlockQueueNotificationRegisterWindowExecutor.InvokesRegisterWindowAccessExecutor
            && VirtioBlockQueueNotificationRegisterWindowExecutor.WritesCallerOwnedWindowBuffer
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.ReadsQueueMemory
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.ReadsStatusBuffer
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.WritesQueueMemory
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.WritesRequestHeader
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.MutatesCallerOwnedCursorState
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.PerformsMmio
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.MapsBars
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.ContainsVolatileAccess
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.AccessesPhysicalAddresses
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.ExecutesMemoryBarriers
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.ConfiguresLiveQueues
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.NotifiesLiveDevice
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.PollsDevice
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.HandlesInterrupts
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.PerformsDmaMapping
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.AllocatesDmaMemory
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.PerformsBlockIo
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.PerformsDiskRead
            && !VirtioBlockQueueNotificationRegisterWindowExecutor.WaitsForUsedRingCompletion
            && !VirtioPciBarRegisterWindowAccessExecutor.PerformsLiveMmio
            && !VirtioPciBarRegisterWindowAccessExecutor.WritesLiveDeviceRegisters
            && !VirtioPciBarRegisterWindowAccessExecutor.NotifiesLiveDevice
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockQueueNotificationRegisterWindowExecutionResult>();
    }

    private static bool TestNotifyWindowBarrierWritesPlannedNotifyAccess()
    {
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            CreateSuccessfulNotifyOffset(queueNotifyOffset: 7, notifyOffsetMultiplier: 4);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult result =
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notifyOffset,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(31),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        ulong barRelativeOffset = NotifyApertureBarOffset + (7UL * NotifyOffsetMultiplier);
        int byteOffset = checked((int)barRelativeOffset);
        return notifyOffset.Succeeded
            && windows.Succeeded
            && result.IsInitialized
            && result.Succeeded
            && result.Status == VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.Success
            && result.PlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.QueueNotifyOffset == 7
            && result.NotifyRelativeOffset == 28
            && result.BarrierSucceeded
            && result.BarrierSequence == 31
            && result.RegisterWindowExecutionAttempted
            && result.BarRelativeOffset == barRelativeOffset
            && result.WrittenValue == 0
            && ReadUInt16AtBarOffset(notifyBytes, result.BarRelativeOffset) == 0
            && AllBytesEqual(notifyBytes, 0, byteOffset, 0xCC)
            && AllBytesEqual(
                notifyBytes,
                byteOffset + sizeof(ushort),
                notifyBytes.Length - byteOffset - sizeof(ushort),
                0xCC);
    }

    private static bool TestNotifyWindowBarrierRejectsMissingBarrier()
    {
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            CreateSuccessfulNotifyOffset(queueNotifyOffset: 2, notifyOffsetMultiplier: 4);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockQueueNotificationBarrierReadiness barrier =
            new VirtioBlockQueueNotificationBarrierReadiness(
                descriptorWritesVisible: true,
                availableRingWritesVisible: false,
                orderedBeforeNotify: true,
                barrierSequence: 32);

        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult result =
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notifyOffset,
                barrier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return notifyOffset.Succeeded
            && windows.Succeeded
            && !barrier.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady
            && result.PlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.DescriptorWritesVisible
            && !result.AvailableRingWritesVisible
            && result.OrderedBeforeNotify
            && !result.BarrierSucceeded
            && result.BarrierSequence == 32
            && !result.RegisterWindowExecutionAttempted
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestNotifyWindowBarrierRejectsFailedPlan()
    {
        VirtioBlockQueueNotificationMetadataResult notification =
            CreateSuccessfulNotification(queueNotifyOffset: ushort.MaxValue);
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            VirtioBlockQueueNotifyOffsetPlanner.Create(
                notification,
                notifyOffsetMultiplier: uint.MaxValue);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult result =
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notifyOffset,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(33),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return !notifyOffset.Succeeded
            && windows.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.PlanFailed
            && result.PlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.NotifyOffsetPlanFailed
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.RelativeOffsetOverflow
            && result.BarrierSucceeded
            && result.BarrierSequence == 33
            && !result.RegisterWindowExecutionAttempted
            && result.AccessWidthBytes == 0
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestNotifyWindowBarrierPropagatesAccessFailure()
    {
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            CreateSuccessfulNotifyOffset(queueNotifyOffset: 3, notifyOffsetMultiplier: 4);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        ulong barRelativeOffset = NotifyApertureBarOffset + (3UL * NotifyOffsetMultiplier);
        byte[] notifyBytes = CreateNotifyWindowBytes(length: checked((int)barRelativeOffset + 1));

        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult result =
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notifyOffset,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(34),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return notifyOffset.Succeeded
            && windows.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed
            && result.PlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && result.BarrierSucceeded
            && result.BarrierSequence == 34
            && result.RegisterWindowExecutionAttempted
            && result.BarRelativeOffset == barRelativeOffset
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestNotifyWindowBarrierDefaultResultIsNotSuccess()
    {
        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.Success
            && result.PlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.QueueIndex == 0
            && result.QueueNotifyOffset == 0
            && result.NotifyOffsetMultiplier == 0
            && result.NotifyRelativeOffset == 0
            && result.PublishedDescriptorCount == 0
            && result.PreviousAvailableRingIndex == 0
            && result.NextAvailableRingIndex == 0
            && result.DescriptorHead == 0
            && result.WriteValue == 0
            && result.WriteWidthBytes == 0
            && !result.DescriptorWritesVisible
            && !result.AvailableRingWritesVisible
            && !result.OrderedBeforeNotify
            && !result.BarrierSucceeded
            && result.BarrierSequence == 0
            && !result.RegisterWindowExecutionAttempted
            && result.CapabilityType == (VirtioPciCapabilityType)0
            && result.BarIndex == 0
            && result.AccessWidth == (VirtioPciRegisterAccessWidth)0
            && result.AccessWidthBytes == 0
            && result.BarRelativeOffset == 0
            && result.WrittenValue == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult>();
    }

    private static bool TestNotifyWindowBarrierPreservesNoLiveDeviceBoundary()
    {
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            CreateSuccessfulNotifyOffset(queueNotifyOffset: 1, notifyOffsetMultiplier: 4);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult result =
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notifyOffset,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(35),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return result.Succeeded
            && VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.RequiresBarrierReadiness
            && VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.RequiresRegisterWindowBinding
            && VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.UsesCallerOwnedWindowBuffer
            && VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.InvokesRegisterWindowAccessExecutor
            && VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.WritesCallerOwnedWindowBuffer
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.ReadsQueueMemory
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.ReadsStatusBuffer
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.WritesQueueMemory
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.WritesRequestHeader
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.MutatesCallerOwnedCursorState
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.PerformsMmio
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.MapsBars
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.ContainsVolatileAccess
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.AccessesPhysicalAddresses
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.ExecutesMemoryBarriers
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.ConfiguresLiveQueues
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.NotifiesLiveDevice
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.PollsDevice
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.HandlesInterrupts
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.PerformsDmaMapping
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.AllocatesDmaMemory
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.PerformsBlockIo
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.PerformsDiskRead
            && !VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockQueueNotificationBarrierReadiness>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult>();
    }

    private static bool TestReadNotifyComposesPublicationAndBarrierNotify()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out _,
                out _);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockReadQueueNotifyReadinessResult result =
            VirtioBlockReadQueueNotifyReadinessExecutor.Execute(
                publish,
                queueIndex: 0,
                queueNotifyOffset: 7,
                notifyOffsetMultiplier: NotifyOffsetMultiplier,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(41),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        ulong barRelativeOffset = NotifyApertureBarOffset + (7UL * NotifyOffsetMultiplier);
        int byteOffset = checked((int)barRelativeOffset);
        return publish.Succeeded
            && windows.Succeeded
            && result.IsInitialized
            && result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyReadinessStatus.Success
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.NotifyExecutionStatus == VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.Success
            && result.NotifyRegisterWindowPlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.NotificationMetadataAttempted
            && result.NotifyOffsetPlanAttempted
            && result.NotifyExecutionAttempted
            && result.QueueIndex == 0
            && result.QueueNotifyOffset == 7
            && result.NotifyOffsetMultiplier == NotifyOffsetMultiplier
            && result.NotifyRelativeOffset == 28
            && result.PublishedDescriptorCount == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount
            && result.PreviousAvailableRingIndex == 4
            && result.NextAvailableRingIndex == 5
            && result.DescriptorHead == 0
            && result.WriteValue == 0
            && result.WriteWidthBytes == sizeof(ushort)
            && result.BarrierSucceeded
            && result.BarrierSequence == 41
            && result.RegisterWindowExecutionAttempted
            && result.CapabilityType == VirtioPciCapabilityType.NotifyConfiguration
            && result.BarIndex == 1
            && result.AccessWidth == VirtioPciRegisterAccessWidth.Word
            && result.AccessWidthBytes == sizeof(ushort)
            && result.BarRelativeOffset == barRelativeOffset
            && result.WrittenValue == 0
            && ReadUInt16AtBarOffset(notifyBytes, result.BarRelativeOffset) == 0
            && AllBytesEqual(notifyBytes, 0, byteOffset, 0xCC)
            && AllBytesEqual(
                notifyBytes,
                byteOffset + sizeof(ushort),
                notifyBytes.Length - byteOffset - sizeof(ushort),
                0xCC);
    }

    private static bool TestReadNotifyRejectsFailedPublication()
    {
        BlockDeviceInfo device = CreateDevice(8);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 2,
                usedRingIndex: 3);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 8,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 512,
                out _,
                out _,
                out _);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockReadQueueNotifyReadinessResult result =
            VirtioBlockReadQueueNotifyReadinessExecutor.Execute(
                publish,
                queueIndex: 0,
                queueNotifyOffset: 9,
                notifyOffsetMultiplier: NotifyOffsetMultiplier,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(42),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return !publish.Succeeded
            && windows.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyReadinessStatus.PublicationFailed
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.InvalidSectorRange
            && !result.NotificationMetadataAttempted
            && !result.NotifyOffsetPlanAttempted
            && !result.NotifyExecutionAttempted
            && !result.RegisterWindowExecutionAttempted
            && result.QueueIndex == 0
            && result.QueueNotifyOffset == 9
            && result.NotifyOffsetMultiplier == NotifyOffsetMultiplier
            && result.BarrierSucceeded
            && result.BarrierSequence == 42
            && result.BarRelativeOffset == 0
            && AllBytesEqual(notifyBytes, 0xCC)
            && cursor.AvailableRingIndex == 2
            && cursor.UsedRingIndex == 3;
    }

    private static bool TestReadNotifyRejectsMissingBarrier()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out _,
                out _);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockQueueNotificationBarrierReadiness barrier =
            new VirtioBlockQueueNotificationBarrierReadiness(
                descriptorWritesVisible: true,
                availableRingWritesVisible: false,
                orderedBeforeNotify: true,
                barrierSequence: 43);

        VirtioBlockReadQueueNotifyReadinessResult result =
            VirtioBlockReadQueueNotifyReadinessExecutor.Execute(
                publish,
                queueIndex: 0,
                queueNotifyOffset: 2,
                notifyOffsetMultiplier: NotifyOffsetMultiplier,
                barrier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return publish.Succeeded
            && windows.Succeeded
            && !barrier.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyReadinessStatus.MemoryBarrierNotReady
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.NotifyExecutionStatus == VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady
            && result.NotifyRegisterWindowPlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && result.NotificationMetadataAttempted
            && result.NotifyOffsetPlanAttempted
            && result.NotifyExecutionAttempted
            && !result.RegisterWindowExecutionAttempted
            && result.DescriptorWritesVisible
            && !result.AvailableRingWritesVisible
            && result.OrderedBeforeNotify
            && !result.BarrierSucceeded
            && result.BarrierSequence == 43
            && result.QueueNotifyOffset == 2
            && result.NotifyRelativeOffset == 8
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadNotifyPropagatesNotifyExecutionFailure()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out _,
                out _);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        ulong barRelativeOffset = NotifyApertureBarOffset + (3UL * NotifyOffsetMultiplier);
        byte[] notifyBytes = CreateNotifyWindowBytes(length: checked((int)barRelativeOffset + 1));

        VirtioBlockReadQueueNotifyReadinessResult result =
            VirtioBlockReadQueueNotifyReadinessExecutor.Execute(
                publish,
                queueIndex: 0,
                queueNotifyOffset: 3,
                notifyOffsetMultiplier: NotifyOffsetMultiplier,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(44),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return publish.Succeeded
            && windows.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyReadinessStatus.RegisterWindowAccessFailed
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.NotifyExecutionStatus == VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed
            && result.NotifyRegisterWindowPlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.WindowBufferTooSmall
            && result.NotificationMetadataAttempted
            && result.NotifyOffsetPlanAttempted
            && result.NotifyExecutionAttempted
            && result.RegisterWindowExecutionAttempted
            && result.BarrierSucceeded
            && result.BarrierSequence == 44
            && result.QueueNotifyOffset == 3
            && result.NotifyRelativeOffset == 12
            && result.BarRelativeOffset == barRelativeOffset
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadNotifyDefaultResultIsNotSuccess()
    {
        VirtioBlockReadQueueNotifyReadinessResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyReadinessStatus.Success
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.NotifyExecutionStatus == VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.Success
            && result.NotifyRegisterWindowPlanStatus == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success
            && result.RegisterWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && result.AccessExecutionStatus == VirtioPciBarRegisterWindowAccessExecutionStatus.Success
            && !result.NotificationMetadataAttempted
            && !result.NotifyOffsetPlanAttempted
            && !result.NotifyExecutionAttempted
            && result.QueueIndex == 0
            && result.QueueNotifyOffset == 0
            && result.NotifyOffsetMultiplier == 0
            && result.NotifyRelativeOffset == 0
            && result.PublishedDescriptorCount == 0
            && result.PreviousAvailableRingIndex == 0
            && result.NextAvailableRingIndex == 0
            && result.DescriptorHead == 0
            && result.WriteValue == 0
            && result.WriteWidthBytes == 0
            && !result.DescriptorWritesVisible
            && !result.AvailableRingWritesVisible
            && !result.OrderedBeforeNotify
            && !result.BarrierSucceeded
            && result.BarrierSequence == 0
            && !result.RegisterWindowExecutionAttempted
            && result.CapabilityType == (VirtioPciCapabilityType)0
            && result.BarIndex == 0
            && result.AccessWidth == (VirtioPciRegisterAccessWidth)0
            && result.AccessWidthBytes == 0
            && result.BarRelativeOffset == 0
            && result.WrittenValue == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockReadQueueNotifyReadinessResult>();
    }

    private static bool TestReadNotifyPreservesNoLiveDeviceBoundary()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out _,
                out _);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockReadQueueNotifyReadinessResult result =
            VirtioBlockReadQueueNotifyReadinessExecutor.Execute(
                publish,
                queueIndex: 0,
                queueNotifyOffset: 1,
                notifyOffsetMultiplier: NotifyOffsetMultiplier,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(45),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return result.Succeeded
            && VirtioBlockReadQueueNotifyReadinessExecutor.ConsumesReadQueuePublication
            && VirtioBlockReadQueueNotifyReadinessExecutor.InvokesNotificationMetadataPlanner
            && VirtioBlockReadQueueNotifyReadinessExecutor.InvokesNotifyOffsetPlanner
            && VirtioBlockReadQueueNotifyReadinessExecutor.RequiresBarrierReadiness
            && VirtioBlockReadQueueNotifyReadinessExecutor.RequiresRegisterWindowBinding
            && VirtioBlockReadQueueNotifyReadinessExecutor.UsesCallerOwnedWindowBuffer
            && VirtioBlockReadQueueNotifyReadinessExecutor.InvokesRegisterWindowBarrierExecutor
            && VirtioBlockReadQueueNotifyReadinessExecutor.WritesCallerOwnedWindowBuffer
            && !VirtioBlockReadQueueNotifyReadinessExecutor.ReadsQueueMemory
            && !VirtioBlockReadQueueNotifyReadinessExecutor.ReadsStatusBuffer
            && !VirtioBlockReadQueueNotifyReadinessExecutor.WritesQueueMemory
            && !VirtioBlockReadQueueNotifyReadinessExecutor.WritesRequestHeader
            && !VirtioBlockReadQueueNotifyReadinessExecutor.MutatesCallerOwnedCursorState
            && !VirtioBlockReadQueueNotifyReadinessExecutor.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockReadQueueNotifyReadinessExecutor.PerformsMmio
            && !VirtioBlockReadQueueNotifyReadinessExecutor.MapsBars
            && !VirtioBlockReadQueueNotifyReadinessExecutor.ContainsVolatileAccess
            && !VirtioBlockReadQueueNotifyReadinessExecutor.AccessesPhysicalAddresses
            && !VirtioBlockReadQueueNotifyReadinessExecutor.ExecutesMemoryBarriers
            && !VirtioBlockReadQueueNotifyReadinessExecutor.ConfiguresLiveQueues
            && !VirtioBlockReadQueueNotifyReadinessExecutor.NotifiesLiveDevice
            && !VirtioBlockReadQueueNotifyReadinessExecutor.PollsDevice
            && !VirtioBlockReadQueueNotifyReadinessExecutor.HandlesInterrupts
            && !VirtioBlockReadQueueNotifyReadinessExecutor.PerformsDmaMapping
            && !VirtioBlockReadQueueNotifyReadinessExecutor.AllocatesDmaMemory
            && !VirtioBlockReadQueueNotifyReadinessExecutor.PerformsBlockIo
            && !VirtioBlockReadQueueNotifyReadinessExecutor.PerformsDiskRead
            && !VirtioBlockReadQueueNotifyReadinessExecutor.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockReadQueueNotifyReadinessResult>();
    }

    private static bool TestReadNotifyCompletionCompletesAfterNotifyReadiness()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        VirtioBlockReadQueueNotifyReadinessResult readiness =
            CreateSuccessfulReadNotifyReadiness(publish, queueNotifyOffset: 7);
        WriteSuccessfulCompletion(publish, queueMemory, status);

        VirtioBlockReadQueueNotifyCompletionResult result =
            VirtioBlockReadQueueNotifyCompletionPlanner.CompleteAfterNotify(
                publish,
                readiness,
                queueMemory,
                status,
                states,
                ref cursor);

        return publish.Succeeded
            && readiness.Succeeded
            && result.IsInitialized
            && result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyCompletionStatus.Success
            && result.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.Success
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.Success
            && result.CursorReclaimStatus == VirtioBlockQueueCursorReclaimStatus.Success
            && result.ReclaimPlanStatus == VirtioBlockDescriptorReclaimPlanStatus.Success
            && result.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.Success
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.Success
            && result.CompletionAttempted
            && result.PreviousAvailableRingIndex == 5
            && result.CurrentAvailableRingIndex == 5
            && result.PreviousUsedRingIndex == 8
            && result.CurrentUsedRingIndex == 9
            && cursor.AvailableRingIndex == 5
            && cursor.UsedRingIndex == 9
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free);
    }

    private static bool TestReadNotifyCompletionRejectsFailedReadiness()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out _,
                out _);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockQueueNotificationBarrierReadiness barrier =
            new VirtioBlockQueueNotificationBarrierReadiness(
                descriptorWritesVisible: true,
                availableRingWritesVisible: false,
                orderedBeforeNotify: true,
                barrierSequence: 51);
        VirtioBlockReadQueueNotifyReadinessResult readiness =
            VirtioBlockReadQueueNotifyReadinessExecutor.Execute(
                publish,
                queueIndex: 0,
                queueNotifyOffset: 2,
                notifyOffsetMultiplier: NotifyOffsetMultiplier,
                barrier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        VirtioBlockReadQueueNotifyCompletionResult result =
            VirtioBlockReadQueueNotifyCompletionPlanner.CompleteAfterNotify(
                publish,
                readiness,
                ReadOnlySpan<byte>.Empty,
                ReadOnlySpan<byte>.Empty,
                states,
                ref cursor);

        return publish.Succeeded
            && windows.Succeeded
            && !readiness.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyCompletionStatus.NotifyReadinessFailed
            && result.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.MemoryBarrierNotReady
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.Success
            && !result.CompletionAttempted
            && result.PreviousAvailableRingIndex == 5
            && result.CurrentAvailableRingIndex == 5
            && result.PreviousUsedRingIndex == 8
            && result.CurrentUsedRingIndex == 8
            && cursor.AvailableRingIndex == 5
            && cursor.UsedRingIndex == 8
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight;
    }

    private static bool TestReadNotifyCompletionRejectsNoCompletion()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 1,
                usedRingIndex: 6);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 2,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 512,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        EndianPrimitives.WriteUInt16LittleEndian(
            queueMemory.AsSpan((int)publish.CursorPublish.Publish.LifecyclePublish.QueuePlan.UsedRing.Offset + 2, sizeof(ushort)),
            publish.CursorPublish.Publish.PreviousUsedIndex);
        VirtioBlockReadQueueNotifyReadinessResult readiness =
            CreateSuccessfulReadNotifyReadiness(publish, queueNotifyOffset: 3);

        VirtioBlockReadQueueNotifyCompletionResult result =
            VirtioBlockReadQueueNotifyCompletionPlanner.CompleteAfterNotify(
                publish,
                readiness,
                queueMemory,
                status,
                states,
                ref cursor);

        return publish.Succeeded
            && readiness.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyCompletionStatus.CompletionFailed
            && result.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.CursorReclaimFailed
            && result.CursorReclaimStatus == VirtioBlockQueueCursorReclaimStatus.ReclaimPlanFailed
            && result.ReclaimPlanStatus == VirtioBlockDescriptorReclaimPlanStatus.CompletionFailed
            && result.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.CompletionFailed
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.NoCompletion
            && result.CompletionAttempted
            && result.PreviousAvailableRingIndex == 2
            && result.CurrentAvailableRingIndex == 2
            && result.PreviousUsedRingIndex == 6
            && result.CurrentUsedRingIndex == 6
            && cursor.AvailableRingIndex == 2
            && cursor.UsedRingIndex == 6
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight;
    }

    private static bool TestReadNotifyCompletionPropagatesOwnershipFailure()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        VirtioBlockReadQueueNotifyReadinessResult readiness =
            CreateSuccessfulReadNotifyReadiness(publish, queueNotifyOffset: 4);
        states[1] = VirtioBlockDescriptorOwnershipState.Free;
        WriteSuccessfulCompletion(publish, queueMemory, status);

        VirtioBlockReadQueueNotifyCompletionResult result =
            VirtioBlockReadQueueNotifyCompletionPlanner.CompleteAfterNotify(
                publish,
                readiness,
                queueMemory,
                status,
                states,
                ref cursor);

        return publish.Succeeded
            && readiness.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyCompletionStatus.CompletionFailed
            && result.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.CursorReclaimFailed
            && result.CursorReclaimStatus == VirtioBlockQueueCursorReclaimStatus.ReclaimPlanFailed
            && result.ReclaimPlanStatus == VirtioBlockDescriptorReclaimPlanStatus.OwnershipReclaimFailed
            && result.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.Success
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.DescriptorNotInFlight
            && result.CompletionAttempted
            && result.PreviousAvailableRingIndex == 5
            && result.CurrentAvailableRingIndex == 5
            && result.PreviousUsedRingIndex == 8
            && result.CurrentUsedRingIndex == 8
            && cursor.AvailableRingIndex == 5
            && cursor.UsedRingIndex == 8
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.Free
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight;
    }

    private static bool TestReadNotifyCompletionDefaultResultIsNotSuccess()
    {
        VirtioBlockReadQueueNotifyCompletionResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyCompletionStatus.Success
            && result.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.Success
            && result.NotificationMetadataStatus == VirtioBlockQueueNotificationMetadataStatus.Success
            && result.NotifyOffsetPlanStatus == VirtioBlockQueueNotifyOffsetPlanStatus.Success
            && result.NotifyExecutionStatus == VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.Success
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.Success
            && result.CursorReclaimStatus == VirtioBlockQueueCursorReclaimStatus.Success
            && result.ReclaimPlanStatus == VirtioBlockDescriptorReclaimPlanStatus.Success
            && result.CompletionStatus == VirtioBlockReadLifecycleCompletionStatus.Success
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.Success
            && !result.CompletionAttempted
            && result.PreviousAvailableRingIndex == 0
            && result.CurrentAvailableRingIndex == 0
            && result.PreviousUsedRingIndex == 0
            && result.CurrentUsedRingIndex == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockReadQueueNotifyCompletionResult>();
    }

    private static bool TestReadNotifyCompletionPreservesNoLiveDeviceBoundary()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        VirtioBlockReadQueueNotifyReadinessResult readiness =
            CreateSuccessfulReadNotifyReadiness(publish, queueNotifyOffset: 5);
        WriteSuccessfulCompletion(publish, queueMemory, status);

        VirtioBlockReadQueueNotifyCompletionResult result =
            VirtioBlockReadQueueNotifyCompletionPlanner.CompleteAfterNotify(
                publish,
                readiness,
                queueMemory,
                status,
                states,
                ref cursor);

        return result.Succeeded
            && VirtioBlockReadQueueNotifyCompletionPlanner.ConsumesReadNotifyReadiness
            && VirtioBlockReadQueueNotifyCompletionPlanner.RequiresSuccessfulReadNotifyReadiness
            && VirtioBlockReadQueueNotifyCompletionPlanner.DelegatesReadQueueCompletionPlanner
            && VirtioBlockReadQueueNotifyCompletionPlanner.RequiresCallerOwnedCursorState
            && VirtioBlockReadQueueNotifyCompletionPlanner.RequiresCallerOwnedQueueMemory
            && VirtioBlockReadQueueNotifyCompletionPlanner.RequiresCallerOwnedStatusBuffer
            && VirtioBlockReadQueueNotifyCompletionPlanner.RequiresCallerOwnedDescriptorStateTable
            && VirtioBlockReadQueueNotifyCompletionPlanner.ReadsQueueMemory
            && VirtioBlockReadQueueNotifyCompletionPlanner.ReadsStatusBuffer
            && !VirtioBlockReadQueueNotifyCompletionPlanner.WritesQueueMemory
            && !VirtioBlockReadQueueNotifyCompletionPlanner.WritesRequestHeader
            && VirtioBlockReadQueueNotifyCompletionPlanner.MutatesCallerOwnedCursorState
            && VirtioBlockReadQueueNotifyCompletionPlanner.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockReadQueueNotifyCompletionPlanner.PerformsMmio
            && !VirtioBlockReadQueueNotifyCompletionPlanner.MapsBars
            && !VirtioBlockReadQueueNotifyCompletionPlanner.ContainsVolatileAccess
            && !VirtioBlockReadQueueNotifyCompletionPlanner.AccessesPhysicalAddresses
            && !VirtioBlockReadQueueNotifyCompletionPlanner.ExecutesMemoryBarriers
            && !VirtioBlockReadQueueNotifyCompletionPlanner.ConfiguresLiveQueues
            && !VirtioBlockReadQueueNotifyCompletionPlanner.NotifiesLiveDevice
            && !VirtioBlockReadQueueNotifyCompletionPlanner.PollsDevice
            && !VirtioBlockReadQueueNotifyCompletionPlanner.HandlesInterrupts
            && !VirtioBlockReadQueueNotifyCompletionPlanner.PerformsDmaMapping
            && !VirtioBlockReadQueueNotifyCompletionPlanner.AllocatesDmaMemory
            && !VirtioBlockReadQueueNotifyCompletionPlanner.PerformsBlockIo
            && !VirtioBlockReadQueueNotifyCompletionPlanner.PerformsDiskRead
            && !VirtioBlockReadQueueNotifyCompletionPlanner.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockReadQueueNotifyCompletionResult>();
    }

    private static bool TestReadNotifyBlockProjectsCompletedRead()
    {
        VirtioBlockReadQueueNotifyCompletionResult completion =
            CreateCompletedReadNotifyCompletion(
                firstSector: 4,
                sectorCount: 2,
                destinationLength: 1024);

        VirtioBlockReadQueueNotifyBlockReadProjectionResult result =
            VirtioBlockReadQueueNotifyBlockReadProjector.Project(completion);

        return completion.Succeeded
            && result.IsInitialized
            && result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyBlockReadProjectionStatus.Success
            && result.NotifyCompletionStatus == VirtioBlockReadQueueNotifyCompletionStatus.Success
            && result.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.Success
            && result.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.Success
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.CursorReclaimStatus == VirtioBlockQueueCursorReclaimStatus.Success
            && result.ReclaimPlanStatus == VirtioBlockDescriptorReclaimPlanStatus.Success
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.Success
            && result.BlockRead.Succeeded
            && result.BlockRead.BlocksRead == 2
            && result.BlockRead.BytesRead == 1024
            && result.RequestedBlockCount == 2
            && result.RequestedByteCount == 1024
            && result.CompletedByteCount == 1024;
    }

    private static bool TestReadNotifyBlockMapsReadinessFailure()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out _,
                out _);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockQueueNotificationBarrierReadiness barrier =
            new VirtioBlockQueueNotificationBarrierReadiness(
                descriptorWritesVisible: true,
                availableRingWritesVisible: false,
                orderedBeforeNotify: true,
                barrierSequence: 61);
        VirtioBlockReadQueueNotifyReadinessResult readiness =
            VirtioBlockReadQueueNotifyReadinessExecutor.Execute(
                publish,
                queueIndex: 0,
                queueNotifyOffset: 2,
                notifyOffsetMultiplier: NotifyOffsetMultiplier,
                barrier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);
        VirtioBlockReadQueueNotifyCompletionResult completion =
            VirtioBlockReadQueueNotifyCompletionPlanner.CompleteAfterNotify(
                publish,
                readiness,
                ReadOnlySpan<byte>.Empty,
                ReadOnlySpan<byte>.Empty,
                states,
                ref cursor);

        VirtioBlockReadQueueNotifyBlockReadProjectionResult result =
            VirtioBlockReadQueueNotifyBlockReadProjector.Project(completion);

        return !readiness.Succeeded
            && !completion.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyBlockReadProjectionStatus.NotifyReadinessFailed
            && result.NotifyCompletionStatus == VirtioBlockReadQueueNotifyCompletionStatus.NotifyReadinessFailed
            && result.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.MemoryBarrierNotReady
            && result.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.Success
            && result.BlockRead.Status == BlockDeviceStatus.IoError
            && result.BlockRead.BlocksRead == 0
            && result.BlockRead.BytesRead == 0
            && result.RequestedBlockCount == 0
            && result.RequestedByteCount == 0
            && result.CompletedByteCount == 0;
    }

    private static bool TestReadNotifyBlockMapsCompletionFailure()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 1,
                usedRingIndex: 6);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 2,
                sectorCount: 1,
                states,
                ref cursor,
                destinationLength: 512,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        EndianPrimitives.WriteUInt16LittleEndian(
            queueMemory.AsSpan((int)publish.CursorPublish.Publish.LifecyclePublish.QueuePlan.UsedRing.Offset + 2, sizeof(ushort)),
            publish.CursorPublish.Publish.PreviousUsedIndex);
        VirtioBlockReadQueueNotifyReadinessResult readiness =
            CreateSuccessfulReadNotifyReadiness(publish, queueNotifyOffset: 3);
        VirtioBlockReadQueueNotifyCompletionResult completion =
            VirtioBlockReadQueueNotifyCompletionPlanner.CompleteAfterNotify(
                publish,
                readiness,
                queueMemory,
                status,
                states,
                ref cursor);

        VirtioBlockReadQueueNotifyBlockReadProjectionResult result =
            VirtioBlockReadQueueNotifyBlockReadProjector.Project(completion);

        return readiness.Succeeded
            && !completion.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyBlockReadProjectionStatus.CompletionFailed
            && result.NotifyCompletionStatus == VirtioBlockReadQueueNotifyCompletionStatus.CompletionFailed
            && result.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.Success
            && result.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.CursorReclaimFailed
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.NoCompletion
            && result.BlockRead.Status == BlockDeviceStatus.IoError
            && result.RequestedBlockCount == 1
            && result.RequestedByteCount == 512
            && result.CompletedByteCount == 0;
    }

    private static bool TestReadNotifyBlockDefaultResultIsNotSuccess()
    {
        VirtioBlockReadQueueNotifyBlockReadProjectionResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueueNotifyBlockReadProjectionStatus.Success
            && result.NotifyCompletionStatus == VirtioBlockReadQueueNotifyCompletionStatus.Success
            && result.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.Success
            && result.ReadQueueCompletionStatus == VirtioBlockReadQueueCompletionStatus.Success
            && result.ReadQueuePublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.CursorReclaimStatus == VirtioBlockQueueCursorReclaimStatus.Success
            && result.ReclaimPlanStatus == VirtioBlockDescriptorReclaimPlanStatus.Success
            && result.CompletionReaderStatus == VirtioBlockUsedRingCompletionReaderStatus.Success
            && result.OwnershipStatus == VirtioBlockDescriptorOwnershipStatus.Success
            && !result.BlockRead.Succeeded
            && result.BlockRead.Status == BlockDeviceStatus.Success
            && result.RequestedBlockCount == 0
            && result.RequestedByteCount == 0
            && result.CompletedByteCount == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockReadQueueNotifyBlockReadProjectionResult>();
    }

    private static bool TestReadNotifyBlockPreservesNoLiveDeviceBoundary()
    {
        VirtioBlockReadQueueNotifyCompletionResult completion =
            CreateCompletedReadNotifyCompletion(
                firstSector: 4,
                sectorCount: 2,
                destinationLength: 1024);

        VirtioBlockReadQueueNotifyBlockReadProjectionResult result =
            VirtioBlockReadQueueNotifyBlockReadProjector.Project(completion);

        return result.Succeeded
            && VirtioBlockReadQueueNotifyBlockReadProjector.ConsumesReadNotifyCompletionResult
            && VirtioBlockReadQueueNotifyBlockReadProjector.DelegatesReadQueueBlockReadProjector
            && !VirtioBlockReadQueueNotifyBlockReadProjector.ReadsQueueMemory
            && !VirtioBlockReadQueueNotifyBlockReadProjector.ReadsStatusBuffer
            && !VirtioBlockReadQueueNotifyBlockReadProjector.WritesQueueMemory
            && !VirtioBlockReadQueueNotifyBlockReadProjector.WritesRequestHeader
            && !VirtioBlockReadQueueNotifyBlockReadProjector.MutatesCallerOwnedCursorState
            && !VirtioBlockReadQueueNotifyBlockReadProjector.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockReadQueueNotifyBlockReadProjector.PerformsMmio
            && !VirtioBlockReadQueueNotifyBlockReadProjector.MapsBars
            && !VirtioBlockReadQueueNotifyBlockReadProjector.ContainsVolatileAccess
            && !VirtioBlockReadQueueNotifyBlockReadProjector.AccessesPhysicalAddresses
            && !VirtioBlockReadQueueNotifyBlockReadProjector.ExecutesMemoryBarriers
            && !VirtioBlockReadQueueNotifyBlockReadProjector.ConfiguresLiveQueues
            && !VirtioBlockReadQueueNotifyBlockReadProjector.NotifiesLiveDevice
            && !VirtioBlockReadQueueNotifyBlockReadProjector.PollsDevice
            && !VirtioBlockReadQueueNotifyBlockReadProjector.HandlesInterrupts
            && !VirtioBlockReadQueueNotifyBlockReadProjector.PerformsDmaMapping
            && !VirtioBlockReadQueueNotifyBlockReadProjector.AllocatesDmaMemory
            && !VirtioBlockReadQueueNotifyBlockReadProjector.PerformsBlockIo
            && !VirtioBlockReadQueueNotifyBlockReadProjector.PerformsDiskRead
            && !VirtioBlockReadQueueNotifyBlockReadProjector.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockReadQueueNotifyBlockReadProjectionResult>();
    }

    private static bool TestReadPipelineProjectsCompletedRead()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        PrewriteCompletionSnapshot(
            queueMemory,
            status,
            previousUsedIndex: 8,
            currentUsedIndex: 9,
            descriptorHead: 0,
            usedByteCount: 1024);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockReadQueuePipelineResult result =
            VirtioBlockReadQueuePipelineComposer.Read(
                device,
                firstSector: 4,
                sectorCount: 2,
                QueueSize,
                states,
                ref cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 7,
                NotifyOffsetMultiplier,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(71),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return windows.Succeeded
            && result.IsInitialized
            && result.Succeeded
            && result.Status == VirtioBlockReadQueuePipelineStatus.Success
            && result.PublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.Success
            && result.NotifyCompletionStatus == VirtioBlockReadQueueNotifyCompletionStatus.Success
            && result.BlockProjectionStatus == VirtioBlockReadQueueNotifyBlockReadProjectionStatus.Success
            && result.NotifyReadinessAttempted
            && result.NotifyCompletionAttempted
            && result.BlockProjectionAttempted
            && result.BlockRead.Succeeded
            && result.BlockRead.BlocksRead == 2
            && result.BlockRead.BytesRead == 1024
            && result.BlockProjection.CompletedByteCount == 1024
            && cursor.AvailableRingIndex == 5
            && cursor.UsedRingIndex == 9
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free);
    }

    private static bool TestReadPipelineRejectsPublicationFailure()
    {
        BlockDeviceInfo device = CreateDevice(8);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 2,
                usedRingIndex: 3);
        CreateReadPipelineBuffers(
            destinationLength: 512,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockReadQueuePipelineResult result =
            VirtioBlockReadQueuePipelineComposer.Read(
                device,
                firstSector: 8,
                sectorCount: 1,
                QueueSize,
                states,
                ref cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 7,
                NotifyOffsetMultiplier,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(72),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return windows.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueuePipelineStatus.PublicationFailed
            && result.PublishStatus == VirtioBlockReadQueuePublishStatus.InvalidSectorRange
            && !result.NotifyReadinessAttempted
            && !result.NotifyCompletionAttempted
            && !result.BlockProjectionAttempted
            && result.BlockRead.Status == BlockDeviceStatus.IoError
            && cursor.AvailableRingIndex == 2
            && cursor.UsedRingIndex == 3
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free)
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadPipelineRejectsReadinessFailure()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        PrewriteCompletionSnapshot(
            queueMemory,
            status,
            previousUsedIndex: 8,
            currentUsedIndex: 9,
            descriptorHead: 0,
            usedByteCount: 1024);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockQueueNotificationBarrierReadiness barrier =
            new VirtioBlockQueueNotificationBarrierReadiness(
                descriptorWritesVisible: true,
                availableRingWritesVisible: false,
                orderedBeforeNotify: true,
                barrierSequence: 73);

        VirtioBlockReadQueuePipelineResult result =
            VirtioBlockReadQueuePipelineComposer.Read(
                device,
                firstSector: 4,
                sectorCount: 2,
                QueueSize,
                states,
                ref cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 2,
                NotifyOffsetMultiplier,
                barrier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return windows.Succeeded
            && !barrier.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueuePipelineStatus.NotifyReadinessFailed
            && result.PublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.MemoryBarrierNotReady
            && result.NotifyReadinessAttempted
            && !result.NotifyCompletionAttempted
            && !result.BlockProjectionAttempted
            && cursor.AvailableRingIndex == 5
            && cursor.UsedRingIndex == 8
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadPipelineMapsCompletionFailure()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 1,
                usedRingIndex: 6);
        CreateReadPipelineBuffers(
            destinationLength: 512,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        EndianPrimitives.WriteUInt16LittleEndian(
            queueMemory.AsSpan((int)VirtqueueSplitRingPlan.Create(QueueSize).Plan.UsedRing.Offset + 2, sizeof(ushort)),
            6);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockReadQueuePipelineResult result =
            VirtioBlockReadQueuePipelineComposer.Read(
                device,
                firstSector: 2,
                sectorCount: 1,
                QueueSize,
                states,
                ref cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 3,
                NotifyOffsetMultiplier,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(74),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return windows.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueuePipelineStatus.NotifyCompletionFailed
            && result.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.Success
            && result.NotifyCompletionStatus == VirtioBlockReadQueueNotifyCompletionStatus.CompletionFailed
            && result.BlockProjectionStatus == VirtioBlockReadQueueNotifyBlockReadProjectionStatus.CompletionFailed
            && result.BlockRead.Status == BlockDeviceStatus.IoError
            && result.BlockProjection.RequestedBlockCount == 1
            && result.BlockProjection.RequestedByteCount == 512
            && result.BlockProjection.CompletedByteCount == 0
            && cursor.AvailableRingIndex == 2
            && cursor.UsedRingIndex == 6
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight;
    }

    private static bool TestReadPipelineDefaultResultIsNotSuccess()
    {
        VirtioBlockReadQueuePipelineResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueuePipelineStatus.Success
            && result.PublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.Success
            && result.NotifyCompletionStatus == VirtioBlockReadQueueNotifyCompletionStatus.Success
            && result.BlockProjectionStatus == VirtioBlockReadQueueNotifyBlockReadProjectionStatus.Success
            && !result.NotifyReadinessAttempted
            && !result.NotifyCompletionAttempted
            && !result.BlockProjectionAttempted
            && !result.BlockRead.Succeeded
            && result.BlockRead.Status == BlockDeviceStatus.IoError
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadQueuePipelineResult>();
    }

    private static bool TestReadPipelinePreservesNoLiveDeviceBoundary()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        PrewriteCompletionSnapshot(
            queueMemory,
            status,
            previousUsedIndex: 8,
            currentUsedIndex: 9,
            descriptorHead: 0,
            usedByteCount: 1024);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockReadQueuePipelineResult result =
            VirtioBlockReadQueuePipelineComposer.Read(
                device,
                firstSector: 4,
                sectorCount: 2,
                QueueSize,
                states,
                ref cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 7,
                NotifyOffsetMultiplier,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(75),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return result.Succeeded
            && VirtioBlockReadQueuePipelineComposer.PublishesReadQueue
            && VirtioBlockReadQueuePipelineComposer.InvokesReadNotifyReadinessExecutor
            && VirtioBlockReadQueuePipelineComposer.InvokesReadNotifyCompletionPlanner
            && VirtioBlockReadQueuePipelineComposer.InvokesReadNotifyBlockReadProjector
            && VirtioBlockReadQueuePipelineComposer.RequiresCallerOwnedCursorState
            && VirtioBlockReadQueuePipelineComposer.RequiresCallerOwnedQueueMemory
            && VirtioBlockReadQueuePipelineComposer.RequiresCallerOwnedStatusBuffer
            && VirtioBlockReadQueuePipelineComposer.RequiresCallerOwnedDescriptorStateTable
            && VirtioBlockReadQueuePipelineComposer.RequiresRegisterWindowBinding
            && VirtioBlockReadQueuePipelineComposer.UsesCallerOwnedNotifyWindowBuffer
            && VirtioBlockReadQueuePipelineComposer.ReadsQueueMemory
            && VirtioBlockReadQueuePipelineComposer.ReadsStatusBuffer
            && VirtioBlockReadQueuePipelineComposer.WritesQueueMemory
            && VirtioBlockReadQueuePipelineComposer.WritesRequestHeader
            && VirtioBlockReadQueuePipelineComposer.MutatesCallerOwnedCursorState
            && VirtioBlockReadQueuePipelineComposer.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockReadQueuePipelineComposer.PerformsMmio
            && !VirtioBlockReadQueuePipelineComposer.MapsBars
            && !VirtioBlockReadQueuePipelineComposer.ContainsVolatileAccess
            && !VirtioBlockReadQueuePipelineComposer.AccessesPhysicalAddresses
            && !VirtioBlockReadQueuePipelineComposer.ExecutesMemoryBarriers
            && !VirtioBlockReadQueuePipelineComposer.ConfiguresLiveQueues
            && !VirtioBlockReadQueuePipelineComposer.NotifiesLiveDevice
            && !VirtioBlockReadQueuePipelineComposer.PollsDevice
            && !VirtioBlockReadQueuePipelineComposer.HandlesInterrupts
            && !VirtioBlockReadQueuePipelineComposer.PerformsDmaMapping
            && !VirtioBlockReadQueuePipelineComposer.AllocatesDmaMemory
            && !VirtioBlockReadQueuePipelineComposer.PerformsBlockIo
            && !VirtioBlockReadQueuePipelineComposer.PerformsDiskRead
            && !VirtioBlockReadQueuePipelineComposer.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadQueuePipelineResult>();
    }

    private static bool TestReadAdapterExecutesPipelineRead()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        PrewriteCompletionSnapshot(
            queueMemory,
            status,
            previousUsedIndex: 8,
            currentUsedIndex: 9,
            descriptorHead: 0,
            usedByteCount: 1024);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockReadQueuePipelineAdapterResult result =
            VirtioBlockReadQueuePipelineAdapter.Read(
                device,
                firstSector: 4,
                sectorCount: 2,
                QueueSize,
                states,
                ref cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 7,
                NotifyOffsetMultiplier,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(76),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        ulong barRelativeOffset = NotifyApertureBarOffset + (7UL * NotifyOffsetMultiplier);
        return windows.Succeeded
            && result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockReadQueuePipelineAdapterStatus.Success
            && result.PipelineAttempted
            && result.PipelineStatus == VirtioBlockReadQueuePipelineStatus.Success
            && result.Pipeline.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.Success
            && result.Pipeline.NotifyCompletionStatus == VirtioBlockReadQueueNotifyCompletionStatus.Success
            && result.Pipeline.BlockProjectionStatus == VirtioBlockReadQueueNotifyBlockReadProjectionStatus.Success
            && result.BlockRead.Succeeded
            && result.BlockRead.BlocksRead == 2
            && result.BlockRead.BytesRead == 1024
            && cursor.AvailableRingIndex == 5
            && cursor.UsedRingIndex == 9
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free)
            && ReadUInt16AtBarOffset(notifyBytes, barRelativeOffset) == 0;
    }

    private static bool TestReadAdapterRejectsShortRequestBuffer()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out _,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength - 1];
        Array.Fill(requestHeader, (byte)0x11);
        Array.Fill(destination, (byte)0x22);
        Array.Fill(status, (byte)0x33);
        Array.Fill(queueMemory, (byte)0x44);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockReadQueuePipelineAdapterResult result =
            VirtioBlockReadQueuePipelineAdapter.Read(
                device,
                firstSector: 4,
                sectorCount: 2,
                QueueSize,
                states,
                ref cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 7,
                NotifyOffsetMultiplier,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(77),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return windows.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueuePipelineAdapterStatus.RequestHeaderTooSmall
            && !result.PipelineAttempted
            && result.RequiredByteCount == VirtioBlockRequestPlan.RequestHeaderLength
            && result.AvailableByteCount == (ulong)requestHeader.Length
            && cursor.AvailableRingIndex == 4
            && cursor.UsedRingIndex == 8
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free)
            && AllBytesEqual(requestHeader, 0x11)
            && AllBytesEqual(destination, 0x22)
            && AllBytesEqual(status, 0x33)
            && AllBytesEqual(queueMemory, 0x44)
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadAdapterRejectsShortNotifyWindow()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        Array.Fill(requestHeader, (byte)0x11);
        Array.Fill(destination, (byte)0x22);
        Array.Fill(status, (byte)0x33);
        Array.Fill(queueMemory, (byte)0x44);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        ulong requiredNotifyBytes = NotifyApertureBarOffset + (7UL * NotifyOffsetMultiplier) + sizeof(ushort);
        byte[] notifyBytes = CreateNotifyWindowBytes(checked((int)requiredNotifyBytes - 1));

        VirtioBlockReadQueuePipelineAdapterResult result =
            VirtioBlockReadQueuePipelineAdapter.Read(
                device,
                firstSector: 4,
                sectorCount: 2,
                QueueSize,
                states,
                ref cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 7,
                NotifyOffsetMultiplier,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(78),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return windows.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueuePipelineAdapterStatus.NotifyWindowTooSmall
            && !result.PipelineAttempted
            && result.RequiredByteCount == requiredNotifyBytes
            && result.AvailableByteCount == (ulong)notifyBytes.Length
            && result.NotifyWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && cursor.AvailableRingIndex == 4
            && cursor.UsedRingIndex == 8
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free)
            && AllBytesEqual(requestHeader, 0x11)
            && AllBytesEqual(destination, 0x22)
            && AllBytesEqual(status, 0x33)
            && AllBytesEqual(queueMemory, 0x44)
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadAdapterPropagatesPipelineFailure()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockQueueNotificationBarrierReadiness barrier =
            new VirtioBlockQueueNotificationBarrierReadiness(
                descriptorWritesVisible: true,
                availableRingWritesVisible: false,
                orderedBeforeNotify: true,
                barrierSequence: 79);

        VirtioBlockReadQueuePipelineAdapterResult result =
            VirtioBlockReadQueuePipelineAdapter.Read(
                device,
                firstSector: 4,
                sectorCount: 2,
                QueueSize,
                states,
                ref cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 2,
                NotifyOffsetMultiplier,
                barrier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return windows.Succeeded
            && !barrier.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueuePipelineAdapterStatus.PipelineFailed
            && result.PipelineAttempted
            && result.PipelineStatus == VirtioBlockReadQueuePipelineStatus.NotifyReadinessFailed
            && result.Pipeline.PublishStatus == VirtioBlockReadQueuePublishStatus.Success
            && result.Pipeline.NotifyReadinessStatus == VirtioBlockReadQueueNotifyReadinessStatus.MemoryBarrierNotReady
            && result.BlockRead.Status == BlockDeviceStatus.IoError
            && cursor.AvailableRingIndex == 5
            && cursor.UsedRingIndex == 8
            && states[0] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[1] == VirtioBlockDescriptorOwnershipState.InFlight
            && states[2] == VirtioBlockDescriptorOwnershipState.InFlight
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadAdapterDefaultResultIsNotSuccess()
    {
        VirtioBlockReadQueuePipelineAdapterResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadQueuePipelineAdapterStatus.Success
            && result.PipelineStatus == VirtioBlockReadQueuePipelineStatus.Success
            && !result.PipelineAttempted
            && !result.Pipeline.Succeeded
            && result.BlockRead.Status == BlockDeviceStatus.IoError
            && result.RequiredByteCount == 0
            && result.AvailableByteCount == 0
            && result.RequiredElementCount == 0
            && result.AvailableElementCount == 0
            && result.QueuePlanStatus == VirtqueueSplitRingPlanStatus.Success
            && result.NotifyWindowPlanStatus == VirtioPciBarRegisterWindowPlanStatus.Success
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadQueuePipelineAdapterResult>();
    }

    private static bool TestReadAdapterPreservesNoLiveDeviceBoundary()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        PrewriteCompletionSnapshot(
            queueMemory,
            status,
            previousUsedIndex: 8,
            currentUsedIndex: 9,
            descriptorHead: 0,
            usedByteCount: 1024);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();

        VirtioBlockReadQueuePipelineAdapterResult result =
            VirtioBlockReadQueuePipelineAdapter.Read(
                device,
                firstSector: 4,
                sectorCount: 2,
                QueueSize,
                states,
                ref cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 7,
                NotifyOffsetMultiplier,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(80),
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        return windows.Succeeded
            && result.Succeeded
            && result.IsInitialized
            && VirtioBlockReadQueuePipelineAdapter.ValidatesCallerOwnedBuffersBeforePipeline
            && VirtioBlockReadQueuePipelineAdapter.DelegatesReadQueuePipelineComposer
            && !VirtioBlockReadQueuePipelineAdapter.PreflightFailuresMutateCallerOwnedState
            && VirtioBlockReadQueuePipelineAdapter.RequiresCallerOwnedCursorState
            && VirtioBlockReadQueuePipelineAdapter.RequiresCallerOwnedRequestHeader
            && VirtioBlockReadQueuePipelineAdapter.RequiresCallerOwnedDestinationBuffer
            && VirtioBlockReadQueuePipelineAdapter.RequiresCallerOwnedStatusBuffer
            && VirtioBlockReadQueuePipelineAdapter.RequiresCallerOwnedQueueMemory
            && VirtioBlockReadQueuePipelineAdapter.RequiresCallerOwnedDescriptorStateTable
            && VirtioBlockReadQueuePipelineAdapter.RequiresRegisterWindowBinding
            && VirtioBlockReadQueuePipelineAdapter.UsesCallerOwnedNotifyWindowBuffer
            && VirtioBlockReadQueuePipelineAdapter.PrevalidatesNotifyWindowBeforePublication
            && VirtioBlockReadQueuePipelineAdapter.ReadsQueueMemory
            && VirtioBlockReadQueuePipelineAdapter.ReadsStatusBuffer
            && VirtioBlockReadQueuePipelineAdapter.WritesQueueMemory
            && VirtioBlockReadQueuePipelineAdapter.WritesRequestHeader
            && VirtioBlockReadQueuePipelineAdapter.MutatesCallerOwnedCursorState
            && VirtioBlockReadQueuePipelineAdapter.MutatesCallerOwnedDescriptorStateTable
            && !VirtioBlockReadQueuePipelineAdapter.PerformsMmio
            && !VirtioBlockReadQueuePipelineAdapter.MapsBars
            && !VirtioBlockReadQueuePipelineAdapter.ContainsVolatileAccess
            && !VirtioBlockReadQueuePipelineAdapter.AccessesPhysicalAddresses
            && !VirtioBlockReadQueuePipelineAdapter.ExecutesMemoryBarriers
            && !VirtioBlockReadQueuePipelineAdapter.ConfiguresLiveQueues
            && !VirtioBlockReadQueuePipelineAdapter.NotifiesLiveDevice
            && !VirtioBlockReadQueuePipelineAdapter.PollsDevice
            && !VirtioBlockReadQueuePipelineAdapter.HandlesInterrupts
            && !VirtioBlockReadQueuePipelineAdapter.PerformsDmaMapping
            && !VirtioBlockReadQueuePipelineAdapter.AllocatesDmaMemory
            && !VirtioBlockReadQueuePipelineAdapter.PerformsBlockIo
            && !VirtioBlockReadQueuePipelineAdapter.PerformsDiskRead
            && !VirtioBlockReadQueuePipelineAdapter.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadQueuePipelineAdapterResult>();
    }

    private static bool TestReadSessionDelegatesAdapterRead()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        PrewriteCompletionSnapshot(
            queueMemory,
            status,
            previousUsedIndex: 8,
            currentUsedIndex: 9,
            descriptorHead: 0,
            usedByteCount: 1024);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockReadOnlyReadSession session =
            new VirtioBlockReadOnlyReadSession(
                device,
                QueueSize,
                states,
                cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 7,
                NotifyOffsetMultiplier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        VirtioBlockReadOnlyReadSessionResult result =
            session.Read(
                firstSector: 4,
                sectorCount: 2,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(81));

        return windows.Succeeded
            && result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockReadOnlyReadSessionStatus.Success
            && result.Adapter.Succeeded
            && result.Adapter.PipelineAttempted
            && result.Adapter.PipelineStatus == VirtioBlockReadQueuePipelineStatus.Success
            && result.BlockRead.Succeeded
            && result.BlockRead.BlocksRead == 2
            && result.BlockRead.BytesRead == 1024
            && result.CursorBefore.AvailableRingIndex == 4
            && result.CursorBefore.UsedRingIndex == 8
            && result.CursorAfter.AvailableRingIndex == 5
            && result.CursorAfter.UsedRingIndex == 9
            && session.Cursor.AvailableRingIndex == 5
            && session.Cursor.UsedRingIndex == 9
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free);
    }

    private static bool TestReadSessionPreservesCursorState()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        PrewriteCompletionSnapshot(
            queueMemory,
            status,
            previousUsedIndex: 8,
            currentUsedIndex: 9,
            descriptorHead: 0,
            usedByteCount: 512);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockReadOnlyReadSession session =
            new VirtioBlockReadOnlyReadSession(
                device,
                QueueSize,
                states,
                cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 7,
                NotifyOffsetMultiplier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        VirtioBlockReadOnlyReadSessionResult first =
            session.Read(
                firstSector: 4,
                sectorCount: 1,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(82));
        PrewriteCompletionSnapshot(
            queueMemory,
            status,
            previousUsedIndex: 9,
            currentUsedIndex: 10,
            descriptorHead: 0,
            usedByteCount: 512);
        VirtioBlockReadOnlyReadSessionResult second =
            session.Read(
                firstSector: 5,
                sectorCount: 1,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(83));

        return windows.Succeeded
            && first.Succeeded
            && first.IsInitialized
            && second.Succeeded
            && second.IsInitialized
            && first.CursorBefore.AvailableRingIndex == 4
            && first.CursorBefore.UsedRingIndex == 8
            && first.CursorAfter.AvailableRingIndex == 5
            && first.CursorAfter.UsedRingIndex == 9
            && second.CursorBefore.AvailableRingIndex == 5
            && second.CursorBefore.UsedRingIndex == 9
            && second.CursorAfter.AvailableRingIndex == 6
            && second.CursorAfter.UsedRingIndex == 10
            && session.Cursor.AvailableRingIndex == 6
            && session.Cursor.UsedRingIndex == 10
            && first.BlockRead.BlocksRead == 1
            && first.BlockRead.BytesRead == 512
            && second.BlockRead.BlocksRead == 1
            && second.BlockRead.BytesRead == 512
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free);
    }

    private static bool TestReadSessionRejectsInvalidSetup()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out _,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength - 1];
        Array.Fill(requestHeader, (byte)0x11);
        Array.Fill(destination, (byte)0x22);
        Array.Fill(status, (byte)0x33);
        Array.Fill(queueMemory, (byte)0x44);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockReadOnlyReadSession session =
            new VirtioBlockReadOnlyReadSession(
                device,
                QueueSize,
                states,
                cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: 7,
                NotifyOffsetMultiplier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);

        VirtioBlockReadOnlyReadSessionResult result =
            session.Read(
                firstSector: 4,
                sectorCount: 2,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(84));

        return windows.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadOnlyReadSessionStatus.AdapterFailed
            && result.Adapter.Status == VirtioBlockReadQueuePipelineAdapterStatus.RequestHeaderTooSmall
            && !result.Adapter.PipelineAttempted
            && result.CursorBefore.AvailableRingIndex == 4
            && result.CursorBefore.UsedRingIndex == 8
            && result.CursorAfter.AvailableRingIndex == 4
            && result.CursorAfter.UsedRingIndex == 8
            && session.Cursor.AvailableRingIndex == 4
            && session.Cursor.UsedRingIndex == 8
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free)
            && AllBytesEqual(requestHeader, 0x11)
            && AllBytesEqual(destination, 0x22)
            && AllBytesEqual(status, 0x33)
            && AllBytesEqual(queueMemory, 0x44)
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadSessionDefaultResultIsNotSuccess()
    {
        VirtioBlockReadOnlyReadSessionResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadOnlyReadSessionStatus.Success
            && !result.Adapter.IsInitialized
            && !result.Adapter.Succeeded
            && result.Adapter.Status == VirtioBlockReadQueuePipelineAdapterStatus.Success
            && result.BlockRead.Status == BlockDeviceStatus.IoError
            && result.CursorBefore.AvailableRingIndex == 0
            && result.CursorBefore.UsedRingIndex == 0
            && result.CursorAfter.AvailableRingIndex == 0
            && result.CursorAfter.UsedRingIndex == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadOnlyReadSessionResult>();
    }

    private static bool TestReadSessionPreservesNoLiveDeviceBoundary()
    {
        return VirtioBlockReadOnlyReadSession.OwnsNoAllocations
            && VirtioBlockReadOnlyReadSession.DelegatesReadQueuePipelineAdapter
            && VirtioBlockReadOnlyReadSession.CarriesCallerOwnedCursorState
            && VirtioBlockReadOnlyReadSession.CarriesCallerOwnedDescriptorStateTable
            && VirtioBlockReadOnlyReadSession.CarriesCallerOwnedRequestHeader
            && VirtioBlockReadOnlyReadSession.CarriesCallerOwnedDestinationBuffer
            && VirtioBlockReadOnlyReadSession.CarriesCallerOwnedStatusBuffer
            && VirtioBlockReadOnlyReadSession.CarriesCallerOwnedQueueMemory
            && VirtioBlockReadOnlyReadSession.CarriesCallerOwnedNotifyWindowBuffer
            && VirtioBlockReadOnlyReadSession.RequiresRegisterWindowBinding
            && !VirtioBlockReadOnlyReadSession.PerformsMmio
            && !VirtioBlockReadOnlyReadSession.MapsBars
            && !VirtioBlockReadOnlyReadSession.ContainsVolatileAccess
            && !VirtioBlockReadOnlyReadSession.AccessesPhysicalAddresses
            && !VirtioBlockReadOnlyReadSession.ExecutesMemoryBarriers
            && !VirtioBlockReadOnlyReadSession.ConfiguresLiveQueues
            && !VirtioBlockReadOnlyReadSession.NotifiesLiveDevice
            && !VirtioBlockReadOnlyReadSession.PollsDevice
            && !VirtioBlockReadOnlyReadSession.HandlesInterrupts
            && !VirtioBlockReadOnlyReadSession.PerformsDmaMapping
            && !VirtioBlockReadOnlyReadSession.AllocatesDmaMemory
            && !VirtioBlockReadOnlyReadSession.PerformsBlockIo
            && !VirtioBlockReadOnlyReadSession.PerformsDiskRead
            && !VirtioBlockReadOnlyReadSession.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioBlockReadOnlyReadSessionResult>();
    }

    private static bool TestReadSessionSetupGateComposesSetupAndRead()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        PrewriteCompletionSnapshot(
            queueMemory,
            status,
            previousUsedIndex: 8,
            currentUsedIndex: 9,
            descriptorHead: 0,
            usedByteCount: 1024);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockReadOnlyReadSession session =
            new VirtioBlockReadOnlyReadSession(
                device,
                QueueSize,
                states,
                cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: QueueNotifyOffset,
                NotifyOffsetMultiplier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult setup =
            ExecuteQueueSetupBarrier(
                validSetupPlan: true,
                barrierReady: true,
                barrierSequence: 101);

        VirtioBlockReadOnlyReadSessionSetupGateResult result =
            VirtioBlockReadOnlyReadSessionSetupGate.ReadAfterQueueSetup(
                ref session,
                setup,
                firstSector: 4,
                sectorCount: 2,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(102));

        ulong barRelativeOffset = NotifyApertureBarOffset + ((ulong)QueueNotifyOffset * NotifyOffsetMultiplier);
        return windows.Succeeded
            && setup.Succeeded
            && result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioBlockReadOnlyReadSessionSetupGateStatus.Success
            && result.QueueSetupStatus == VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.Success
            && result.QueueSetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && result.QueueSetupBarrierSucceeded
            && result.QueueSetupBarrierSequence == 101
            && result.QueueEnableAttempted
            && result.ReadSessionAttempted
            && result.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.Success
            && result.ReadSessionPipelineStatus == VirtioBlockReadQueuePipelineStatus.Success
            && result.BlockRead.Succeeded
            && result.BlockRead.BlocksRead == 2
            && result.BlockRead.BytesRead == 1024
            && result.CursorBefore.AvailableRingIndex == 4
            && result.CursorBefore.UsedRingIndex == 8
            && result.CursorAfter.AvailableRingIndex == 5
            && result.CursorAfter.UsedRingIndex == 9
            && session.Cursor.AvailableRingIndex == 5
            && session.Cursor.UsedRingIndex == 9
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free)
            && ReadUInt16AtBarOffset(notifyBytes, barRelativeOffset) == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockReadOnlyReadSessionSetupGateResult>();
    }

    private static bool TestReadSessionSetupGateRejectsFailedSetup()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        Array.Fill(requestHeader, (byte)0x11);
        Array.Fill(destination, (byte)0x22);
        Array.Fill(status, (byte)0x33);
        Array.Fill(queueMemory, (byte)0x44);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockReadOnlyReadSession session =
            new VirtioBlockReadOnlyReadSession(
                device,
                QueueSize,
                states,
                cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: QueueNotifyOffset,
                NotifyOffsetMultiplier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult setup =
            ExecuteQueueSetupBarrier(
                validSetupPlan: false,
                barrierReady: true,
                barrierSequence: 103);

        VirtioBlockReadOnlyReadSessionSetupGateResult result =
            VirtioBlockReadOnlyReadSessionSetupGate.ReadAfterQueueSetup(
                ref session,
                setup,
                firstSector: 4,
                sectorCount: 2,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(104));

        return windows.Succeeded
            && setup.IsInitialized
            && !setup.Succeeded
            && setup.Status == VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.SetupPlanFailed
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadOnlyReadSessionSetupGateStatus.QueueSetupExecutionFailed
            && result.QueueSetupStatus == VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.SetupPlanFailed
            && result.QueueSetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.DescriptorNotReady
            && result.QueueSetupBarrierSucceeded
            && !result.QueueEnableAttempted
            && !result.ReadSessionAttempted
            && result.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.Success
            && result.ReadSessionPipelineStatus == VirtioBlockReadQueuePipelineStatus.Success
            && result.CursorBefore.AvailableRingIndex == 4
            && result.CursorAfter.AvailableRingIndex == 4
            && session.Cursor.AvailableRingIndex == 4
            && session.Cursor.UsedRingIndex == 8
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free)
            && AllBytesEqual(requestHeader, 0x11)
            && AllBytesEqual(destination, 0x22)
            && AllBytesEqual(status, 0x33)
            && AllBytesEqual(queueMemory, 0x44)
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadSessionSetupGateRejectsMissingBarrier()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out byte[] requestHeader,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        Array.Fill(requestHeader, (byte)0x11);
        Array.Fill(destination, (byte)0x22);
        Array.Fill(status, (byte)0x33);
        Array.Fill(queueMemory, (byte)0x44);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockReadOnlyReadSession session =
            new VirtioBlockReadOnlyReadSession(
                device,
                QueueSize,
                states,
                cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: QueueNotifyOffset,
                NotifyOffsetMultiplier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult setup =
            ExecuteQueueSetupBarrier(
                validSetupPlan: true,
                barrierReady: false,
                barrierSequence: 105);

        VirtioBlockReadOnlyReadSessionSetupGateResult result =
            VirtioBlockReadOnlyReadSessionSetupGate.ReadAfterQueueSetup(
                ref session,
                setup,
                firstSector: 4,
                sectorCount: 2,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(106));

        return windows.Succeeded
            && setup.IsInitialized
            && !setup.Succeeded
            && setup.Status == VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady
            && !setup.BarrierSucceeded
            && !setup.QueueEnableAttempted
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadOnlyReadSessionSetupGateStatus.QueueSetupBarrierNotReady
            && result.QueueSetupStatus ==
                VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady
            && !result.QueueSetupBarrierSucceeded
            && !result.QueueEnableAttempted
            && result.QueueSetupBarrierSequence == 105
            && !result.ReadSessionAttempted
            && result.CursorBefore.AvailableRingIndex == 4
            && result.CursorAfter.AvailableRingIndex == 4
            && session.Cursor.AvailableRingIndex == 4
            && session.Cursor.UsedRingIndex == 8
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free)
            && AllBytesEqual(requestHeader, 0x11)
            && AllBytesEqual(destination, 0x22)
            && AllBytesEqual(status, 0x33)
            && AllBytesEqual(queueMemory, 0x44)
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadSessionSetupGatePropagatesReadFailure()
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        CreateReadPipelineBuffers(
            destinationLength: 1024,
            out _,
            out byte[] destination,
            out byte[] status,
            out byte[] queueMemory);
        byte[] requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength - 1];
        Array.Fill(requestHeader, (byte)0x11);
        Array.Fill(destination, (byte)0x22);
        Array.Fill(status, (byte)0x33);
        Array.Fill(queueMemory, (byte)0x44);
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        byte[] notifyBytes = CreateNotifyWindowBytes();
        VirtioBlockReadOnlyReadSession session =
            new VirtioBlockReadOnlyReadSession(
                device,
                QueueSize,
                states,
                cursor,
                HeaderPhysicalAddress,
                DataPhysicalAddress,
                StatusPhysicalAddress,
                requestHeader,
                destination,
                status,
                queueMemory,
                queueIndex: 0,
                queueNotifyOffset: QueueNotifyOffset,
                NotifyOffsetMultiplier,
                windows.Bindings.NotifyConfiguration,
                notifyBytes);
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult setup =
            ExecuteQueueSetupBarrier(
                validSetupPlan: true,
                barrierReady: true,
                barrierSequence: 107);

        VirtioBlockReadOnlyReadSessionSetupGateResult result =
            VirtioBlockReadOnlyReadSessionSetupGate.ReadAfterQueueSetup(
                ref session,
                setup,
                firstSector: 4,
                sectorCount: 2,
                VirtioBlockQueueNotificationBarrierReadiness.Ready(108));

        return windows.Succeeded
            && setup.Succeeded
            && result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadOnlyReadSessionSetupGateStatus.ReadSessionFailed
            && result.QueueSetupStatus == VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.Success
            && result.QueueSetupBarrierSucceeded
            && result.QueueEnableAttempted
            && result.ReadSessionAttempted
            && result.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.AdapterFailed
            && result.ReadSession.Adapter.Status == VirtioBlockReadQueuePipelineAdapterStatus.RequestHeaderTooSmall
            && !result.ReadSession.Adapter.PipelineAttempted
            && result.CursorBefore.AvailableRingIndex == 4
            && result.CursorAfter.AvailableRingIndex == 4
            && session.Cursor.AvailableRingIndex == 4
            && session.Cursor.UsedRingIndex == 8
            && AllStatesAre(states, VirtioBlockDescriptorOwnershipState.Free)
            && AllBytesEqual(requestHeader, 0x11)
            && AllBytesEqual(destination, 0x22)
            && AllBytesEqual(status, 0x33)
            && AllBytesEqual(queueMemory, 0x44)
            && AllBytesEqual(notifyBytes, 0xCC);
    }

    private static bool TestReadSessionSetupGateDefaultResultIsNotSuccess()
    {
        VirtioBlockReadOnlyReadSessionSetupGateResult result = default;

        return !result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioBlockReadOnlyReadSessionSetupGateStatus.Success
            && result.QueueSetupStatus == VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.Success
            && result.QueueSetupPlanStatus == VirtioBlockQueueMmioSetupPlanStatus.Success
            && !result.QueueSetupBarrierSucceeded
            && !result.QueueEnableAttempted
            && result.QueueSetupBarrierSequence == 0
            && !result.ReadSessionAttempted
            && result.ReadSessionStatus == VirtioBlockReadOnlyReadSessionStatus.Success
            && result.ReadSessionPipelineStatus == VirtioBlockReadQueuePipelineStatus.Success
            && result.BlockRead.Status == BlockDeviceStatus.IoError
            && VirtioBlockReadOnlyReadSessionSetupGate.RequiresSuccessfulQueueSetupBarrierExecution
            && VirtioBlockReadOnlyReadSessionSetupGate.RequiresQueueSetupBarrierReadiness
            && VirtioBlockReadOnlyReadSessionSetupGate.RequiresQueueEnableAttempted
            && VirtioBlockReadOnlyReadSessionSetupGate.DelegatesReadSessionAfterSetup
            && VirtioBlockReadOnlyReadSessionSetupGate.PreservesCursorOnSetupFailure
            && !VirtioBlockReadOnlyReadSessionSetupGate.PerformsMmio
            && !VirtioBlockReadOnlyReadSessionSetupGate.MapsBars
            && !VirtioBlockReadOnlyReadSessionSetupGate.ContainsVolatileAccess
            && !VirtioBlockReadOnlyReadSessionSetupGate.AccessesPhysicalAddresses
            && !VirtioBlockReadOnlyReadSessionSetupGate.ExecutesMemoryBarriers
            && !VirtioBlockReadOnlyReadSessionSetupGate.ConfiguresLiveQueues
            && !VirtioBlockReadOnlyReadSessionSetupGate.NotifiesLiveDevice
            && !VirtioBlockReadOnlyReadSessionSetupGate.PollsDevice
            && !VirtioBlockReadOnlyReadSessionSetupGate.HandlesInterrupts
            && !VirtioBlockReadOnlyReadSessionSetupGate.PerformsDmaMapping
            && !VirtioBlockReadOnlyReadSessionSetupGate.AllocatesDmaMemory
            && !VirtioBlockReadOnlyReadSessionSetupGate.PerformsBlockIo
            && !VirtioBlockReadOnlyReadSessionSetupGate.PerformsDiskRead
            && !VirtioBlockReadOnlyReadSessionSetupGate.WaitsForUsedRingCompletion
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioBlockReadOnlyReadSessionSetupGateResult>();
    }

    private static BlockDeviceInfo CreateDevice(ulong blockCount)
    {
        return CreateDevice(VirtioBlockRequestPlan.SectorSizeBytes, blockCount);
    }

    private static BlockDeviceInfo CreateDevice(uint blockSize, ulong blockCount)
    {
        BlockDeviceInfoResult result = BlockDeviceInfo.Create(blockSize, blockCount);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Invalid test block device: " + result.Status);
        }

        return result.Info;
    }

    private static VirtioBlockReadQueuePublishResult PublishReadQueue(
        BlockDeviceInfo device,
        ulong firstSector,
        uint sectorCount,
        Span<VirtioBlockDescriptorOwnershipState> states,
        ref VirtioBlockQueueCursorState cursor,
        int destinationLength,
        out byte[] requestHeader,
        out byte[] status,
        out byte[] queueMemory)
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        byte[] destination = new byte[destinationLength];
        status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        queueMemory = new byte[(int)queue.Plan.EndOffset];

        return VirtioBlockReadQueuePlanner.PublishRead(
            device,
            firstSector,
            sectorCount,
            QueueSize,
            states,
            ref cursor,
            requestHeaderPhysicalAddress: HeaderPhysicalAddress,
            dataBufferPhysicalAddress: DataPhysicalAddress,
            statusBytePhysicalAddress: StatusPhysicalAddress,
            requestHeader,
            destination,
            status,
            queueMemory);
    }

    private static void CreateReadPipelineBuffers(
        int destinationLength,
        out byte[] requestHeader,
        out byte[] destination,
        out byte[] status,
        out byte[] queueMemory)
    {
        VirtqueueSplitRingPlanResult queue = VirtqueueSplitRingPlan.Create(QueueSize);
        requestHeader = new byte[(int)VirtioBlockRequestPlan.RequestHeaderLength];
        destination = new byte[destinationLength];
        status = new byte[(int)VirtioBlockRequestPlan.StatusLength];
        queueMemory = new byte[(int)queue.Plan.EndOffset];
    }

    private static void PrewriteCompletionSnapshot(
        byte[] queueMemory,
        byte[] status,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        uint descriptorHead,
        uint usedByteCount)
    {
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        WriteUsedCompletion(
            VirtqueueSplitRingPlan.Create(QueueSize).Plan,
            queueMemory,
            previousUsedIndex,
            currentUsedIndex,
            descriptorHead,
            usedByteCount);
    }

    private static VirtioBlockQueueNotificationMetadataResult CreateSuccessfulNotification(
        ushort queueNotifyOffset)
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector: 4,
                sectorCount: 2,
                states,
                ref cursor,
                destinationLength: 1024,
                out _,
                out _,
                out _);

        return VirtioBlockQueueNotificationMetadataPlanner.Create(
            publish,
            queueIndex: 0,
            queueNotifyOffset);
    }

    private static VirtioBlockQueueNotifyOffsetPlanResult CreateSuccessfulNotifyOffset(
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier)
    {
        VirtioBlockQueueNotificationMetadataResult notification =
            CreateSuccessfulNotification(queueNotifyOffset);

        return VirtioBlockQueueNotifyOffsetPlanner.Create(
            notification,
            notifyOffsetMultiplier);
    }

    private static VirtioBlockReadQueueNotifyReadinessResult CreateSuccessfulReadNotifyReadiness(
        VirtioBlockReadQueuePublishResult publish,
        ushort queueNotifyOffset)
    {
        VirtioPciBarRegisterWindowBindingResult windows = CreateBoundRegisterWindows();
        if (!windows.Succeeded)
        {
            throw new InvalidOperationException("Invalid test register window binding: " + windows.Status);
        }

        byte[] notifyBytes = CreateNotifyWindowBytes();
        return VirtioBlockReadQueueNotifyReadinessExecutor.Execute(
            publish,
            queueIndex: 0,
            queueNotifyOffset,
            NotifyOffsetMultiplier,
            VirtioBlockQueueNotificationBarrierReadiness.Ready(50),
            windows.Bindings.NotifyConfiguration,
            notifyBytes);
    }

    private static VirtioBlockReadQueueCompletionResult CreateCompletedReadQueue(
        ulong firstSector,
        uint sectorCount,
        int destinationLength)
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector,
                sectorCount,
                states,
                ref cursor,
                destinationLength,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        WriteSuccessfulCompletion(publish, queueMemory, status);

        return VirtioBlockReadQueueCompletionPlanner.CompleteRead(
            publish,
            queueMemory,
            status,
            states,
            ref cursor);
    }

    private static VirtioBlockReadQueueNotifyCompletionResult CreateCompletedReadNotifyCompletion(
        ulong firstSector,
        uint sectorCount,
        int destinationLength)
    {
        BlockDeviceInfo device = CreateDevice(64);
        VirtioBlockDescriptorOwnershipState[] states =
            new VirtioBlockDescriptorOwnershipState[QueueSize];
        VirtioBlockQueueCursorState cursor =
            new VirtioBlockQueueCursorState(
                availableRingIndex: 4,
                usedRingIndex: 8);
        VirtioBlockReadQueuePublishResult publish =
            PublishReadQueue(
                device,
                firstSector,
                sectorCount,
                states,
                ref cursor,
                destinationLength,
                out _,
                out byte[] status,
                out byte[] queueMemory);
        VirtioBlockReadQueueNotifyReadinessResult readiness =
            CreateSuccessfulReadNotifyReadiness(publish, queueNotifyOffset: 6);
        WriteSuccessfulCompletion(publish, queueMemory, status);

        return VirtioBlockReadQueueNotifyCompletionPlanner.CompleteAfterNotify(
            publish,
            readiness,
            queueMemory,
            status,
            states,
            ref cursor);
    }

    private static VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult ExecuteQueueSetupBarrier(
        bool validSetupPlan,
        bool barrierReady,
        uint barrierSequence)
    {
        VirtioBlockQueueMmioSetupCommand[] commands =
            new VirtioBlockQueueMmioSetupCommand[VirtioBlockQueueMmioSetupPlanner.RequiredCommandCount];
        VirtioBlockQueueMmioSetupPlanResult setup =
            VirtioBlockQueueMmioSetupPlanner.Create(
                validSetupPlan ? CreateBlockDescriptor() : default,
                QueueSize,
                CreateSetupAddresses(),
                CreateSetupMetadata(),
                commands);
        VirtioBlockQueueMmioSetupBarrierReadiness barrier = barrierReady ?
            VirtioBlockQueueMmioSetupBarrierReadiness.Ready(barrierSequence) :
            new VirtioBlockQueueMmioSetupBarrierReadiness(
                queueSelectVisible: true,
                queueSizeVisible: true,
                queueAddressWritesVisible: true,
                orderedBeforeQueueEnable: false,
                barrierSequence);

        return VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutor.Execute(
            setup,
            commands,
            CreateBoundRegisterWindows().Bindings.CommonConfiguration,
            CreateCommonWindowBytes(),
            barrier);
    }

    private static VirtioBlockQueueMmioSetupQueueAddresses CreateSetupAddresses()
    {
        return new VirtioBlockQueueMmioSetupQueueAddresses(
            descriptorTablePhysicalAddress: 0x100000,
            driverRingPhysicalAddress: 0x101000,
            deviceRingPhysicalAddress: 0x102000);
    }

    private static VirtioBlockQueueMmioSetupQueueDeviceMetadata CreateSetupMetadata()
    {
        return new VirtioBlockQueueMmioSetupQueueDeviceMetadata(
            DeviceMaximumQueueSize,
            hasNotifyOffset: true,
            QueueNotifyOffset);
    }

    private static VirtioDeviceDescriptor CreateBlockDescriptor()
    {
        VirtioDeviceDescriptorSource[] sources =
        [
            new VirtioDeviceDescriptorSource(
                sourceIndex: 50,
                CreateBindingPlan(),
                VirtioDeviceInitializationProfile.BlockOptionalFeatureMask,
                VirtioDeviceInitializationProfile.BlockRequiredQueueCount,
                featuresOkAccepted: true)
        ];
        VirtioDeviceConfigurationSource[] configurations =
        [
            new VirtioDeviceConfigurationSource(
                sourceIndex: 50,
                CreateBlockConfiguration(capacitySectors: 64))
        ];
        VirtioDeviceDescriptor[] destination = new VirtioDeviceDescriptor[1];

        VirtioDeviceDescriptorCatalogResult result =
            VirtioDeviceDescriptorCatalog.Build(
                sources,
                configurations,
                destination);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Failed to create deterministic virtio-blk descriptor.");
        }

        return destination[0];
    }

    private static byte[] CreateBlockConfiguration(ulong capacitySectors)
    {
        byte[] config = new byte[VirtioBlockDeviceConfig.CapacityLength];
        EndianPrimitives.WriteUInt64LittleEndian(config, capacitySectors);
        return config;
    }

    private static void WriteSuccessfulCompletion(
        VirtioBlockReadQueuePublishResult publish,
        byte[] queueMemory,
        byte[] status)
    {
        status[0] = (byte)VirtioBlockDeviceStatus.Ok;
        VirtioBlockReadLifecyclePublishResult lifecycle =
            publish.CursorPublish.Publish.LifecyclePublish;
        WriteUsedCompletion(
            lifecycle.QueuePlan,
            queueMemory,
            lifecycle.PreviousUsedIndex,
            lifecycle.ExpectedNextUsedIndex,
            lifecycle.DescriptorHead,
            lifecycle.DataByteCount);
    }

    private static void WriteUsedCompletion(
        VirtqueueSplitRingPlan queue,
        byte[] queueMemory,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        uint descriptorHead,
        uint usedByteCount)
    {
        EndianPrimitives.WriteUInt16LittleEndian(
            queueMemory.AsSpan((int)queue.UsedRing.Offset + 2, sizeof(ushort)),
            currentUsedIndex);

        ushort slot = (ushort)(previousUsedIndex & (queue.QueueSize - 1));
        int elementOffset = (int)queue.UsedRing.Offset +
            (int)VirtqueueSplitRingPlan.UsedRingHeaderLength +
            (slot * (int)VirtqueueSplitRingPlan.UsedRingElementLength);
        EndianPrimitives.WriteUInt32LittleEndian(
            queueMemory.AsSpan(elementOffset, sizeof(uint)),
            descriptorHead);
        EndianPrimitives.WriteUInt32LittleEndian(
            queueMemory.AsSpan(elementOffset + sizeof(uint), sizeof(uint)),
            usedByteCount);
    }

    private static VirtioPciBarRegisterWindowBindingResult CreateBoundRegisterWindows()
    {
        return VirtioPciBarRegisterWindowPlanner.BindRequiredApertures(
            CreateBindingPlan(),
            CreateRegisterWindows());
    }

    private static VirtioPciBindingPlanResult CreateBindingPlan()
    {
        ushort deviceId = VirtioPciDeviceDiscovery.ModernDeviceIdStart +
            VirtioPciDeviceDiscovery.BlockDeviceId;

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
            offsetValue: NotifyApertureBarOffset,
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
            length: VirtioPciDeviceDiscovery.IsrStatusMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);
        WriteVirtioCapability(
            config,
            DeviceOffset,
            nextOffset: 0,
            VirtioPciCapabilityType.DeviceConfiguration,
            bar: 3,
            offsetValue: 0,
            length: VirtioPciDeviceDiscovery.BlockDeviceConfigurationMinimumLength,
            notifyOffsetMultiplier: 0,
            capabilityLength: VirtioPciCapabilityListParser.ModernVirtioCapabilityLength);

        return config;
    }

    private static ulong[] CreateBarSizes()
    {
        return new ulong[]
        {
            0x1000,
            0x1000,
            0x1000,
            0x1000,
            0,
            0
        };
    }

    private static VirtioPciMappedBarWindow[] CreateRegisterWindows()
    {
        return new VirtioPciMappedBarWindow[]
        {
            CreateRegisterWindow(0, 0x80000000UL, 0xFFFF800000000000UL),
            CreateRegisterWindow(1, NotifyBarPhysicalBase, NotifyBarVirtualBase),
            CreateRegisterWindow(2, 0x80002000UL, 0xFFFF800000002000UL),
            CreateRegisterWindow(3, 0x80003000UL, 0xFFFF800000003000UL),
            default,
            default
        };
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

    private static byte[] CreateCommonWindowBytes(int length = 0x1000)
    {
        byte[] commonBytes = new byte[length];
        Array.Fill(commonBytes, (byte)0xCC);
        return commonBytes;
    }

    private static byte[] CreateNotifyWindowBytes(int length = 0x1000)
    {
        byte[] notifyBytes = new byte[length];
        Array.Fill(notifyBytes, (byte)0xCC);
        return notifyBytes;
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

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, sizeof(ushort)));
    }

    private static ushort ReadUInt16AtBarOffset(ReadOnlySpan<byte> bytes, ulong barRelativeOffset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(
            bytes.Slice(checked((int)barRelativeOffset), sizeof(ushort)));
    }

    private static bool AllStatesAre(
        ReadOnlySpan<VirtioBlockDescriptorOwnershipState> states,
        VirtioBlockDescriptorOwnershipState expected)
    {
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i] != expected)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllBytesEqual(
        ReadOnlySpan<byte> bytes,
        byte value)
    {
        return AllBytesEqual(bytes, 0, bytes.Length, value);
    }

    private static bool AllBytesEqual(
        ReadOnlySpan<byte> bytes,
        int offset,
        int count,
        byte value)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i >= offset && i < offset + count && bytes[i] != value)
            {
                return false;
            }
        }

        return true;
    }
}
