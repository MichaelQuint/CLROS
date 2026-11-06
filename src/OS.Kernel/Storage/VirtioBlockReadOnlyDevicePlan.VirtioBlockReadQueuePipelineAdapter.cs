using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockReadQueuePipelineAdapter
{
    public const bool ValidatesCallerOwnedBuffersBeforePipeline = true;
    public const bool DelegatesReadQueuePipelineComposer = true;
    public const bool PreflightFailuresMutateCallerOwnedState = false;
    public const bool RequiresCallerOwnedCursorState = true;
    public const bool RequiresCallerOwnedRequestHeader = true;
    public const bool RequiresCallerOwnedDestinationBuffer = true;
    public const bool RequiresCallerOwnedStatusBuffer = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedDescriptorStateTable = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedNotifyWindowBuffer = true;
    public const bool PrevalidatesNotifyWindowBeforePublication = true;
    public const bool ReadsQueueMemory = true;
    public const bool ReadsStatusBuffer = true;
    public const bool WritesQueueMemory = true;
    public const bool WritesRequestHeader = true;
    public const bool MutatesCallerOwnedCursorState = true;
    public const bool MutatesCallerOwnedDescriptorStateTable = true;
    public const bool PerformsMmio = false;
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
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool WaitsForUsedRingCompletion = false;

    public static VirtioBlockReadQueuePipelineAdapterResult Read(
        BlockDeviceInfo device,
        ulong firstSector,
        uint sectorCount,
        ushort queueSize,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        ref VirtioBlockQueueCursorState cursor,
        ulong requestHeaderPhysicalAddress,
        ulong dataBufferPhysicalAddress,
        ulong statusBytePhysicalAddress,
        Span<byte> requestHeader,
        Span<byte> destination,
        Span<byte> statusBuffer,
        Span<byte> queueMemory,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier,
        VirtioBlockQueueNotificationBarrierReadiness barrier,
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        Span<byte> notifyWindowBytes)
    {
        VirtioBlockReadQueuePipelineAdapterResult preflight =
            ValidatePreflight(
                device,
                firstSector,
                sectorCount,
                queueSize,
                descriptorStates.Length,
                requestHeader.Length,
                destination.Length,
                statusBuffer.Length,
                queueMemory.Length,
                queueNotifyOffset,
                notifyOffsetMultiplier,
                notifyConfiguration,
                notifyWindowBytes.Length);
        if (!preflight.Succeeded)
        {
            return preflight;
        }

        VirtioBlockReadQueuePipelineResult pipeline =
            VirtioBlockReadQueuePipelineComposer.Read(
                device,
                firstSector,
                sectorCount,
                queueSize,
                descriptorStates,
                ref cursor,
                requestHeaderPhysicalAddress,
                dataBufferPhysicalAddress,
                statusBytePhysicalAddress,
                requestHeader,
                destination,
                statusBuffer,
                queueMemory,
                queueIndex,
                queueNotifyOffset,
                notifyOffsetMultiplier,
                barrier,
                notifyConfiguration,
                notifyWindowBytes);

        return VirtioBlockReadQueuePipelineAdapterResult.FromPipeline(pipeline);
    }

    private static VirtioBlockReadQueuePipelineAdapterResult ValidatePreflight(
        BlockDeviceInfo device,
        ulong firstSector,
        uint sectorCount,
        ushort queueSize,
        int descriptorStateCount,
        int requestHeaderLength,
        int destinationLength,
        int statusBufferLength,
        int queueMemoryLength,
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier,
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        int notifyWindowLength)
    {
        if ((uint)requestHeaderLength < VirtioBlockRequestPlan.RequestHeaderLength)
        {
            return VirtioBlockReadQueuePipelineAdapterResult.PreflightFailure(
                VirtioBlockReadQueuePipelineAdapterStatus.RequestHeaderTooSmall,
                VirtioBlockRequestPlan.RequestHeaderLength,
                (uint)requestHeaderLength,
                requiredElementCount: 0,
                availableElementCount: 0,
                queuePlanStatus: VirtqueueSplitRingPlanStatus.Success,
                notifyWindowPlanStatus: VirtioPciBarRegisterWindowPlanStatus.Success);
        }

        if ((uint)statusBufferLength < VirtioBlockRequestPlan.StatusLength)
        {
            return VirtioBlockReadQueuePipelineAdapterResult.PreflightFailure(
                VirtioBlockReadQueuePipelineAdapterStatus.StatusBufferTooSmall,
                VirtioBlockRequestPlan.StatusLength,
                (uint)statusBufferLength,
                requiredElementCount: 0,
                availableElementCount: 0,
                queuePlanStatus: VirtqueueSplitRingPlanStatus.Success,
                notifyWindowPlanStatus: VirtioPciBarRegisterWindowPlanStatus.Success);
        }

        if (VirtioBlockReadOnlyDevicePlan.ValidateGeometry(device) == BlockDeviceStatus.Success &&
            VirtioBlockReadOnlyDevicePlan.ValidateRange(device, firstSector, sectorCount) == BlockDeviceStatus.Success)
        {
            ulong requestedByteCount = (ulong)sectorCount * device.BlockSize;
            if ((ulong)destinationLength < requestedByteCount)
            {
                return VirtioBlockReadQueuePipelineAdapterResult.PreflightFailure(
                    VirtioBlockReadQueuePipelineAdapterStatus.DestinationTooSmall,
                    requestedByteCount,
                    (uint)destinationLength,
                    requiredElementCount: 0,
                    availableElementCount: 0,
                    queuePlanStatus: VirtqueueSplitRingPlanStatus.Success,
                    notifyWindowPlanStatus: VirtioPciBarRegisterWindowPlanStatus.Success);
            }
        }

        VirtqueueSplitRingPlanResult queuePlan = VirtqueueSplitRingPlan.Create(queueSize);
        if (!queuePlan.Succeeded)
        {
            return VirtioBlockReadQueuePipelineAdapterResult.PreflightFailure(
                VirtioBlockReadQueuePipelineAdapterStatus.QueuePlanFailed,
                requiredByteCount: 0,
                availableByteCount: 0,
                requiredElementCount: queueSize,
                availableElementCount: (uint)descriptorStateCount,
                queuePlanStatus: queuePlan.Status,
                notifyWindowPlanStatus: VirtioPciBarRegisterWindowPlanStatus.Success);
        }

        if ((ulong)queueMemoryLength < queuePlan.Plan.EndOffset)
        {
            return VirtioBlockReadQueuePipelineAdapterResult.PreflightFailure(
                VirtioBlockReadQueuePipelineAdapterStatus.QueueMemoryTooSmall,
                queuePlan.Plan.EndOffset,
                (uint)queueMemoryLength,
                requiredElementCount: 0,
                availableElementCount: 0,
                queuePlanStatus: queuePlan.Status,
                notifyWindowPlanStatus: VirtioPciBarRegisterWindowPlanStatus.Success);
        }

        if (descriptorStateCount < queueSize)
        {
            return VirtioBlockReadQueuePipelineAdapterResult.PreflightFailure(
                VirtioBlockReadQueuePipelineAdapterStatus.DescriptorStateTableTooSmall,
                requiredByteCount: 0,
                availableByteCount: 0,
                requiredElementCount: queueSize,
                availableElementCount: (uint)descriptorStateCount,
                queuePlanStatus: queuePlan.Status,
                notifyWindowPlanStatus: VirtioPciBarRegisterWindowPlanStatus.Success);
        }

        ulong notifyRelativeOffset = (ulong)queueNotifyOffset * notifyOffsetMultiplier;
        if (notifyRelativeOffset <= uint.MaxValue)
        {
            VirtioPciBarRegisterWindowAccessPlanResult notifyAccess =
                VirtioPciBarRegisterWindowPlanner.PlanAccess(
                    notifyConfiguration,
                    (uint)notifyRelativeOffset,
                    VirtioPciRegisterAccessWidth.Word);
            if (notifyAccess.Succeeded)
            {
                ulong notifyRequiredBytes = notifyAccess.Plan.BarRelativeOffset + notifyAccess.Plan.WidthBytes;
                if (notifyRequiredBytes > (ulong)notifyWindowLength)
                {
                    return VirtioBlockReadQueuePipelineAdapterResult.PreflightFailure(
                        VirtioBlockReadQueuePipelineAdapterStatus.NotifyWindowTooSmall,
                        notifyRequiredBytes,
                        (uint)notifyWindowLength,
                        requiredElementCount: 0,
                        availableElementCount: 0,
                        queuePlanStatus: queuePlan.Status,
                        notifyWindowPlanStatus: notifyAccess.Status);
                }
            }
        }

        return VirtioBlockReadQueuePipelineAdapterResult.PreflightFailure(
            VirtioBlockReadQueuePipelineAdapterStatus.Success,
            requiredByteCount: 0,
            availableByteCount: 0,
            requiredElementCount: 0,
            availableElementCount: 0,
            queuePlanStatus: queuePlan.Status,
            notifyWindowPlanStatus: VirtioPciBarRegisterWindowPlanStatus.Success);
    }
}
