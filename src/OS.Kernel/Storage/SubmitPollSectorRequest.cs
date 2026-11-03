using OS.Drivers;

namespace OS.Storage;

/// <summary>Exactly one 512-byte IN request on a fresh queue zero. Requires
/// VERSION_1 (and RO when offered), FEATURES_OK=11, initially MSI-X disabled, no optional
/// ring/block features, and firmware DMA already quiesced. Uses descriptors 0-2
/// exactly once. Reset is the sole lifetime boundary; no queue reuse or writes.
/// Destination and pool metadata are exclusive mapped RAM, without hidden
/// physical aliases. Original backing stays live through return, including the
/// post-reset copy after pool-token release. Uncertain cleanup retains backing.
/// No output bytes are committed unless completion AND cleanup succeed.</summary>
public static unsafe class SubmitPollSectorRequest
{
    public static VirtioBlkReadResult Run(VirtioMappedRegion common, VirtioMappedRegion notify,
        VirtioMappedRegion device, uint notifyMultiplier, DmaBufferPool* dma, VirtioBlkReadAccess access,
        ulong sector, byte* destination, uint destinationBytes, uint pollLimit, uint resetLimit, ulong expectedCapacity = 0)
    {
        VirtioBlkReadResult r = default;
        r.Status = VirtioBlkReadStatus.InvalidAccess;
        r.Sector = sector;
        r.DeviceStatus = 0xff;
        var a = access.Queue;
        if (dma == null || ((ulong)dma & 7) != 0 || (ulong)dma < 4096 ||
            (ulong)dma >= 0x0000800000000000UL - (ulong)sizeof(DmaBufferPool) ||
            !common.IsValid || common.Bytes < 56 || (common.VirtualStart & 3) != 0 ||
            !notify.IsValid || notify.Bytes < 2 || (notify.VirtualStart & 1) != 0 ||
            !device.IsValid || device.Bytes < 8 || (device.VirtualStart & 3) != 0 ||
            (notifyMultiplier != 0 && (notifyMultiplier < 2 || (notifyMultiplier & (notifyMultiplier - 1)) != 0)) ||
            pollLimit == 0 || pollLimit > 1000000 || resetLimit == 0 || resetLimit > 1024 ||
            a.Read8 == null || a.Write8 == null || a.Read16 == null || a.Write16 == null ||
            a.Read32 == null || a.Write32 == null || a.Fence == null || a.BusMasterOff == null || access.BusMasterOn == null ||
            ((access.PrepareInterrupt != null || access.WaitInterrupt != null || access.StopInterrupt != null) &&
             (access.PrepareInterrupt == null || access.WaitInterrupt == null || access.StopInterrupt == null)))
            return r;
        if (dma->Self != (ulong)dma || dma->Slots == null || dma->Count == 0 || dma->Count > 64)
            return r;
        if (destinationBytes < 512 || (ulong)destination < 4096 ||
            (ulong)destination >= 0x0000800000000000UL - 512 ||
            Aliases(destination, common) || Aliases(destination, notify) || Aliases(destination, device) ||
            Overlap((ulong)destination, 512, (ulong)dma, (ulong)sizeof(DmaBufferPool)) ||
            Overlap((ulong)destination, 512, (ulong)dma->Slots, (ulong)dma->Count * (ulong)sizeof(DmaBufferSlot)))
        {
            r.Status = VirtioBlkReadStatus.InvalidDestination;
            return r;
        }
        DmaBufferAllocation allocation = default;
        bool deviceOwned = false;
        r.Status = Execute(common, notify, device, notifyMultiplier, dma, access, destination,
            pollLimit, expectedCapacity, ref allocation, ref deviceOwned, ref r);
        if (r.InterruptAttempted)
        {
            r.InterruptStopped = access.StopInterrupt(a.Context) == 1;
            if (!r.InterruptStopped)
                r.Status = VirtioBlkReadStatus.InterruptShutdownFailed;
        }
        // Reset and bus-master-off still run after uncertain interrupt shutdown,
        // but backing/token ownership is retained; no payload copy is permitted.
        VirtioBlkQueueN0.Cleanup(common, dma, a, resetLimit,
            r.InterruptAttempted && !r.InterruptStopped ? default : allocation, deviceOwned, ref r.Queue);
        if (r.Status == VirtioBlkReadStatus.Success && r.Queue.Succeeded)
        {
            byte* source = (byte*)(r.Queue.Layout.Range.CpuVirtualAddress + VirtioBlkDmaLayout.DataOffset);
            for (uint i = 0; i < 512; i++)
                destination[i] = source[i];
            r.BytesRead = 512;
        }
        return r;
    }

    private static VirtioBlkReadStatus Execute(VirtioMappedRegion common, VirtioMappedRegion notify,
        VirtioMappedRegion device, uint multiplier, DmaBufferPool* dma, VirtioBlkReadAccess access,
        byte* destination, uint pollLimit, ulong expectedCapacity, ref DmaBufferAllocation allocation, ref bool deviceOwned, ref VirtioBlkReadResult r)
    {
        var a = access.Queue;
        if (a.BusMasterOff(a.Context) != 1)
            return VirtioBlkReadStatus.BusMasterFailed;
        var status = CheckStatus(common.VirtualStart, 11, a);
        if (status != VirtioBlkReadStatus.Success)
            return status;
        status = ReadCapacity(common, device, a, ref r);
        if (status != VirtioBlkReadStatus.Success)
            return status;
        if (expectedCapacity != 0 && r.Capacity != expectedCapacity)
            return VirtioBlkReadStatus.ConfigurationChanged;
        if (r.Sector >= r.Capacity)
            return VirtioBlkReadStatus.SectorOutOfRange;
        if (access.PrepareInterrupt != null)
        {
            r.InterruptAttempted = true; // A failed callback may have written hardware.
            r.InterruptPrepared = access.PrepareInterrupt(a.Context, common) == 1;
            if (!r.InterruptPrepared)
                return VirtioBlkReadStatus.InterruptPrepareFailed;
        }
        r.Queue.Status = VirtioBlkQueueN0.Configure(common, dma, a, ref allocation, ref deviceOwned,
            ref r.Queue, notify, device, (ulong)destination, r.InterruptPrepared ? (ushort)0 : (ushort)0xffff);
        if (r.Queue.Status != VirtioBlkQueueStatus.Success)
            return VirtioBlkReadStatus.QueueRejected;
        ushort offset = 0;
        if (a.Read16(a.Context, common.VirtualStart + 30, &offset) != 1)
            return VirtioBlkReadStatus.ReadFailed;
        r.NotifyOffset = offset;
        ulong displacement = (ulong)offset * multiplier;
        if (displacement > notify.Bytes - 2u)
            return VirtioBlkReadStatus.InvalidNotify;
        r.NotifyAddress = notify.VirtualStart + displacement;
        var layout = r.Queue.Layout;
        byte* bytes = (byte*)layout.Range.CpuVirtualAddress;
        ulong address = layout.Range.DeviceAddress;
        // IN=0 and reserved=0 were cleared while CPU-owned. Only the sector varies.
        *(ulong*)(bytes + VirtioBlkDmaLayout.RequestOffset + 8) = r.Sector;
        WriteDescriptor(bytes, address + VirtioBlkDmaLayout.RequestOffset, 16, 1, 1);
        WriteDescriptor(bytes + 16, address + VirtioBlkDmaLayout.DataOffset, 512, 3, 2);
        WriteDescriptor(bytes + 32, address + VirtioBlkDmaLayout.StatusOffset, 1, 2, 0);
        ushort* available = (ushort*)(bytes + layout.Rings.AvailableRing.Offset);
        // A prepared MSI-X transaction needs completion delivery. The default
        // polling path retains NO_INTERRUPT; masked recovery is controlled by
        // the admitted MSI-X function mask, not by suppressing the ring event.
        available[0] = r.InterruptPrepared ? (ushort)0 : (ushort)1;
        available[2] = 0;
        if (!Fence(a, ref r))
            return VirtioBlkReadStatus.OrderingFailed;
        if (access.BusMasterOn(a.Context) != 1)
            return VirtioBlkReadStatus.BusMasterFailed;
        status = CheckStatus(common.VirtualStart, 11, a);
        if (status != VirtioBlkReadStatus.Success)
            return status;
        if (a.Write8(a.Context, common.VirtualStart + 20, 15) != 1)
            return VirtioBlkReadStatus.WriteFailed;
        status = CheckStatus(common.VirtualStart, 15, a);
        if (status != VirtioBlkReadStatus.Success)
            return status;
        byte generation = 0;
        if (a.Read8(a.Context, common.VirtualStart + 21, &generation) != 1)
            return VirtioBlkReadStatus.ReadFailed;
        if (generation != r.Generation)
            return VirtioBlkReadStatus.ConfigurationChanged;
        // Descriptor/header/data writes must precede idx; idx must precede notify.
        if (!Fence(a, ref r))
            return VirtioBlkReadStatus.OrderingFailed;
        available[1] = 1;
        r.Published = true;
        if (!Fence(a, ref r))
            return VirtioBlkReadStatus.OrderingFailed;
        if (a.Write16(a.Context, r.NotifyAddress, 0) != 1)
            return VirtioBlkReadStatus.WriteFailed;
        r.Notified = true;
        if (r.InterruptPrepared)
        {
            r.CompletionRoute = (BlockCompletionRoute)access.WaitInterrupt(a.Context, pollLimit);
            if (r.CompletionRoute != BlockCompletionRoute.Interrupt && r.CompletionRoute != BlockCompletionRoute.PollingRecovery)
                return VirtioBlkReadStatus.InterruptWaitFailed;
            // IRQ is notification, not completion. Acquire and validate the same
            // used ring/status/generation; recovery keeps the original poll bound.
            if (r.CompletionRoute == BlockCompletionRoute.Interrupt)
                pollLimit = 1;
        }
        return UsedRingCompletionStatusByteTimeoutDeviceReset.Poll(common, a, pollLimit, ref r);
    }

    private static VirtioBlkReadStatus ReadCapacity(VirtioMappedRegion common, VirtioMappedRegion device,
        VirtioBlkQueueAccess a, ref VirtioBlkReadResult r)
    {
        byte before = 0, after = 0;
        uint low = 0, high = 0;
        for (uint i = 0; i < 16; i++)
        {
            r.CapacityReads++;
            if (a.Read8(a.Context, common.VirtualStart + 21, &before) != 1 ||
                a.Read32(a.Context, device.VirtualStart, &low) != 1 ||
                a.Read32(a.Context, device.VirtualStart + 4, &high) != 1 ||
                a.Read8(a.Context, common.VirtualStart + 21, &after) != 1)
                return VirtioBlkReadStatus.ReadFailed;
            if (before != after)
                continue;
            r.Capacity = ((ulong)high << 32) | low;
            r.Generation = after;
            return r.Capacity == 0 || r.Capacity > ulong.MaxValue / 512
                ? VirtioBlkReadStatus.InvalidCapacity : VirtioBlkReadStatus.Success;
        }
        return VirtioBlkReadStatus.ConfigurationChanged;
    }
    private static void WriteDescriptor(byte* descriptor, ulong address, uint length, ushort flags, ushort next)
    {
        *(ulong*)descriptor = address;
        *(uint*)(descriptor + 8) = length;
        *(ushort*)(descriptor + 12) = flags;
        *(ushort*)(descriptor + 14) = next;
    }
    private static bool Aliases(byte* output, VirtioMappedRegion region) =>
        (ulong)output < region.VirtualStart + region.Bytes && region.VirtualStart < (ulong)output + 512;
    private static bool Overlap(ulong a, ulong n, ulong b, ulong m) => a < b + m && b < a + n;
    internal static VirtioBlkReadStatus CheckStatus(ulong common, byte expected, VirtioBlkQueueAccess a)
    {
        byte value = 0;
        return a.Read8(a.Context, common + 20, &value) != 1 ? VirtioBlkReadStatus.ReadFailed :
            value == expected ? VirtioBlkReadStatus.Success : VirtioBlkReadStatus.StatusRejected;
    }
    internal static bool Fence(VirtioBlkQueueAccess a, ref VirtioBlkReadResult r)
    {
        if (a.Fence(a.Context) != 1)
            return false;
        r.Fences++;
        return true;
    }
}
