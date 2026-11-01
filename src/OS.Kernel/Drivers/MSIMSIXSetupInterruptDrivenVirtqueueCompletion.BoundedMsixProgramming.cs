using OS.Std.Binary;

namespace OS.Drivers;
/// <summary>Programs only MSI-X entry zero for vector 0xe0, physical BSP delivery.
/// All other entries stay masked; configuration-change interrupts use NO_VECTOR.
/// Prepare leaves the function mask set, so the queue can be published before
/// the bounded wait explicitly unmasks it. Failure does not imply rollback; the
/// adapter must keep IF clear, mask/disable and reset or retain all backing.</summary>

public static unsafe class BoundedMsixProgramming
{
    public static MsixProgrammingResult Prepare(MsixCapability cap, MsixRegions regions, ulong configPage, VirtioMappedRegion common, byte bspId, MsixRegisterAccess a)
    {
        MsixProgrammingResult r = default;
        r.Status = MsixStatus.InvalidAccess;
        if (!ControlAccess(cap, configPage, a) || a.Read32 == null || a.Write32 == null || !common.IsValid || common.Bytes < 56 || (common.VirtualStart & 3) != 0 || bspId == 255)
            return r;
        r.Status = MsixStatus.InvalidAddress;
        if (!regions.Table.IsValid || !regions.Pba.IsValid || regions.Table.Bytes != (uint)cap.TableSize * 16 || regions.Pba.Bytes != ((uint)cap.TableSize + 63) / 64 * 8 || (regions.Table.PhysicalStart < regions.Pba.PhysicalStart + regions.Pba.Bytes && regions.Pba.PhysicalStart < regions.Table.PhysicalStart + regions.Table.Bytes))
            return r;
        r.Status = Program(cap, regions, configPage, common, bspId, a, ref r);
        return r;
    }

    private static MsixStatus Program(MsixCapability cap, MsixRegions regions, ulong configPage, VirtioMappedRegion common, byte bspId, MsixRegisterAccess a, ref MsixProgrammingResult r)
    {
        ulong control = configPage + cap.CapabilityOffset + 2;
        ushort observed = 0;
        if (a.Read16(a.Context, control, &observed) != 1)
            return MsixStatus.ReadFailed;
        if (observed != cap.Control)
            return MsixStatus.ReadBackFailed;
        r.Control = (ushort)((cap.Control & 0x7ff) | 0xc000);
        var status = Write16(a, control, r.Control);
        if (status != MsixStatus.Success)
            return status;
        for (uint i = 0; i < cap.TableSize; i++)
        {
            status = Write32(a, regions.Table.PhysicalStart + i * 16 + 12, 1);
            if (status != MsixStatus.Success)
                return status;
            r.MaskedEntries++;
        }

        for (uint i = 0; i < regions.Pba.Bytes; i += 4)
        {
            uint pending = 0;
            if (a.Read32(a.Context, regions.Pba.PhysicalStart + i, &pending) != 1)
                return MsixStatus.ReadFailed;
            if (pending != 0)
                return MsixStatus.PendingInterrupt;
            r.PendingWords++;
        }

        r.MessageAddress = 0xfee00000U | ((uint)bspId << 12);
        r.MessageData = BoundedXApicSetup.CompletionVector;
        status = Write32(a, regions.Table.PhysicalStart, r.MessageAddress);
        if (status != MsixStatus.Success)
            return status;
        status = Write32(a, regions.Table.PhysicalStart + 4, 0);
        if (status != MsixStatus.Success)
            return status;
        status = Write32(a, regions.Table.PhysicalStart + 8, r.MessageData);
        if (status != MsixStatus.Success)
            return status;
        status = Write16(a, common.VirtualStart + 16, 0xffff);
        if (status != MsixStatus.Success)
            return status;
        r.ConfigVector = 0xffff;
        // Entry zero becomes eligible, but the function mask still blocks delivery.
        return Write32(a, regions.Table.PhysicalStart + 12, 0);
    }

    public static MsixStatus SetFunctionMask(MsixCapability cap, ulong configPage, bool masked, MsixRegisterAccess a)
    {
        if (!ControlAccess(cap, configPage, a))
            return MsixStatus.InvalidAccess;
        ulong control = configPage + cap.CapabilityOffset + 2;
        ushort observed = 0;
        if (a.Read16(a.Context, control, &observed) != 1)
            return MsixStatus.ReadFailed;
        ushort enabled = (ushort)((cap.Control & 0x7ff) | 0x8000);
        if ((observed & ~0x4000) != enabled)
            return MsixStatus.ReadBackFailed;
        return Write16(a, control, (ushort)(enabled | (masked ? 0x4000 : 0)));
    }

    public static MsixStatus Disable(MsixCapability cap, ulong configPage, MsixRegisterAccess a)
    {
        if (!ControlAccess(cap, configPage, a))
            return MsixStatus.InvalidAccess;
        ulong control = configPage + cap.CapabilityOffset + 2;
        ushort observed = 0;
        if (a.Read16(a.Context, control, &observed) != 1)
            return MsixStatus.ReadFailed;
        if ((observed & 0x3fff) != (cap.Control & 0x7ff))
            return MsixStatus.ReadBackFailed;
        var status = Write16(a, control, (ushort)(observed | 0x4000));
        return status == MsixStatus.Success ? Write16(a, control, cap.Control) : status;
    }

    private static bool ControlAccess(MsixCapability cap, ulong page, MsixRegisterAccess a) => cap.IsValid && page >= 0x100000 && page <= 0x0000800000000000UL - 4096 && (page & 4095) == 0 && a.Read16 != null && a.Write16 != null;
    private static MsixStatus Write16(MsixRegisterAccess a, ulong address, ushort value)
    {
        if (a.Write16(a.Context, address, value) != 1)
            return MsixStatus.WriteFailed;
        ushort observed = 0;
        if (a.Read16(a.Context, address, &observed) != 1)
            return MsixStatus.ReadFailed;
        return observed == value ? MsixStatus.Success : MsixStatus.ReadBackFailed;
    }

    private static MsixStatus Write32(MsixRegisterAccess a, ulong address, uint value)
    {
        if (a.Write32(a.Context, address, value) != 1)
            return MsixStatus.WriteFailed;
        uint observed = 0;
        if (a.Read32(a.Context, address, &observed) != 1)
            return MsixStatus.ReadFailed;
        return observed == value ? MsixStatus.Success : MsixStatus.ReadBackFailed;
    }
}
