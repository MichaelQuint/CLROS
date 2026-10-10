using OS.Std.Memory;

namespace OS.Drivers;

public enum VirtqueuePollingStatus : byte
{
    Uninitialized, Success, InvalidAccess, ReadFailed, WriteFailed, StatusRejected,
    InvalidQueue, InvalidNotify, DmaRejected, DescriptorRejected, OrderingFailed,
    BusMasterFailed, Timeout, InvalidCompletion, ResetFailed
}

/// <summary>Exclusive, non-throwing access (1=success). The adapter authenticates
/// MMIO widths/mappings, coherent DMA, the real fence and PCI bus-master writes.
/// Read callbacks must reload device/shared memory on every invocation.</summary>
public unsafe struct VirtqueuePollingAccess
{
    public void* Context;
    public delegate* unmanaged<void*, ulong, byte*, byte> Read8;
    public delegate* unmanaged<void*, ulong, byte, byte> Write8;
    public delegate* unmanaged<void*, ulong, ushort*, byte> Read16;
    public delegate* unmanaged<void*, ulong, uint, byte> Write16;
    public delegate* unmanaged<void*, ulong, uint*, byte> Read32;
    public delegate* unmanaged<void*, ulong, uint, byte> Write32;
    public delegate* unmanaged<void*, byte> Fence;
    public delegate* unmanaged<void*, byte, byte> BusMaster;
}

public struct VirtqueuePollingResult
{
    public VirtqueuePollingStatus Status, CleanupStatus;
    public ushort MaximumSize, Size, NotifyOffset, AvailableIndex, UsedIndex;
    public uint Polls, UsedId, UsedLength, Fences;
    public ulong CpuAddress, DeviceAddress, NotifyAddress;
    public bool Published, ResetComplete, BusMasterDisabled, DmaReleased;
    public readonly bool Succeeded => Status == VirtqueuePollingStatus.Success &&
        CleanupStatus == VirtqueuePollingStatus.Success && ResetComplete && BusMasterDisabled && DmaReleased;
}

/// <summary>One VERSION_1-only modern-net TX transaction: queue 1, size 8,
/// one read-only descriptor with a 12-byte zero virtio header and a local test
/// Ethernet frame. No receive, offload, indirect/event-index, interrupt or reuse.
/// Caller provides an exclusive CPU-owned DMA pool with mapped coherent WB/RW/NX
/// backing and retains that backing on failure. Device starts at status 11,
/// reset queues and bus mastering off. Reset completion plus bus-master disable
/// and a successful fence are required before releasing even unused backing.
/// Numeric addresses and callbacks do not establish those platform guarantees.</summary>
public static unsafe class SplitVirtqueuePollingNotificationCompletion
{
    public const ushort QueueIndex = 1, QueueSize = 8;
    public const uint PayloadOffset = 256, PayloadBytes = 72;

    public static VirtqueuePollingResult Run(VirtioMappedRegion common, VirtioMappedRegion notify,
        uint notifyMultiplier, DmaBufferPool* dma, VirtqueuePollingAccess access, uint pollLimit, uint resetLimit)
    {
        VirtqueuePollingResult result = default;
        result.Status = VirtqueuePollingStatus.InvalidAccess;
        if (!common.IsValid || common.Bytes < 56 || (common.VirtualStart & 3) != 0 ||
            !notify.IsValid || notify.Bytes < 2 || (notify.VirtualStart & 1) != 0 ||
            (notifyMultiplier != 0 && (notifyMultiplier < 2 || (notifyMultiplier & (notifyMultiplier - 1)) != 0)) ||
            pollLimit == 0 || pollLimit > 1000000 || resetLimit == 0 || resetLimit > 1024 ||
            access.Read8 == null || access.Write8 == null || access.Read16 == null || access.Write16 == null ||
            access.Read32 == null || access.Write32 == null || access.Fence == null || access.BusMaster == null)
            return result;
        DmaBufferAllocation allocation = default;
        VirtqueueDescriptorPoolState pool = default;
        VirtqueueDescriptorSlot* slots = stackalloc VirtqueueDescriptorSlot[QueueSize];
        for (uint i = 0; i < QueueSize; i++) slots[i] = default;
        VirtqueueDescriptorToken token = default;
        result.Status = Execute(common, notify, notifyMultiplier, dma, access, pollLimit,
            &pool, slots, ref allocation, ref token, ref result);
        // Reset is the sole device-lifetime boundary, including timeout and
        // failures after an MMIO callback may already have performed its write.
        result.CleanupStatus = VirtqueuePollingStatus.ResetFailed;
        byte status = 0xff;
        if (access.Write8(access.Context, common.VirtualStart + 20, 0) == 1)
            for (uint i = 0; i < resetLimit; i++)
            {
                if (access.Read8(access.Context, common.VirtualStart + 20, &status) != 1) break;
                if (status == 0) { result.ResetComplete = true; break; }
            }
        result.BusMasterDisabled = access.BusMaster(access.Context, 0) == 1;
        if (!result.ResetComplete) return result;
        if (!result.BusMasterDisabled) { result.CleanupStatus = VirtqueuePollingStatus.BusMasterFailed; return result; }
        if (!Fence(access, ref result)) { result.CleanupStatus = VirtqueuePollingStatus.OrderingFailed; return result; }
        result.CleanupStatus = VirtqueuePollingStatus.Success;
        if (allocation.IsInitialized)
        {
            // GiveToDevice occurs immediately after allocation, before any
            // queue address is programmed. Its rejection never releases data.
            if (PageAlignedDmaBuffers.TryReturnToCpu(dma, allocation.Token) != DmaBufferStatus.Success ||
                PageAlignedDmaBuffers.TryRelease(dma, allocation.Token) != DmaBufferStatus.Success)
                result.CleanupStatus = VirtqueuePollingStatus.DmaRejected;
            else result.DmaReleased = true;
        }
        if (token.IsInitialized)
        {
            if (result.Published && VirtqueueDescriptors.TryReclaim(&pool, token) != VirtqueueDescriptorStatus.Success)
                result.CleanupStatus = VirtqueuePollingStatus.DescriptorRejected;
            else if (VirtqueueDescriptors.TryRelease(&pool, token) != VirtqueueDescriptorStatus.Success)
                result.CleanupStatus = VirtqueuePollingStatus.DescriptorRejected;
        }
        return result;
    }

    private static VirtqueuePollingStatus Execute(VirtioMappedRegion common, VirtioMappedRegion notify,
        uint multiplier, DmaBufferPool* dma, VirtqueuePollingAccess a, uint pollLimit,
        VirtqueueDescriptorPoolState* descriptors, VirtqueueDescriptorSlot* slots,
        ref DmaBufferAllocation allocation, ref VirtqueueDescriptorToken token, ref VirtqueuePollingResult r)
    {
        ulong c = common.VirtualStart;
        var status = Status(c, 11, a);
        if (status != VirtqueuePollingStatus.Success) return status;
        ushort queues = 0, enabled = 0, maximum = 0, offset = 0, vector = 0;
        if (a.Read16(a.Context, c + 18, &queues) != 1) return VirtqueuePollingStatus.ReadFailed;
        if (queues <= QueueIndex) return VirtqueuePollingStatus.InvalidQueue;
        if (a.Write16(a.Context, c + 22, QueueIndex) != 1) return VirtqueuePollingStatus.WriteFailed;
        if (a.Read16(a.Context, c + 28, &enabled) != 1 || a.Read16(a.Context, c + 24, &maximum) != 1 ||
            a.Read16(a.Context, c + 30, &offset) != 1) return VirtqueuePollingStatus.ReadFailed;
        r.MaximumSize = maximum; r.NotifyOffset = offset;
        if (enabled != 0 || BoundedVirtqueueSize.Validate(QueueSize, maximum, out r.Size) != VirtqueueSizeStatus.Success)
            return VirtqueuePollingStatus.InvalidQueue;
        ulong displacement = (ulong)offset * multiplier;
        if (displacement > notify.Bytes - 2u) return VirtqueuePollingStatus.InvalidNotify;
        r.NotifyAddress = notify.VirtualStart + displacement;
        if (PageAlignedDmaBuffers.TryAllocate(dma, 1, out allocation) != DmaBufferStatus.Success)
            return VirtqueuePollingStatus.DmaRejected;
        r.CpuAddress = allocation.Range.CpuVirtualAddress; r.DeviceAddress = allocation.Range.DeviceAddress;
        if (allocation.Range.ByteLength != 4096 || r.CpuAddress >= 0x0000800000000000UL - 4096 ||
            PageAlignedDmaBuffers.TryGiveToDevice(dma, allocation.Token) != DmaBufferStatus.Success)
            return VirtqueuePollingStatus.DmaRejected;
        if (VirtqueueDescriptors.TryInitialize(descriptors, slots, QueueSize, 1) != VirtqueueDescriptorStatus.Success ||
            VirtqueueDescriptors.TryAllocate(descriptors, out token) != VirtqueueDescriptorStatus.Success)
            return VirtqueuePollingStatus.DescriptorRejected;
        var layout = VirtqueueSplitRingPlan.Create(QueueSize);
        if (!layout.Succeeded || layout.Plan.EndOffset > PayloadOffset) return VirtqueuePollingStatus.InvalidQueue;
        var plan = layout.Plan;
        byte* data = (byte*)r.CpuAddress;
        for (uint i = 0; i < 4096; i++) data[i] = 0;
        // Local experimental EtherType 0x88b5. No IP/ARP or remote destination.
        data[PayloadOffset + 12] = 2; data[PayloadOffset + 17] = 2;
        data[PayloadOffset + 18] = 2; data[PayloadOffset + 23] = 1;
        data[PayloadOffset + 24] = 0x88; data[PayloadOffset + 25] = 0xb5;
        byte* descriptor = data + plan.DescriptorTable.Offset + (uint)token.Index * 16;
        *(ulong*)descriptor = r.DeviceAddress + PayloadOffset;
        *(uint*)(descriptor + 8) = PayloadBytes; // flags and next remain zero.
        ushort* available = (ushort*)(data + plan.AvailableRing.Offset);
        available[0] = 1; // VIRTQ_AVAIL_F_NO_INTERRUPT; PCI INTx remains disabled.
        available[2] = token.Index;
        if (!Fence(a, ref r)) return VirtqueuePollingStatus.OrderingFailed;
        if (a.Write16(a.Context, c + 24, QueueSize) != 1 || a.Write16(a.Context, c + 26, 0xffff) != 1 ||
            !WriteAddress(a, c + 32, r.DeviceAddress + plan.DescriptorTable.Offset) ||
            !WriteAddress(a, c + 40, r.DeviceAddress + plan.AvailableRing.Offset) ||
            !WriteAddress(a, c + 48, r.DeviceAddress + plan.UsedRing.Offset)) return VirtqueuePollingStatus.WriteFailed;
        if (a.Read16(a.Context, c + 24, &maximum) != 1 || a.Read16(a.Context, c + 26, &vector) != 1)
            return VirtqueuePollingStatus.ReadFailed;
        if (maximum != QueueSize || vector != 0xffff) return VirtqueuePollingStatus.InvalidQueue;
        status = Status(c, 11, a);
        if (status != VirtqueuePollingStatus.Success) return status;
        if (a.Write16(a.Context, c + 28, 1) != 1) return VirtqueuePollingStatus.WriteFailed;
        if (a.Read16(a.Context, c + 28, &enabled) != 1) return VirtqueuePollingStatus.ReadFailed;
        if (enabled != 1) return VirtqueuePollingStatus.InvalidQueue;
        if (a.BusMaster(a.Context, 1) != 1) return VirtqueuePollingStatus.BusMasterFailed;
        status = Status(c, 11, a); // Never clear a newly asserted device-failure bit.
        if (status != VirtqueuePollingStatus.Success) return status;
        if (a.Write8(a.Context, c + 20, 15) != 1) return VirtqueuePollingStatus.WriteFailed;
        status = Status(c, 15, a);
        if (status != VirtqueuePollingStatus.Success) return status;
        if (VirtqueueDescriptors.TryPublish(descriptors, token) != VirtqueueDescriptorStatus.Success)
            return VirtqueuePollingStatus.DescriptorRejected;
        r.Published = true;
        // All descriptor/ring writes precede this publication fence. The
        // second fence orders idx before notification (always notifying is legal).
        if (!Fence(a, ref r)) return VirtqueuePollingStatus.OrderingFailed;
        available[1] = 1; r.AvailableIndex = 1;
        if (!Fence(a, ref r)) return VirtqueuePollingStatus.OrderingFailed;
        if (a.Write16(a.Context, r.NotifyAddress, QueueIndex) != 1) return VirtqueuePollingStatus.WriteFailed;
        ushort used = 0;
        for (uint i = 0; i < pollLimit; i++)
        {
            r.Polls++;
            status = Status(c, 15, a);
            if (status != VirtqueuePollingStatus.Success) return status;
            if (a.Read16(a.Context, r.CpuAddress + plan.UsedRing.Offset + 2, &used) != 1)
                return VirtqueuePollingStatus.ReadFailed;
            r.UsedIndex = used;
            if (used == 0) continue;
            if (BoundedVirtqueueSize.CountPending(0, used, QueueSize, out ushort count) != VirtqueueSizeStatus.Success || count != 1)
                return VirtqueuePollingStatus.InvalidCompletion;
            if (!Fence(a, ref r)) return VirtqueuePollingStatus.OrderingFailed;
            uint id = 0, length = 0;
            if (a.Read32(a.Context, r.CpuAddress + plan.UsedRing.Offset + 4, &id) != 1 ||
                a.Read32(a.Context, r.CpuAddress + plan.UsedRing.Offset + 8, &length) != 1)
                return VirtqueuePollingStatus.ReadFailed;
            r.UsedId = id; r.UsedLength = length;
            if (BoundedVirtqueueSize.TryDescriptor(id, QueueSize, out ushort index) != VirtqueueSizeStatus.Success ||
                index != token.Index || length != 0) return VirtqueuePollingStatus.InvalidCompletion;
            return Status(c, 15, a);
        }
        return VirtqueuePollingStatus.Timeout;
    }

    private static bool WriteAddress(VirtqueuePollingAccess a, ulong register, ulong value)
    {
        if (a.Write32(a.Context, register, (uint)value) != 1 || a.Write32(a.Context, register + 4, (uint)(value >> 32)) != 1) return false;
        uint low = 0, high = 0;
        return a.Read32(a.Context, register, &low) == 1 && a.Read32(a.Context, register + 4, &high) == 1 &&
            (((ulong)high << 32) | low) == value;
    }
    private static VirtqueuePollingStatus Status(ulong common, byte expected, VirtqueuePollingAccess a)
    {
        byte value = 0;
        return a.Read8(a.Context, common + 20, &value) != 1 ? VirtqueuePollingStatus.ReadFailed :
            value == expected ? VirtqueuePollingStatus.Success : VirtqueuePollingStatus.StatusRejected;
    }
    private static bool Fence(VirtqueuePollingAccess a, ref VirtqueuePollingResult result)
    {
        if (MemoryOrder.Fence(a.Context, a.Fence) != MemoryOrderStatus.Success) return false;
        result.Fences++;
        return true;
    }
}
