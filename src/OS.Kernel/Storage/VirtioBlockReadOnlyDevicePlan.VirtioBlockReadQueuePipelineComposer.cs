using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockReadQueuePipelineComposer
{
    public const bool PublishesReadQueue = true;
    public const bool InvokesReadNotifyReadinessExecutor = true;
    public const bool InvokesReadNotifyCompletionPlanner = true;
    public const bool InvokesReadNotifyBlockReadProjector = true;
    public const bool RequiresCallerOwnedCursorState = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedStatusBuffer = true;
    public const bool RequiresCallerOwnedDescriptorStateTable = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedNotifyWindowBuffer = true;
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

    public static VirtioBlockReadQueuePipelineResult Read(
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
        VirtioBlockReadQueuePublishResult publish =
            VirtioBlockReadQueuePlanner.PublishRead(
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
                queueMemory);
        if (!publish.Succeeded)
        {
            return VirtioBlockReadQueuePipelineResult.Create(
                VirtioBlockReadQueuePipelineStatus.PublicationFailed,
                publish,
                default,
                default,
                default,
                notifyReadinessAttempted: false,
                notifyCompletionAttempted: false,
                blockProjectionAttempted: false);
        }

        VirtioBlockReadQueueNotifyReadinessResult readiness =
            VirtioBlockReadQueueNotifyReadinessExecutor.Execute(
                publish,
                queueIndex,
                queueNotifyOffset,
                notifyOffsetMultiplier,
                barrier,
                notifyConfiguration,
                notifyWindowBytes);
        if (!readiness.Succeeded)
        {
            return VirtioBlockReadQueuePipelineResult.Create(
                VirtioBlockReadQueuePipelineStatus.NotifyReadinessFailed,
                publish,
                readiness,
                default,
                default,
                notifyReadinessAttempted: true,
                notifyCompletionAttempted: false,
                blockProjectionAttempted: false);
        }

        VirtioBlockReadQueueNotifyCompletionResult notifyCompletion =
            VirtioBlockReadQueueNotifyCompletionPlanner.CompleteAfterNotify(
                publish,
                readiness,
                queueMemory,
                statusBuffer,
                descriptorStates,
                ref cursor);
        if (!notifyCompletion.Succeeded)
        {
            VirtioBlockReadQueueNotifyBlockReadProjectionResult failedProjection =
                VirtioBlockReadQueueNotifyBlockReadProjector.Project(notifyCompletion);
            return VirtioBlockReadQueuePipelineResult.Create(
                VirtioBlockReadQueuePipelineStatus.NotifyCompletionFailed,
                publish,
                readiness,
                notifyCompletion,
                failedProjection,
                notifyReadinessAttempted: true,
                notifyCompletionAttempted: true,
                blockProjectionAttempted: true);
        }

        VirtioBlockReadQueueNotifyBlockReadProjectionResult projection =
            VirtioBlockReadQueueNotifyBlockReadProjector.Project(notifyCompletion);
        if (!projection.Succeeded)
        {
            return VirtioBlockReadQueuePipelineResult.Create(
                VirtioBlockReadQueuePipelineStatus.BlockReadProjectionFailed,
                publish,
                readiness,
                notifyCompletion,
                projection,
                notifyReadinessAttempted: true,
                notifyCompletionAttempted: true,
                blockProjectionAttempted: true);
        }

        return VirtioBlockReadQueuePipelineResult.Create(
            VirtioBlockReadQueuePipelineStatus.Success,
            publish,
            readiness,
            notifyCompletion,
            projection,
            notifyReadinessAttempted: true,
            notifyCompletionAttempted: true,
            blockProjectionAttempted: true);
    }
}
