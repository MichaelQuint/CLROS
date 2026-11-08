using OS.Drivers;
using OS.Kernel.Platform;

namespace OS.Storage;

/// <summary>Allocates a legacy page or an explicit three-page readonly layout. No descriptor is published
/// and no device ownership is granted. Original allocator backing remains the
/// adapter's responsibility; the DMA token protects metadata lifetime only.</summary>
public static unsafe class DMASafeRequestStatusDescriptorAvailableUsedRing
{
    public static VirtioBlkDmaStatus TryLayout(DmaAddressRange range, out VirtioBlkDmaLayout layout)
        => TryLayoutCore(range, false, out layout);

    /// <summary>Validate one control page followed by8KiB of data across three
    /// contiguous pages in each admitted address domain. No mapping or token
    /// authority is created. Invalid input returns an empty layout unchanged.</summary>
    public static VirtioBlkDmaStatus TryContinuingReadLayout(DmaAddressRange range, out VirtioBlkDmaLayout layout)
        => TryLayoutCore(range, true, out layout);

    private static VirtioBlkDmaStatus TryLayoutCore(DmaAddressRange range, bool continuingRead, out VirtioBlkDmaLayout layout)
    {
        layout = default;
        uint bytes = continuingRead ? VirtioBlkDmaLayout.ContinuingReadPages * VirtioBlkDmaLayout.PageBytes : VirtioBlkDmaLayout.PageBytes;
        uint dataOffset = continuingRead ? VirtioBlkDmaLayout.ContinuingReadDataOffset : VirtioBlkDmaLayout.DataOffset;
        uint dataBytes = continuingRead ? VirtioBlkDmaLayout.ContinuingReadBytes : VirtioBlkDmaLayout.MaximumReadBytes;
        if (!range.IsInitialized || range.ByteLength != bytes ||
            ((range.CpuVirtualAddress | range.PhysicalAddress | range.DeviceAddress) & 4095) != 0 ||
            range.CpuVirtualAddress < 4096 || range.CpuVirtualAddress >= 0x0000800000000000UL - bytes)
            return VirtioBlkDmaStatus.InvalidRange;
        var rings = VirtqueueSplitRingPlan.Create(VirtioBlkDmaLayout.QueueSize);
        if (!rings.Succeeded || rings.Plan.EndOffset > VirtioBlkDmaLayout.RequestOffset ||
            VirtioBlkDmaLayout.RequestOffset + VirtioBlkDmaLayout.RequestBytes > VirtioBlkDmaLayout.StatusOffset ||
            VirtioBlkDmaLayout.StatusOffset + VirtioBlkDmaLayout.StatusBytes > dataOffset ||
            dataOffset + dataBytes > range.ByteLength)
            return VirtioBlkDmaStatus.InvalidLayout;
        layout = new(range, rings.Plan, continuingRead);
        return VirtioBlkDmaStatus.Success;
    }

    public static VirtioBlkDmaStatus TryAllocate(DmaBufferPool* pool,
        out DmaBufferAllocation allocation, out VirtioBlkDmaLayout layout)
        => TryAllocateCore(pool, false, out allocation, out layout);

    /// <summary>Reserve the exact three-page CPU token for the continuing-read
    /// layout. The serialized adapter retains mapped coherent backing until
    /// quiescence and exact release. Failure publishes no device authority and
    /// preserves a nonempty allocation if its immediate CPU release fails.</summary>
    public static VirtioBlkDmaStatus TryAllocateContinuingRead(DmaBufferPool* pool,
        out DmaBufferAllocation allocation, out VirtioBlkDmaLayout layout)
        => TryAllocateCore(pool, true, out allocation, out layout);

    private static VirtioBlkDmaStatus TryAllocateCore(DmaBufferPool* pool, bool continuingRead,
        out DmaBufferAllocation allocation, out VirtioBlkDmaLayout layout)
    {
        layout = default;
        if (PageAlignedDmaBuffers.TryAllocate(pool, continuingRead ? VirtioBlkDmaLayout.ContinuingReadPages : 1, out allocation) != DmaBufferStatus.Success)
            return VirtioBlkDmaStatus.AllocationFailed;
        var status = TryLayoutCore(allocation.Range, continuingRead, out layout);
        if (status != VirtioBlkDmaStatus.Success)
        {
            // Never exposed to the device: release the exact fresh CPU token.
            if (PageAlignedDmaBuffers.TryRelease(pool, allocation.Token) == DmaBufferStatus.Success)
                allocation = default;
            return status;
        }
        return VirtioBlkDmaStatus.Success;
    }

    /// <summary>Caller holds the exact live CPU token and authenticates writable,
    /// coherent backing disjoint from MMIO and metadata. No device has seen it.</summary>
    public static void Initialize(VirtioBlkDmaLayout layout)
    {
        if (!layout.IsInitialized)
            return;
        byte* bytes = (byte*)layout.Range.CpuVirtualAddress;
        for (ulong i = 0; i < layout.Range.ByteLength; i++)
            bytes[i] = 0;
        bytes[layout.Rings.AvailableRing.Offset] = 1; // NO_INTERRUPT; available index stays zero.
        bytes[VirtioBlkDmaLayout.StatusOffset] = 0xff; // Not a completed request.
    }
}
