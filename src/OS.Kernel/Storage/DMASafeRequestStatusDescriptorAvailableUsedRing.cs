using OS.Drivers;
using OS.Kernel.Platform;

namespace OS.Storage;

/// <summary>Allocates one CPU-owned page. No descriptor is published
/// and no device ownership is granted. Original allocator backing remains the
/// adapter's responsibility; the DMA token protects metadata lifetime only.</summary>
public static unsafe class DMASafeRequestStatusDescriptorAvailableUsedRing
{
    public static VirtioBlkDmaStatus TryLayout(DmaAddressRange range, out VirtioBlkDmaLayout layout)
    {
        layout = default;
        if (!range.IsInitialized || range.ByteLength != VirtioBlkDmaLayout.PageBytes ||
            ((range.CpuVirtualAddress | range.PhysicalAddress | range.DeviceAddress) & 4095) != 0 ||
            range.CpuVirtualAddress < 4096 || range.CpuVirtualAddress >= 0x0000800000000000UL - 4096)
            return VirtioBlkDmaStatus.InvalidRange;
        var rings = VirtqueueSplitRingPlan.Create(VirtioBlkDmaLayout.QueueSize);
        if (!rings.Succeeded || rings.Plan.EndOffset > VirtioBlkDmaLayout.RequestOffset ||
            VirtioBlkDmaLayout.RequestOffset + VirtioBlkDmaLayout.RequestBytes > VirtioBlkDmaLayout.StatusOffset ||
            VirtioBlkDmaLayout.StatusOffset + VirtioBlkDmaLayout.StatusBytes > VirtioBlkDmaLayout.DataOffset ||
            VirtioBlkDmaLayout.DataOffset + VirtioBlkDmaLayout.MaximumReadBytes > range.ByteLength)
            return VirtioBlkDmaStatus.InvalidLayout;
        layout = new(range, rings.Plan);
        return VirtioBlkDmaStatus.Success;
    }

    public static VirtioBlkDmaStatus TryAllocate(DmaBufferPool* pool,
        out DmaBufferAllocation allocation, out VirtioBlkDmaLayout layout)
    {
        layout = default;
        if (PageAlignedDmaBuffers.TryAllocate(pool, 1, out allocation) != DmaBufferStatus.Success)
            return VirtioBlkDmaStatus.AllocationFailed;
        var status = TryLayout(allocation.Range, out layout);
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
        for (uint i = 0; i < VirtioBlkDmaLayout.PageBytes; i++)
            bytes[i] = 0;
        bytes[layout.Rings.AvailableRing.Offset] = 1; // NO_INTERRUPT; available index stays zero.
        bytes[VirtioBlkDmaLayout.StatusOffset] = 0xff; // Not a completed request.
    }
}
