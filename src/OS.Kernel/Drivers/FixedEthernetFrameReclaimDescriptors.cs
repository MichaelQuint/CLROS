namespace OS.Drivers;

/// <summary>One fixed 60-byte experimental Ethernet frame, preceded by the
/// VERSION_1 12-byte zero header. RX is primed, TX used id/index/length and fresh
/// device status are checked, then reset, bus-master disable and a fence permit
/// descriptor and DMA release. Completion is local device evidence; external
/// packet capture is needed to establish transmission outside the device.</summary>
public static unsafe class FixedEthernetFrameReclaimDescriptors
{
    public static VirtioNetQueueResult Run(VirtioMappedRegion common, VirtioMappedRegion notify,
        uint multiplier, DmaBufferPool* dma, VirtqueuePollingAccess a, uint pollLimit, uint resetLimit)
    {
        VirtioNetQueueResult r = default;
        r.Status = VirtioNetQueueStatus.InvalidAccess;
        if (!VirtioNetQueue.Valid(common, notify, multiplier, a, resetLimit) || pollLimit == 0 || pollLimit > 1000000)
            return r;
        DmaBufferAllocation allocation = default;
        bool deviceOwned = false;
        VirtqueueDescriptorPoolState rx = default, tx = default;
        VirtqueueDescriptorSlot* slots = stackalloc VirtqueueDescriptorSlot[VirtioNetQueue.Size * 2];
        for (uint i = 0; i < VirtioNetQueue.Size * 2; i++)
            slots[i] = default;
        VirtqueueDescriptorToken rxToken = default, txToken = default;
        r.Status = VirtioNetQueue.Prepare(common, notify, multiplier, dma, a, ref allocation, ref deviceOwned,
            &rx, &tx, slots, ref rxToken, ref txToken, ref r);
        if (r.Status == VirtioNetQueueStatus.Success)
            r.Status = Transmit(common, a, pollLimit, &tx, txToken, ref r);
        VirtioNetQueue.Cleanup(common, dma, a, resetLimit, allocation, deviceOwned, &rx, &tx, rxToken, txToken, ref r);
        return r;
    }

    private static VirtioNetQueueStatus Transmit(VirtioMappedRegion common, VirtqueuePollingAccess a,
        uint pollLimit, VirtqueueDescriptorPoolState* tx, VirtqueueDescriptorToken token, ref VirtioNetQueueResult r)
    {
        byte* payload = (byte*)(r.CpuAddress + VirtioNetQueue.TransmitBufferOffset);
        // Local unicast destination 02:00:00:00:00:02, source ...:01,
        // experimental EtherType 88b5, deterministic CLROS-M07 tag, zero padding.
        payload[12] = 2;
        payload[17] = 2;
        payload[18] = 2;
        payload[23] = 1;
        payload[24] = 0x88;
        payload[25] = 0xb5;
        payload[26] = 67;
        payload[27] = 76;
        payload[28] = 82;
        payload[29] = 79;
        payload[30] = 83;
        payload[31] = 45;
        payload[32] = 77;
        payload[33] = 48;
        payload[34] = 55;
        if (a.BusMaster(a.Context, 1) != 1)
            return VirtioNetQueueStatus.BusMasterFailed;
        var status = VirtioNetQueue.Status(common, a, 11);
        if (status != VirtioNetQueueStatus.Success)
            return status;
        if (a.Write8(a.Context, common.VirtualStart + 20, 15) != 1)
            return VirtioNetQueueStatus.WriteFailed;
        status = VirtioNetQueue.Status(common, a, 15);
        if (status != VirtioNetQueueStatus.Success)
            return status;
        if (VirtqueueDescriptors.TryPublish(tx, token) != VirtqueueDescriptorStatus.Success)
            return VirtioNetQueueStatus.DescriptorRejected;
        r.Transmit.Published = true;
        if (!VirtioNetQueue.Fence(a, ref r))
            return VirtioNetQueueStatus.OrderingFailed;
        *(ushort*)(r.CpuAddress + (r.Transmit.Available - r.DeviceAddress) + 2) = 1;
        r.Transmit.AvailableIndex = 1;
        if (!VirtioNetQueue.Fence(a, ref r))
            return VirtioNetQueueStatus.OrderingFailed;
        if (a.Write16(a.Context, r.Receive.Notify, 0) != 1 || a.Write16(a.Context, r.Transmit.Notify, 1) != 1)
            return VirtioNetQueueStatus.WriteFailed;
        ulong used = r.CpuAddress + (r.Transmit.Used - r.DeviceAddress);
        for (uint i = 0; i < pollLimit; i++)
        {
            r.Polls++;
            status = VirtioNetQueue.Status(common, a, 15);
            if (status != VirtioNetQueueStatus.Success)
                return status;
            ushort index = 0;
            if (a.Read16(a.Context, used + 2, &index) != 1)
                return VirtioNetQueueStatus.ReadFailed;
            r.UsedIndex = index;
            if (index == 0)
                continue;
            if (BoundedVirtqueueSize.CountPending(0, index, VirtioNetQueue.Size, out ushort count) != VirtqueueSizeStatus.Success || count != 1)
                return VirtioNetQueueStatus.InvalidCompletion;
            if (!VirtioNetQueue.Fence(a, ref r))
                return VirtioNetQueueStatus.OrderingFailed;
            uint id = 0, length = 0;
            if (a.Read32(a.Context, used + 4, &id) != 1 || a.Read32(a.Context, used + 8, &length) != 1)
                return VirtioNetQueueStatus.ReadFailed;
            r.UsedId = id;
            r.UsedLength = length;
            if (id != token.Index || length != 0)
                return VirtioNetQueueStatus.InvalidCompletion;
            status = VirtioNetQueue.Status(common, a, 15);
            if (status == VirtioNetQueueStatus.Success)
                r.TransmitCompleted = true;
            return status;
        }
        return VirtioNetQueueStatus.Timeout;
    }
}
