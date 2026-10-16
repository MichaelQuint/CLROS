using OS.Std.Memory;

namespace OS.Drivers;

public enum VirtioNetQueueStatus : byte
{
    Uninitialized, Success, InvalidAccess, ReadFailed, WriteFailed, StatusRejected,
    InvalidQueue, InvalidNotify, DmaRejected, Overlap, DescriptorRejected,
    OrderingFailed, BusMasterFailed, Timeout, InvalidCompletion, ResetFailed
}

public struct VirtioNetQueueReceipt
{
    public ushort Index, MaximumSize, Size, NotifyOffset, AvailableIndex;
    public ulong Descriptor, Available, Used, Notify;
    public bool Configured, Published, DescriptorReleased;
}

public struct VirtioNetQueueResult
{
    public VirtioNetQueueStatus Status, CleanupStatus;
    public VirtioNetQueueReceipt Receive, Transmit;
    public ulong CpuAddress, DeviceAddress;
    public uint Fences, ResetReads, Polls, UsedId, UsedLength;
    public ushort UsedIndex;
    public bool ResetComplete, BusMasterDisabled, DmaReleased, TransmitCompleted;
    public readonly bool Succeeded => Status == VirtioNetQueueStatus.Success &&
        CleanupStatus == VirtioNetQueueStatus.Success && Receive.Configured && Transmit.Configured &&
        Receive.DescriptorReleased && Transmit.DescriptorReleased && ResetComplete && BusMasterDisabled && DmaReleased;
}

/// <summary>One exclusive modern receive/transmit pair, each size eight. The
/// caller's feature profile owns admission of negotiated device features.
/// A single owned page holds disjoint rings and buffers. One 1526-byte writable
/// RX buffer is published before DRIVER_OK; this configuration-only entry point
/// never enables bus mastering or DRIVER_OK. The adapter supplies authenticated
/// coherent WB/RW/NX backing, MMIO and a real fence, starts at FEATURES_OK (11)
/// with disabled MSI-X, and retains backing when cleanup cannot prove quiescence.
/// No receive processing, offload, mergeable buffers, reuse or continuing I/O.</summary>
public static unsafe class VirtioNetQueue
{
    public const ushort Size = 8;
    public const uint TransmitRingOffset = 2048, ReceiveBufferOffset = 256,
        ReceiveBufferBytes = 1526, TransmitBufferOffset = 2304, TransmitBufferBytes = 72;

    public static VirtioNetQueueResult Run(VirtioMappedRegion common, VirtioMappedRegion notify,
        uint multiplier, DmaBufferPool* dma, VirtqueuePollingAccess access, uint resetLimit)
    {
        VirtioNetQueueResult r = default; r.Status = VirtioNetQueueStatus.InvalidAccess;
        if (!Valid(common, notify, multiplier, access, resetLimit)) return r;
        DmaBufferAllocation allocation = default; bool deviceOwned = false;
        VirtqueueDescriptorPoolState rx = default, tx = default;
        VirtqueueDescriptorSlot* slots = stackalloc VirtqueueDescriptorSlot[Size * 2];
        for (uint i = 0; i < Size * 2; i++) slots[i] = default;
        VirtqueueDescriptorToken rxToken = default, txToken = default;
        r.Status = Prepare(common, notify, multiplier, dma, access, ref allocation, ref deviceOwned,
            &rx, &tx, slots, ref rxToken, ref txToken, ref r);
        Cleanup(common, dma, access, resetLimit, allocation, deviceOwned, &rx, &tx, rxToken, txToken, ref r);
        return r;
    }

    internal static bool Valid(VirtioMappedRegion common, VirtioMappedRegion notify,
        uint multiplier, VirtqueuePollingAccess a, uint resetLimit) =>
        common.IsValid && common.Bytes >= 56 && (common.VirtualStart & 3) == 0 &&
        notify.IsValid && notify.Bytes >= 2 && (notify.VirtualStart & 1) == 0 &&
        (multiplier == 0 || (multiplier >= 2 && (multiplier & (multiplier - 1)) == 0)) &&
        resetLimit > 0 && resetLimit <= 1024 && a.Read8 != null && a.Write8 != null &&
        a.Read16 != null && a.Write16 != null && a.Read32 != null && a.Write32 != null &&
        a.Fence != null && a.BusMaster != null;

    internal static VirtioNetQueueStatus Prepare(VirtioMappedRegion common, VirtioMappedRegion notify,
        uint multiplier, DmaBufferPool* dma, VirtqueuePollingAccess a,
        ref DmaBufferAllocation allocation, ref bool deviceOwned,
        VirtqueueDescriptorPoolState* rx, VirtqueueDescriptorPoolState* tx, VirtqueueDescriptorSlot* slots,
        ref VirtqueueDescriptorToken rxToken, ref VirtqueueDescriptorToken txToken, ref VirtioNetQueueResult r)
    {
        if (a.BusMaster(a.Context, 0) != 1) return VirtioNetQueueStatus.BusMasterFailed;
        var status = Status(common, a, 11); if (status != VirtioNetQueueStatus.Success) return status;
        ushort queues = 0;
        if (a.Read16(a.Context, common.VirtualStart + 18, &queues) != 1) return VirtioNetQueueStatus.ReadFailed;
        if (queues < 2) return VirtioNetQueueStatus.InvalidQueue;
        if (PageAlignedDmaBuffers.TryAllocate(dma, 1, out allocation) != DmaBufferStatus.Success)
            return VirtioNetQueueStatus.DmaRejected;
        var range = allocation.Range;
        r.CpuAddress = range.CpuVirtualAddress; r.DeviceAddress = range.DeviceAddress;
        if (range.ByteLength != 4096 || r.CpuAddress >= 0x0000800000000000UL - 4096)
            return VirtioNetQueueStatus.DmaRejected;
        if (Aliases(common, range.CpuVirtualAddress, range.PhysicalAddress) ||
            Aliases(notify, range.CpuVirtualAddress, range.PhysicalAddress)) return VirtioNetQueueStatus.Overlap;
        if (VirtqueueDescriptors.TryInitialize(rx, slots, Size, 1) != VirtqueueDescriptorStatus.Success ||
            VirtqueueDescriptors.TryInitialize(tx, slots + Size, Size, 1) != VirtqueueDescriptorStatus.Success ||
            VirtqueueDescriptors.TryAllocate(rx, out rxToken) != VirtqueueDescriptorStatus.Success ||
            VirtqueueDescriptors.TryAllocate(tx, out txToken) != VirtqueueDescriptorStatus.Success ||
            rxToken.Index != 0 || txToken.Index != 0) return VirtioNetQueueStatus.DescriptorRejected;
        var layout = VirtqueueSplitRingPlan.Create(Size);
        if (!layout.Succeeded || layout.Plan.EndOffset > ReceiveBufferOffset ||
            ReceiveBufferOffset + ReceiveBufferBytes > TransmitRingOffset ||
            TransmitRingOffset + layout.Plan.EndOffset > TransmitBufferOffset ||
            TransmitBufferOffset + TransmitBufferBytes > 4096) return VirtioNetQueueStatus.InvalidQueue;
        byte* data = (byte*)r.CpuAddress;
        for (uint i = 0; i < 4096; i++) data[i] = 0;
        *(ulong*)data = r.DeviceAddress + ReceiveBufferOffset;
        *(uint*)(data + 8) = ReceiveBufferBytes;
        *(ushort*)(data + 12) = 2; // VIRTQ_DESC_F_WRITE, no NEXT/INDIRECT.
        *(ulong*)(data + TransmitRingOffset) = r.DeviceAddress + TransmitBufferOffset;
        *(uint*)(data + TransmitRingOffset + 8) = TransmitBufferBytes;
        *(ushort*)(data + layout.Plan.AvailableRing.Offset) = 1; // NO_INTERRUPT.
        *(ushort*)(data + TransmitRingOffset + layout.Plan.AvailableRing.Offset) = 1;
        if (PageAlignedDmaBuffers.TryGiveToDevice(dma, allocation.Token) != DmaBufferStatus.Success)
            return VirtioNetQueueStatus.DmaRejected;
        deviceOwned = true;
        if (!Fence(a, ref r)) return VirtioNetQueueStatus.OrderingFailed;
        status = Configure(common, notify, multiplier, a, layout.Plan, 0, ref r.Receive, ref r);
        if (status != VirtioNetQueueStatus.Success) return status;
        status = Configure(common, notify, multiplier, a, layout.Plan, 1, ref r.Transmit, ref r);
        if (status != VirtioNetQueueStatus.Success) return status;
        if (VirtqueueDescriptors.TryPublish(rx, rxToken) != VirtqueueDescriptorStatus.Success)
            return VirtioNetQueueStatus.DescriptorRejected;
        r.Receive.Published = true;
        if (!Fence(a, ref r)) return VirtioNetQueueStatus.OrderingFailed;
        *(ushort*)(data + layout.Plan.AvailableRing.Offset + 2) = 1;
        r.Receive.AvailableIndex = 1;
        if (!Fence(a, ref r)) return VirtioNetQueueStatus.OrderingFailed;
        return Status(common, a, 11);
    }

    internal static VirtioNetQueueStatus Configure(VirtioMappedRegion common, VirtioMappedRegion notify,
        uint multiplier, VirtqueuePollingAccess a, VirtqueueSplitRingPlan plan, ushort index,
        ref VirtioNetQueueReceipt q, ref VirtioNetQueueResult r)
    {
        ulong c = common.VirtualStart; ushort selected = 0xffff, enabled = 0xffff, maximum = 0, offset = 0;
        q.Index = index;
        if (a.Write16(a.Context, c + 22, index) != 1) return VirtioNetQueueStatus.WriteFailed;
        if (a.Read16(a.Context, c + 22, &selected) != 1 || a.Read16(a.Context, c + 28, &enabled) != 1 ||
            a.Read16(a.Context, c + 24, &maximum) != 1 || a.Read16(a.Context, c + 30, &offset) != 1)
            return VirtioNetQueueStatus.ReadFailed;
        q.MaximumSize = maximum; q.NotifyOffset = offset;
        if (selected != index || enabled != 0 || BoundedVirtqueueSize.Validate(Size, maximum, out q.Size) != VirtqueueSizeStatus.Success)
            return VirtioNetQueueStatus.InvalidQueue;
        ulong displacement = (ulong)offset * multiplier;
        if (displacement > notify.Bytes - 2u) return VirtioNetQueueStatus.InvalidNotify;
        q.Notify = notify.VirtualStart + displacement;
        ulong start = r.DeviceAddress + (index == 0 ? 0 : TransmitRingOffset);
        q.Descriptor = start + plan.DescriptorTable.Offset;
        q.Available = start + plan.AvailableRing.Offset; q.Used = start + plan.UsedRing.Offset;
        if (a.Write16(a.Context, c + 16, 0xffff) != 1 || a.Write16(a.Context, c + 26, 0xffff) != 1 ||
            a.Write16(a.Context, c + 24, Size) != 1 || !WriteAddress(a, c + 32, q.Descriptor) ||
            !WriteAddress(a, c + 40, q.Available) || !WriteAddress(a, c + 48, q.Used)) return VirtioNetQueueStatus.WriteFailed;
        ushort size = 0, vector = 0, configVector = 0;
        if (a.Read16(a.Context, c + 24, &size) != 1 || a.Read16(a.Context, c + 26, &vector) != 1 ||
            a.Read16(a.Context, c + 16, &configVector) != 1) return VirtioNetQueueStatus.ReadFailed;
        if (size != Size || vector != 0xffff || configVector != 0xffff) return VirtioNetQueueStatus.InvalidQueue;
        var status = Status(common, a, 11); if (status != VirtioNetQueueStatus.Success) return status;
        if (a.Write16(a.Context, c + 28, 1) != 1) return VirtioNetQueueStatus.WriteFailed;
        if (a.Read16(a.Context, c + 28, &enabled) != 1) return VirtioNetQueueStatus.ReadFailed;
        if (enabled != 1) return VirtioNetQueueStatus.InvalidQueue;
        q.Configured = true;
        return Status(common, a, 11);
    }

    internal static void Cleanup(VirtioMappedRegion common, DmaBufferPool* dma, VirtqueuePollingAccess a,
        uint resetLimit, DmaBufferAllocation allocation, bool deviceOwned,
        VirtqueueDescriptorPoolState* rx, VirtqueueDescriptorPoolState* tx,
        VirtqueueDescriptorToken rxToken, VirtqueueDescriptorToken txToken, ref VirtioNetQueueResult r)
    {
        Quiesce(common, a, resetLimit, ref r);
        ReleaseBacking(dma, allocation, deviceOwned, rx, tx, rxToken, txToken, ref r);
    }

    internal static void Quiesce(VirtioMappedRegion common, VirtqueuePollingAccess a,
        uint resetLimit, ref VirtioNetQueueResult r)
    {
        r.CleanupStatus = VirtioNetQueueStatus.ResetFailed;
        byte status = 0xff;
        if (a.Write8(a.Context, common.VirtualStart + 20, 0) == 1)
            for (uint i = 0; i < resetLimit; i++)
            {
                r.ResetReads++;
                if (a.Read8(a.Context, common.VirtualStart + 20, &status) != 1) break;
                if (status == 0) { r.ResetComplete = true; break; }
            }
        r.BusMasterDisabled = a.BusMaster(a.Context, 0) == 1;
        if (!r.ResetComplete) return;
        if (!r.BusMasterDisabled) { r.CleanupStatus = VirtioNetQueueStatus.BusMasterFailed; return; }
        if (!Fence(a, ref r)) { r.CleanupStatus = VirtioNetQueueStatus.OrderingFailed; return; }
        r.CleanupStatus = VirtioNetQueueStatus.Success;
    }

    internal static void ReleaseBacking(DmaBufferPool* dma, DmaBufferAllocation allocation,
        bool deviceOwned, VirtqueueDescriptorPoolState* rx, VirtqueueDescriptorPoolState* tx,
        VirtqueueDescriptorToken rxToken, VirtqueueDescriptorToken txToken, ref VirtioNetQueueResult r)
    {
        if (r.CleanupStatus != VirtioNetQueueStatus.Success || !r.ResetComplete || !r.BusMasterDisabled) return;
        if (!Release(rx, rxToken, ref r.Receive) || !Release(tx, txToken, ref r.Transmit))
        { r.CleanupStatus = VirtioNetQueueStatus.DescriptorRejected; return; }
        if (allocation.IsInitialized)
        {
            if ((deviceOwned && PageAlignedDmaBuffers.TryReturnToCpu(dma, allocation.Token) != DmaBufferStatus.Success) ||
                PageAlignedDmaBuffers.TryRelease(dma, allocation.Token) != DmaBufferStatus.Success)
                r.CleanupStatus = VirtioNetQueueStatus.DmaRejected;
            else r.DmaReleased = true;
        }
    }

    private static bool Release(VirtqueueDescriptorPoolState* pool, VirtqueueDescriptorToken token, ref VirtioNetQueueReceipt q)
    {
        if (!token.IsInitialized) return true;
        if (q.Published && VirtqueueDescriptors.TryReclaim(pool, token) != VirtqueueDescriptorStatus.Success) return false;
        if (VirtqueueDescriptors.TryRelease(pool, token) != VirtqueueDescriptorStatus.Success) return false;
        q.DescriptorReleased = true; return true;
    }
    internal static VirtioNetQueueStatus Status(VirtioMappedRegion common, VirtqueuePollingAccess a, byte expected)
    {
        byte value = 0;
        return a.Read8(a.Context, common.VirtualStart + 20, &value) != 1 ? VirtioNetQueueStatus.ReadFailed :
            value == expected ? VirtioNetQueueStatus.Success : VirtioNetQueueStatus.StatusRejected;
    }
    internal static bool Fence(VirtqueuePollingAccess a, ref VirtioNetQueueResult r)
    {
        if (MemoryOrder.Fence(a.Context, a.Fence) != MemoryOrderStatus.Success) return false;
        r.Fences++; return true;
    }
    private static bool WriteAddress(VirtqueuePollingAccess a, ulong address, ulong value)
    {
        if (a.Write32(a.Context, address, (uint)value) != 1 || a.Write32(a.Context, address + 4, (uint)(value >> 32)) != 1) return false;
        uint low = 0, high = 0;
        return a.Read32(a.Context, address, &low) == 1 && a.Read32(a.Context, address + 4, &high) == 1 &&
            (((ulong)high << 32) | low) == value;
    }
    private static bool Aliases(VirtioMappedRegion region, ulong cpu, ulong physical) =>
        (region.VirtualStart < cpu ? cpu - region.VirtualStart < region.Bytes : region.VirtualStart - cpu < 4096) ||
        (region.PhysicalStart < physical ? physical - region.PhysicalStart < region.Bytes : region.PhysicalStart - physical < 4096);
}
