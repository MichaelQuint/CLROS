using OS.Drivers;

namespace OS.Storage;

/// <summary>Completion for the single fresh three-descriptor block IN chain.
/// Polls acquire the device index before reading the element/status. Reset and
/// token release are shared with VirtioBlkQueueN0.Cleanup and run on every
/// started transaction, including any failed publication/notification callback.
/// No completion alone grants permission to reuse/free the DMA backing.</summary>
public static unsafe class UsedRingCompletionStatusByteTimeoutDeviceReset
{
    internal static VirtioBlkReadStatus Poll(VirtioMappedRegion common, VirtioBlkQueueAccess a,
        uint pollLimit, ref VirtioBlkReadResult r)
    {
        ulong cpu = r.Queue.Layout.Range.CpuVirtualAddress;
        ulong used = cpu + r.Queue.Layout.Rings.UsedRing.Offset;
        ushort index = 0;
        uint id = 0, length = 0;
        byte deviceStatus = 0xff, generation = 0;
        for (uint i = 0; i < pollLimit; i++)
        {
            r.Polls++;
            var status = SubmitPollSectorRequest.CheckStatus(common.VirtualStart, 15, a);
            if (status != VirtioBlkReadStatus.Success)
                return status;
            if (a.Read16(a.Context, used + 2, &index) != 1)
                return VirtioBlkReadStatus.ReadFailed;
            r.UsedIndex = index;
            if (index == 0)
                continue;
            if (index != 1)
                return VirtioBlkReadStatus.InvalidCompletion;
            if (!SubmitPollSectorRequest.Fence(a, ref r))
                return VirtioBlkReadStatus.OrderingFailed;
            if (a.Read32(a.Context, used + 4, &id) != 1 || a.Read32(a.Context, used + 8, &length) != 1 ||
                a.Read8(a.Context, cpu + VirtioBlkDmaLayout.StatusOffset, &deviceStatus) != 1)
                return VirtioBlkReadStatus.ReadFailed;
            r.UsedId = id;
            r.UsedLength = length;
            r.DeviceStatus = deviceStatus;
            status = Validate(index, id, length, deviceStatus);
            if (status != VirtioBlkReadStatus.Success)
                return status;
            if (a.Read8(a.Context, common.VirtualStart + 21, &generation) != 1)
                return VirtioBlkReadStatus.ReadFailed;
            if (generation != r.Generation)
                return VirtioBlkReadStatus.ConfigurationChanged;
            return SubmitPollSectorRequest.CheckStatus(common.VirtualStart, 15, a);
        }
        return VirtioBlkReadStatus.Timeout;
    }

    public static VirtioBlkReadStatus Validate(ushort usedIndex, uint id, uint length, byte status)
        => Validate(usedIndex, id, length, status, 1);

    /// <summary>Validate one explicitly bounded read's used head, data-plus-status
    /// length and status. Numeric observations supply no device or DMA authority.
    /// Error statuses may report a short used length; success requires every
    /// requested data byte and the status byte. Legacy callers still select1.</summary>
    public static VirtioBlkReadStatus Validate(ushort usedIndex, uint id, uint length, byte status, uint sectors)
    {
        if (sectors is 0 or > VirtioBlkDmaLayout.MaximumReadSectors)
            return VirtioBlkReadStatus.InvalidCompletion;
        uint expected = sectors * VirtioBlkDmaLayout.DataBytes + 1;
        if (usedIndex != 1 || id != 0 || length == 0 || length > expected)
            return VirtioBlkReadStatus.InvalidCompletion;
        if (status == 1)
            return VirtioBlkReadStatus.DeviceIoError;
        if (status == 2)
            return VirtioBlkReadStatus.UnsupportedRequest;
        return status == 0 && length == expected ? VirtioBlkReadStatus.Success : VirtioBlkReadStatus.InvalidCompletion;
    }
}
