using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadOnlyDevicePlanStatus
{
    Success = 0,
    InvalidBlockGeometry = 1,
    InvalidSectorRange = 2,
    LifecyclePublishFailed = 3
}

public readonly struct VirtioBlockReadOnlyDevicePlanResult
{
    private VirtioBlockReadOnlyDevicePlanResult(
        VirtioBlockReadOnlyDevicePlanStatus status,
        BlockDeviceStatus blockDeviceStatus,
        bool lifecycleInvoked,
        uint sectorSizeBytes,
        ulong deviceSectorCount,
        ulong firstSector,
        uint sectorCount,
        ulong requestedByteCount,
        ushort queueSize,
        VirtioBlockReadLifecyclePublishResult publish)
    {
        IsInitialized = true;
        Status = status;
        BlockDeviceStatus = blockDeviceStatus;
        LifecycleInvoked = lifecycleInvoked;
        SectorSizeBytes = sectorSizeBytes;
        DeviceSectorCount = deviceSectorCount;
        FirstSector = firstSector;
        SectorCount = sectorCount;
        RequestedByteCount = requestedByteCount;
        QueueSize = queueSize;
        Publish = publish;
    }

    public VirtioBlockReadOnlyDevicePlanStatus Status { get; }

    public bool IsInitialized { get; }

    public BlockDeviceStatus BlockDeviceStatus { get; }

    public bool LifecycleInvoked { get; }

    public uint SectorSizeBytes { get; }

    public ulong DeviceSectorCount { get; }

    public ulong FirstSector { get; }

    public uint SectorCount { get; }

    public ulong RequestedByteCount { get; }

    public ushort QueueSize { get; }

    public VirtioBlockReadLifecyclePublishResult Publish { get; }

    public VirtioBlockReadLifecyclePublishStatus LifecyclePublishStatus => Publish.Status;

    public VirtioBlockReadTransactionPlanStatus ReadPlanStatus => Publish.ReadPlanStatus;

    public VirtioBlockRequestPlanStatus RequestPlanStatus => Publish.RequestPlanStatus;

    public VirtqueueSplitRingPlanStatus QueuePlanStatus => Publish.QueuePlanStatus;

    public VirtioBlockDescriptorChainPlanStatus DescriptorPlanStatus => Publish.DescriptorPlanStatus;

    public VirtioBlockDescriptorChainWriterStatus DescriptorWriterStatus => Publish.DescriptorWriterStatus;

    public ulong LastSectorInclusive => Publish.LastSectorInclusive;

    public uint DataByteCount => Publish.DataByteCount;

    public ushort DescriptorHead => Publish.DescriptorHead;

    public ushort PreviousAvailableRingIndex => Publish.PreviousAvailableRingIndex;

    public ushort NextAvailableRingIndex => Publish.NextAvailableRingIndex;

    public ushort PreviousUsedIndex => Publish.PreviousUsedIndex;

    public ushort ExpectedNextUsedIndex => Publish.ExpectedNextUsedIndex;

    public ushort AvailableRingSlot => Publish.AvailableRingSlot;

    public uint AvailableRingEntryOffset => Publish.AvailableRingEntryOffset;

    public ushort DescriptorsWritten => Publish.DescriptorsWritten;

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockReadOnlyDevicePlanStatus.Success;

    internal static VirtioBlockReadOnlyDevicePlanResult Success(
        BlockDeviceInfo device,
        ulong requestedByteCount,
        VirtioBlockReadLifecyclePublishResult publish)
    {
        return new VirtioBlockReadOnlyDevicePlanResult(
            VirtioBlockReadOnlyDevicePlanStatus.Success,
            BlockDeviceStatus.Success,
            lifecycleInvoked: true,
            device.BlockSize,
            device.BlockCount,
            publish.FirstSector,
            publish.SectorCount,
            requestedByteCount,
            publish.QueueSize,
            publish);
    }

    internal static VirtioBlockReadOnlyDevicePlanResult FailBeforeLifecycle(
        VirtioBlockReadOnlyDevicePlanStatus status,
        BlockDeviceStatus blockDeviceStatus,
        BlockDeviceInfo device,
        ulong firstSector,
        uint sectorCount,
        ulong requestedByteCount,
        ushort queueSize)
    {
        return new VirtioBlockReadOnlyDevicePlanResult(
            status,
            blockDeviceStatus,
            lifecycleInvoked: false,
            device.BlockSize,
            device.BlockCount,
            firstSector,
            sectorCount,
            requestedByteCount,
            queueSize,
            default);
    }

    internal static VirtioBlockReadOnlyDevicePlanResult FailAfterLifecycle(
        BlockDeviceInfo device,
        ulong requestedByteCount,
        VirtioBlockReadLifecyclePublishResult publish)
    {
        return new VirtioBlockReadOnlyDevicePlanResult(
            VirtioBlockReadOnlyDevicePlanStatus.LifecyclePublishFailed,
            BlockDeviceStatus.Success,
            lifecycleInvoked: true,
            device.BlockSize,
            device.BlockCount,
            publish.FirstSector,
            publish.SectorCount,
            requestedByteCount,
            publish.QueueSize,
            publish);
    }
}

public enum VirtioBlockReadQueuePublishStatus
{
    Success = 0,
    InvalidBlockGeometry = 1,
    InvalidSectorRange = 2,
    CursorPublishFailed = 3
}

public readonly struct VirtioBlockReadQueuePublishResult
{
    private VirtioBlockReadQueuePublishResult(
        VirtioBlockReadQueuePublishStatus status,
        BlockDeviceStatus blockDeviceStatus,
        VirtioBlockQueueCursorPublishStatus cursorPublishStatus,
        VirtioBlockDescriptorPublishPlanStatus descriptorPublishStatus,
        VirtioBlockDescriptorFreeListStatus freeListStatus,
        VirtioBlockReadLifecyclePublishStatus lifecyclePublishStatus,
        VirtioBlockDescriptorOwnershipStatus ownershipStatus,
        bool cursorPublishAttempted,
        uint sectorSizeBytes,
        ulong deviceSectorCount,
        ulong firstSector,
        uint sectorCount,
        ulong requestedByteCount,
        ushort queueSize,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex,
        VirtioBlockQueueCursorPublishResult cursorPublish,
        bool isInitialized)
    {
        Status = status;
        BlockDeviceStatus = blockDeviceStatus;
        CursorPublishStatus = cursorPublishStatus;
        DescriptorPublishStatus = descriptorPublishStatus;
        FreeListStatus = freeListStatus;
        LifecyclePublishStatus = lifecyclePublishStatus;
        OwnershipStatus = ownershipStatus;
        CursorPublishAttempted = cursorPublishAttempted;
        SectorSizeBytes = sectorSizeBytes;
        DeviceSectorCount = deviceSectorCount;
        FirstSector = firstSector;
        SectorCount = sectorCount;
        RequestedByteCount = requestedByteCount;
        QueueSize = queueSize;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        CurrentAvailableRingIndex = currentAvailableRingIndex;
        PreviousUsedRingIndex = previousUsedRingIndex;
        CurrentUsedRingIndex = currentUsedRingIndex;
        CursorPublish = cursorPublish;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueuePublishStatus Status { get; }

    public BlockDeviceStatus BlockDeviceStatus { get; }

    public VirtioBlockQueueCursorPublishStatus CursorPublishStatus { get; }

    public VirtioBlockDescriptorPublishPlanStatus DescriptorPublishStatus { get; }

    public VirtioBlockDescriptorFreeListStatus FreeListStatus { get; }

    public VirtioBlockReadLifecyclePublishStatus LifecyclePublishStatus { get; }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus { get; }

    public bool CursorPublishAttempted { get; }

    public uint SectorSizeBytes { get; }

    public ulong DeviceSectorCount { get; }

    public ulong FirstSector { get; }

    public uint SectorCount { get; }

    public ulong RequestedByteCount { get; }

    public ushort QueueSize { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort CurrentAvailableRingIndex { get; }

    public ushort PreviousUsedRingIndex { get; }

    public ushort CurrentUsedRingIndex { get; }

    public VirtioBlockQueueCursorPublishResult CursorPublish { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockReadQueuePublishStatus.Success;

    internal static VirtioBlockReadQueuePublishResult FailBeforeCursorPublish(
        VirtioBlockReadQueuePublishStatus status,
        BlockDeviceStatus blockDeviceStatus,
        BlockDeviceInfo device,
        ulong firstSector,
        uint sectorCount,
        ulong requestedByteCount,
        ushort queueSize,
        VirtioBlockQueueCursorState cursor)
    {
        return new VirtioBlockReadQueuePublishResult(
            status,
            blockDeviceStatus,
            VirtioBlockQueueCursorPublishStatus.Success,
            VirtioBlockDescriptorPublishPlanStatus.Success,
            VirtioBlockDescriptorFreeListStatus.Success,
            VirtioBlockReadLifecyclePublishStatus.Success,
            VirtioBlockDescriptorOwnershipStatus.Success,
            cursorPublishAttempted: false,
            device.BlockSize,
            device.BlockCount,
            firstSector,
            sectorCount,
            requestedByteCount,
            queueSize,
            cursor.AvailableRingIndex,
            cursor.AvailableRingIndex,
            cursor.UsedRingIndex,
            cursor.UsedRingIndex,
            default,
            isInitialized: true);
    }

    internal static VirtioBlockReadQueuePublishResult Create(
        VirtioBlockReadQueuePublishStatus status,
        BlockDeviceInfo device,
        ulong requestedByteCount,
        VirtioBlockQueueCursorPublishResult cursorPublish)
    {
        return new VirtioBlockReadQueuePublishResult(
            status,
            BlockDeviceStatus.Success,
            cursorPublish.Status,
            cursorPublish.PublishPlanStatus,
            cursorPublish.FreeListStatus,
            cursorPublish.LifecyclePublishStatus,
            cursorPublish.OwnershipStatus,
            cursorPublishAttempted: true,
            device.BlockSize,
            device.BlockCount,
            cursorPublish.Publish.LifecyclePublish.FirstSector,
            cursorPublish.Publish.LifecyclePublish.SectorCount,
            requestedByteCount,
            cursorPublish.Publish.LifecyclePublish.QueueSize,
            cursorPublish.PreviousAvailableRingIndex,
            cursorPublish.CurrentAvailableRingIndex,
            cursorPublish.PreviousUsedRingIndex,
            cursorPublish.CurrentUsedRingIndex,
            cursorPublish,
            isInitialized: true);
    }
}

public static class VirtioBlockReadQueuePlanner
{
    public const bool RequiresBlockDeviceInfo = true;
    public const bool ValidatesBlockGeometryAndRange = true;
    public const bool RequiresCallerOwnedCursorState = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedStatusBuffer = true;
    public const bool RequiresCallerOwnedDescriptorStateTable = true;
    public const bool ReadsQueueMemory = false;
    public const bool ReadsStatusBuffer = false;
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

    public static VirtioBlockReadQueuePublishResult PublishRead(
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
        Span<byte> queueMemory)
    {
        BlockDeviceStatus geometryStatus =
            VirtioBlockReadOnlyDevicePlan.ValidateGeometry(device);
        if (geometryStatus != BlockDeviceStatus.Success)
        {
            return VirtioBlockReadQueuePublishResult.FailBeforeCursorPublish(
                VirtioBlockReadQueuePublishStatus.InvalidBlockGeometry,
                geometryStatus,
                device,
                firstSector,
                sectorCount,
                requestedByteCount: 0,
                queueSize,
                cursor);
        }

        ulong requestedByteCount = (ulong)sectorCount * device.BlockSize;
        BlockDeviceStatus rangeStatus =
            VirtioBlockReadOnlyDevicePlan.ValidateRange(device, firstSector, sectorCount);
        if (rangeStatus != BlockDeviceStatus.Success)
        {
            return VirtioBlockReadQueuePublishResult.FailBeforeCursorPublish(
                VirtioBlockReadQueuePublishStatus.InvalidSectorRange,
                rangeStatus,
                device,
                firstSector,
                sectorCount,
                requestedByteCount,
                queueSize,
                cursor);
        }

        VirtioBlockQueueCursorPublishResult cursorPublish =
            VirtioBlockQueueCursorPlanner.PublishReadFromFreeList(
                device.BlockSize,
                device.BlockCount,
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
        if (!cursorPublish.Succeeded)
        {
            return VirtioBlockReadQueuePublishResult.Create(
                VirtioBlockReadQueuePublishStatus.CursorPublishFailed,
                device,
                requestedByteCount,
                cursorPublish);
        }

        return VirtioBlockReadQueuePublishResult.Create(
            VirtioBlockReadQueuePublishStatus.Success,
            device,
            requestedByteCount,
            cursorPublish);
    }
}

public enum VirtioBlockReadQueueCompletionStatus
{
    Success = 0,
    PublicationFailed = 1,
    CursorReclaimFailed = 2
}

public readonly struct VirtioBlockReadQueueCompletionResult
{
    private VirtioBlockReadQueueCompletionResult(
        VirtioBlockReadQueueCompletionStatus status,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        VirtioBlockQueueCursorReclaimStatus cursorReclaimStatus,
        VirtioBlockDescriptorReclaimPlanStatus reclaimPlanStatus,
        VirtioBlockReadLifecycleCompletionStatus completionStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        VirtioBlockDescriptorOwnershipStatus ownershipStatus,
        bool cursorReclaimAttempted,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex,
        VirtioBlockReadQueuePublishResult readQueuePublish,
        VirtioBlockQueueCursorReclaimResult cursorReclaim,
        bool isInitialized)
    {
        Status = status;
        ReadQueuePublishStatus = readQueuePublishStatus;
        CursorReclaimStatus = cursorReclaimStatus;
        ReclaimPlanStatus = reclaimPlanStatus;
        CompletionStatus = completionStatus;
        CompletionReaderStatus = completionReaderStatus;
        OwnershipStatus = ownershipStatus;
        CursorReclaimAttempted = cursorReclaimAttempted;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        CurrentAvailableRingIndex = currentAvailableRingIndex;
        PreviousUsedRingIndex = previousUsedRingIndex;
        CurrentUsedRingIndex = currentUsedRingIndex;
        ReadQueuePublish = readQueuePublish;
        CursorReclaim = cursorReclaim;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueueCompletionStatus Status { get; }

    public VirtioBlockReadQueuePublishStatus ReadQueuePublishStatus { get; }

    public VirtioBlockQueueCursorReclaimStatus CursorReclaimStatus { get; }

    public VirtioBlockDescriptorReclaimPlanStatus ReclaimPlanStatus { get; }

    public VirtioBlockReadLifecycleCompletionStatus CompletionStatus { get; }

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus { get; }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus { get; }

    public bool CursorReclaimAttempted { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort CurrentAvailableRingIndex { get; }

    public ushort PreviousUsedRingIndex { get; }

    public ushort CurrentUsedRingIndex { get; }

    public VirtioBlockReadQueuePublishResult ReadQueuePublish { get; }

    public VirtioBlockQueueCursorReclaimResult CursorReclaim { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockReadQueueCompletionStatus.Success;

    internal static VirtioBlockReadQueueCompletionResult Create(
        VirtioBlockReadQueueCompletionStatus status,
        VirtioBlockReadQueuePublishResult readQueuePublish,
        VirtioBlockQueueCursorReclaimResult cursorReclaim,
        bool cursorReclaimAttempted,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex)
    {
        return new VirtioBlockReadQueueCompletionResult(
            status,
            readQueuePublish.Status,
            cursorReclaimAttempted
                ? cursorReclaim.Status
                : VirtioBlockQueueCursorReclaimStatus.Success,
            cursorReclaimAttempted
                ? cursorReclaim.ReclaimPlanStatus
                : VirtioBlockDescriptorReclaimPlanStatus.Success,
            cursorReclaimAttempted
                ? cursorReclaim.CompletionStatus
                : VirtioBlockReadLifecycleCompletionStatus.Success,
            cursorReclaimAttempted
                ? cursorReclaim.CompletionReaderStatus
                : VirtioBlockUsedRingCompletionReaderStatus.Success,
            cursorReclaimAttempted
                ? cursorReclaim.OwnershipStatus
                : VirtioBlockDescriptorOwnershipStatus.Success,
            cursorReclaimAttempted,
            previousAvailableRingIndex,
            currentAvailableRingIndex,
            previousUsedRingIndex,
            currentUsedRingIndex,
            readQueuePublish,
            cursorReclaim,
            isInitialized: true);
    }
}

public static class VirtioBlockReadQueueCompletionPlanner
{
    public const bool RequiresSuccessfulReadQueuePublication = true;
    public const bool RequiresCallerOwnedCursorState = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedStatusBuffer = true;
    public const bool RequiresCallerOwnedDescriptorStateTable = true;
    public const bool ReadsQueueMemory = true;
    public const bool ReadsStatusBuffer = true;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
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

    public static VirtioBlockReadQueueCompletionResult CompleteRead(
        VirtioBlockReadQueuePublishResult publish,
        ReadOnlySpan<byte> queueMemory,
        ReadOnlySpan<byte> statusBuffer,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        ref VirtioBlockQueueCursorState cursor)
    {
        ushort previousAvailableRingIndex = cursor.AvailableRingIndex;
        ushort previousUsedRingIndex = cursor.UsedRingIndex;
        if (!publish.Succeeded)
        {
            return VirtioBlockReadQueueCompletionResult.Create(
                VirtioBlockReadQueueCompletionStatus.PublicationFailed,
                publish,
                default,
                cursorReclaimAttempted: false,
                previousAvailableRingIndex,
                cursor.AvailableRingIndex,
                previousUsedRingIndex,
                cursor.UsedRingIndex);
        }

        VirtioBlockQueueCursorReclaimResult cursorReclaim =
            VirtioBlockQueueCursorPlanner.CompletePublishedRead(
                publish.CursorPublish.Publish,
                queueMemory,
                statusBuffer,
                descriptorStates,
                ref cursor);
        if (!cursorReclaim.Succeeded)
        {
            return VirtioBlockReadQueueCompletionResult.Create(
                VirtioBlockReadQueueCompletionStatus.CursorReclaimFailed,
                publish,
                cursorReclaim,
                cursorReclaimAttempted: true,
                previousAvailableRingIndex,
                cursor.AvailableRingIndex,
                previousUsedRingIndex,
                cursor.UsedRingIndex);
        }

        return VirtioBlockReadQueueCompletionResult.Create(
            VirtioBlockReadQueueCompletionStatus.Success,
            publish,
            cursorReclaim,
            cursorReclaimAttempted: true,
            previousAvailableRingIndex,
            cursor.AvailableRingIndex,
            previousUsedRingIndex,
            cursor.UsedRingIndex);
    }
}

public enum VirtioBlockReadQueueBlockReadProjectionStatus
{
    Success = 0,
    PublicationFailed = 1,
    CompletionFailed = 2
}

public readonly struct VirtioBlockReadQueueBlockReadProjectionResult
{
    private VirtioBlockReadQueueBlockReadProjectionResult(
        VirtioBlockReadQueueBlockReadProjectionStatus status,
        BlockReadResult blockRead,
        VirtioBlockReadQueueCompletionStatus readQueueCompletionStatus,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        VirtioBlockQueueCursorReclaimStatus cursorReclaimStatus,
        VirtioBlockDescriptorReclaimPlanStatus reclaimPlanStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        VirtioBlockDescriptorOwnershipStatus ownershipStatus,
        uint requestedBlockCount,
        ulong requestedByteCount,
        uint completedByteCount,
        bool isInitialized)
    {
        Status = status;
        BlockRead = blockRead;
        ReadQueueCompletionStatus = readQueueCompletionStatus;
        ReadQueuePublishStatus = readQueuePublishStatus;
        CursorReclaimStatus = cursorReclaimStatus;
        ReclaimPlanStatus = reclaimPlanStatus;
        CompletionReaderStatus = completionReaderStatus;
        OwnershipStatus = ownershipStatus;
        RequestedBlockCount = requestedBlockCount;
        RequestedByteCount = requestedByteCount;
        CompletedByteCount = completedByteCount;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueueBlockReadProjectionStatus Status { get; }

    public BlockReadResult BlockRead { get; }

    public VirtioBlockReadQueueCompletionStatus ReadQueueCompletionStatus { get; }

    public VirtioBlockReadQueuePublishStatus ReadQueuePublishStatus { get; }

    public VirtioBlockQueueCursorReclaimStatus CursorReclaimStatus { get; }

    public VirtioBlockDescriptorReclaimPlanStatus ReclaimPlanStatus { get; }

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus { get; }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus { get; }

    public uint RequestedBlockCount { get; }

    public ulong RequestedByteCount { get; }

    public uint CompletedByteCount { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockReadQueueBlockReadProjectionStatus.Success;

    internal static VirtioBlockReadQueueBlockReadProjectionResult Create(
        VirtioBlockReadQueueBlockReadProjectionStatus status,
        BlockReadResult blockRead,
        VirtioBlockReadQueueCompletionResult completion)
    {
        return new VirtioBlockReadQueueBlockReadProjectionResult(
            status,
            blockRead,
            completion.Status,
            completion.ReadQueuePublishStatus,
            completion.CursorReclaimStatus,
            completion.ReclaimPlanStatus,
            completion.CompletionReaderStatus,
            completion.OwnershipStatus,
            completion.ReadQueuePublish.SectorCount,
            completion.ReadQueuePublish.RequestedByteCount,
            completion.CursorReclaim.Reclaim.CompletedByteCount,
            isInitialized: true);
    }
}

public static class VirtioBlockReadQueueBlockReadProjector
{
    public const bool ConsumesReadQueueCompletionResult = true;
    public const bool ReadsQueueMemory = false;
    public const bool ReadsStatusBuffer = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
    public const bool MutatesCallerOwnedCursorState = false;
    public const bool MutatesCallerOwnedDescriptorStateTable = false;
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

    public static VirtioBlockReadQueueBlockReadProjectionResult Project(
        VirtioBlockReadQueueCompletionResult completion)
    {
        if (completion.Succeeded)
        {
            return VirtioBlockReadQueueBlockReadProjectionResult.Create(
                VirtioBlockReadQueueBlockReadProjectionStatus.Success,
                BlockReadResult.Success(
                    completion.ReadQueuePublish.SectorCount,
                    completion.ReadQueuePublish.RequestedByteCount),
                completion);
        }

        if (completion.Status == VirtioBlockReadQueueCompletionStatus.PublicationFailed)
        {
            BlockDeviceStatus status = completion.ReadQueuePublish.BlockDeviceStatus;
            if (status == BlockDeviceStatus.Success)
            {
                status = BlockDeviceStatus.IoError;
            }

            return VirtioBlockReadQueueBlockReadProjectionResult.Create(
                VirtioBlockReadQueueBlockReadProjectionStatus.PublicationFailed,
                BlockReadResult.Fail(status),
                completion);
        }

        return VirtioBlockReadQueueBlockReadProjectionResult.Create(
            VirtioBlockReadQueueBlockReadProjectionStatus.CompletionFailed,
            BlockReadResult.Fail(BlockDeviceStatus.IoError),
            completion);
    }
}

public enum VirtioBlockQueueNotificationMetadataStatus
{
    Success = 0,
    PublicationFailed = 1
}

public readonly struct VirtioBlockQueueNotificationMetadataResult
{
    private VirtioBlockQueueNotificationMetadataResult(
        VirtioBlockQueueNotificationMetadataStatus status,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        VirtioBlockQueueCursorPublishStatus cursorPublishStatus,
        VirtioBlockDescriptorPublishPlanStatus descriptorPublishStatus,
        ushort queueIndex,
        ushort queueNotifyOffset,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        ushort descriptorHead,
        ushort writeValue,
        uint writeWidthBytes,
        bool isInitialized)
    {
        Status = status;
        ReadQueuePublishStatus = readQueuePublishStatus;
        CursorPublishStatus = cursorPublishStatus;
        DescriptorPublishStatus = descriptorPublishStatus;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorHead = descriptorHead;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        IsInitialized = isInitialized;
    }

    public VirtioBlockQueueNotificationMetadataStatus Status { get; }

    public VirtioBlockReadQueuePublishStatus ReadQueuePublishStatus { get; }

    public VirtioBlockQueueCursorPublishStatus CursorPublishStatus { get; }

    public VirtioBlockDescriptorPublishPlanStatus DescriptorPublishStatus { get; }

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public ushort DescriptorHead { get; }

    public ushort WriteValue { get; }

    public uint WriteWidthBytes { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockQueueNotificationMetadataStatus.Success;

    internal static VirtioBlockQueueNotificationMetadataResult Create(
        VirtioBlockQueueNotificationMetadataStatus status,
        VirtioBlockReadQueuePublishResult publish,
        ushort queueIndex,
        ushort queueNotifyOffset)
    {
        bool succeeded = status == VirtioBlockQueueNotificationMetadataStatus.Success;
        return new VirtioBlockQueueNotificationMetadataResult(
            status,
            publish.Status,
            publish.CursorPublishStatus,
            publish.DescriptorPublishStatus,
            queueIndex,
            queueNotifyOffset,
            succeeded ? publish.CursorPublish.Publish.DescriptorsWritten : (ushort)0,
            publish.PreviousAvailableRingIndex,
            publish.CurrentAvailableRingIndex,
            succeeded ? publish.CursorPublish.Publish.FirstDescriptorIndex : (ushort)0,
            queueIndex,
            sizeof(ushort),
            isInitialized: true);
    }
}

public static class VirtioBlockQueueNotificationMetadataPlanner
{
    public const bool ConsumesReadQueuePublication = true;
    public const bool ProducesFutureNotifyWriteMetadata = true;
    public const bool ReadsQueueMemory = false;
    public const bool ReadsStatusBuffer = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
    public const bool MutatesCallerOwnedCursorState = false;
    public const bool MutatesCallerOwnedDescriptorStateTable = false;
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

    public static VirtioBlockQueueNotificationMetadataResult Create(
        VirtioBlockReadQueuePublishResult publish,
        ushort queueIndex,
        ushort queueNotifyOffset)
    {
        if (!publish.Succeeded)
        {
            return VirtioBlockQueueNotificationMetadataResult.Create(
                VirtioBlockQueueNotificationMetadataStatus.PublicationFailed,
                publish,
                queueIndex,
                queueNotifyOffset);
        }

        return VirtioBlockQueueNotificationMetadataResult.Create(
            VirtioBlockQueueNotificationMetadataStatus.Success,
            publish,
            queueIndex,
            queueNotifyOffset);
    }
}

public enum VirtioBlockQueueNotifyOffsetPlanStatus
{
    Success = 0,
    NotificationMetadataFailed = 1,
    RelativeOffsetOverflow = 2
}

public readonly struct VirtioBlockQueueNotifyOffsetPlanResult
{
    private VirtioBlockQueueNotifyOffsetPlanResult(
        VirtioBlockQueueNotifyOffsetPlanStatus status,
        VirtioBlockQueueNotificationMetadataStatus notificationMetadataStatus,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier,
        uint notifyRelativeOffset,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        ushort descriptorHead,
        ushort writeValue,
        uint writeWidthBytes,
        bool isInitialized)
    {
        Status = status;
        NotificationMetadataStatus = notificationMetadataStatus;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
        NotifyRelativeOffset = notifyRelativeOffset;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorHead = descriptorHead;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        IsInitialized = isInitialized;
    }

    public VirtioBlockQueueNotifyOffsetPlanStatus Status { get; }

    public VirtioBlockQueueNotificationMetadataStatus NotificationMetadataStatus { get; }

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public uint NotifyOffsetMultiplier { get; }

    public uint NotifyRelativeOffset { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public ushort DescriptorHead { get; }

    public ushort WriteValue { get; }

    public uint WriteWidthBytes { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockQueueNotifyOffsetPlanStatus.Success;

    internal static VirtioBlockQueueNotifyOffsetPlanResult Create(
        VirtioBlockQueueNotifyOffsetPlanStatus status,
        VirtioBlockQueueNotificationMetadataResult notification,
        uint notifyOffsetMultiplier,
        uint notifyRelativeOffset)
    {
        return new VirtioBlockQueueNotifyOffsetPlanResult(
            status,
            notification.Status,
            notification.QueueIndex,
            notification.QueueNotifyOffset,
            notifyOffsetMultiplier,
            notifyRelativeOffset,
            notification.PublishedDescriptorCount,
            notification.PreviousAvailableRingIndex,
            notification.NextAvailableRingIndex,
            notification.DescriptorHead,
            notification.WriteValue,
            notification.WriteWidthBytes,
            isInitialized: true);
    }
}

public static class VirtioBlockQueueNotifyOffsetPlanner
{
    public const bool ConsumesNotificationMetadata = true;
    public const bool ComputesNotifyRelativeOffset = true;
    public const bool ReadsQueueMemory = false;
    public const bool ReadsStatusBuffer = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
    public const bool MutatesCallerOwnedCursorState = false;
    public const bool MutatesCallerOwnedDescriptorStateTable = false;
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

    public static VirtioBlockQueueNotifyOffsetPlanResult Create(
        VirtioBlockQueueNotificationMetadataResult notification,
        uint notifyOffsetMultiplier)
    {
        if (!notification.Succeeded)
        {
            return VirtioBlockQueueNotifyOffsetPlanResult.Create(
                VirtioBlockQueueNotifyOffsetPlanStatus.NotificationMetadataFailed,
                notification,
                notifyOffsetMultiplier,
                notifyRelativeOffset: 0);
        }

        ulong relativeOffset = (ulong)notification.QueueNotifyOffset * notifyOffsetMultiplier;
        if (relativeOffset > uint.MaxValue)
        {
            return VirtioBlockQueueNotifyOffsetPlanResult.Create(
                VirtioBlockQueueNotifyOffsetPlanStatus.RelativeOffsetOverflow,
                notification,
                notifyOffsetMultiplier,
                notifyRelativeOffset: 0);
        }

        return VirtioBlockQueueNotifyOffsetPlanResult.Create(
            VirtioBlockQueueNotifyOffsetPlanStatus.Success,
            notification,
            notifyOffsetMultiplier,
            (uint)relativeOffset);
    }
}

public enum VirtioBlockQueueNotificationRegisterWindowPlanStatus
{
    Success = 0,
    NotifyOffsetPlanFailed = 1,
    UnsupportedWriteWidth = 2,
    RegisterWindowPlanFailed = 3
}

public readonly struct VirtioBlockQueueNotificationRegisterWindowPlanResult
{
    private VirtioBlockQueueNotificationRegisterWindowPlanResult(
        VirtioBlockQueueNotificationRegisterWindowPlanStatus status,
        VirtioBlockQueueNotifyOffsetPlanStatus notifyOffsetPlanStatus,
        VirtioBlockQueueNotificationMetadataStatus notificationMetadataStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier,
        uint notifyRelativeOffset,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        ushort descriptorHead,
        ushort writeValue,
        uint writeWidthBytes,
        VirtioPciBarRegisterWindowAccessPlan accessPlan,
        bool isInitialized)
    {
        Status = status;
        NotifyOffsetPlanStatus = notifyOffsetPlanStatus;
        NotificationMetadataStatus = notificationMetadataStatus;
        RegisterWindowPlanStatus = registerWindowPlanStatus;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
        NotifyRelativeOffset = notifyRelativeOffset;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorHead = descriptorHead;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        AccessPlan = accessPlan;
        IsInitialized = isInitialized;
    }

    public VirtioBlockQueueNotificationRegisterWindowPlanStatus Status { get; }

    public VirtioBlockQueueNotifyOffsetPlanStatus NotifyOffsetPlanStatus { get; }

    public VirtioBlockQueueNotificationMetadataStatus NotificationMetadataStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus { get; }

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public uint NotifyOffsetMultiplier { get; }

    public uint NotifyRelativeOffset { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public ushort DescriptorHead { get; }

    public ushort WriteValue { get; }

    public uint WriteWidthBytes { get; }

    public VirtioPciBarRegisterWindowAccessPlan AccessPlan { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized && Status == VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success;

    internal static VirtioBlockQueueNotificationRegisterWindowPlanResult Create(
        VirtioBlockQueueNotificationRegisterWindowPlanStatus status,
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessPlan accessPlan)
    {
        return new VirtioBlockQueueNotificationRegisterWindowPlanResult(
            status,
            notifyOffset.Status,
            notifyOffset.NotificationMetadataStatus,
            registerWindowPlanStatus,
            notifyOffset.QueueIndex,
            notifyOffset.QueueNotifyOffset,
            notifyOffset.NotifyOffsetMultiplier,
            notifyOffset.NotifyRelativeOffset,
            notifyOffset.PublishedDescriptorCount,
            notifyOffset.PreviousAvailableRingIndex,
            notifyOffset.NextAvailableRingIndex,
            notifyOffset.DescriptorHead,
            notifyOffset.WriteValue,
            notifyOffset.WriteWidthBytes,
            accessPlan,
            isInitialized: true);
    }
}

public static class VirtioBlockQueueNotificationRegisterWindowPlanner
{
    public const bool ConsumesNotifyOffsetPlan = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool PlansMappedRegisterAddresses = true;
    public const bool ReadsQueueMemory = false;
    public const bool ReadsStatusBuffer = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
    public const bool MutatesCallerOwnedCursorState = false;
    public const bool MutatesCallerOwnedDescriptorStateTable = false;
    public const bool PerformsMmio = false;
    public const bool MapsBars = false;
    public const bool ContainsVolatileAccess = false;
    public const bool ReadsDeviceRegisters = false;
    public const bool WritesDeviceRegisters = false;
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

    public static VirtioBlockQueueNotificationRegisterWindowPlanResult PlanAccess(
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset,
        VirtioPciBarRegisterWindowBinding notifyConfiguration)
    {
        if (!notifyOffset.Succeeded)
        {
            return VirtioBlockQueueNotificationRegisterWindowPlanResult.Create(
                VirtioBlockQueueNotificationRegisterWindowPlanStatus.NotifyOffsetPlanFailed,
                notifyOffset,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                default);
        }

        if (notifyOffset.WriteWidthBytes !=
            VirtioPciModernRegisterMap.Notification.NoNotificationDataWriteWidthBytes)
        {
            return VirtioBlockQueueNotificationRegisterWindowPlanResult.Create(
                VirtioBlockQueueNotificationRegisterWindowPlanStatus.UnsupportedWriteWidth,
                notifyOffset,
                VirtioPciBarRegisterWindowPlanStatus.UnsupportedAccessWidth,
                default);
        }

        VirtioPciBarRegisterWindowAccessPlanResult accessPlan =
            VirtioPciBarRegisterWindowPlanner.PlanAccess(
                notifyConfiguration,
                notifyOffset.NotifyRelativeOffset,
                VirtioPciRegisterAccessWidth.Word);
        if (!accessPlan.Succeeded)
        {
            return VirtioBlockQueueNotificationRegisterWindowPlanResult.Create(
                VirtioBlockQueueNotificationRegisterWindowPlanStatus.RegisterWindowPlanFailed,
                notifyOffset,
                accessPlan.Status,
                default);
        }

        return VirtioBlockQueueNotificationRegisterWindowPlanResult.Create(
            VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success,
            notifyOffset,
            accessPlan.Status,
            accessPlan.Plan);
    }
}

public enum VirtioBlockQueueNotificationRegisterWindowExecutionStatus
{
    Success = 0,
    PlanFailed = 1,
    AccessExecutionFailed = 2
}

public readonly struct VirtioBlockQueueNotificationRegisterWindowExecutionResult
{
    private VirtioBlockQueueNotificationRegisterWindowExecutionResult(
        VirtioBlockQueueNotificationRegisterWindowExecutionStatus status,
        VirtioBlockQueueNotificationRegisterWindowPlanStatus planStatus,
        VirtioBlockQueueNotifyOffsetPlanStatus notifyOffsetPlanStatus,
        VirtioBlockQueueNotificationMetadataStatus notificationMetadataStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier,
        uint notifyRelativeOffset,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        ushort descriptorHead,
        ushort writeValue,
        uint writeWidthBytes,
        VirtioPciCapabilityType capabilityType,
        byte barIndex,
        VirtioPciRegisterAccessWidth accessWidth,
        uint accessWidthBytes,
        ulong barRelativeOffset,
        ulong writtenValue,
        bool isInitialized)
    {
        Status = status;
        PlanStatus = planStatus;
        NotifyOffsetPlanStatus = notifyOffsetPlanStatus;
        NotificationMetadataStatus = notificationMetadataStatus;
        RegisterWindowPlanStatus = registerWindowPlanStatus;
        AccessExecutionStatus = accessExecutionStatus;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
        NotifyRelativeOffset = notifyRelativeOffset;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorHead = descriptorHead;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        AccessWidth = accessWidth;
        AccessWidthBytes = accessWidthBytes;
        BarRelativeOffset = barRelativeOffset;
        WrittenValue = writtenValue;
        IsInitialized = isInitialized;
    }

    public VirtioBlockQueueNotificationRegisterWindowExecutionStatus Status { get; }

    public VirtioBlockQueueNotificationRegisterWindowPlanStatus PlanStatus { get; }

    public VirtioBlockQueueNotifyOffsetPlanStatus NotifyOffsetPlanStatus { get; }

    public VirtioBlockQueueNotificationMetadataStatus NotificationMetadataStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus { get; }

    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus { get; }

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public uint NotifyOffsetMultiplier { get; }

    public uint NotifyRelativeOffset { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public ushort DescriptorHead { get; }

    public ushort WriteValue { get; }

    public uint WriteWidthBytes { get; }

    public VirtioPciCapabilityType CapabilityType { get; }

    public byte BarIndex { get; }

    public VirtioPciRegisterAccessWidth AccessWidth { get; }

    public uint AccessWidthBytes { get; }

    public ulong BarRelativeOffset { get; }

    public ulong WrittenValue { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockQueueNotificationRegisterWindowExecutionStatus.Success;

    internal static VirtioBlockQueueNotificationRegisterWindowExecutionResult FromPlan(
        VirtioBlockQueueNotificationRegisterWindowExecutionStatus status,
        VirtioBlockQueueNotificationRegisterWindowPlanResult plan,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus)
    {
        return new VirtioBlockQueueNotificationRegisterWindowExecutionResult(
            status,
            plan.Status,
            plan.NotifyOffsetPlanStatus,
            plan.NotificationMetadataStatus,
            plan.RegisterWindowPlanStatus,
            accessExecutionStatus,
            plan.QueueIndex,
            plan.QueueNotifyOffset,
            plan.NotifyOffsetMultiplier,
            plan.NotifyRelativeOffset,
            plan.PublishedDescriptorCount,
            plan.PreviousAvailableRingIndex,
            plan.NextAvailableRingIndex,
            plan.DescriptorHead,
            plan.WriteValue,
            plan.WriteWidthBytes,
            plan.AccessPlan.CapabilityType,
            plan.AccessPlan.BarIndex,
            plan.AccessPlan.Width,
            plan.AccessPlan.WidthBytes,
            plan.AccessPlan.BarRelativeOffset,
            plan.WriteValue,
            isInitialized: true);
    }
}

public static class VirtioBlockQueueNotificationRegisterWindowExecutor
{
    public const bool InvokesRegisterWindowPlanner = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool WritesCallerOwnedWindowBuffer = true;
    public const bool ReadsQueueMemory = false;
    public const bool ReadsStatusBuffer = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
    public const bool MutatesCallerOwnedCursorState = false;
    public const bool MutatesCallerOwnedDescriptorStateTable = false;
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

    public static VirtioBlockQueueNotificationRegisterWindowExecutionResult Execute(
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset,
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        Span<byte> notifyWindowBytes)
    {
        VirtioBlockQueueNotificationRegisterWindowPlanResult plan =
            VirtioBlockQueueNotificationRegisterWindowPlanner.PlanAccess(
                notifyOffset,
                notifyConfiguration);
        if (!plan.Succeeded)
        {
            return VirtioBlockQueueNotificationRegisterWindowExecutionResult.FromPlan(
                VirtioBlockQueueNotificationRegisterWindowExecutionStatus.PlanFailed,
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
            return VirtioBlockQueueNotificationRegisterWindowExecutionResult.FromPlan(
                VirtioBlockQueueNotificationRegisterWindowExecutionStatus.AccessExecutionFailed,
                plan,
                write.Status);
        }

        return VirtioBlockQueueNotificationRegisterWindowExecutionResult.FromPlan(
            VirtioBlockQueueNotificationRegisterWindowExecutionStatus.Success,
            plan,
            write.Status);
    }
}

public readonly struct VirtioBlockQueueNotificationBarrierReadiness
{
    public VirtioBlockQueueNotificationBarrierReadiness(
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

    public static VirtioBlockQueueNotificationBarrierReadiness Ready(uint barrierSequence)
    {
        return new VirtioBlockQueueNotificationBarrierReadiness(
            descriptorWritesVisible: true,
            availableRingWritesVisible: true,
            orderedBeforeNotify: true,
            barrierSequence);
    }
}

public enum VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus
{
    Success = 0,
    PlanFailed = 1,
    MemoryBarrierNotReady = 2,
    AccessExecutionFailed = 3
}

public readonly struct VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult
{
    private VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult(
        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus status,
        VirtioBlockQueueNotificationRegisterWindowPlanStatus planStatus,
        VirtioBlockQueueNotifyOffsetPlanStatus notifyOffsetPlanStatus,
        VirtioBlockQueueNotificationMetadataStatus notificationMetadataStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier,
        uint notifyRelativeOffset,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        ushort descriptorHead,
        ushort writeValue,
        uint writeWidthBytes,
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
        PlanStatus = planStatus;
        NotifyOffsetPlanStatus = notifyOffsetPlanStatus;
        NotificationMetadataStatus = notificationMetadataStatus;
        RegisterWindowPlanStatus = registerWindowPlanStatus;
        AccessExecutionStatus = accessExecutionStatus;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
        NotifyRelativeOffset = notifyRelativeOffset;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorHead = descriptorHead;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
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

    public VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus Status { get; }

    public VirtioBlockQueueNotificationRegisterWindowPlanStatus PlanStatus { get; }

    public VirtioBlockQueueNotifyOffsetPlanStatus NotifyOffsetPlanStatus { get; }

    public VirtioBlockQueueNotificationMetadataStatus NotificationMetadataStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus { get; }

    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus { get; }

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public uint NotifyOffsetMultiplier { get; }

    public uint NotifyRelativeOffset { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public ushort DescriptorHead { get; }

    public ushort WriteValue { get; }

    public uint WriteWidthBytes { get; }

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
        Status == VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.Success;

    internal static VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult FromPlan(
        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus status,
        VirtioBlockQueueNotificationRegisterWindowPlanResult plan,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioBlockQueueNotificationBarrierReadiness barrier,
        bool registerWindowExecutionAttempted)
    {
        return new VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult(
            status,
            plan.Status,
            plan.NotifyOffsetPlanStatus,
            plan.NotificationMetadataStatus,
            plan.RegisterWindowPlanStatus,
            accessExecutionStatus,
            plan.QueueIndex,
            plan.QueueNotifyOffset,
            plan.NotifyOffsetMultiplier,
            plan.NotifyRelativeOffset,
            plan.PublishedDescriptorCount,
            plan.PreviousAvailableRingIndex,
            plan.NextAvailableRingIndex,
            plan.DescriptorHead,
            plan.WriteValue,
            plan.WriteWidthBytes,
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
}

public static class VirtioBlockQueueNotificationRegisterWindowBarrierExecutor
{
    public const bool RequiresBarrierReadiness = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool InvokesRegisterWindowAccessExecutor = true;
    public const bool WritesCallerOwnedWindowBuffer = true;
    public const bool ReadsQueueMemory = false;
    public const bool ReadsStatusBuffer = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
    public const bool MutatesCallerOwnedCursorState = false;
    public const bool MutatesCallerOwnedDescriptorStateTable = false;
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

    public static VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult Execute(
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset,
        VirtioBlockQueueNotificationBarrierReadiness barrier,
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        Span<byte> notifyWindowBytes)
    {
        VirtioBlockQueueNotificationRegisterWindowPlanResult plan =
            VirtioBlockQueueNotificationRegisterWindowPlanner.PlanAccess(
                notifyOffset,
                notifyConfiguration);
        if (!plan.Succeeded)
        {
            return VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(
                VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.PlanFailed,
                plan,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                barrier,
                registerWindowExecutionAttempted: false);
        }

        if (!barrier.Succeeded)
        {
            return VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(
                VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady,
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
            return VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(
                VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed,
                plan,
                write.Status,
                barrier,
                registerWindowExecutionAttempted: true);
        }

        return VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult.FromPlan(
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.Success,
            plan,
            write.Status,
            barrier,
            registerWindowExecutionAttempted: true);
    }
}

public enum VirtioBlockReadQueueNotifyReadinessStatus
{
    Success = 0,
    PublicationFailed = 1,
    NotificationMetadataFailed = 2,
    NotifyOffsetPlanFailed = 3,
    RegisterWindowPlanFailed = 4,
    MemoryBarrierNotReady = 5,
    RegisterWindowAccessFailed = 6
}

public readonly struct VirtioBlockReadQueueNotifyReadinessResult
{
    private VirtioBlockReadQueueNotifyReadinessResult(
        VirtioBlockReadQueueNotifyReadinessStatus status,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        ushort queueIndex,
        ushort queueNotifyOffset,
        VirtioBlockQueueNotificationMetadataResult notificationMetadata,
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset,
        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult notifyExecution,
        VirtioBlockQueueNotificationBarrierReadiness barrier,
        uint notifyOffsetMultiplier,
        bool notificationMetadataAttempted,
        bool notifyOffsetPlanAttempted,
        bool notifyExecutionAttempted,
        bool isInitialized)
    {
        Status = status;
        ReadQueuePublishStatus = readQueuePublishStatus;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotificationMetadata = notificationMetadata;
        NotifyOffset = notifyOffset;
        NotifyExecution = notifyExecution;
        Barrier = barrier;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
        NotificationMetadataAttempted = notificationMetadataAttempted;
        NotifyOffsetPlanAttempted = notifyOffsetPlanAttempted;
        NotifyExecutionAttempted = notifyExecutionAttempted;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueueNotifyReadinessStatus Status { get; }

    public VirtioBlockReadQueuePublishStatus ReadQueuePublishStatus { get; }

    public VirtioBlockQueueNotificationMetadataResult NotificationMetadata { get; }

    public VirtioBlockQueueNotifyOffsetPlanResult NotifyOffset { get; }

    public VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult NotifyExecution { get; }

    public VirtioBlockQueueNotificationBarrierReadiness Barrier { get; }

    public uint NotifyOffsetMultiplier { get; }

    public bool NotificationMetadataAttempted { get; }

    public bool NotifyOffsetPlanAttempted { get; }

    public bool NotifyExecutionAttempted { get; }

    public VirtioBlockQueueNotificationMetadataStatus NotificationMetadataStatus =>
        NotificationMetadataAttempted ?
            NotificationMetadata.Status :
            VirtioBlockQueueNotificationMetadataStatus.Success;

    public VirtioBlockQueueNotifyOffsetPlanStatus NotifyOffsetPlanStatus =>
        NotifyOffsetPlanAttempted ?
            NotifyOffset.Status :
            VirtioBlockQueueNotifyOffsetPlanStatus.Success;

    public VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus NotifyExecutionStatus =>
        NotifyExecutionAttempted ?
            NotifyExecution.Status :
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.Success;

    public VirtioBlockQueueNotificationRegisterWindowPlanStatus NotifyRegisterWindowPlanStatus =>
        NotifyExecutionAttempted ?
            NotifyExecution.PlanStatus :
            VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success;

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus =>
        NotifyExecutionAttempted ?
            NotifyExecution.RegisterWindowPlanStatus :
            VirtioPciBarRegisterWindowPlanStatus.Success;

    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus =>
        NotifyExecutionAttempted ?
            NotifyExecution.AccessExecutionStatus :
            VirtioPciBarRegisterWindowAccessExecutionStatus.Success;

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public uint NotifyRelativeOffset =>
        NotifyOffsetPlanAttempted ?
            NotifyOffset.NotifyRelativeOffset :
            0;

    public ushort PublishedDescriptorCount => NotificationMetadata.PublishedDescriptorCount;

    public ushort PreviousAvailableRingIndex => NotificationMetadata.PreviousAvailableRingIndex;

    public ushort NextAvailableRingIndex => NotificationMetadata.NextAvailableRingIndex;

    public ushort DescriptorHead => NotificationMetadata.DescriptorHead;

    public ushort WriteValue => NotificationMetadata.WriteValue;

    public uint WriteWidthBytes => NotificationMetadata.WriteWidthBytes;

    public bool DescriptorWritesVisible => Barrier.DescriptorWritesVisible;

    public bool AvailableRingWritesVisible => Barrier.AvailableRingWritesVisible;

    public bool OrderedBeforeNotify => Barrier.OrderedBeforeNotify;

    public uint BarrierSequence => Barrier.BarrierSequence;

    public bool BarrierSucceeded => Barrier.Succeeded;

    public bool RegisterWindowExecutionAttempted =>
        NotifyExecutionAttempted &&
        NotifyExecution.RegisterWindowExecutionAttempted;

    public VirtioPciCapabilityType CapabilityType =>
        NotifyExecutionAttempted ?
            NotifyExecution.CapabilityType :
            default;

    public byte BarIndex =>
        NotifyExecutionAttempted ?
            NotifyExecution.BarIndex :
            (byte)0;

    public VirtioPciRegisterAccessWidth AccessWidth =>
        NotifyExecutionAttempted ?
            NotifyExecution.AccessWidth :
            default;

    public uint AccessWidthBytes =>
        NotifyExecutionAttempted ?
            NotifyExecution.AccessWidthBytes :
            0;

    public ulong BarRelativeOffset =>
        NotifyExecutionAttempted ?
            NotifyExecution.BarRelativeOffset :
            0;

    public ulong WrittenValue =>
        NotifyExecutionAttempted ?
            NotifyExecution.WrittenValue :
            0;

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockReadQueueNotifyReadinessStatus.Success;

    internal static VirtioBlockReadQueueNotifyReadinessResult Create(
        VirtioBlockReadQueueNotifyReadinessStatus status,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        ushort queueIndex,
        ushort queueNotifyOffset,
        VirtioBlockQueueNotificationMetadataResult notificationMetadata,
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset,
        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult notifyExecution,
        VirtioBlockQueueNotificationBarrierReadiness barrier,
        uint notifyOffsetMultiplier,
        bool notificationMetadataAttempted,
        bool notifyOffsetPlanAttempted,
        bool notifyExecutionAttempted)
    {
        return new VirtioBlockReadQueueNotifyReadinessResult(
            status,
            readQueuePublishStatus,
            queueIndex,
            queueNotifyOffset,
            notificationMetadata,
            notifyOffset,
            notifyExecution,
            barrier,
            notifyOffsetMultiplier,
            notificationMetadataAttempted,
            notifyOffsetPlanAttempted,
            notifyExecutionAttempted,
            isInitialized: true);
    }
}

public static class VirtioBlockReadQueueNotifyReadinessExecutor
{
    public const bool ConsumesReadQueuePublication = true;
    public const bool InvokesNotificationMetadataPlanner = true;
    public const bool InvokesNotifyOffsetPlanner = true;
    public const bool RequiresBarrierReadiness = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool InvokesRegisterWindowBarrierExecutor = true;
    public const bool WritesCallerOwnedWindowBuffer = true;
    public const bool ReadsQueueMemory = false;
    public const bool ReadsStatusBuffer = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
    public const bool MutatesCallerOwnedCursorState = false;
    public const bool MutatesCallerOwnedDescriptorStateTable = false;
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

    public static VirtioBlockReadQueueNotifyReadinessResult Execute(
        VirtioBlockReadQueuePublishResult publish,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier,
        VirtioBlockQueueNotificationBarrierReadiness barrier,
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        Span<byte> notifyWindowBytes)
    {
        if (!publish.Succeeded)
        {
            return VirtioBlockReadQueueNotifyReadinessResult.Create(
                VirtioBlockReadQueueNotifyReadinessStatus.PublicationFailed,
                publish.Status,
                queueIndex,
                queueNotifyOffset,
                default,
                default,
                default,
                barrier,
                notifyOffsetMultiplier,
                notificationMetadataAttempted: false,
                notifyOffsetPlanAttempted: false,
                notifyExecutionAttempted: false);
        }

        VirtioBlockQueueNotificationMetadataResult notificationMetadata =
            VirtioBlockQueueNotificationMetadataPlanner.Create(
                publish,
                queueIndex,
                queueNotifyOffset);
        if (!notificationMetadata.Succeeded)
        {
            return VirtioBlockReadQueueNotifyReadinessResult.Create(
                VirtioBlockReadQueueNotifyReadinessStatus.NotificationMetadataFailed,
                publish.Status,
                queueIndex,
                queueNotifyOffset,
                notificationMetadata,
                default,
                default,
                barrier,
                notifyOffsetMultiplier,
                notificationMetadataAttempted: true,
                notifyOffsetPlanAttempted: false,
                notifyExecutionAttempted: false);
        }

        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            VirtioBlockQueueNotifyOffsetPlanner.Create(
                notificationMetadata,
                notifyOffsetMultiplier);
        if (!notifyOffset.Succeeded)
        {
            return VirtioBlockReadQueueNotifyReadinessResult.Create(
                VirtioBlockReadQueueNotifyReadinessStatus.NotifyOffsetPlanFailed,
                publish.Status,
                queueIndex,
                queueNotifyOffset,
                notificationMetadata,
                notifyOffset,
                default,
                barrier,
                notifyOffsetMultiplier,
                notificationMetadataAttempted: true,
                notifyOffsetPlanAttempted: true,
                notifyExecutionAttempted: false);
        }

        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult notifyExecution =
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notifyOffset,
                barrier,
                notifyConfiguration,
                notifyWindowBytes);
        if (notifyExecution.Status ==
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.PlanFailed)
        {
            return VirtioBlockReadQueueNotifyReadinessResult.Create(
                VirtioBlockReadQueueNotifyReadinessStatus.RegisterWindowPlanFailed,
                publish.Status,
                queueIndex,
                queueNotifyOffset,
                notificationMetadata,
                notifyOffset,
                notifyExecution,
                barrier,
                notifyOffsetMultiplier,
                notificationMetadataAttempted: true,
                notifyOffsetPlanAttempted: true,
                notifyExecutionAttempted: true);
        }

        if (notifyExecution.Status ==
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady)
        {
            return VirtioBlockReadQueueNotifyReadinessResult.Create(
                VirtioBlockReadQueueNotifyReadinessStatus.MemoryBarrierNotReady,
                publish.Status,
                queueIndex,
                queueNotifyOffset,
                notificationMetadata,
                notifyOffset,
                notifyExecution,
                barrier,
                notifyOffsetMultiplier,
                notificationMetadataAttempted: true,
                notifyOffsetPlanAttempted: true,
                notifyExecutionAttempted: true);
        }

        if (notifyExecution.Status ==
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed)
        {
            return VirtioBlockReadQueueNotifyReadinessResult.Create(
                VirtioBlockReadQueueNotifyReadinessStatus.RegisterWindowAccessFailed,
                publish.Status,
                queueIndex,
                queueNotifyOffset,
                notificationMetadata,
                notifyOffset,
                notifyExecution,
                barrier,
                notifyOffsetMultiplier,
                notificationMetadataAttempted: true,
                notifyOffsetPlanAttempted: true,
                notifyExecutionAttempted: true);
        }

        return VirtioBlockReadQueueNotifyReadinessResult.Create(
            VirtioBlockReadQueueNotifyReadinessStatus.Success,
            publish.Status,
            queueIndex,
            queueNotifyOffset,
            notificationMetadata,
            notifyOffset,
            notifyExecution,
            barrier,
            notifyOffsetMultiplier,
            notificationMetadataAttempted: true,
            notifyOffsetPlanAttempted: true,
            notifyExecutionAttempted: true);
    }
}

public enum VirtioBlockReadQueueNotifyCompletionStatus
{
    Success = 0,
    NotifyReadinessFailed = 1,
    CompletionFailed = 2
}

public readonly struct VirtioBlockReadQueueNotifyCompletionResult
{
    private VirtioBlockReadQueueNotifyCompletionResult(
        VirtioBlockReadQueueNotifyCompletionStatus status,
        VirtioBlockReadQueueNotifyReadinessResult notifyReadiness,
        VirtioBlockReadQueueCompletionResult completion,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        bool completionAttempted,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex,
        bool isInitialized)
    {
        Status = status;
        NotifyReadiness = notifyReadiness;
        Completion = completion;
        ReadQueuePublishStatus = readQueuePublishStatus;
        CompletionAttempted = completionAttempted;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        CurrentAvailableRingIndex = currentAvailableRingIndex;
        PreviousUsedRingIndex = previousUsedRingIndex;
        CurrentUsedRingIndex = currentUsedRingIndex;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueueNotifyCompletionStatus Status { get; }

    public VirtioBlockReadQueueNotifyReadinessResult NotifyReadiness { get; }

    public VirtioBlockReadQueueCompletionResult Completion { get; }

    public VirtioBlockReadQueuePublishStatus ReadQueuePublishStatus { get; }

    public bool CompletionAttempted { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort CurrentAvailableRingIndex { get; }

    public ushort PreviousUsedRingIndex { get; }

    public ushort CurrentUsedRingIndex { get; }

    public VirtioBlockReadQueueNotifyReadinessStatus NotifyReadinessStatus =>
        NotifyReadiness.Status;

    public VirtioBlockQueueNotificationMetadataStatus NotificationMetadataStatus =>
        NotifyReadiness.NotificationMetadataStatus;

    public VirtioBlockQueueNotifyOffsetPlanStatus NotifyOffsetPlanStatus =>
        NotifyReadiness.NotifyOffsetPlanStatus;

    public VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus NotifyExecutionStatus =>
        NotifyReadiness.NotifyExecutionStatus;

    public VirtioBlockReadQueueCompletionStatus ReadQueueCompletionStatus =>
        CompletionAttempted ?
            Completion.Status :
            VirtioBlockReadQueueCompletionStatus.Success;

    public VirtioBlockQueueCursorReclaimStatus CursorReclaimStatus =>
        CompletionAttempted ?
            Completion.CursorReclaimStatus :
            VirtioBlockQueueCursorReclaimStatus.Success;

    public VirtioBlockDescriptorReclaimPlanStatus ReclaimPlanStatus =>
        CompletionAttempted ?
            Completion.ReclaimPlanStatus :
            VirtioBlockDescriptorReclaimPlanStatus.Success;

    public VirtioBlockReadLifecycleCompletionStatus CompletionStatus =>
        CompletionAttempted ?
            Completion.CompletionStatus :
            VirtioBlockReadLifecycleCompletionStatus.Success;

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus =>
        CompletionAttempted ?
            Completion.CompletionReaderStatus :
            VirtioBlockUsedRingCompletionReaderStatus.Success;

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus =>
        CompletionAttempted ?
            Completion.OwnershipStatus :
            VirtioBlockDescriptorOwnershipStatus.Success;

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockReadQueueNotifyCompletionStatus.Success;

    internal static VirtioBlockReadQueueNotifyCompletionResult Create(
        VirtioBlockReadQueueNotifyCompletionStatus status,
        VirtioBlockReadQueueNotifyReadinessResult notifyReadiness,
        VirtioBlockReadQueueCompletionResult completion,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        bool completionAttempted,
        ushort previousAvailableRingIndex,
        ushort currentAvailableRingIndex,
        ushort previousUsedRingIndex,
        ushort currentUsedRingIndex)
    {
        return new VirtioBlockReadQueueNotifyCompletionResult(
            status,
            notifyReadiness,
            completion,
            readQueuePublishStatus,
            completionAttempted,
            previousAvailableRingIndex,
            currentAvailableRingIndex,
            previousUsedRingIndex,
            currentUsedRingIndex,
            isInitialized: true);
    }
}

public static class VirtioBlockReadQueueNotifyCompletionPlanner
{
    public const bool ConsumesReadNotifyReadiness = true;
    public const bool RequiresSuccessfulReadNotifyReadiness = true;
    public const bool DelegatesReadQueueCompletionPlanner = true;
    public const bool RequiresCallerOwnedCursorState = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedStatusBuffer = true;
    public const bool RequiresCallerOwnedDescriptorStateTable = true;
    public const bool ReadsQueueMemory = true;
    public const bool ReadsStatusBuffer = true;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
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

    public static VirtioBlockReadQueueNotifyCompletionResult CompleteAfterNotify(
        VirtioBlockReadQueuePublishResult publish,
        VirtioBlockReadQueueNotifyReadinessResult notifyReadiness,
        ReadOnlySpan<byte> queueMemory,
        ReadOnlySpan<byte> statusBuffer,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        ref VirtioBlockQueueCursorState cursor)
    {
        ushort previousAvailableRingIndex = cursor.AvailableRingIndex;
        ushort previousUsedRingIndex = cursor.UsedRingIndex;
        if (!notifyReadiness.Succeeded)
        {
            return VirtioBlockReadQueueNotifyCompletionResult.Create(
                VirtioBlockReadQueueNotifyCompletionStatus.NotifyReadinessFailed,
                notifyReadiness,
                default,
                publish.Status,
                completionAttempted: false,
                previousAvailableRingIndex,
                cursor.AvailableRingIndex,
                previousUsedRingIndex,
                cursor.UsedRingIndex);
        }

        VirtioBlockReadQueueCompletionResult completion =
            VirtioBlockReadQueueCompletionPlanner.CompleteRead(
                publish,
                queueMemory,
                statusBuffer,
                descriptorStates,
                ref cursor);
        if (!completion.Succeeded)
        {
            return VirtioBlockReadQueueNotifyCompletionResult.Create(
                VirtioBlockReadQueueNotifyCompletionStatus.CompletionFailed,
                notifyReadiness,
                completion,
                publish.Status,
                completionAttempted: true,
                previousAvailableRingIndex,
                cursor.AvailableRingIndex,
                previousUsedRingIndex,
                cursor.UsedRingIndex);
        }

        return VirtioBlockReadQueueNotifyCompletionResult.Create(
            VirtioBlockReadQueueNotifyCompletionStatus.Success,
            notifyReadiness,
            completion,
            publish.Status,
            completionAttempted: true,
            previousAvailableRingIndex,
            cursor.AvailableRingIndex,
            previousUsedRingIndex,
            cursor.UsedRingIndex);
    }
}

public enum VirtioBlockReadQueueNotifyBlockReadProjectionStatus
{
    Success = 0,
    NotifyReadinessFailed = 1,
    CompletionFailed = 2
}

public readonly struct VirtioBlockReadQueueNotifyBlockReadProjectionResult
{
    private VirtioBlockReadQueueNotifyBlockReadProjectionResult(
        VirtioBlockReadQueueNotifyBlockReadProjectionStatus status,
        BlockReadResult blockRead,
        VirtioBlockReadQueueNotifyCompletionStatus notifyCompletionStatus,
        VirtioBlockReadQueueNotifyReadinessStatus notifyReadinessStatus,
        VirtioBlockReadQueueCompletionStatus readQueueCompletionStatus,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        VirtioBlockQueueCursorReclaimStatus cursorReclaimStatus,
        VirtioBlockDescriptorReclaimPlanStatus reclaimPlanStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        VirtioBlockDescriptorOwnershipStatus ownershipStatus,
        uint requestedBlockCount,
        ulong requestedByteCount,
        uint completedByteCount,
        bool isInitialized)
    {
        Status = status;
        BlockRead = blockRead;
        NotifyCompletionStatus = notifyCompletionStatus;
        NotifyReadinessStatus = notifyReadinessStatus;
        ReadQueueCompletionStatus = readQueueCompletionStatus;
        ReadQueuePublishStatus = readQueuePublishStatus;
        CursorReclaimStatus = cursorReclaimStatus;
        ReclaimPlanStatus = reclaimPlanStatus;
        CompletionReaderStatus = completionReaderStatus;
        OwnershipStatus = ownershipStatus;
        RequestedBlockCount = requestedBlockCount;
        RequestedByteCount = requestedByteCount;
        CompletedByteCount = completedByteCount;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueueNotifyBlockReadProjectionStatus Status { get; }

    public BlockReadResult BlockRead { get; }

    public VirtioBlockReadQueueNotifyCompletionStatus NotifyCompletionStatus { get; }

    public VirtioBlockReadQueueNotifyReadinessStatus NotifyReadinessStatus { get; }

    public VirtioBlockReadQueueCompletionStatus ReadQueueCompletionStatus { get; }

    public VirtioBlockReadQueuePublishStatus ReadQueuePublishStatus { get; }

    public VirtioBlockQueueCursorReclaimStatus CursorReclaimStatus { get; }

    public VirtioBlockDescriptorReclaimPlanStatus ReclaimPlanStatus { get; }

    public VirtioBlockUsedRingCompletionReaderStatus CompletionReaderStatus { get; }

    public VirtioBlockDescriptorOwnershipStatus OwnershipStatus { get; }

    public uint RequestedBlockCount { get; }

    public ulong RequestedByteCount { get; }

    public uint CompletedByteCount { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockReadQueueNotifyBlockReadProjectionStatus.Success;

    internal static VirtioBlockReadQueueNotifyBlockReadProjectionResult Create(
        VirtioBlockReadQueueNotifyBlockReadProjectionStatus status,
        BlockReadResult blockRead,
        VirtioBlockReadQueueNotifyCompletionResult notifyCompletion,
        VirtioBlockReadQueueBlockReadProjectionResult readQueueProjection)
    {
        bool completionAttempted = notifyCompletion.CompletionAttempted;
        return new VirtioBlockReadQueueNotifyBlockReadProjectionResult(
            status,
            blockRead,
            notifyCompletion.Status,
            notifyCompletion.NotifyReadinessStatus,
            notifyCompletion.ReadQueueCompletionStatus,
            notifyCompletion.ReadQueuePublishStatus,
            notifyCompletion.CursorReclaimStatus,
            notifyCompletion.ReclaimPlanStatus,
            notifyCompletion.CompletionReaderStatus,
            notifyCompletion.OwnershipStatus,
            completionAttempted ? readQueueProjection.RequestedBlockCount : 0,
            completionAttempted ? readQueueProjection.RequestedByteCount : 0,
            completionAttempted ? readQueueProjection.CompletedByteCount : 0,
            isInitialized: true);
    }
}

public static class VirtioBlockReadQueueNotifyBlockReadProjector
{
    public const bool ConsumesReadNotifyCompletionResult = true;
    public const bool DelegatesReadQueueBlockReadProjector = true;
    public const bool ReadsQueueMemory = false;
    public const bool ReadsStatusBuffer = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
    public const bool MutatesCallerOwnedCursorState = false;
    public const bool MutatesCallerOwnedDescriptorStateTable = false;
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

    public static VirtioBlockReadQueueNotifyBlockReadProjectionResult Project(
        VirtioBlockReadQueueNotifyCompletionResult notifyCompletion)
    {
        if (!notifyCompletion.CompletionAttempted)
        {
            return VirtioBlockReadQueueNotifyBlockReadProjectionResult.Create(
                VirtioBlockReadQueueNotifyBlockReadProjectionStatus.NotifyReadinessFailed,
                BlockReadResult.Fail(BlockDeviceStatus.IoError),
                notifyCompletion,
                default);
        }

        VirtioBlockReadQueueBlockReadProjectionResult readQueueProjection =
            VirtioBlockReadQueueBlockReadProjector.Project(notifyCompletion.Completion);
        if (notifyCompletion.Succeeded && readQueueProjection.Succeeded)
        {
            return VirtioBlockReadQueueNotifyBlockReadProjectionResult.Create(
                VirtioBlockReadQueueNotifyBlockReadProjectionStatus.Success,
                readQueueProjection.BlockRead,
                notifyCompletion,
                readQueueProjection);
        }

        return VirtioBlockReadQueueNotifyBlockReadProjectionResult.Create(
            VirtioBlockReadQueueNotifyBlockReadProjectionStatus.CompletionFailed,
            readQueueProjection.BlockRead,
            notifyCompletion,
            readQueueProjection);
    }
}

public enum VirtioBlockReadQueuePipelineStatus
{
    Success = 0,
    PublicationFailed = 1,
    NotifyReadinessFailed = 2,
    NotifyCompletionFailed = 3,
    BlockReadProjectionFailed = 4
}

public readonly struct VirtioBlockReadQueuePipelineResult
{
    private VirtioBlockReadQueuePipelineResult(
        VirtioBlockReadQueuePipelineStatus status,
        VirtioBlockReadQueuePublishResult publish,
        VirtioBlockReadQueueNotifyReadinessResult notifyReadiness,
        VirtioBlockReadQueueNotifyCompletionResult notifyCompletion,
        VirtioBlockReadQueueNotifyBlockReadProjectionResult blockProjection,
        bool notifyReadinessAttempted,
        bool notifyCompletionAttempted,
        bool blockProjectionAttempted,
        bool isInitialized)
    {
        Status = status;
        Publish = publish;
        NotifyReadiness = notifyReadiness;
        NotifyCompletion = notifyCompletion;
        BlockProjection = blockProjection;
        NotifyReadinessAttempted = notifyReadinessAttempted;
        NotifyCompletionAttempted = notifyCompletionAttempted;
        BlockProjectionAttempted = blockProjectionAttempted;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueuePipelineStatus Status { get; }

    public VirtioBlockReadQueuePublishResult Publish { get; }

    public VirtioBlockReadQueueNotifyReadinessResult NotifyReadiness { get; }

    public VirtioBlockReadQueueNotifyCompletionResult NotifyCompletion { get; }

    public VirtioBlockReadQueueNotifyBlockReadProjectionResult BlockProjection { get; }

    public bool NotifyReadinessAttempted { get; }

    public bool NotifyCompletionAttempted { get; }

    public bool BlockProjectionAttempted { get; }

    public VirtioBlockReadQueuePublishStatus PublishStatus => Publish.Status;

    public VirtioBlockReadQueueNotifyReadinessStatus NotifyReadinessStatus =>
        NotifyReadinessAttempted ?
            NotifyReadiness.Status :
            VirtioBlockReadQueueNotifyReadinessStatus.Success;

    public VirtioBlockReadQueueNotifyCompletionStatus NotifyCompletionStatus =>
        NotifyCompletionAttempted ?
            NotifyCompletion.Status :
            VirtioBlockReadQueueNotifyCompletionStatus.Success;

    public VirtioBlockReadQueueNotifyBlockReadProjectionStatus BlockProjectionStatus =>
        BlockProjectionAttempted ?
            BlockProjection.Status :
            VirtioBlockReadQueueNotifyBlockReadProjectionStatus.Success;

    public BlockReadResult BlockRead =>
        BlockProjectionAttempted ?
            BlockProjection.BlockRead :
            BlockReadResult.Fail(BlockDeviceStatus.IoError);

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockReadQueuePipelineStatus.Success;

    internal static VirtioBlockReadQueuePipelineResult Create(
        VirtioBlockReadQueuePipelineStatus status,
        VirtioBlockReadQueuePublishResult publish,
        VirtioBlockReadQueueNotifyReadinessResult notifyReadiness,
        VirtioBlockReadQueueNotifyCompletionResult notifyCompletion,
        VirtioBlockReadQueueNotifyBlockReadProjectionResult blockProjection,
        bool notifyReadinessAttempted,
        bool notifyCompletionAttempted,
        bool blockProjectionAttempted)
    {
        return new VirtioBlockReadQueuePipelineResult(
            status,
            publish,
            notifyReadiness,
            notifyCompletion,
            blockProjection,
            notifyReadinessAttempted,
            notifyCompletionAttempted,
            blockProjectionAttempted,
            isInitialized: true);
    }
}

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

public enum VirtioBlockReadQueuePipelineAdapterStatus
{
    Success = 0,
    RequestHeaderTooSmall = 1,
    DestinationTooSmall = 2,
    StatusBufferTooSmall = 3,
    QueuePlanFailed = 4,
    QueueMemoryTooSmall = 5,
    DescriptorStateTableTooSmall = 6,
    NotifyWindowTooSmall = 7,
    PipelineFailed = 8
}

public readonly struct VirtioBlockReadQueuePipelineAdapterResult
{
    private VirtioBlockReadQueuePipelineAdapterResult(
        VirtioBlockReadQueuePipelineAdapterStatus status,
        VirtioBlockReadQueuePipelineResult pipeline,
        bool isInitialized,
        bool pipelineAttempted,
        ulong requiredByteCount,
        ulong availableByteCount,
        uint requiredElementCount,
        uint availableElementCount,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        VirtioPciBarRegisterWindowPlanStatus notifyWindowPlanStatus)
    {
        Status = status;
        Pipeline = pipeline;
        IsInitialized = isInitialized;
        PipelineAttempted = pipelineAttempted;
        RequiredByteCount = requiredByteCount;
        AvailableByteCount = availableByteCount;
        RequiredElementCount = requiredElementCount;
        AvailableElementCount = availableElementCount;
        QueuePlanStatus = queuePlanStatus;
        NotifyWindowPlanStatus = notifyWindowPlanStatus;
    }

    public VirtioBlockReadQueuePipelineAdapterStatus Status { get; }

    public VirtioBlockReadQueuePipelineResult Pipeline { get; }

    public bool IsInitialized { get; }

    public bool PipelineAttempted { get; }

    public ulong RequiredByteCount { get; }

    public ulong AvailableByteCount { get; }

    public uint RequiredElementCount { get; }

    public uint AvailableElementCount { get; }

    public VirtqueueSplitRingPlanStatus QueuePlanStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus NotifyWindowPlanStatus { get; }

    public VirtioBlockReadQueuePipelineStatus PipelineStatus =>
        PipelineAttempted ?
            Pipeline.Status :
            VirtioBlockReadQueuePipelineStatus.Success;

    public BlockReadResult BlockRead =>
        PipelineAttempted ?
            Pipeline.BlockRead :
            BlockReadResult.Fail(BlockDeviceStatus.IoError);

    public bool Succeeded =>
        IsInitialized && Status == VirtioBlockReadQueuePipelineAdapterStatus.Success;

    internal static VirtioBlockReadQueuePipelineAdapterResult PreflightFailure(
        VirtioBlockReadQueuePipelineAdapterStatus status,
        ulong requiredByteCount,
        ulong availableByteCount,
        uint requiredElementCount,
        uint availableElementCount,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        VirtioPciBarRegisterWindowPlanStatus notifyWindowPlanStatus)
    {
        return new VirtioBlockReadQueuePipelineAdapterResult(
            status,
            default,
            isInitialized: true,
            pipelineAttempted: false,
            requiredByteCount,
            availableByteCount,
            requiredElementCount,
            availableElementCount,
            queuePlanStatus,
            notifyWindowPlanStatus);
    }

    internal static VirtioBlockReadQueuePipelineAdapterResult FromPipeline(
        VirtioBlockReadQueuePipelineResult pipeline)
    {
        VirtioBlockReadQueuePipelineAdapterStatus status = pipeline.Succeeded ?
            VirtioBlockReadQueuePipelineAdapterStatus.Success :
            VirtioBlockReadQueuePipelineAdapterStatus.PipelineFailed;
        return new VirtioBlockReadQueuePipelineAdapterResult(
            status,
            pipeline,
            isInitialized: true,
            pipelineAttempted: true,
            requiredByteCount: 0,
            availableByteCount: 0,
            requiredElementCount: 0,
            availableElementCount: 0,
            VirtqueueSplitRingPlanStatus.Success,
            VirtioPciBarRegisterWindowPlanStatus.Success);
    }
}

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

public enum VirtioBlockReadOnlyReadSessionStatus
{
    Success = 0,
    AdapterFailed = 1
}

public readonly struct VirtioBlockReadOnlyReadSessionResult
{
    private VirtioBlockReadOnlyReadSessionResult(
        VirtioBlockReadOnlyReadSessionStatus status,
        VirtioBlockReadQueuePipelineAdapterResult adapter,
        bool isInitialized,
        VirtioBlockQueueCursorState cursorBefore,
        VirtioBlockQueueCursorState cursorAfter)
    {
        Status = status;
        Adapter = adapter;
        IsInitialized = isInitialized;
        CursorBefore = cursorBefore;
        CursorAfter = cursorAfter;
    }

    public VirtioBlockReadOnlyReadSessionStatus Status { get; }

    public VirtioBlockReadQueuePipelineAdapterResult Adapter { get; }

    public bool IsInitialized { get; }

    public VirtioBlockQueueCursorState CursorBefore { get; }

    public VirtioBlockQueueCursorState CursorAfter { get; }

    public BlockReadResult BlockRead => Adapter.BlockRead;

    public bool Succeeded =>
        IsInitialized && Status == VirtioBlockReadOnlyReadSessionStatus.Success;

    internal static VirtioBlockReadOnlyReadSessionResult FromAdapter(
        VirtioBlockReadQueuePipelineAdapterResult adapter,
        VirtioBlockQueueCursorState cursorBefore,
        VirtioBlockQueueCursorState cursorAfter)
    {
        VirtioBlockReadOnlyReadSessionStatus status = adapter.Succeeded ?
            VirtioBlockReadOnlyReadSessionStatus.Success :
            VirtioBlockReadOnlyReadSessionStatus.AdapterFailed;
        return new VirtioBlockReadOnlyReadSessionResult(
            status,
            adapter,
            isInitialized: true,
            cursorBefore,
            cursorAfter);
    }
}

public enum VirtioBlockReadOnlyReadSessionSetupGateStatus
{
    Success = 0,
    QueueSetupExecutionFailed = 1,
    QueueSetupBarrierNotReady = 2,
    QueueEnableNotAttempted = 3,
    ReadSessionFailed = 4
}

public readonly struct VirtioBlockReadOnlyReadSessionSetupGateResult
{
    private VirtioBlockReadOnlyReadSessionSetupGateResult(
        VirtioBlockReadOnlyReadSessionSetupGateStatus status,
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult queueSetup,
        VirtioBlockReadOnlyReadSessionResult readSession,
        bool readSessionAttempted,
        VirtioBlockQueueCursorState cursorBefore,
        VirtioBlockQueueCursorState cursorAfter,
        bool isInitialized)
    {
        Status = status;
        QueueSetup = queueSetup;
        ReadSession = readSession;
        ReadSessionAttempted = readSessionAttempted;
        CursorBefore = cursorBefore;
        CursorAfter = cursorAfter;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadOnlyReadSessionSetupGateStatus Status { get; }

    public VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult QueueSetup { get; }

    public VirtioBlockReadOnlyReadSessionResult ReadSession { get; }

    public bool ReadSessionAttempted { get; }

    public VirtioBlockQueueCursorState CursorBefore { get; }

    public VirtioBlockQueueCursorState CursorAfter { get; }

    public bool IsInitialized { get; }

    public VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus QueueSetupStatus =>
        QueueSetup.Status;

    public VirtioBlockQueueMmioSetupPlanStatus QueueSetupPlanStatus =>
        QueueSetup.SetupPlanStatus;

    public bool QueueSetupBarrierSucceeded => QueueSetup.BarrierSucceeded;

    public bool QueueEnableAttempted => QueueSetup.QueueEnableAttempted;

    public uint QueueSetupBarrierSequence => QueueSetup.BarrierSequence;

    public VirtioBlockReadOnlyReadSessionStatus ReadSessionStatus =>
        ReadSessionAttempted ?
            ReadSession.Status :
            VirtioBlockReadOnlyReadSessionStatus.Success;

    public VirtioBlockReadQueuePipelineStatus ReadSessionPipelineStatus =>
        ReadSessionAttempted ?
            ReadSession.Adapter.PipelineStatus :
            VirtioBlockReadQueuePipelineStatus.Success;

    public BlockReadResult BlockRead =>
        ReadSessionAttempted ?
            ReadSession.BlockRead :
            BlockReadResult.Fail(BlockDeviceStatus.IoError);

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockReadOnlyReadSessionSetupGateStatus.Success;

    internal static VirtioBlockReadOnlyReadSessionSetupGateResult Reject(
        VirtioBlockReadOnlyReadSessionSetupGateStatus status,
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult queueSetup,
        VirtioBlockQueueCursorState cursor)
    {
        return new VirtioBlockReadOnlyReadSessionSetupGateResult(
            status,
            queueSetup,
            default,
            readSessionAttempted: false,
            cursor,
            cursor,
            isInitialized: true);
    }

    internal static VirtioBlockReadOnlyReadSessionSetupGateResult FromReadSession(
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult queueSetup,
        VirtioBlockReadOnlyReadSessionResult readSession)
    {
        VirtioBlockReadOnlyReadSessionSetupGateStatus status = readSession.Succeeded ?
            VirtioBlockReadOnlyReadSessionSetupGateStatus.Success :
            VirtioBlockReadOnlyReadSessionSetupGateStatus.ReadSessionFailed;
        return new VirtioBlockReadOnlyReadSessionSetupGateResult(
            status,
            queueSetup,
            readSession,
            readSessionAttempted: true,
            readSession.CursorBefore,
            readSession.CursorAfter,
            isInitialized: true);
    }
}

public ref struct VirtioBlockReadOnlyReadSession
{
    private readonly BlockDeviceInfo device;
    private readonly ushort queueSize;
    private readonly ulong requestHeaderPhysicalAddress;
    private readonly ulong dataBufferPhysicalAddress;
    private readonly ulong statusBytePhysicalAddress;
    private readonly ushort queueIndex;
    private readonly ushort queueNotifyOffset;
    private readonly uint notifyOffsetMultiplier;
    private readonly VirtioPciBarRegisterWindowBinding notifyConfiguration;
    private Span<VirtioBlockDescriptorOwnershipState> descriptorStates;
    private Span<byte> requestHeader;
    private Span<byte> destination;
    private Span<byte> statusBuffer;
    private Span<byte> queueMemory;
    private Span<byte> notifyWindowBytes;
    private VirtioBlockQueueCursorState cursor;

    public VirtioBlockReadOnlyReadSession(
        BlockDeviceInfo device,
        ushort queueSize,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        VirtioBlockQueueCursorState cursor,
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
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        Span<byte> notifyWindowBytes)
    {
        this.device = device;
        this.queueSize = queueSize;
        this.descriptorStates = descriptorStates;
        this.cursor = cursor;
        this.requestHeaderPhysicalAddress = requestHeaderPhysicalAddress;
        this.dataBufferPhysicalAddress = dataBufferPhysicalAddress;
        this.statusBytePhysicalAddress = statusBytePhysicalAddress;
        this.requestHeader = requestHeader;
        this.destination = destination;
        this.statusBuffer = statusBuffer;
        this.queueMemory = queueMemory;
        this.queueIndex = queueIndex;
        this.queueNotifyOffset = queueNotifyOffset;
        this.notifyOffsetMultiplier = notifyOffsetMultiplier;
        this.notifyConfiguration = notifyConfiguration;
        this.notifyWindowBytes = notifyWindowBytes;
    }

    public const bool OwnsNoAllocations = true;
    public const bool DelegatesReadQueuePipelineAdapter = true;
    public const bool CarriesCallerOwnedCursorState = true;
    public const bool CarriesCallerOwnedDescriptorStateTable = true;
    public const bool CarriesCallerOwnedRequestHeader = true;
    public const bool CarriesCallerOwnedDestinationBuffer = true;
    public const bool CarriesCallerOwnedStatusBuffer = true;
    public const bool CarriesCallerOwnedQueueMemory = true;
    public const bool CarriesCallerOwnedNotifyWindowBuffer = true;
    public const bool RequiresRegisterWindowBinding = true;
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

    public VirtioBlockQueueCursorState Cursor => cursor;

    public VirtioBlockReadOnlyReadSessionResult Read(
        ulong firstSector,
        uint sectorCount,
        VirtioBlockQueueNotificationBarrierReadiness barrier)
    {
        VirtioBlockQueueCursorState cursorBefore = cursor;
        VirtioBlockReadQueuePipelineAdapterResult adapter =
            VirtioBlockReadQueuePipelineAdapter.Read(
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

        return VirtioBlockReadOnlyReadSessionResult.FromAdapter(
            adapter,
            cursorBefore,
            cursor);
    }
}

public static class VirtioBlockReadOnlyReadSessionSetupGate
{
    public const bool RequiresSuccessfulQueueSetupBarrierExecution = true;
    public const bool RequiresQueueSetupBarrierReadiness = true;
    public const bool RequiresQueueEnableAttempted = true;
    public const bool DelegatesReadSessionAfterSetup = true;
    public const bool PreservesCursorOnSetupFailure = true;
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

    public static VirtioBlockReadOnlyReadSessionSetupGateResult ReadAfterQueueSetup(
        ref VirtioBlockReadOnlyReadSession session,
        VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionResult queueSetup,
        ulong firstSector,
        uint sectorCount,
        VirtioBlockQueueNotificationBarrierReadiness notificationBarrier)
    {
        VirtioBlockQueueCursorState cursorBefore = session.Cursor;

        if (!queueSetup.IsInitialized)
        {
            return VirtioBlockReadOnlyReadSessionSetupGateResult.Reject(
                VirtioBlockReadOnlyReadSessionSetupGateStatus.QueueSetupExecutionFailed,
                queueSetup,
                cursorBefore);
        }

        if (!queueSetup.BarrierSucceeded ||
            queueSetup.Status == VirtioBlockQueueMmioSetupRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady)
        {
            return VirtioBlockReadOnlyReadSessionSetupGateResult.Reject(
                VirtioBlockReadOnlyReadSessionSetupGateStatus.QueueSetupBarrierNotReady,
                queueSetup,
                cursorBefore);
        }

        if (!queueSetup.Succeeded)
        {
            return VirtioBlockReadOnlyReadSessionSetupGateResult.Reject(
                VirtioBlockReadOnlyReadSessionSetupGateStatus.QueueSetupExecutionFailed,
                queueSetup,
                cursorBefore);
        }

        if (!queueSetup.QueueEnableAttempted)
        {
            return VirtioBlockReadOnlyReadSessionSetupGateResult.Reject(
                VirtioBlockReadOnlyReadSessionSetupGateStatus.QueueEnableNotAttempted,
                queueSetup,
                cursorBefore);
        }

        VirtioBlockReadOnlyReadSessionResult readSession =
            session.Read(
                firstSector,
                sectorCount,
                notificationBarrier);

        return VirtioBlockReadOnlyReadSessionSetupGateResult.FromReadSession(
            queueSetup,
            readSession);
    }
}

public static class VirtioBlockReadOnlyDevicePlan
{
    public const bool NotifiesDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool CopiesPayload = false;
    public const bool PerformsDiskRead = false;

    public static VirtioBlockReadOnlyDevicePlanResult PublishRead(
        BlockDeviceInfo device,
        ulong firstSector,
        uint sectorCount,
        ushort queueSize,
        ushort firstDescriptorIndex,
        ushort availableDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort previousUsedIndex,
        ulong requestHeaderPhysicalAddress,
        ulong dataBufferPhysicalAddress,
        ulong statusBytePhysicalAddress,
        Span<byte> requestHeader,
        Span<byte> destination,
        Span<byte> statusBuffer,
        Span<byte> queueMemory)
    {
        BlockDeviceStatus geometryStatus = ValidateGeometry(device);
        if (geometryStatus != BlockDeviceStatus.Success)
        {
            return VirtioBlockReadOnlyDevicePlanResult.FailBeforeLifecycle(
                VirtioBlockReadOnlyDevicePlanStatus.InvalidBlockGeometry,
                geometryStatus,
                device,
                firstSector,
                sectorCount,
                requestedByteCount: 0,
                queueSize);
        }

        ulong requestedByteCount = (ulong)sectorCount * device.BlockSize;
        BlockDeviceStatus rangeStatus = ValidateRange(device, firstSector, sectorCount);
        if (rangeStatus != BlockDeviceStatus.Success)
        {
            return VirtioBlockReadOnlyDevicePlanResult.FailBeforeLifecycle(
                VirtioBlockReadOnlyDevicePlanStatus.InvalidSectorRange,
                rangeStatus,
                device,
                firstSector,
                sectorCount,
                requestedByteCount,
                queueSize);
        }

        VirtioBlockReadLifecyclePublishResult publish =
            VirtioBlockReadLifecycle.PublishReadRequest(
                device.BlockSize,
                device.BlockCount,
                firstSector,
                sectorCount,
                queueSize,
                firstDescriptorIndex,
                availableDescriptorCount,
                previousAvailableRingIndex,
                previousUsedIndex,
                requestHeaderPhysicalAddress,
                dataBufferPhysicalAddress,
                statusBytePhysicalAddress,
                requestHeader,
                destination,
                statusBuffer,
                queueMemory);

        if (!publish.Succeeded)
        {
            return VirtioBlockReadOnlyDevicePlanResult.FailAfterLifecycle(
                device,
                requestedByteCount,
                publish);
        }

        return VirtioBlockReadOnlyDevicePlanResult.Success(
            device,
            requestedByteCount,
            publish);
    }

    internal static BlockDeviceStatus ValidateGeometry(BlockDeviceInfo device)
    {
        if (device.BlockSize != VirtioBlockRequestPlan.SectorSizeBytes)
        {
            return BlockDeviceStatus.InvalidBlockSize;
        }

        if (device.BlockCount == 0)
        {
            return BlockDeviceStatus.InvalidBlockCount;
        }

        if (device.BlockCount > (ulong.MaxValue / device.BlockSize))
        {
            return BlockDeviceStatus.Overflow;
        }

        if (device.CapacityBytes != (device.BlockCount * device.BlockSize))
        {
            return BlockDeviceStatus.Overflow;
        }

        return BlockDeviceStatus.Success;
    }

    internal static BlockDeviceStatus ValidateRange(
        BlockDeviceInfo device,
        ulong firstSector,
        uint sectorCount)
    {
        if (sectorCount == 0 || firstSector >= device.BlockCount)
        {
            return BlockDeviceStatus.InvalidBlockRange;
        }

        ulong remainingSectors = device.BlockCount - firstSector;
        if (sectorCount > remainingSectors)
        {
            return BlockDeviceStatus.InvalidBlockRange;
        }

        return BlockDeviceStatus.Success;
    }
}
