using OS.Drivers;

namespace OS.Storage;

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
