using OS.Drivers;

namespace OS.Storage;

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
