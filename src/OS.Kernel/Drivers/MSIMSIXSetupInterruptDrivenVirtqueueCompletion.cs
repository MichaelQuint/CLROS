using OS.Std.Binary;

namespace OS.Drivers;
/// <summary>
/// Numeric declarations from stable caller-owned snapshots, never MMIO access or
/// interrupt setup. The adapter authenticates device/BAR identity, lifetime and
/// UC/RW/NX mappings, including disjointness from RAM and other device resources.
/// Table and PBA bytes must not overlap; their mapping pages may be shared.
/// Physical ranges are restricted to the lower 48-bit canonical address domain.
/// </summary>

public static unsafe class MSIMSIXSetupInterruptDrivenVirtqueueCompletion
{
    public static MsixStatus Discover(byte* config, uint bytes, out MsixCapability capability)
    {
        capability = default;
        if (bytes != 256)
            return MsixStatus.InvalidStorage;
        var walk = PciCapabilityWalk.Validate(config, bytes);
        if (walk.Status == PciCapabilityWalkStatus.InvalidStorage)
            return MsixStatus.InvalidStorage;
        if (walk.Status == PciCapabilityWalkStatus.AbsentDevice)
            return MsixStatus.AbsentDevice;
        if (walk.Status == PciCapabilityWalkStatus.UnsupportedHeader)
            return MsixStatus.UnsupportedHeader;
        if (!walk.Succeeded)
            return MsixStatus.InvalidChain;
        if ((config[0x0e] & 0x7f) != 0)
            return MsixStatus.UnsupportedHeader;
        MsixCapability pending = default;
        uint node = config[0x34];
        ulong occupied = 0;
        for (uint i = 0; i < walk.Count; i++)
        {
            bool msix = config[node] == 0x11;
            if (msix && node > 244)
                return MsixStatus.TruncatedCapability;
            // Unknown capability payloads remain opaque; protect every linked header
            // against the entire known 12-byte MSI-X record, in either list order.
            ulong extent = (msix ? 7UL : 1UL) << (int)(node >> 2);
            if ((occupied & extent) != 0)
                return MsixStatus.Overlap;
            occupied |= extent;
            if (msix)
            {
                if (pending.IsValid)
                    return MsixStatus.DuplicateCapability;
                ushort control = (ushort)(EndianPrimitives.ReadUInt32LittleEndian(config + node) >> 16);
                if ((control & 0x3800) != 0)
                    return MsixStatus.InvalidCapability;
                if ((control & 0x8000) != 0)
                    return MsixStatus.AlreadyEnabled;
                pending = new((byte)node, control, EndianPrimitives.ReadUInt32LittleEndian(config + node + 4), EndianPrimitives.ReadUInt32LittleEndian(config + node + 8));
                if (!pending.IsValid)
                    return MsixStatus.InvalidCapability;
            }

            node = config[node + 1];
        }

        if (!pending.IsValid)
            return MsixStatus.MissingCapability;
        capability = pending;
        return MsixStatus.Success;
    }

    public static MsixStatus Plan(MsixCapability capability, PciSizedBar* sixBars, uint count, out MsixRegions regions)
    {
        regions = default;
        if (sixBars == null || count != 6 || ((ulong)sixBars & 7) != 0 || (ulong)sixBars > ~0UL - 6UL * (ulong)sizeof(PciSizedBar))
            return MsixStatus.InvalidStorage;
        if (!capability.IsValid)
            return MsixStatus.InvalidCapability;
        uint tableBytes = (uint)capability.TableSize * 16;
        uint pbaBytes = ((uint)capability.TableSize + 63) / 64 * 8;
        var status = Region(sixBars, capability.TableBar, capability.TableOffset, tableBytes, out var table);
        if (status != MsixStatus.Success)
            return status;
        status = Region(sixBars, capability.PbaBar, capability.PbaOffset, pbaBytes, out var pba);
        if (status != MsixStatus.Success)
            return status;
        if (table.PhysicalStart < pba.PhysicalStart + pba.Bytes && pba.PhysicalStart < table.PhysicalStart + table.Bytes)
            return MsixStatus.Overlap;
        var tableBar = sixBars[capability.TableBar];
        var pbaBar = sixBars[capability.PbaBar];
        if (capability.TableBar != capability.PbaBar && tableBar.Base < pbaBar.Base + pbaBar.Bytes && pbaBar.Base < tableBar.Base + tableBar.Bytes)
            return MsixStatus.Overlap;
        regions = new(table, pba);
        return MsixStatus.Success;
    }

    private static MsixStatus Region(PciSizedBar* bars, byte index, uint offset, uint bytes, out MsixRegion region)
    {
        region = default;
        var bar = bars[index];
        if ((bar.Kind != PciSizedBarKind.Memory32 && bar.Kind != PciSizedBarKind.Memory64) || (index > 0 && bars[index - 1].Kind == PciSizedBarKind.Memory64) || (bar.Kind == PciSizedBarKind.Memory64 && (index == 5 || bars[index + 1].Kind != PciSizedBarKind.UpperHalf)) || !bar.IsAssigned || bar.Bytes < 16 || (bar.Bytes & (bar.Bytes - 1)) != 0 || (bar.Base & (bar.Bytes - 1)) != 0 || bar.Base > ~0UL - bar.Bytes || (bar.Kind == PciSizedBarKind.Memory32 && bar.Base + bar.Bytes > 0x100000000UL))
            return MsixStatus.InvalidBar;
        if (bar.Base < 0x100000 || bar.Base + bar.Bytes > 0x0000800000000000UL)
            return MsixStatus.InvalidAddress;
        if ((ulong)offset + bytes > bar.Bytes)
            return MsixStatus.OutsideBar;
        region = new(bar.Base + offset, bytes);
        return MsixStatus.Success;
    }
}
