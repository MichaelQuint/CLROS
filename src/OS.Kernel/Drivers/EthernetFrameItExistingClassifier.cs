namespace OS.Drivers;
/// <summary>One 60-byte TX trigger and one completed modern RX frame. A fresh
/// exclusive page is reset and bus mastering disabled before caller storage is
/// filled; uncertain cleanup retains backing and exposes no frame. No rearming,
/// offload, multi-buffer assembly, interrupt receive, or continuing traffic.</summary>

public static unsafe class EthernetFrameItExistingClassifier
{
    public static EthernetFrameReceiveResult Run(VirtioMappedRegion common, VirtioMappedRegion notify, uint multiplier, DmaBufferPool* dma, VirtqueuePollingAccess a, uint pollLimit, uint resetLimit, ReadOnlySpan<byte> transmitFrame, Span<byte> receiveBuffer)
    {
        EthernetFrameReceiveResult result = default;
        ref VirtioNetQueueResult r = ref result.Queue;
        r.Status = VirtioNetQueueStatus.InvalidAccess;
        if (!VirtioNetQueue.Valid(common, notify, multiplier, a, resetLimit) || pollLimit == 0 || pollLimit > 1000000 || transmitFrame.Length != 60 || receiveBuffer.Length < VirtioNetQueue.ReceiveBufferBytes)
            return result;
        fixed (byte* input = transmitFrame)
        fixed (byte* output = receiveBuffer)
        {
            if (!Range((ulong)input, transmitFrame.Length) || !Range((ulong)output, receiveBuffer.Length) || !Range((ulong)dma, sizeof(DmaBufferPool)))
                return result;
            if (dma->Self != (ulong)dma || !dma->Window.IsInitialized || dma->Count == 0 || dma->Count > 64)
            {
                r.Status = VirtioNetQueueStatus.DmaRejected;
                return result;
            }

            if (Overlaps((ulong)input, transmitFrame.Length, (ulong)output, (ulong)receiveBuffer.Length) || Aliases((ulong)input, transmitFrame.Length, common, notify, dma) || Aliases((ulong)output, receiveBuffer.Length, common, notify, dma))
            {
                r.Status = VirtioNetQueueStatus.Overlap;
                return result;
            }
        }

        DmaBufferAllocation allocation = default;
        bool deviceOwned = false;
        VirtqueueDescriptorPoolState rx = default, tx = default;
        VirtqueueDescriptorSlot* slots = stackalloc VirtqueueDescriptorSlot[VirtioNetQueue.Size * 2];
        for (uint i = 0; i < VirtioNetQueue.Size * 2; i++)
            slots[i] = default;
        VirtqueueDescriptorToken rxToken = default, txToken = default;
        r.Status = VirtioNetQueue.Prepare(common, notify, multiplier, dma, a, ref allocation, ref deviceOwned, &rx, &tx, slots, ref rxToken, ref txToken, ref r);
        if (r.Status == VirtioNetQueueStatus.Success)
        {
            byte* payload = (byte*)(r.CpuAddress + VirtioNetQueue.TransmitBufferOffset);
            for (int i = 0; i < 60; i++)
                payload[12 + i] = transmitFrame[i];
            r.Status = Exchange(common, a, pollLimit, &tx, txToken, ref result);
        }

        VirtioNetQueue.Quiesce(common, a, resetLimit, ref r);
        // Stage bytes while backing is still owned. Caller publication follows
        // successful descriptor and DMA reclamation, so failure exposes no data.
        byte* staged = stackalloc byte[(int)VirtioNetQueue.ReceiveBufferBytes];
        if (r.Status == VirtioNetQueueStatus.Success && r.CleanupStatus == VirtioNetQueueStatus.Success)
        {
            byte* source = (byte*)(r.CpuAddress + VirtioNetQueue.ReceiveBufferOffset);
            for (int i = 0; i < result.ReceiveUsedLength; i++)
                staged[i] = source[i];
        }

        VirtioNetQueue.ReleaseBacking(dma, allocation, deviceOwned, &rx, &tx, rxToken, txToken, ref r);
        if (r.Succeeded)
        {
            for (int i = 0; i < result.ReceiveUsedLength; i++)
                receiveBuffer[i] = staged[i];
            result.CapturedBytes = result.ReceiveUsedLength;
            result.Classification = VirtioNetReceiveFrameClassifier.ClassifyModern(receiveBuffer.Slice(0, (int)result.CapturedBytes));
        }

        return result;
    }

    private static bool Range(ulong start, int bytes) => start != 0 && start < 0x0000800000000000UL && (ulong)bytes <= 0x0000800000000000UL - start;
    private static bool Aliases(ulong address, int bytes, VirtioMappedRegion common, VirtioMappedRegion notify, DmaBufferPool* dma) => Overlaps(address, bytes, common.VirtualStart, common.Bytes) || Overlaps(address, bytes, notify.VirtualStart, notify.Bytes) || Overlaps(address, bytes, dma->Window.CpuVirtualBase, dma->Window.ByteLength) || Overlaps(address, bytes, (ulong)dma, (ulong)sizeof(DmaBufferPool)) || Overlaps(address, bytes, (ulong)dma->Slots, (ulong)dma->Count * (ulong)sizeof(DmaBufferSlot));
    private static bool Overlaps(ulong start, int bytes, ulong other, ulong otherBytes) => start < other ? other - start < (ulong)bytes : start - other < otherBytes;
    private static VirtioNetQueueStatus Exchange(VirtioMappedRegion common, VirtqueuePollingAccess a, uint pollLimit, VirtqueueDescriptorPoolState* tx, VirtqueueDescriptorToken token, ref EthernetFrameReceiveResult result)
    {
        ref VirtioNetQueueResult r = ref result.Queue;
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
        ulong transmitUsed = r.CpuAddress + (r.Transmit.Used - r.DeviceAddress);
        ulong receiveUsed = r.CpuAddress + (r.Receive.Used - r.DeviceAddress);
        bool receiveCompleted = false;
        for (uint i = 0; i < pollLimit; i++)
        {
            r.Polls++;
            status = VirtioNetQueue.Status(common, a, 15);
            if (status != VirtioNetQueueStatus.Success)
                return status;
            if (!r.TransmitCompleted)
            {
                status = ReadCompletion(a, transmitUsed, false, ref result);
                if (status != VirtioNetQueueStatus.Success)
                    return status;
            }

            if (!receiveCompleted)
            {
                status = ReadCompletion(a, receiveUsed, true, ref result);
                if (status != VirtioNetQueueStatus.Success)
                    return status;
                receiveCompleted = result.ReceiveUsedIndex == 1;
            }

            status = VirtioNetQueue.Status(common, a, 15);
            if (status != VirtioNetQueueStatus.Success)
                return status;
            if (r.TransmitCompleted && receiveCompleted)
                return VirtioNetQueueStatus.Success;
        }

        return VirtioNetQueueStatus.Timeout;
    }

    private static VirtioNetQueueStatus ReadCompletion(VirtqueuePollingAccess a, ulong used, bool receive, ref EthernetFrameReceiveResult result)
    {
        ref VirtioNetQueueResult r = ref result.Queue;
        ushort index = 0;
        if (a.Read16(a.Context, used + 2, &index) != 1)
            return VirtioNetQueueStatus.ReadFailed;
        if (receive)
            result.ReceiveUsedIndex = index;
        else
            r.UsedIndex = index;
        if (index == 0)
            return VirtioNetQueueStatus.Success;
        if (BoundedVirtqueueSize.CountPending(0, index, VirtioNetQueue.Size, out ushort count) != VirtqueueSizeStatus.Success || count != 1)
            return VirtioNetQueueStatus.InvalidCompletion;
        if (!VirtioNetQueue.Fence(a, ref r))
            return VirtioNetQueueStatus.OrderingFailed;
        uint id = 0, length = 0;
        if (a.Read32(a.Context, used + 4, &id) != 1 || a.Read32(a.Context, used + 8, &length) != 1)
            return VirtioNetQueueStatus.ReadFailed;
        if (receive)
        {
            result.ReceiveUsedId = id;
            result.ReceiveUsedLength = length;
        }
        else
        {
            r.UsedId = id;
            r.UsedLength = length;
        }

        if (id != 0 || (receive ? length < 26 || length > VirtioNetQueue.ReceiveBufferBytes : length != 0))
            return VirtioNetQueueStatus.InvalidCompletion;
        if (!receive)
            r.TransmitCompleted = true;
        return VirtioNetQueueStatus.Success;
    }
}
