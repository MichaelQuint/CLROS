using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlkQueueStatus : byte
{
    Uninitialized, Success, InvalidAccess, ReadFailed, WriteFailed, StatusRejected,
    InvalidQueue, DmaRejected, Overlap, ReadbackMismatch, OrderingFailed, BusMasterFailed, ResetFailed
}

/// <summary>Exclusive nonthrowing MMIO access; 1 means success. BusMasterOff must
/// disable and read back PCI bus mastering and INTx. Queue-only use requires
/// MSI-X disabled; an admitted IRQ adapter may instead hold delivery masked
/// during configuration and must disable it before cleanup/token release.
/// Fence is the admitted real DMA/MMIO fence. Numeric callbacks are not proof of
/// those properties; the platform adapter owns mappings and device authority.</summary>
public unsafe struct VirtioBlkQueueAccess
{
    public void* Context;
    public delegate* unmanaged<void*, ulong, byte*, byte> Read8;
    public delegate* unmanaged<void*, ulong, byte, byte> Write8;
    public delegate* unmanaged<void*, ulong, ushort*, byte> Read16;
    public delegate* unmanaged<void*, ulong, uint, byte> Write16;
    public delegate* unmanaged<void*, ulong, uint*, byte> Read32;
    public delegate* unmanaged<void*, ulong, uint, byte> Write32;
    public delegate* unmanaged<void*, byte> Fence, BusMasterOff;
}

public struct VirtioBlkQueueResult
{
    public VirtioBlkQueueStatus Status, CleanupStatus;
    public VirtioBlkDmaLayout Layout;
    public ushort MaximumSize, Size;
    public uint Fences, ResetReads;
    public bool Configured, ResetComplete, BusMasterDisabled, DmaReleased;
    public readonly bool Succeeded => Status == VirtioBlkQueueStatus.Success &&
        CleanupStatus == VirtioBlkQueueStatus.Success && Configured && ResetComplete && BusMasterDisabled && DmaReleased;
}

/// <summary>One modern block queue, size eight, no negotiated MQ/event-index/
/// indirect/packed features. Requires completed FEATURES_OK (11), firmware DMA
/// already quiesced and MSI-X initially disabled. Configures/readbacks queue zero while
/// bus mastering stays off; an admitted MSI-X adapter may supply vector zero
/// after preparing delivery masking. Never sets DRIVER_OK, publishes or notifies. Resets
/// and releases its exact DMA token only after observed quiescence and a fence.
/// The adapter retains the original backing when cleanup is uncertain.</summary>
public static unsafe class VirtioBlkQueueN0
{
    public static VirtioBlkQueueResult Run(VirtioMappedRegion common, DmaBufferPool* dma,
        VirtioBlkQueueAccess access, uint resetLimit)
    {
        VirtioBlkQueueResult r = default;
        r.Status = VirtioBlkQueueStatus.InvalidAccess;
        if (!common.IsValid || common.Bytes < 56 || (common.VirtualStart & 3) != 0 ||
            resetLimit == 0 || resetLimit > 1024 || access.Read8 == null || access.Write8 == null ||
            access.Read16 == null || access.Write16 == null || access.Read32 == null || access.Write32 == null ||
            access.Fence == null || access.BusMasterOff == null) return r;
        DmaBufferAllocation allocation = default;
        bool deviceOwned = false;
        r.Status = Configure(common, dma, access, ref allocation, ref deviceOwned, ref r);
        Cleanup(common, dma, access, resetLimit, allocation, deviceOwned, ref r);
        return r;
    }

    // Shared by the bounded read transaction. The adapter keeps the original
    // backing alive until its synchronous caller returns; pool release alone
    // does not free that backing or permit concurrent pool access.
    internal static void Cleanup(VirtioMappedRegion common, DmaBufferPool* dma,
        VirtioBlkQueueAccess access, uint resetLimit, DmaBufferAllocation allocation,
        bool deviceOwned, ref VirtioBlkQueueResult r)
    {
        r.CleanupStatus = VirtioBlkQueueStatus.ResetFailed;
        byte status = 0xff;
        if (access.Write8(access.Context, common.VirtualStart + 20, 0) == 1)
            for (uint i = 0; i < resetLimit; i++)
            {
                r.ResetReads++;
                if (access.Read8(access.Context, common.VirtualStart + 20, &status) != 1) break;
                if (status == 0) { r.ResetComplete = true; break; }
            }
        r.BusMasterDisabled = access.BusMasterOff(access.Context) == 1;
        if (!r.ResetComplete) return;
        if (!r.BusMasterDisabled) { r.CleanupStatus = VirtioBlkQueueStatus.BusMasterFailed; return; }
        if (!Fence(access, ref r)) { r.CleanupStatus = VirtioBlkQueueStatus.OrderingFailed; return; }
        r.CleanupStatus = VirtioBlkQueueStatus.Success;
        if (allocation.IsInitialized)
        {
            if ((deviceOwned && PageAlignedDmaBuffers.TryReturnToCpu(dma, allocation.Token) != DmaBufferStatus.Success) ||
                PageAlignedDmaBuffers.TryRelease(dma, allocation.Token) != DmaBufferStatus.Success)
                r.CleanupStatus = VirtioBlkQueueStatus.DmaRejected;
            else r.DmaReleased = true;
        }
    }

    internal static VirtioBlkQueueStatus Configure(VirtioMappedRegion common, DmaBufferPool* dma,
        VirtioBlkQueueAccess a, ref DmaBufferAllocation allocation, ref bool deviceOwned, ref VirtioBlkQueueResult r,
        VirtioMappedRegion notify = default, VirtioMappedRegion device = default, ulong destination = 0,
        ushort msixVector = 0xffff)
    {
        ulong c = common.VirtualStart;
        if (a.BusMasterOff(a.Context) != 1) return VirtioBlkQueueStatus.BusMasterFailed;
        var status = Status(c, a);
        if (status != VirtioBlkQueueStatus.Success) return status;
        ushort queues = 0, selected = 0xffff, enabled = 0xffff, maximum = 0;
        if (a.Read16(a.Context, c + 18, &queues) != 1) return VirtioBlkQueueStatus.ReadFailed;
        if (queues == 0) return VirtioBlkQueueStatus.InvalidQueue;
        if (a.Write16(a.Context, c + 22, 0) != 1) return VirtioBlkQueueStatus.WriteFailed;
        if (a.Read16(a.Context, c + 22, &selected) != 1 || a.Read16(a.Context, c + 28, &enabled) != 1 ||
            a.Read16(a.Context, c + 24, &maximum) != 1) return VirtioBlkQueueStatus.ReadFailed;
        r.MaximumSize = maximum;
        if (selected != 0 || enabled != 0 ||
            BoundedVirtqueueSize.Validate(VirtioBlkDmaLayout.QueueSize, maximum, out r.Size) != VirtqueueSizeStatus.Success)
            return VirtioBlkQueueStatus.InvalidQueue;
        if (DMASafeRequestStatusDescriptorAvailableUsedRing.TryAllocate(dma, out allocation, out r.Layout) != VirtioBlkDmaStatus.Success)
            return VirtioBlkQueueStatus.DmaRejected;
        var range = r.Layout.Range;
        if (Overlap(c, common.Bytes, range.CpuVirtualAddress, range.ByteLength) ||
            Overlap(common.PhysicalStart, common.Bytes, range.PhysicalAddress, range.ByteLength) ||
            Aliases(notify, range) || Aliases(device, range) ||
            (destination != 0 && Overlap(destination, 512, range.CpuVirtualAddress, range.ByteLength)))
            return VirtioBlkQueueStatus.Overlap;
        DMASafeRequestStatusDescriptorAvailableUsedRing.Initialize(r.Layout);
        if (PageAlignedDmaBuffers.TryGiveToDevice(dma, allocation.Token) != DmaBufferStatus.Success)
            return VirtioBlkQueueStatus.DmaRejected;
        deviceOwned = true; // Hold even when an MMIO callback reports failure after writing.
        if (!Fence(a, ref r)) return VirtioBlkQueueStatus.OrderingFailed;
        var rings = r.Layout.Rings;
        ulong d = range.DeviceAddress;
        if (a.Write16(a.Context, c + 16, 0xffff) != 1 || a.Write16(a.Context, c + 26, msixVector) != 1 ||
            a.Write16(a.Context, c + 24, r.Size) != 1 ||
            !WriteAddress(a, c + 32, d + rings.DescriptorTable.Offset) ||
            !WriteAddress(a, c + 40, d + rings.AvailableRing.Offset) ||
            !WriteAddress(a, c + 48, d + rings.UsedRing.Offset)) return VirtioBlkQueueStatus.WriteFailed;
        ushort size = 0, configVector = 0, queueVector = 0;
        ulong descriptor = 0, available = 0, used = 0;
        if (a.Read16(a.Context, c + 24, &size) != 1 || a.Read16(a.Context, c + 16, &configVector) != 1 ||
            a.Read16(a.Context, c + 26, &queueVector) != 1 || !ReadAddress(a, c + 32, out descriptor) ||
            !ReadAddress(a, c + 40, out available) || !ReadAddress(a, c + 48, out used)) return VirtioBlkQueueStatus.ReadFailed;
        if (size != r.Size || configVector != 0xffff || queueVector != msixVector ||
            descriptor != d + rings.DescriptorTable.Offset || available != d + rings.AvailableRing.Offset ||
            used != d + rings.UsedRing.Offset) return VirtioBlkQueueStatus.ReadbackMismatch;
        status = Status(c, a);
        if (status != VirtioBlkQueueStatus.Success) return status;
        if (a.Write16(a.Context, c + 28, 1) != 1) return VirtioBlkQueueStatus.WriteFailed;
        if (a.Read16(a.Context, c + 28, &enabled) != 1) return VirtioBlkQueueStatus.ReadFailed;
        if (enabled != 1) return VirtioBlkQueueStatus.ReadbackMismatch;
        status = Status(c, a);
        if (status != VirtioBlkQueueStatus.Success) return status;
        r.Configured = true;
        return VirtioBlkQueueStatus.Success;
    }

    private static VirtioBlkQueueStatus Status(ulong common, VirtioBlkQueueAccess a)
    {
        byte status = 0;
        if (a.Read8(a.Context, common + 20, &status) != 1) return VirtioBlkQueueStatus.ReadFailed;
        return status == 11 ? VirtioBlkQueueStatus.Success : VirtioBlkQueueStatus.StatusRejected;
    }
    private static bool Fence(VirtioBlkQueueAccess a, ref VirtioBlkQueueResult r)
    { if (a.Fence(a.Context) != 1) return false; r.Fences++; return true; }
    private static bool Overlap(ulong a, ulong n, ulong b, ulong m) => a < b + m && b < a + n;
    private static bool Aliases(VirtioMappedRegion region, OS.Kernel.Platform.DmaAddressRange range) =>
        region.IsValid && (Overlap(region.VirtualStart, region.Bytes, range.CpuVirtualAddress, range.ByteLength) ||
            Overlap(region.PhysicalStart, region.Bytes, range.PhysicalAddress, range.ByteLength));
    private static bool WriteAddress(VirtioBlkQueueAccess a, ulong address, ulong value) =>
        a.Write32(a.Context, address, (uint)value) == 1 && a.Write32(a.Context, address + 4, (uint)(value >> 32)) == 1;
    private static bool ReadAddress(VirtioBlkQueueAccess a, ulong address, out ulong value)
    {
        value = 0;
        uint low = 0, high = 0;
        if (a.Read32(a.Context, address, &low) != 1 || a.Read32(a.Context, address + 4, &high) != 1) return false;
        value = ((ulong)high << 32) | low;
        return true;
    }
}
